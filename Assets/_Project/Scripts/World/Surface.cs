using UnityEngine;

namespace AntColony
{
    /// <summary>
    /// The overworld, seen from above: an autumn meadow with the ant hill (the nest entrance) in the middle,
    /// trees around the edge, a few groves inside, and bushes, boulders, stumps, logs, flowers and small
    /// decorations scattered over it. Scenery is only for looks: surface creatures move freely anywhere inside
    /// <see cref="Bounds"/>. Tree canopies inside the field turn see-through while a unit is under them.
    /// </summary>
    /// <summary>The standard look of one kind of scenery: its size, draw order and shadow.</summary>
    public readonly struct SceneryType
    {
        // Trees block only at the trunk (the canopy is overhead); bushes and flowers can be walked through.
        public static readonly SceneryType Tree = new SceneryType("Tree", 4f, SortingOrder.TreeCanopies, 0.3f, new Vector2(0.08f, -0.08f), blockRadius: 0.11f);
        public static readonly SceneryType Bush = new SceneryType("Bush", 1.5f, SortingOrder.Scenery, 0.25f, new Vector2(0.08f, -0.1f));
        public static readonly SceneryType Boulder = new SceneryType("Boulder", 1.8f, SortingOrder.Scenery, 0.25f, new Vector2(0.08f, -0.1f), blockRadius: 0.4f);
        public static readonly SceneryType Stump = new SceneryType("Stump", 1.05f, SortingOrder.Scenery, 0.25f, new Vector2(0.08f, -0.1f), blockRadius: 0.4f);
        public static readonly SceneryType Log = new SceneryType("Fallen Log", 2.5f, SortingOrder.Scenery, 0f, Vector2.zero, blockRadius: 0.4f, isLong: true);
        public static readonly SceneryType Flower = new SceneryType("Flower", 0.28f, SortingOrder.Decorations, 0f, Vector2.zero, randomFlip: false);

        public readonly string Name;
        /// <summary>Width in world units at scale 1.</summary>
        public readonly float Width;
        public readonly int Order;
        /// <summary>0 for no shadow.</summary>
        public readonly float ShadowAlpha;
        /// <summary>Where the shadow sits, in widths of the object (light comes from the top left).</summary>
        public readonly Vector2 ShadowOffset;
        public readonly bool RandomFlip;
        /// <summary>
        /// Size of the solid part that units walk around, as a share of the sprite's width (0: not solid).
        /// For long objects it is a share of the height, along the whole length.
        /// </summary>
        public readonly float BlockRadius;
        public readonly bool IsLong;

        SceneryType(string name, float width, int sortingOrder, float shadowAlpha, Vector2 shadowOffset, bool randomFlip = true,
                    float blockRadius = 0f, bool isLong = false)
        {
            BlockRadius = blockRadius;
            IsLong = isLong;
            Name = name;
            Width = width;
            Order = sortingOrder;
            ShadowAlpha = shadowAlpha;
            ShadowOffset = shadowOffset;
            RandomFlip = randomFlip;
        }
    }

    /// <summary>A solid round spot on the surface that units walk around.</summary>
    public readonly struct Obstacle
    {
        public readonly Vector2 Center;
        public readonly float Radius;

        public Obstacle(Vector2 center, float radius)
        {
            Center = center;
            Radius = radius;
        }
    }

    public class Surface : MonoBehaviour
    {
        const float SideMargin = 14f;        // scenery drawn past the playable edges
        const float EntranceClearance = 3f;  // keep decorations away from the ant hill
        const float AntHillWidth = 2.6f;
        const float TreeSpacing = 4.5f;
        /// <summary>Keep the bigger scenery (groves, boulders, logs) this far from the ant hill so its approach stays open.</summary>
        const float SceneryClearance = 6f;
        const float CanopyFadeSpeed = 4f;
        const float FadedCanopyAlpha = 0.35f;

        /// <summary>Petal colours of flowers built in code (the flower prefabs carry their own colour).</summary>
        public static readonly Color[] FlowerColors = { AutumnPalette.Cream, AutumnPalette.Gold, AutumnPalette.PlumLight, AutumnPalette.Orange };

