using System.Collections.Generic;
using UnityEngine;

namespace AntColony
{
    public enum NestTile
    {
        Soil,
        Rock,
        Tunnel,
        /// <summary>Soil full of stones: dug like soil but slower, and gives stone.</summary>
        StoneVein,
    }

    /// <summary>Something hidden in a soil tile, found when the tile is dug. Seen once a tunnel reaches the tile next to it.</summary>
    public enum BuriedLoot
    {
        None,
        SeedCache,
        Grub,
    }

    /// <summary>
    /// One point of a route. A waypoint marked <see cref="ThroughEntrance"/> is reached instantly:
    /// the walker goes in one side of the nest entrance and comes out the other, in the other scene.
    /// </summary>
    public readonly struct Waypoint
    {
        public readonly Vector2 Position;
        public readonly bool ThroughEntrance;

        public Waypoint(Vector2 position, bool throughEntrance = false)
        {
            Position = position;
            ThroughEntrance = throughEntrance;
        }
    }

    /// <summary>
    /// The underground base, built in the Nest scene and seen from the side: a grid of soil, hard rock and
    /// dug-out tunnel tiles. The player designs it: marks soil to dig (builders dig it out) and places rooms on
    /// tunnel tiles. Ants climb anywhere a tunnel goes, so they find their way along the shortest dug route.
    /// The entrance is the top tile in the middle; the surface's ant hill is its other end.
    /// </summary>
    public class Nest : MonoBehaviour
    {
        public const float TileSize = 1f;
        const int TilePixels = 16;
        /// <summary>Ants walk this far above the bottom of a tile (on its floor).</summary>
        const float FloorHeight = 0.12f;
        const float SoilMargin = 30f;
        const float EntranceLightHeight = 0.8f;
        /// <summary>Stone veins take this many times longer to dig than soil.</summary>
        const float StoneVeinDigFactor = 2f;

        static readonly Vector2Int[] Neighbours = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        ColonyGame game;
        NestTile[,] tiles;
        BuriedLoot[,] buried;
        bool[,] digOrders;
        float[,] digProgress;
        readonly HashSet<Vector2Int> claimedDigs = new HashSet<Vector2Int>();
        readonly Dictionary<Vector2Int, SpriteRenderer> digMarkers = new Dictionary<Vector2Int, SpriteRenderer>();
        readonly List<Room> rooms = new List<Room>();
        Texture2D texture;
        int[] pathParents; // scratch buffer for searches

        public int Width { get; private set; }
        public int Height { get; private set; }
        /// <summary>World position of the bottom-left corner of tile (0, 0).</summary>
        public Vector2 Origin { get; private set; }
        public Vector2Int EntranceTile { get; private set; }
        public Rect WorldBounds => new Rect(Origin, new Vector2(Width, Height) * TileSize);
        public float TopY => Origin.y + Height * TileSize;
        public float ShaftX => TileCenter(EntranceTile).x;
        /// <summary>The point just above the entrance tile where ants leave for the surface.</summary>
        public Vector2 ExitPoint => new Vector2(ShaftX, TopY + 0.2f);
        public IReadOnlyList<Room> Rooms => rooms;
        public Room QueenRoom { get; private set; }
        public int DigOrderCount { get; private set; }
        /// <summary>Goes up whenever tiles, dig orders or rooms change, so previews know when to recompute.</summary>
        public int Revision { get; private set; }
        /// <summary>Tiles dug out since the game started (not counting the starting nest).</summary>
        public int TilesDug { get; private set; }
        /// <summary>Dig orders with no dug tile next to them, which nobody can reach yet.</summary>
        public int UnreachableDigOrders { get; private set; }

        public void Build(ColonyGame colonyGame)
        {
            game = colonyGame;
            GameSettings settings = game.Settings;
            Width = Mathf.Max(9, settings.nestWidth);
            Height = Mathf.Max(12, settings.nestDepth);
            Origin = new Vector2(game.Surface.EntrancePoint.x - Width * TileSize * 0.5f, settings.nestTopY - Height * TileSize);
            EntranceTile = new Vector2Int(Width / 2, Height - 1);

            tiles = new NestTile[Width, Height];
            buried = new BuriedLoot[Width, Height];
            digOrders = new bool[Width, Height];
            digProgress = new float[Width, Height];
            pathParents = new int[Width * Height];

            PlaceRock(settings);
            PlaceStoneVeins(settings);
            DigStarterNest();
            BuryLoot(settings);

            DrawSoilBackground();
            DrawEntranceLight();
            CreateTexture();
            CreateStarterRooms();
        }

        // ---------- Tiles and coordinates ----------

