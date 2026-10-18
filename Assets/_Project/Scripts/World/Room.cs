using UnityEngine;

namespace AntColony
{
    public enum RoomType
    {
        Queen,
        Storage,
        Cradle,
        Garden,
    }

    /// <summary>Fixed facts about a kind of room: its footprint in tiles, what it costs and what it does.</summary>
    public readonly struct RoomInfo
    {
        public readonly string Name;
        public readonly Vector2Int Size;
        public readonly string Description;

        RoomInfo(string name, Vector2Int size, string description)
        {
            Name = name;
            Size = size;
            Description = description;
        }

        public static RoomInfo Of(RoomType type)
        {
            switch (type)
            {
                case RoomType.Queen: return new RoomInfo("Queen", new Vector2Int(3, 2), "The queen lays eggs into free cradles.");
                case RoomType.Storage: return new RoomInfo("Storage", new Vector2Int(2, 1), "Holds more food and leaves.");
                case RoomType.Cradle: return new RoomInfo("Cradle", new Vector2Int(1, 1), "Holds one egg or larva.");
                default: return new RoomInfo("Fungus Garden", new Vector2Int(2, 1), "Turns leaves into food.");
            }
        }

        public static int CostOf(RoomType type, GameSettings settings)
        {
            switch (type)
            {
                case RoomType.Storage: return settings.storageRoomCost;
                case RoomType.Cradle: return settings.cradleCost;
                case RoomType.Garden: return settings.gardenCost;
                default: return 0;
            }
        }
    }

    /// <summary>
    /// A room placed on dug-out tiles of the nest. It starts as a see-through blueprint that builders finish.
    /// Ants walk through rooms freely; a room only marks what those tiles are used for.
    /// </summary>
    public class Room
    {
        static readonly Color BlueprintTint = new Color(1f, 1f, 1f, 0.4f);

        readonly ColonyGame game;
        readonly Transform root;
        readonly SpriteRenderer visual;
        HealthBar buildBar;
        float buildProgress;

        public RoomType Type { get; }
        public RoomInfo Info => RoomInfo.Of(Type);
        /// <summary>Bottom-left tile of the footprint.</summary>
        public Vector2Int Origin { get; }
        public Vector2Int Size => Info.Size;
        public bool IsBuilt { get; private set; }
        /// <summary>True once the player has removed it.</summary>
        public bool IsRemoved { get; private set; }
        /// <summary>Cradles only: the egg or larva in it, or null.</summary>
        public Brood Brood { get; set; }
        /// <summary>The queen is on her way to lay an egg here, so no one else picks this cradle.</summary>
        public bool AwaitingEgg { get; set; }
        /// <summary>Gardens only: seconds into turning the current leaf into food.</summary>
        public float GardenTimer { get; set; }
        /// <summary>Gardens only: true while it had a leaf to work on.</summary>
        public bool IsWorking { get; set; }
        /// <summary>Food paid for it (for refunds).</summary>
        public int PaidCost { get; }

        public Room(ColonyGame game, RoomType type, Vector2Int origin, bool built, int paidCost)
        {
            this.game = game;
            Type = type;
            Origin = origin;
            PaidCost = paidCost;

            Nest nest = game.Nest;
            root = new GameObject($"{Info.Name} Room").transform;
            root.SetParent(nest.transform, false);
            root.position = nest.FootprintBottomCenter(origin, Size);

            Sprite sprite = SpriteFor(type);
            if (sprite != null)
            {
                visual = game.Art.CreateSprite("Visual", root, sprite, SortingOrder.Rooms);
                GameArt.ScaleToWidth(visual, Size.x * Nest.TileSize);
                visual.transform.localPosition = new Vector3(0f, GameArt.PivotHeight(visual), 0f);
            }

            if (built)
            {
                IsBuilt = true;
            }
            else
            {
                if (visual != null) visual.color = BlueprintTint;
                buildBar = HealthBar.Create(game.Art, root, Size.y * Nest.TileSize + 0.15f, Size.x * 0.8f, AutumnPalette.Gold, alwaysVisible: true);
                buildBar.SetFraction(0f);
            }
        }

        /// <summary>The room's footprint in the world (for highlighting it).</summary>
        public Rect WorldRect
        {
            get
            {
                Vector2 bottomCenter = root.position;
                float width = Size.x * Nest.TileSize;
                return new Rect(bottomCenter.x - width * 0.5f, bottomCenter.y, width, Size.y * Nest.TileSize);
            }
        }

        /// <summary>The middle of the footprint's floor, where ants stand to use the room.</summary>
        public Vector2 StandPoint => game.Nest.StandPoint(Origin + new Vector2Int(Size.x / 2, 0));
        public Vector2Int StandTile => Origin + new Vector2Int(Size.x / 2, 0);
        public Vector2 LabelPoint => (Vector2)root.position + new Vector2(0f, Size.y * Nest.TileSize + 0.35f);

        public bool Covers(Vector2Int tile)
        {
            return tile.x >= Origin.x && tile.x < Origin.x + Size.x && tile.y >= Origin.y && tile.y < Origin.y + Size.y;
        }

        public float BuildProgress => buildProgress;

        /// <summary>Puts a loaded blueprint's progress back.</summary>
        public void RestoreProgress(float progress)
        {
            if (IsBuilt) return;
            buildProgress = Mathf.Clamp01(progress);
            buildBar.SetFraction(buildProgress);
        }

        /// <summary>A builder spends <paramref name="seconds"/> of work on the blueprint.</summary>
        public void AddWork(float seconds)
        {
            if (IsBuilt) return;

            buildProgress = Mathf.Min(1f, buildProgress + seconds / game.Settings.roomBuildTime);
            buildBar.SetFraction(buildProgress);
            if (buildProgress < 1f) return;

            IsBuilt = true;
            if (visual != null) visual.color = Color.white;
            Object.Destroy(buildBar.gameObject);
            game.PlayEffect(EffectKind.RoomBuilt, StandPoint);
        }

        public void Destroy()
        {
            IsRemoved = true;
            Brood?.Destroy();
            Brood = null;
            Object.Destroy(root.gameObject);
        }

        Sprite SpriteFor(RoomType type)
        {
            switch (type)
            {
                case RoomType.Storage: return game.Art.StoragePile;
                case RoomType.Cradle: return game.Art.Cradle;
                case RoomType.Garden: return game.Art.FungusGarden;
                default: return null; // the queen herself marks her chamber
            }
        }
    }
}