        readonly System.Collections.Generic.List<SpriteRenderer> fieldCanopies = new System.Collections.Generic.List<SpriteRenderer>();
        readonly System.Collections.Generic.List<Obstacle> obstacles = new System.Collections.Generic.List<Obstacle>();
        /// <summary>How far ahead a walker looks for something in its way.</summary>
        const float LookAhead = 3f;
        ColonyGame game;

        public Rect Bounds { get; private set; }
        public Vector2 EntrancePoint { get; private set; }

        GameArt art;
        System.Random random;
        SurfaceLife life;

        public void Build(ColonyGame game)
        {
            this.game = game;
            GameSettings settings = game.Settings;
            art = game.Art;
            random = new System.Random(settings.worldSeed);

            EntrancePoint = Vector2.zero;
            Bounds = new Rect(-settings.worldHalfWidth, -settings.worldHalfHeight,
                settings.worldHalfWidth * 2f, settings.worldHalfHeight * 2f);

            SpriteRenderer meadow = art.CreateSprite("Meadow", transform, art.GrassTile, SortingOrder.Meadow);
            meadow.drawMode = SpriteDrawMode.Tiled;
            meadow.tileMode = SpriteTileMode.Continuous;
            meadow.size = Bounds.size + Vector2.one * SideMargin * 2f;
            meadow.transform.position = Bounds.center;

            ShadeOutsidePlayArea();
            life = gameObject.AddComponent<SurfaceLife>();
            life.Init(art, Bounds, new System.Random(settings.worldSeed + 1)); // its own numbers: the layout stays as it was
            PlaceTrees();
            PlaceDecorations();
            PlaceScenery();
            PlaceGroves();
            nav = new SurfaceNav(Bounds, obstacles);

            SpriteRenderer hill = art.CreateSprite("Ant Hill", transform, art.AntHill, SortingOrder.AntHill);
            GameArt.ScaleToWidth(hill, AntHillWidth);
            hill.transform.position = EntrancePoint;
        }

        SurfaceNav nav;
        readonly System.Collections.Generic.List<Vector2> routeBuffer = new System.Collections.Generic.List<Vector2>();

        /// <summary>
        /// Adds waypoints for walking from <paramref name="from"/> to <paramref name="to"/> on the surface, around
        /// solid scenery and finished walls and towers (ending at <paramref name="to"/>). Straight there if there's no way round.
        /// </summary>
        public void AddRoute(Vector2 from, Vector2 to, System.Collections.Generic.List<Waypoint> path)
        {
            if (nav != null)
            {
                nav.Refresh(game.Defenses != null ? game.Defenses.Structures : null);
                if (nav.FindPath(from, to, routeBuffer))
                {
                    foreach (Vector2 point in routeBuffer) path.Add(new Waypoint(point));
                    return;
                }
            }
            path.Add(new Waypoint(to));
        }

        /// <summary>
        /// Waypoints for an enemy from <paramref name="from"/> to <paramref name="to"/>: around rocks, logs and trunks,
        /// but straight through walls and towers (it chews them). Just the target if there's no way round.
        /// </summary>
        public void FindEnemyRoute(Vector2 from, Vector2 to, System.Collections.Generic.List<Vector2> route)
        {
            route.Clear();
            if (nav != null && nav.FindPath(from, to, route, includeStructures: false)) return;
            route.Clear();
            route.Add(to);
        }

        public Vector2 Clamp(Vector2 point)
        {
            return new Vector2(
                Mathf.Clamp(point.x, Bounds.xMin, Bounds.xMax),
                Mathf.Clamp(point.y, Bounds.yMin, Bounds.yMax));
        }

        /// <summary>True for points in the Surface scene; false for points inside the nest (which sits far below).</summary>
        public bool IsOnSurface(Vector2 point) => point.y > Bounds.yMin - SideMargin * 2f;