        public bool InBounds(Vector2Int tile) => tile.x >= 0 && tile.y >= 0 && tile.x < Width && tile.y < Height;
        public NestTile TileAt(Vector2Int tile) => tiles[tile.x, tile.y];
        public bool IsTunnel(Vector2Int tile) => InBounds(tile) && tiles[tile.x, tile.y] == NestTile.Tunnel;
        public bool HasDigOrder(Vector2Int tile) => InBounds(tile) && digOrders[tile.x, tile.y];

        public Vector2Int WorldToTile(Vector2 point)
        {
            Vector2 local = (point - Origin) / TileSize;
            return new Vector2Int(Mathf.FloorToInt(local.x), Mathf.FloorToInt(local.y));
        }

        public Vector2 TileCenter(Vector2Int tile) => Origin + (new Vector2(tile.x, tile.y) + Vector2.one * 0.5f) * TileSize;

        /// <summary>Where an ant stands in a tile: on its floor, in the middle.</summary>
        public Vector2 StandPoint(Vector2Int tile) => Origin + new Vector2((tile.x + 0.5f) * TileSize, tile.y * TileSize + FloorHeight);

        /// <summary>The middle of the bottom edge of a rectangle of tiles.</summary>
        public Vector2 FootprintBottomCenter(Vector2Int origin, Vector2Int size)
        {
            return Origin + new Vector2((origin.x + size.x * 0.5f) * TileSize, origin.y * TileSize);
        }

        /// <summary>A random floor spot inside a room, so ants don't all stand on one pixel.</summary>
        public Vector2 RandomPointIn(Room room)
        {
            var tile = new Vector2Int(room.Origin.x + Random.Range(0, room.Size.x), room.Origin.y);
            return StandPoint(tile) + new Vector2(Random.Range(-0.3f, 0.3f), 0f);
        }

        // ---------- Paths ----------

        /// <summary>
        /// Waypoints from one point to another. Inside the nest the route follows dug tunnels; between the nest and
        /// the surface it goes through the entrance. Returns false if the target can't be reached (e.g. not dug out).
        /// </summary>
        public bool TryFindPath(Vector2 from, Vector2 to, List<Waypoint> path)
        {
            path.Clear();
            bool fromNest = !game.Surface.IsOnSurface(from);
            bool toNest = !game.Surface.IsOnSurface(to);

            if (!fromNest && !toNest)
            {
                path.Add(new Waypoint(to));
                return true;
            }

            Vector2Int startTile = fromNest ? NearestTunnel(WorldToTile(from)) : EntranceTile;
            Vector2Int endTile = toNest ? WorldToTile(to) : EntranceTile;
            if (!IsTunnel(endTile)) return false;

            if (!fromNest)
            {
                path.Add(new Waypoint(game.Surface.EntrancePoint));
                path.Add(new Waypoint(ExitPoint, throughEntrance: true));
            }

            if (!AddTilePath(startTile, endTile, path)) return false;

            if (toNest)
            {
                path.Add(new Waypoint(to));
            }
            else
            {
                path.Add(new Waypoint(ExitPoint));
                path.Add(new Waypoint(game.Surface.EntrancePoint, throughEntrance: true));
                path.Add(new Waypoint(to));
            }
            return true;
        }