        /// <summary>A random spot in the play area at least <paramref name="minDistanceFromEntrance"/> from the ant hill.</summary>
        public Vector2 RandomPoint(float minDistanceFromEntrance)
        {
            for (int attempt = 0; attempt < 30; attempt++)
            {
                var point = new Vector2(
                    Random.Range(Bounds.xMin + 1f, Bounds.xMax - 1f),
                    Random.Range(Bounds.yMin + 1f, Bounds.yMax - 1f));
                if (Vector2.Distance(point, EntrancePoint) >= minDistanceFromEntrance && !IsBlocked(point, 0.5f)) return point;
            }
            return Clamp(EntrancePoint + Random.insideUnitCircle.normalized * minDistanceFromEntrance);
        }

        /// <summary>A random spot on the edge of the play area, where enemies walk in from.</summary>
        public Vector2 RandomEdgePoint()
        {
            float perimeter = (Bounds.width + Bounds.height) * 2f;
            float along = Random.Range(0f, perimeter);

            if (along < Bounds.width) return new Vector2(Bounds.xMin + along, Bounds.yMax);
            along -= Bounds.width;
            if (along < Bounds.width) return new Vector2(Bounds.xMin + along, Bounds.yMin);
            along -= Bounds.width;
            if (along < Bounds.height) return new Vector2(Bounds.xMin, Bounds.yMin + along);
            along -= Bounds.height;
            return new Vector2(Bounds.xMax, Bounds.yMin + along);
        }

        /// <summary>Darkens the meadow outside the play area so its edge is easy to see.</summary>
        void ShadeOutsidePlayArea()
        {
            Rect outer = new Rect(Bounds.position - Vector2.one * SideMargin, Bounds.size + Vector2.one * SideMargin * 2f);
            ShadeRect(new Rect(outer.xMin, Bounds.yMax, outer.width, outer.yMax - Bounds.yMax));     // top
            ShadeRect(new Rect(outer.xMin, outer.yMin, outer.width, Bounds.yMin - outer.yMin));     // bottom
            ShadeRect(new Rect(outer.xMin, Bounds.yMin, Bounds.xMin - outer.xMin, Bounds.height));  // left
            ShadeRect(new Rect(Bounds.xMax, Bounds.yMin, outer.xMax - Bounds.xMax, Bounds.height)); // right
        }

        void ShadeRect(Rect rect)
        {
            SpriteRenderer shade = art.CreateSprite("Edge Shade", transform, art.Pixel, SortingOrder.PlayAreaEdge);
            shade.color = new Color(0.1f, 0.05f, 0.02f, 0.35f);
            GameArt.Stretch(shade, rect.size);
            shade.transform.position = rect.center;
        }

        /// <summary>A ring of trees just outside the play area frames the meadow without hiding any units.</summary>
        void PlaceTrees()
        {
            Rect ring = new Rect(Bounds.position - Vector2.one * 2.5f, Bounds.size + Vector2.one * 5f);
            for (float x = ring.xMin; x <= ring.xMax; x += TreeSpacing)
            {
                PlaceTree(new Vector2(x, ring.yMax + RandomRange(0f, 2f)));
                PlaceTree(new Vector2(x, ring.yMin - RandomRange(0f, 2f)));
                PlaceTree(new Vector2(x + TreeSpacing * 0.5f, ring.yMax + RandomRange(4f, 7f)));
                PlaceTree(new Vector2(x + TreeSpacing * 0.5f, ring.yMin - RandomRange(4f, 7f)));
            }
            for (float y = ring.yMin; y <= ring.yMax; y += TreeSpacing)
            {
                PlaceTree(new Vector2(ring.xMin - RandomRange(0f, 2f), y));
                PlaceTree(new Vector2(ring.xMax + RandomRange(0f, 2f), y));
                PlaceTree(new Vector2(ring.xMin - RandomRange(4f, 7f), y + TreeSpacing * 0.5f));
                PlaceTree(new Vector2(ring.xMax + RandomRange(4f, 7f), y + TreeSpacing * 0.5f));
            }
        }

        void PlaceTree(Vector2 position)
        {
            PlaceScenery(SceneryType.Tree, game.Prefabs.trees, art.Trees, position, RandomRange(1f, 1.375f), rotate: false);
        }

        void PlaceDecorations()
        {
            Scatter("Grass Tuft", art.GrassTuft, 160, 0.5f, randomRotation: false);
            Scatter("Pebble", art.Pebble, 40, 0.35f, randomRotation: true);
            Scatter("Mushroom", art.Mushroom, 14, 0.7f, randomRotation: true);

            // Fallen leaves lying in the grass.
            for (int i = 0; i < 140; i++)
            {
                SpriteRenderer leaf = Scatter("Fallen Leaf", art.FallingLeaf, 1, 0.22f, randomRotation: true);
                leaf.color = AutumnPalette.LeafColors[random.Next(AutumnPalette.LeafColors.Length)];
            }
        }

        /// <summary>Places <paramref name="count"/> copies of a sprite at random spots; returns the last one.</summary>
        SpriteRenderer Scatter(string objectName, Sprite sprite, int count, float width, bool randomRotation)
        {
            SpriteRenderer last = null;
            for (int i = 0; i < count; i++)
            {
                Vector2 position;
                do
                {
                    position = new Vector2(
                        RandomRange(Bounds.xMin - SideMargin * 0.5f, Bounds.xMax + SideMargin * 0.5f),
                        RandomRange(Bounds.yMin - SideMargin * 0.5f, Bounds.yMax + SideMargin * 0.5f));
                } while (Vector2.Distance(position, EntrancePoint) < EntranceClearance);

                last = art.CreateSprite(objectName, transform, sprite, SortingOrder.Decorations);
                GameArt.ScaleToWidth(last, width);
                last.transform.position = position;
                if (randomRotation) last.transform.localRotation = Quaternion.Euler(0f, 0f, RandomRange(0f, 360f));
                else if (sprite == art.GrassTuft) life.Add(last, SurfaceLife.Kind.Grass);
            }
            return last;
        }

        /// <summary>Bushes, boulders, stumps, fallen logs and patches of flowers across the meadow.</summary>
        void PlaceScenery()
        {
            PrefabLibrary prefabs = game.Prefabs;
            for (int i = 0; i < 18; i++)
                PlaceScenery(SceneryType.Bush, prefabs.bushes, art.Bushes, RandomFieldPoint(SceneryClearance * 0.6f, -SideMargin * 0.3f), RandomRange(0.8f, 1.2f), rotate: false);
            for (int i = 0; i < 10; i++)
                PlaceScenery(SceneryType.Boulder, prefabs.boulders, art.Boulders, RandomFieldPoint(SceneryClearance, -SideMargin * 0.3f), RandomRange(0.72f, 1.28f), rotate: false);
            for (int i = 0; i < 6; i++)
                PlaceScenery(SceneryType.Stump, prefabs.stumps, new[] { art.Stump }, RandomFieldPoint(SceneryClearance, -SideMargin * 0.3f), RandomRange(0.85f, 1.15f), rotate: false);
            for (int i = 0; i < 5; i++)
                PlaceScenery(SceneryType.Log, prefabs.logs, new[] { art.Log }, RandomFieldPoint(SceneryClearance, -SideMargin * 0.3f), RandomRange(0.88f, 1.12f), rotate: true);

            for (int patch = 0; patch < 22; patch++)
            {
                Vector2 center = RandomFieldPoint(EntranceClearance, margin: -SideMargin * 0.4f);
                // One kind of flower per patch: a random prefab, or (built in code) a random petal colour.
                int variant = random.Next(4);
                int count = random.Next(4, 8);
                for (int i = 0; i < count; i++)
                {
                    Vector2 position = center + new Vector2(RandomRange(-0.8f, 0.8f), RandomRange(-0.6f, 0.6f));
                    SpriteRenderer flower = PlaceScenery(SceneryType.Flower, prefabs.flowers, new[] { art.Flower }, position, 1f, rotate: false, variant);
                    if (!HasAny(prefabs.flowers)) flower.color = FlowerColors[variant % FlowerColors.Length];
                }
            }
        }