        /// <summary>
        /// If a walker at <paramref name="from"/> is shut off from the entrance (tunnels around it were filled), gives
        /// the stand point of the closest tunnel that still connects to the entrance. False if it isn't shut off.
        /// </summary>
        public bool TryFindWayOut(Vector2 from, out Vector2 freeSpot)
        {
            freeSpot = from;
            Vector2Int start = NearestTunnel(WorldToTile(from));
            int[,] distances = DistancesFrom(EntranceTile);
            if (InBounds(start) && distances[start.x, start.y] >= 0) return false;

            Vector2Int here = WorldToTile(from);
            float best = float.MaxValue;
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (distances[x, y] < 0) continue;
                    float score = (new Vector2Int(x, y) - here).sqrMagnitude;
                    if (score >= best) continue;
                    best = score;
                    freeSpot = StandPoint(new Vector2Int(x, y));
                }
            }
            return best < float.MaxValue;
        }

        /// <summary>Number of tile steps from every tunnel tile to <paramref name="start"/> (-1 where unreachable).</summary>
        public int[,] DistancesFrom(Vector2Int start)
        {
            var distances = new int[Width, Height];
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++) distances[x, y] = -1;
            }
            if (!IsTunnel(start)) return distances;

            var queue = new Queue<Vector2Int>();
            distances[start.x, start.y] = 0;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                Vector2Int tile = queue.Dequeue();
                foreach (Vector2Int step in Neighbours)
                {
                    Vector2Int next = tile + step;
                    if (!IsTunnel(next) || distances[next.x, next.y] >= 0) continue;
                    distances[next.x, next.y] = distances[tile.x, tile.y] + 1;
                    queue.Enqueue(next);
                }
            }
            return distances;
        }

        /// <summary>The tile a walker at <paramref name="point"/> counts as standing in (the surface counts as the entrance).</summary>
        public Vector2Int TileOf(Vector2 point)
        {
            return game.Surface.IsOnSurface(point) ? EntranceTile : NearestTunnel(WorldToTile(point));
        }

        /// <summary>Breadth-first search over tunnel tiles; appends the stand points of the route (excluding the start).</summary>
        bool AddTilePath(Vector2Int start, Vector2Int end, List<Waypoint> path)
        {
            if (start == end) return true;

            for (int i = 0; i < pathParents.Length; i++) pathParents[i] = -1;
            var queue = new Queue<Vector2Int>();
            pathParents[Index(start)] = Index(start);
            queue.Enqueue(start);

            bool found = false;
            while (queue.Count > 0 && !found)
            {
                Vector2Int tile = queue.Dequeue();
                foreach (Vector2Int step in Neighbours)
                {
                    Vector2Int next = tile + step;
                    if (!IsTunnel(next) || pathParents[Index(next)] >= 0) continue;
                    pathParents[Index(next)] = Index(tile);
                    if (next == end)
                    {
                        found = true;
                        break;
                    }
                    queue.Enqueue(next);
                }
            }
            if (!found) return false;

            var reversed = new List<Vector2Int>();
            for (int index = Index(end); index != Index(start); index = pathParents[index])
                reversed.Add(new Vector2Int(index % Width, index / Width));
            for (int i = reversed.Count - 1; i >= 0; i--)
                path.Add(new Waypoint(StandPoint(reversed[i])));
            return true;
        }

        int Index(Vector2Int tile) => tile.y * Width + tile.x;

        /// <summary>The tile itself if it is dug, otherwise the closest dug tile (used when a walker is mid-step).</summary>
        Vector2Int NearestTunnel(Vector2Int tile)
        {
            if (IsTunnel(tile)) return tile;
            for (int radius = 1; radius < 4; radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        var candidate = new Vector2Int(tile.x + dx, tile.y + dy);
                        if (IsTunnel(candidate)) return candidate;
                    }
                }
            }
            return EntranceTile;
        }

        // ---------- Digging ----------

        /// <summary>Marks or unmarks a soil tile for digging. Rock and dug tiles are ignored. Returns true if it changed.</summary>
        public bool SetDigOrder(Vector2Int tile, bool ordered)
        {
            if (!InBounds(tile) || !IsDiggable(tile) || digOrders[tile.x, tile.y] == ordered) return false;

            digOrders[tile.x, tile.y] = ordered;
            DigOrderCount += ordered ? 1 : -1;
            Revision++;
            UpdateDigMarker(tile);
            return true;
        }

        /// <summary>
        /// The dig order closest to <paramref name="from"/> (by tunnel distance) that can be reached and that nobody
        /// else is digging, and the tile to stand on while digging it.
        /// </summary>
        public bool TryFindDigJob(Vector2 from, out Vector2Int digTile, out Vector2Int standTile)
        {
            digTile = standTile = default;
            if (DigOrderCount == 0) return false;

            int[,] distances = DistancesFrom(TileOf(from));
            int best = int.MaxValue;
            int unreachable = 0;

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (!digOrders[x, y]) continue;
                    var tile = new Vector2Int(x, y);
                    bool reachable = false;

                    foreach (Vector2Int step in Neighbours)
                    {
                        Vector2Int neighbour = tile + step;
                        if (!IsTunnel(neighbour) || distances[neighbour.x, neighbour.y] < 0) continue;
                        reachable = true;
                        if (claimedDigs.Contains(tile)) continue;

                        int distance = distances[neighbour.x, neighbour.y];
                        if (distance < best)
                        {
                            best = distance;
                            digTile = tile;
                            standTile = neighbour;
                        }
                    }
                    if (!reachable) unreachable++;
                }
            }

            UnreachableDigOrders = unreachable;
            if (best == int.MaxValue) return false;
            claimedDigs.Add(digTile);
            return true;
        }

        public void ReleaseDig(Vector2Int tile) => claimedDigs.Remove(tile);

        /// <summary>A builder spends <paramref name="seconds"/> digging a tile. Returns true once it is dug out (or gone).</summary>
        public bool AddDigWork(Vector2Int tile, float seconds)
        {
            if (!digOrders[tile.x, tile.y] || !IsDiggable(tile)) return true;

            float digTime = game.Settings.digTime * (tiles[tile.x, tile.y] == NestTile.StoneVein ? StoneVeinDigFactor : 1f);
            digProgress[tile.x, tile.y] += seconds / Mathf.Max(0.05f, digTime);
            UpdateDigMarker(tile);
            if (digProgress[tile.x, tile.y] < 1f) return false;

            CollectDigLoot(tile);
            SetDigOrder(tile, false);
            claimedDigs.Remove(tile);
            SetTile(tile, NestTile.Tunnel);
            TilesDug++;
            game.PlayEffect(EffectKind.TileDug, TileCenter(tile));
            return true;
        }

        /// <summary>Soil and stone veins can be dug; rock and tunnels can't.</summary>
        public bool IsDiggable(Vector2Int tile)
        {
            NestTile type = tiles[tile.x, tile.y];
            return type == NestTile.Soil || type == NestTile.StoneVein;
        }

        /// <summary>What a dug tile gives: stone from veins (and sometimes from plain soil), and any buried food.</summary>
        void CollectDigLoot(Vector2Int tile)
        {
            GameSettings settings = game.Settings;
            Colony colony = game.Colony;
            Vector2 where = TileCenter(tile);

            int stone = tiles[tile.x, tile.y] == NestTile.StoneVein ? settings.stonePerVeinTile
                      : Random.value < settings.stoneChancePerSoil ? 1 : 0;
            if (stone > 0)
            {
                colony.AddStone(stone);
                game.PlayEffect(EffectKind.StoneFound, where);
            }

            BuriedLoot loot = buried[tile.x, tile.y];
            buried[tile.x, tile.y] = BuriedLoot.None;
            if (loot == BuriedLoot.None) return;

            int amount = loot == BuriedLoot.SeedCache
                ? Random.Range(settings.seedCacheFoodMin, settings.seedCacheFoodMax + 1)
                : settings.grubFood;
            int stored = colony.AddFood(amount, foraged: false);
            string what = loot == BuriedLoot.SeedCache ? "a seed cache" : "a fat grub";
            game.ShowMessage(stored < amount ? $"Found {what}! +{stored} food (storage full)" : $"Found {what}! +{stored} food");
            game.PlayEffect(EffectKind.TreasureFound, where);
        }

        public BuriedLoot LootAt(Vector2Int tile) => buried[tile.x, tile.y];

        /// <summary>True if a tunnel touches the tile, so whatever is buried there can be seen.</summary>
        bool IsRevealed(Vector2Int tile)
        {
            foreach (Vector2Int step in Neighbours)
            {
                if (IsTunnel(tile + step)) return true;
            }
            return false;
        }

        // ---------- Filling tunnels back in ----------

        /// <summary>
        /// Works out which dug tiles in <paramref name="area"/> can be filled with soil again, in order, without
        /// cutting any room off from the entrance. Rooms in <paramref name="roomsBeingRemoved"/> don't count (their
        /// tiles may be filled too). Nothing is changed; <paramref name="keptOpen"/> counts tunnels that must stay.
        /// The preview and the real removal both use this, so what the player sees is what happens.
        /// </summary>
        public List<Vector2Int> PlanFill(RectInt area, ICollection<Room> roomsBeingRemoved, List<Vector2Int> keptOpen)
        {
            var filled = new List<Vector2Int>();
            keptOpen?.Clear();

            foreach (Vector2Int tile in area.allPositionsWithin)
            {
                if (!IsTunnel(tile) || tile == EntranceTile) continue;
                Room room = RoomAt(tile);
                if (room != null && !roomsBeingRemoved.Contains(room)) continue;

                tiles[tile.x, tile.y] = NestTile.Soil;
                if (AllRoomsReachable(roomsBeingRemoved))
                {
                    filled.Add(tile);
                }
                else
                {
                    tiles[tile.x, tile.y] = NestTile.Tunnel;
                    keptOpen?.Add(tile);
                }
            }

            foreach (Vector2Int tile in filled) tiles[tile.x, tile.y] = NestTile.Tunnel; // undo the trial
            return filled;
        }

        /// <summary>Fills tiles from <see cref="PlanFill"/> with soil. Ants standing in them step into the nearest tunnel.</summary>
        public void Fill(List<Vector2Int> planned)
        {
            foreach (Vector2Int tile in planned) tiles[tile.x, tile.y] = NestTile.Soil;
            foreach (Vector2Int tile in planned)
            {
                DrawTile(tile);
                foreach (Vector2Int step in Neighbours)
                {
                    if (InBounds(tile + step)) DrawTile(tile + step);
                }
            }
            texture.Apply();
            Revision++;
            foreach (Vector2Int tile in planned) MoveAntsOutOf(tile);
        }

        bool AllRoomsReachable(ICollection<Room> ignored)
        {
            int[,] distances = DistancesFrom(EntranceTile);
            foreach (Room room in rooms)
            {
                if (ignored.Contains(room)) continue;
                if (distances[room.StandTile.x, room.StandTile.y] < 0) return false;
            }
            return true;
        }

        void MoveAntsOutOf(Vector2Int tile)
        {
            Vector2 safeSpot = StandPoint(NearestTunnel(tile));
            foreach (Ant ant in game.Colony.Ants)
            {
                if (!ant.IsOnSurface && WorldToTile(ant.Position) == tile) ant.Relocate(safeSpot);
            }
        }

        // ---------- Rooms ----------

        /// <summary>True if every tile of the footprint is dug out, inside the nest, free of rooms and not the entrance.</summary>
        public bool CanPlaceRoom(RoomType type, Vector2Int origin)
        {
            Vector2Int size = RoomInfo.Of(type).Size;
            for (int x = origin.x; x < origin.x + size.x; x++)
            {
                for (int y = origin.y; y < origin.y + size.y; y++)
                {
                    var tile = new Vector2Int(x, y);
                    if (!IsTunnel(tile) || tile == EntranceTile || RoomAt(tile) != null) return false;
                }
            }
            return true;
        }

        public Room PlaceRoom(RoomType type, Vector2Int origin, bool built, int paidCost)
        {
            var room = new Room(game, type, origin, built, paidCost);
            rooms.Add(room);
            Revision++;
            if (type == RoomType.Queen) QueenRoom = room;
            return room;
        }

        public void RemoveRoom(Room room)
        {
            rooms.Remove(room);
            room.Destroy();
            Revision++;
        }

        public Room RoomAt(Vector2Int tile)
        {
            foreach (Room room in rooms)
            {
                if (room.Covers(tile)) return room;
            }
            return null;
        }

        public int CountBuilt(RoomType type)
        {
            int count = 0;
            foreach (Room room in rooms)
            {
                if (room.Type == type && room.IsBuilt) count++;
            }
            return count;
        }

        /// <summary>
        /// The finished room of a type that is the fewest tunnel steps from <paramref name="from"/> and passes the
        /// filter, or null. <paramref name="steps"/> is how far it is.
        /// </summary>
        public Room FindNearestRoom(RoomType type, Vector2 from, System.Func<Room, bool> filter, out int steps)
        {
            int[,] distances = DistancesFrom(TileOf(from));
            Room best = null;
            steps = int.MaxValue;

            foreach (Room room in rooms)
            {
                if (room.Type != type || !room.IsBuilt) continue;
                if (filter != null && !filter(room)) continue;

                int distance = distances[room.StandTile.x, room.StandTile.y];
                if (distance >= 0 && distance < steps)
                {
                    best = room;
                    steps = distance;
                }
            }
            return best;
        }

        /// <summary>The closest unfinished room (by tunnel distance), or null.</summary>
        public Room FindRoomJob(Vector2 from)
        {
            int[,] distances = DistancesFrom(TileOf(from));
            Room best = null;
            int bestDistance = int.MaxValue;
            foreach (Room room in rooms)
            {
                if (room.IsBuilt) continue;
                int distance = distances[room.StandTile.x, room.StandTile.y];
                if (distance >= 0 && distance < bestDistance)
                {
                    best = room;
                    bestDistance = distance;
                }
            }
            return best;
        }

        // ---------- Saving ----------

        public void Write(SaveData data)
        {
            data.nestWidth = Width;
            data.nestHeight = Height;
            data.tiles = new int[Width * Height];
            data.buried = new int[Width * Height];
            data.digOrders = new bool[Width * Height];
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    int index = Index(new Vector2Int(x, y));
                    data.tiles[index] = (int)tiles[x, y];
                    data.buried[index] = (int)buried[x, y];
                    data.digOrders[index] = digOrders[x, y];
                }
            }

            foreach (Room room in rooms)
            {
                if (room.Type == RoomType.Queen) continue; // always rebuilt in the same place
                var saved = new RoomSave
                {
                    type = (int)room.Type,
                    originX = room.Origin.x,
                    originY = room.Origin.y,
                    built = room.IsBuilt,
                    buildProgress = room.BuildProgress,
                    paidCost = room.PaidCost,
                    gardenTimer = room.GardenTimer,
                };
                if (room.Brood != null)
                {
                    saved.hasBrood = true;
                    saved.broodCaste = (int)room.Brood.Caste;
                    saved.broodStage = (int)room.Brood.Stage;
                    saved.broodTime = room.Brood.StageTime;
                    saved.broodFed = room.Brood.FoodFed;
                }
                data.rooms.Add(saved);
            }
        }

        /// <summary>Replaces the fresh nest's tiles, dig marks and rooms with the saved ones. False if the sizes don't match.</summary>
        public bool Read(SaveData data)
        {
            int count = Width * Height;
            if (data.nestWidth != Width || data.nestHeight != Height || data.tiles == null || data.tiles.Length != count) return false;

            foreach (Room room in new List<Room>(rooms))
            {
                if (room.Type != RoomType.Queen) RemoveRoom(room);
            }

            DigOrderCount = 0;
            foreach (SpriteRenderer marker in digMarkers.Values)
            {
                if (marker != null) Destroy(marker.gameObject);
            }
            digMarkers.Clear();
            claimedDigs.Clear();

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    int index = Index(new Vector2Int(x, y));
                    tiles[x, y] = (NestTile)data.tiles[index];
                    buried[x, y] = data.buried != null && data.buried.Length == count ? (BuriedLoot)data.buried[index] : BuriedLoot.None;
                    digOrders[x, y] = false;
                    digProgress[x, y] = 0f;
                }
            }
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    DrawTile(new Vector2Int(x, y));
                    int index = Index(new Vector2Int(x, y));
                    if (data.digOrders != null && data.digOrders.Length == count && data.digOrders[index])
                        SetDigOrder(new Vector2Int(x, y), true);
                }
            }
            texture.Apply();

            foreach (RoomSave saved in data.rooms)
            {
                Room room = PlaceRoom((RoomType)saved.type, new Vector2Int(saved.originX, saved.originY), saved.built, saved.paidCost);
                room.RestoreProgress(saved.buildProgress);
                room.GardenTimer = saved.gardenTimer;
                if (!saved.hasBrood) continue;

                room.Brood = new Brood(game, room, (AntRole)saved.broodCaste);
                room.Brood.Restore((BroodStage)saved.broodStage, saved.broodTime, saved.broodFed);
            }
            Revision++;
            return true;
        }

        // ---------- Layout ----------

        void PlaceRock(GameSettings settings)
        {
            var random = new System.Random(settings.worldSeed + 1);
            int blobs = Mathf.RoundToInt(Width * Height * settings.rockAmount / 7f);
            for (int i = 0; i < blobs; i++)
            {
                float centerX = random.Next(Width);
                float centerY = random.Next(Height - 4); // keep the topsoil free
                float radius = 0.8f + (float)random.NextDouble() * 1.6f;
                for (int x = 0; x < Width; x++)
                {
                    for (int y = 0; y < Height; y++)
                    {
                        float dx = x - centerX;
                        float dy = (y - centerY) * 1.3f;
                        if (dx * dx + dy * dy <= radius * radius) tiles[x, y] = NestTile.Rock;
                    }
                }
            }
        }

        /// <summary>Small seams of stony soil, more of them deeper down. Rock wins where they overlap.</summary>
        void PlaceStoneVeins(GameSettings settings)
        {
            var random = new System.Random(settings.worldSeed + 2);
            int seams = Mathf.RoundToInt(Width * Height * settings.stoneVeinAmount / 4f);
            for (int i = 0; i < seams; i++)
            {
                // Square the roll so seams gather toward the bottom (low y).
                float depth = (float)random.NextDouble();
                int centerY = Mathf.Clamp(Mathf.FloorToInt(depth * depth * (Height - 3)), 0, Height - 4);
                int centerX = random.Next(Width);
                float length = 1.5f + (float)random.NextDouble() * 2f;
                for (int x = 0; x < Width; x++)
                {
                    for (int y = 0; y < Height; y++)
                    {
                        float dx = (x - centerX) / length;
                        float dy = y - centerY;
                        if (dx * dx + dy * dy <= 1.1f && tiles[x, y] == NestTile.Soil) tiles[x, y] = NestTile.StoneVein;
                    }
                }
            }
        }

        /// <summary>Hides seed caches and grubs in the soil. Twice as likely at the bottom as at the top.</summary>
        void BuryLoot(GameSettings settings)
        {
            var random = new System.Random(settings.worldSeed + 3);
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height - 2; y++)
                {
                    if (tiles[x, y] != NestTile.Soil) continue;

                    float depthBonus = 2f - (float)y / Height;
                    double roll = random.NextDouble();
                    if (roll < settings.seedCacheChance * depthBonus) buried[x, y] = BuriedLoot.SeedCache;
                    else if (roll < (settings.seedCacheChance + settings.grubChance) * depthBonus) buried[x, y] = BuriedLoot.Grub;
                }
            }
        }

        /// <summary>
        /// The colony starts with a short shaft, a side tunnel with a storage room and a garden,
        /// and the queen's chamber with two cradles.
        /// </summary>
        void DigStarterNest()
        {
            int middle = EntranceTile.x;
            int top = Height - 1;

            for (int y = top; y >= top - 7; y--) ForceTunnel(middle, y);       // shaft
            for (int x = middle - 6; x <= middle + 6; x++) ForceTunnel(x, top - 2); // side tunnel
            for (int x = middle - 3; x <= middle + 3; x++)                     // queen's chamber
            {
                for (int y = top - 9; y <= top - 7; y++) ForceTunnel(x, y);
            }
        }

        void CreateStarterRooms()
        {
            int middle = EntranceTile.x;
            int top = Height - 1;
            PlaceRoom(RoomType.Storage, new Vector2Int(middle - 5, top - 2), built: true, paidCost: 0);
            PlaceRoom(RoomType.Garden, new Vector2Int(middle + 4, top - 2), built: true, paidCost: 0);
            PlaceRoom(RoomType.Queen, new Vector2Int(middle - 1, top - 9), built: true, paidCost: 0);
            PlaceRoom(RoomType.Cradle, new Vector2Int(middle - 3, top - 9), built: true, paidCost: 0);
            PlaceRoom(RoomType.Cradle, new Vector2Int(middle + 3, top - 9), built: true, paidCost: 0);
        }

        void ForceTunnel(int x, int y)
        {
            if (x >= 0 && y >= 0 && x < Width && y < Height) tiles[x, y] = NestTile.Tunnel;
        }

        void SetTile(Vector2Int tile, NestTile type)
        {
            tiles[tile.x, tile.y] = type;
            Revision++;
            DrawTile(tile);
            foreach (Vector2Int step in Neighbours)
            {
                if (InBounds(tile + step)) DrawTile(tile + step);
            }
            texture.Apply();
        }

        // ---------- Drawing ----------

        /// <summary>Plain soil behind and around the grid, so the view never shows empty space.</summary>
        void DrawSoilBackground()
        {
            Rect area = WorldBounds;
            SpriteRenderer soil = game.Art.CreateSprite("Soil", transform, game.Art.DirtTile, SortingOrder.Underground);
            soil.drawMode = SpriteDrawMode.Tiled;
            soil.tileMode = SpriteTileMode.Continuous;
            soil.size = area.size + Vector2.one * SoilMargin * 2f;
            soil.transform.position = area.center;

            // The grass roots line above the nest.
            SpriteRenderer surfaceLine = game.Art.CreateSprite("Surface Line", transform, game.Art.Pixel, SortingOrder.NestTiles);
            surfaceLine.color = AutumnPalette.GrassDark;
            GameArt.Stretch(surfaceLine, new Vector2(area.width + SoilMargin * 2f, 0.4f));
            surfaceLine.transform.position = new Vector2(area.center.x, TopY + EntranceLightHeight + 0.2f);
        }

        /// <summary>Daylight above the entrance tile: the way out to the surface.</summary>
        void DrawEntranceLight()
        {
            SpriteRenderer light = game.Art.CreateSprite("Entrance Light", transform, game.Art.Pixel, SortingOrder.NestTiles);
            light.color = AutumnPalette.SkyBottom;
            GameArt.Stretch(light, new Vector2(TileSize * 0.8f, EntranceLightHeight));
            light.transform.position = new Vector2(ShaftX, TopY + EntranceLightHeight * 0.5f);
        }

        void CreateTexture()
        {
            texture = PixelArt.CreateTexture("Nest Tiles", Width * TilePixels, Height * TilePixels);
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++) DrawTile(new Vector2Int(x, y));
            }
            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero, TilePixels / TileSize);
            sprite.name = "Nest Tiles";
            SpriteRenderer view = game.Art.CreateSprite("Tiles", transform, sprite, SortingOrder.NestTiles);
            view.transform.position = Origin;
        }

        void DrawTile(Vector2Int tile)
        {
            NestTile type = tiles[tile.x, tile.y];
            int baseX = tile.x * TilePixels;
            int baseY = tile.y * TilePixels;

            for (int px = 0; px < TilePixels; px++)
            {
                for (int py = 0; py < TilePixels; py++)
                {
                    int noise = Hash(baseX + px, baseY + py);
                    Color color;
                    if (type == NestTile.Soil) color = SoilColor(noise);
                    else if (type == NestTile.StoneVein) color = StoneVeinColor(baseX + px, baseY + py, noise);
                    else if (type == NestTile.Rock) color = RockColor(tile, px, py, noise);
                    else color = TunnelColor(tile, px, py);
                    texture.SetPixel(baseX + px, baseY + py, color);
                }
            }

            if (type == NestTile.Soil && buried[tile.x, tile.y] != BuriedLoot.None && IsRevealed(tile))
                DrawLootIcon(baseX, baseY, buried[tile.x, tile.y]);
        }

        /// <summary>Soil studded with grey stones: blobs on a coarse grid, so they read as pebbles.</summary>
        static Color StoneVeinColor(int x, int y, int noise)
        {
            int cell = Hash(x / 4, y / 4) % 100;
            if (cell < 55)
            {
                bool rim = x % 4 == 3 || y % 4 == 0;
                if (rim) return AutumnPalette.StoneDark;
                return noise % 100 < 25 ? Color.Lerp(AutumnPalette.Stone, AutumnPalette.Cream, 0.3f) : AutumnPalette.Stone;
            }
            return SoilColor(noise);
        }

        static readonly string[] SeedCacheIcon =
        {
            "..kkk...",
            ".kyyok..",
            "kyyookk.",
            "kyookyok",
            ".kkkyyok",
            "...kook.",
            "....kk..",
        };

        static readonly string[] GrubIcon =
        {
            "..kkkk..",
            ".kwwwwk.",
            "kwwkkwwk",
            "kwk..kwk",
            ".kk..kwk",
            ".....kek",
            "......k.",
        };

        /// <summary>A small picture of what is buried, drawn in the middle of a soil tile.</summary>
        void DrawLootIcon(int baseX, int baseY, BuriedLoot loot)
        {
            string[] rows = loot == BuriedLoot.SeedCache ? SeedCacheIcon : GrubIcon;
            int left = baseX + (TilePixels - rows[0].Length) / 2;
            int top = baseY + (TilePixels + rows.Length) / 2;
            for (int row = 0; row < rows.Length; row++)
            {
                for (int column = 0; column < rows[row].Length; column++)
                {
                    char symbol = rows[row][column];
                    if (symbol == '.') continue;
                    Color color = symbol == 'k' ? AutumnPalette.Outline
                                : symbol == 'y' ? AutumnPalette.Gold
                                : symbol == 'o' ? AutumnPalette.Orange
                                : symbol == 'e' ? AutumnPalette.Rust
                                : AutumnPalette.Cream;
                    texture.SetPixel(left + column, top - 1 - row, color);
                }
            }
        }

        static Color SoilColor(int noise)
        {
            int roll = noise % 100;
            if (roll < 15) return AutumnPalette.DirtDark;
            if (roll < 25) return AutumnPalette.DirtLight;
            if (roll < 27) return AutumnPalette.Stone;
            return AutumnPalette.Dirt;
        }

        Color RockColor(Vector2Int tile, int px, int py, int noise)
        {
            // Darker edge where the rock meets something softer.
            bool edge = (px == 0 && !IsRock(tile + Vector2Int.left)) || (px == TilePixels - 1 && !IsRock(tile + Vector2Int.right))
                     || (py == 0 && !IsRock(tile + Vector2Int.down)) || (py == TilePixels - 1 && !IsRock(tile + Vector2Int.up));
            if (edge) return AutumnPalette.Outline;
            return noise % 100 < 30 ? AutumnPalette.StoneDark : AutumnPalette.Stone;
        }

        Color TunnelColor(Vector2Int tile, int px, int py)
        {
            // A two-pixel rim of packed earth wherever the tunnel touches undug ground.
            const int rim = 2;
            bool rimEdge = (px < rim && !IsTunnel(tile + Vector2Int.left)) || (px >= TilePixels - rim && !IsTunnel(tile + Vector2Int.right))
                        || (py < rim && !IsTunnel(tile + Vector2Int.down)) || (py >= TilePixels - rim && !IsTunnel(tile + Vector2Int.up) && tile != EntranceTile);
            return rimEdge ? AutumnPalette.TunnelRim : AutumnPalette.Tunnel;
        }

        bool IsRock(Vector2Int tile) => !InBounds(tile) || tiles[tile.x, tile.y] == NestTile.Rock;

        static int Hash(int x, int y)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                return (h ^ (h >> 16)) & 0x7fffffff;
            }
        }

        /// <summary>A see-through marker over each tile waiting to be dug; it fills up as the tile is dug.</summary>
        void UpdateDigMarker(Vector2Int tile)
        {
            bool ordered = digOrders[tile.x, tile.y];
            digMarkers.TryGetValue(tile, out SpriteRenderer marker);

            if (!ordered)
            {
                if (marker != null) Destroy(marker.gameObject);
                digMarkers.Remove(tile);
                digProgress[tile.x, tile.y] = 0f;
                return;
            }

            if (marker == null)
            {
                marker = game.Art.CreateSprite("Dig Order", transform, game.Art.Pixel, SortingOrder.DigMarkers);
                GameArt.Stretch(marker, Vector2.one * TileSize * 0.9f);
                marker.transform.position = TileCenter(tile);
                digMarkers[tile] = marker;
            }

            float progress = digProgress[tile.x, tile.y];
            Color gold = AutumnPalette.Gold;
            marker.color = new Color(gold.r, gold.g, gold.b, 0.25f + 0.35f * progress);
        }
    }
}