        /// <summary>
        /// Small clumps of trees inside the play area. Their canopies are drawn over units, so <see cref="Update"/>
        /// fades a canopy while anything walks under it.
        /// </summary>
        void PlaceGroves()
        {
            for (int grove = 0; grove < 5; grove++)
            {
                Vector2 center = RandomFieldPoint(SceneryClearance + 3f, margin: 4f);
                int trees = random.Next(2, 4);
                for (int i = 0; i < trees; i++)
                {
                    Vector2 position = Clamp(center + new Vector2(RandomRange(-2.2f, 2.2f), RandomRange(-1.8f, 1.8f)));
                    SpriteRenderer canopy = PlaceScenery(SceneryType.Tree, game.Prefabs.trees, art.Trees, position, RandomRange(0.8f, 1.05f), rotate: false);
                    if (canopy != null) fieldCanopies.Add(canopy);
                }
            }
        }

        void Update()
        {
            if (game == null || game.Colony == null || game.Waves == null) return;

            foreach (SpriteRenderer canopy in fieldCanopies)
            {
                if (canopy == null) continue;
                float radius = canopy.bounds.extents.x * 0.85f;
                float target = IsAnyUnitUnder(canopy.transform.position, radius) ? FadedCanopyAlpha : 1f;
                Color color = canopy.color;
                color.a = Mathf.MoveTowards(color.a, target, CanopyFadeSpeed * Time.deltaTime);
                canopy.color = color;
            }
        }

        bool IsAnyUnitUnder(Vector2 center, float radius)
        {
            float radiusSquared = radius * radius;
            foreach (Ant ant in game.Colony.Ants)
            {
                if (((Vector2)ant.transform.position - center).sqrMagnitude < radiusSquared) return true;
            }
            foreach (Enemy enemy in game.Waves.Enemies)
            {
                if ((enemy.Position - center).sqrMagnitude < radiusSquared) return true;
            }
            return false;
        }

        /// <summary>
        /// Puts one piece of scenery at <paramref name="position"/>: a random prefab from <paramref name="prefabs"/>
        /// (kept as designed, only resized by <paramref name="scale"/>), or, with no prefabs set, one built in code from
        /// a random sprite. Returns its "Visual" sprite (null if a prefab has none).
        /// <paramref name="variant"/> picks a specific prefab/sprite instead of a random one.
        /// </summary>
        SpriteRenderer PlaceScenery(SceneryType type, GameObject[] prefabs, Sprite[] sprites, Vector2 position, float scale, bool rotate, int variant = -1)
        {
            Transform root;
            if (HasAny(prefabs))
            {
                GameObject[] usable = System.Array.FindAll(prefabs, prefab => prefab != null);
                GameObject prefab = usable[variant >= 0 ? variant % usable.Length : random.Next(usable.Length)];
                root = Instantiate(prefab, transform, false).transform;
                root.name = prefab.name;
                root.localScale = Vector3.Scale(root.localScale, new Vector3(scale, scale, 1f));
            }
            else
            {
                root = new GameObject(type.Name).transform;
                root.SetParent(transform, false);
                Sprite sprite = sprites[variant >= 0 ? variant % sprites.Length : random.Next(sprites.Length)];
                BuildScenery(art, root, type, sprite, art.Shadow);
                root.localScale = new Vector3(scale, scale, 1f);
            }

            root.position = position;
            if (rotate) root.localRotation = Quaternion.Euler(0f, 0f, RandomRange(0f, 180f));

            Transform visualChild = root.Find("Visual");
            SpriteRenderer visual = visualChild != null ? visualChild.GetComponent<SpriteRenderer>() : root.GetComponentInChildren<SpriteRenderer>();
            if (visual != null && type.RandomFlip) visual.flipX = random.Next(2) == 0;
            if (visual != null && type.BlockRadius > 0f && Bounds.Contains(position)) AddObstacles(type, visual);
            if (type.Name == SceneryType.Tree.Name) life.Add(visual, SurfaceLife.Kind.Canopy);
            else if (type.Name == SceneryType.Bush.Name) life.Add(visual, SurfaceLife.Kind.Bush);
            else if (type.Name == SceneryType.Flower.Name) life.Add(visual, SurfaceLife.Kind.Flower);
            return visual;
        }

        /// <summary>Makes a piece of scenery solid, sized from its sprite so edited prefabs stay right.</summary>
        void AddObstacles(SceneryType type, SpriteRenderer visual)
        {
            Vector3 size = Vector3.Scale(visual.sprite.bounds.size, visual.transform.lossyScale);
            Vector2 center = visual.bounds.center;
            if (!type.IsLong)
            {
                obstacles.Add(new Obstacle(center, Mathf.Abs(size.x) * type.BlockRadius));
                return;
            }

            // A log: a row of circles along its length.
            float radius = Mathf.Abs(size.y) * type.BlockRadius;
            Vector2 along = visual.transform.right;
            float halfLength = Mathf.Max(0f, Mathf.Abs(size.x) * 0.5f - radius);
            int circles = Mathf.Max(2, Mathf.CeilToInt(halfLength * 2f / radius) + 1);
            for (int i = 0; i < circles; i++)
            {
                float t = circles == 1 ? 0f : Mathf.Lerp(-halfLength, halfLength, i / (float)(circles - 1));
                obstacles.Add(new Obstacle(center + along * t, radius));
            }
        }

        // ---------- Walking around solid things ----------

        /// <summary>
        /// Every solid spot: scenery, plus finished walls and towers when <paramref name="includeStructures"/>
        /// (ants walk around those; enemies chew through them instead).
        /// </summary>
        System.Collections.Generic.IEnumerable<Obstacle> Solids(bool includeStructures)
        {
            foreach (Obstacle obstacle in obstacles) yield return obstacle;
            if (!includeStructures || game.Defenses == null) yield break;
            foreach (Structure structure in game.Defenses.Structures)
            {
                if (structure.IsBuilt) yield return new Obstacle(structure.Position, structure.Radius);
            }
        }

        /// <summary>True if a circle of <paramref name="radius"/> at <paramref name="point"/> overlaps solid scenery.</summary>
        public bool IsBlocked(Vector2 point, float radius)
        {
            foreach (Obstacle obstacle in obstacles)
            {
                if (Vector2.Distance(point, obstacle.Center) < obstacle.Radius + radius) return true;
            }
            return false;
        }

        /// <summary>True if a circle at <paramref name="point"/> overlaps solid scenery or (optionally) a finished wall or tower.</summary>
        public bool IsSolid(Vector2 point, float radius, bool includeStructures)
        {
            foreach (Obstacle obstacle in Solids(includeStructures))
            {
                if (Vector2.Distance(point, obstacle.Center) < obstacle.Radius + radius) return true;
            }
            return !Bounds.Contains(point);
        }

        /// <summary>Moves a walker of <paramref name="radius"/> out of anything solid it ended up inside.</summary>
        public Vector2 PushOut(Vector2 point, float radius, bool includeStructures)
        {
            foreach (Obstacle obstacle in Solids(includeStructures))
            {
                Vector2 offset = point - obstacle.Center;
                float reach = obstacle.Radius + radius;
                float distance = offset.magnitude;
                if (distance >= reach) continue;
                Vector2 direction = distance > 0.0001f ? offset / distance : Vector2.up;
                point = obstacle.Center + direction * reach;
            }
            return Clamp(point);
        }

        /// <summary>
        /// The direction to walk from <paramref name="from"/> toward <paramref name="to"/>: straight if the way is clear,
        /// otherwise toward a point beside the first solid thing in the way, so walkers go around instead of into it.
        /// Something that covers the target itself is ignored (the walker stops at its edge, see <see cref="ArrivalDistance"/>).
        /// <paramref name="side"/> remembers which way the walker went around (+1 right, -1 left, 0 path clear): it keeps
        /// that side until the way is clear, so it follows a long wall to its end instead of turning back and forth.
        /// </summary>
        public Vector2 SteerAround(Vector2 from, Vector2 to, float radius, bool includeStructures, ref int side)
        {
            Vector2 toTarget = to - from;
            float distance = toTarget.magnitude;
            if (distance < 0.0001f) return Vector2.zero;
            Vector2 direction = toTarget / distance;
            float look = Mathf.Min(distance, LookAhead);

            bool found = false;
            float nearest = float.MaxValue;
            Obstacle blocking = default;
            foreach (Obstacle obstacle in Solids(includeStructures))
            {
                float reach = obstacle.Radius + radius;
                if (Vector2.Distance(to, obstacle.Center) < reach) continue;

                Vector2 relative = obstacle.Center - from;
                float along = Vector2.Dot(relative, direction);
                if (along < -reach * 0.5f || along > look + reach) continue;
                float sideways = Mathf.Abs(direction.x * relative.y - direction.y * relative.x);
                if (sideways >= reach || along >= nearest) continue;

                found = true;
                nearest = along;
                blocking = obstacle;
            }
            if (!found)
            {
                side = 0;
                return direction;
            }

            // First contact: pass on the side the path already leans to (the obstacle's other side).
            if (side == 0)
            {
                Vector2 toCenter = blocking.Center - from;
                side = direction.x * toCenter.y - direction.y * toCenter.x > 0f ? 1 : -1;
            }
            Vector2 passSide = side > 0 ? new Vector2(direction.y, -direction.x) : new Vector2(-direction.y, direction.x);
            Vector2 aim = blocking.Center + passSide * (blocking.Radius + radius + 0.2f);
            Vector2 steer = aim - from;
            if (Vector2.Dot(steer, direction) < 0.1f) steer = passSide + direction * 0.5f; // already alongside: slide past
            return steer.normalized;
        }

        /// <summary>
        /// If the target lies inside something solid, how close a walker can get (it counts as arrived there); else 0.
        /// </summary>
        public float ArrivalDistance(Vector2 target, float radius, bool includeStructures)
        {
            float best = 0f;
            foreach (Obstacle obstacle in Solids(includeStructures))
            {
                float inside = obstacle.Radius + radius - Vector2.Distance(target, obstacle.Center);
                if (inside > 0f) best = Mathf.Max(best, inside + 0.15f);
            }
            return best;
        }

        /// <summary>
        /// Builds a piece of scenery under <paramref name="root"/>: a soft "Shadow" (if the type has one) and the "Visual"
        /// sprite, at the type's standard size. Used at runtime and by the prefab generator, so both look the same.
        /// </summary>
        public static SpriteRenderer BuildScenery(GameArt art, Transform root, SceneryType type, Sprite sprite, Sprite shadowSprite)
        {
            if (type.ShadowAlpha > 0f && shadowSprite != null)
            {
                SpriteRenderer shadow = art.CreateSprite("Shadow", root, shadowSprite, SortingOrder.Shadows);
                shadow.color = new Color(0f, 0f, 0f, type.ShadowAlpha);
                GameArt.ScaleToWidth(shadow, type.Width * 0.95f);
                shadow.transform.localPosition = type.ShadowOffset * type.Width;
            }

            SpriteRenderer visual = art.CreateSprite("Visual", root, sprite, type.Order);
            GameArt.ScaleToWidth(visual, type.Width);
            return visual;
        }

        static bool HasAny(GameObject[] prefabs) => prefabs != null && System.Array.Exists(prefabs, prefab => prefab != null);

        /// <summary>
        /// A random point at least <paramref name="clearance"/> from the ant hill, in the play area shrunk by
        /// <paramref name="margin"/> on every side (a negative margin reaches past the edge into the scenery).
        /// </summary>
        Vector2 RandomFieldPoint(float clearance, float margin)
        {
            Vector2 position;
            int attempts = 0;
            do
            {
                position = new Vector2(
                    RandomRange(Bounds.xMin + margin, Bounds.xMax - margin),
                    RandomRange(Bounds.yMin + margin, Bounds.yMax - margin));
            } while (Vector2.Distance(position, EntrancePoint) < clearance && ++attempts < 50);
            return position;
        }

        float RandomRange(float min, float max) => min + (float)random.NextDouble() * (max - min);
    }
}
