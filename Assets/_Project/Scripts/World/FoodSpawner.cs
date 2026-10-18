using System.Collections.Generic;
using UnityEngine;

namespace AntColony
{
    /// <summary>Scatters food over the surface and helps workers pick which pile to fetch from.</summary>
    public class FoodSpawner : MonoBehaviour
    {
        /// <summary>Workers ignore food that has an enemy this close to it.</summary>
        const float EnemySafetyDistance = 4f;
        /// <summary>A pile this far from the ant hill is picked half as often as one right next to it.</summary>
        const float NearbyDistance = 10f;
        /// <summary>How much each worker already heading to a pile makes it less likely to be picked.</summary>
        const float CrowdingPenalty = 0.8f;

        /// <summary>Widths of the piles built in code (also used by the prefab generator).</summary>
        public const float LeafWidth = 1.1f;
        public const float BerriesWidth = 0.85f;
        public const float AcornWidth = 0.85f;

        struct FoodType
        {
            public string Name;
            public FoodSource Prefab;
            public Sprite Sprite;
            public FoodKind Kind;
            /// <summary>How many trips the pile lasts.</summary>
            public int Trips;
            /// <summary>Food or leaves one worker brings home per trip.</summary>
            public int Load;
            public float Width;
        }

        /// <summary>Food or leaves a worker gets from a defeated enemy per trip.</summary>
        const int CarcassLoad = 2;

        readonly List<FoodSource> sources = new List<FoodSource>();
        ColonyGame game;
        FoodType[] foodTypes;
        float spawnTimer;

        public IReadOnlyList<FoodSource> Sources => sources;

        public void Init(ColonyGame colonyGame)
        {
            game = colonyGame;
            foodTypes = new[]
            {
                // Leaves are common and plentiful but need a fungus garden; berries and acorns are food right away.
                new FoodType { Name = "Leaf", Prefab = game.Prefabs.leaf, Sprite = game.Art.Leaf, Kind = FoodKind.Leaves, Trips = 5, Load = 3, Width = LeafWidth },
                new FoodType { Name = "Berries", Prefab = game.Prefabs.berries, Sprite = game.Art.Berry, Kind = FoodKind.Food, Trips = 3, Load = 2, Width = BerriesWidth },
                new FoodType { Name = "Acorn", Prefab = game.Prefabs.acorn, Sprite = game.Art.Acorn, Kind = FoodKind.Food, Trips = 4, Load = 3, Width = AcornWidth },
            };

            for (int i = 0; i < game.Settings.startingFoodSources; i++)
                SpawnRandomFood();

            spawnTimer = game.Settings.foodSpawnInterval;
        }

        void Update()
        {
            spawnTimer -= Time.deltaTime;
            if (spawnTimer > 0f) return;

            spawnTimer = game.Settings.foodSpawnInterval;
            if (sources.Count < game.Settings.maxFoodSources) SpawnRandomFood();
        }

        /// <summary>
        /// Picks a pile that still has an unclaimed unit and no enemy next to it, and claims one unit of it for the caller.
        /// The pick is random: piles close to the ant hill are likelier, and piles other workers are already heading to
        /// less likely, so the workers spread out over the meadow instead of queueing at one pile. Null if there is none.
        /// </summary>
        /// <param name="only">If set, only piles of this kind are considered.</param>
        /// <param name="markedOnly">Only piles the player marked with the Gather tool.</param>
        public FoodSource ClaimBestFood(FoodKind? only = null, bool markedOnly = false)
        {
            float totalWeight = 0f;
            foreach (FoodSource food in sources)
                totalWeight += PickWeight(food, only, markedOnly);
            if (totalWeight <= 0f) return null;

            float roll = Random.value * totalWeight;
            foreach (FoodSource food in sources)
            {
                float weight = PickWeight(food, only, markedOnly);
                if (weight <= 0f) continue;
                roll -= weight;
                if (roll > 0f) continue;

                food.TryClaim();
                return food;
            }
            return null;
        }

        float PickWeight(FoodSource food, FoodKind? only, bool markedOnly)
        {
            if (!food.HasUnclaimedUnits) return 0f;
            if (markedOnly && !food.IsMarked) return 0f;
            if (only.HasValue && food.Kind != only.Value) return 0f;
            if (game.Waves.IsEnemyNear(food.Position, EnemySafetyDistance)) return 0f;

            float distance = Vector2.Distance(food.Position, game.Surface.EntrancePoint);
            float nearness = 1f / (1f + distance / NearbyDistance);
            float crowding = 1f / (1f + food.ClaimedUnits * CrowdingPenalty);
            return nearness * crowding;
        }

        /// <summary>
        /// A pile of berries on a free spot between <paramref name="minDistance"/> and <paramref name="maxDistance"/>
        /// from <paramref name="center"/>, preferably to its right (clear of the panels on the left). Used by the tutorial.
        /// </summary>
        public FoodSource SpawnNear(Vector2 center, float minDistance, float maxDistance)
        {
            if (!TryFindType("Berries", out FoodType type)) type = foodTypes[0];
            for (int attempt = 0; attempt < 24; attempt++)
            {
                float angle = (attempt % 2 == 0 ? 1f : -1f) * attempt * 9f + 15f; // fan out from just above the right
                Vector2 direction = Quaternion.Euler(0f, 0f, angle) * Vector2.right;
                Vector2 spot = game.Surface.Clamp(center + direction * Mathf.Lerp(minDistance, maxDistance, (attempt % 3) / 2f));
                if (game.Surface.IsBlocked(spot, 0.5f)) continue;

                bool crowded = false;
                foreach (FoodSource food in sources)
                {
                    if (Vector2.Distance(food.Position, spot) < 1f)
                    {
                        crowded = true;
                        break;
                    }
                }
                if (crowded) continue;
                return SpawnFood(type.Name, type.Prefab, spot, type.Sprite, type.Width, type.Trips, type.Kind, type.Load, upsideDown: false);
            }
            return null;
        }

        /// <summary>A defeated enemy turns into food lying on its back.</summary>
        public void SpawnCarcass(Vector2 position, Sprite sprite, float width, int units)
        {
            if (units <= 0) return;
            SpawnFood("Carcass", null, position, sprite, width, units, FoodKind.Food, CarcassLoad, upsideDown: true);
        }

        public void Remove(FoodSource food)
        {
            sources.Remove(food);
        }

        // ---------- Saving ----------

        public void Write(SaveData data)
        {
            foreach (FoodSource food in sources)
            {
                data.foodPiles.Add(new FoodSave
                {
                    name = food.name,
                    kind = (int)food.Kind,
                    position = food.Position,
                    unitsLeft = food.UnitsLeft,
                    startingUnits = food.StartingUnits,
                    load = food.Load,
                    marked = food.IsMarked,
                });
            }
        }

        /// <summary>Replaces the piles of the fresh world with the saved ones.</summary>
        public void Read(SaveData data)
        {
            foreach (FoodSource food in sources) Destroy(food.gameObject);
            sources.Clear();

            foreach (FoodSave saved in data.foodPiles)
            {
                FoodSource food;
                if (TryFindType(saved.name, out FoodType type))
                {
                    food = SpawnFood(type.Name, type.Prefab, saved.position, type.Sprite, type.Width, saved.startingUnits, type.Kind, type.Load, upsideDown: false);
                }
                else
                {
                    // A defeated enemy: drawn as a beetle on its back.
                    food = SpawnFood(saved.name, null, saved.position, game.Art.Beetle.Walk[0], 1.1f, saved.startingUnits, (FoodKind)saved.kind, saved.load, upsideDown: true);
                }
                food.RestoreUnits(saved.unitsLeft, saved.startingUnits);
                if (saved.marked) game.Gather.Mark(food);
            }
        }

        /// <summary>
        /// A new pile: usually next to an existing one (often of the same kind), so food grows in patches;
        /// otherwise at a random spot, which starts a new patch.
        /// </summary>
        void SpawnRandomFood()
        {
            GameSettings settings = game.Settings;
            FoodType type = Random.value < settings.leafChance ? foodTypes[0] : foodTypes[Random.Range(1, foodTypes.Length)];
            bool fewNear = CountNearNest() < settings.foodMinNearPiles;
            Vector2 position = fewNear || Random.value < settings.foodNearShare ? RandomPointNearNest() : game.Surface.RandomPoint(settings.foodMinDistanceFromNest);

            // Often the pile grows next to an existing one (piles nearer the nest are picked more), making patches.
            if (!fewNear && sources.Count > 0 && Random.value < settings.foodClusterChance)
            {
                FoodSource neighbour = PickNeighbour();
                if (neighbour != null && TryFindSpotNear(neighbour.Position, out Vector2 spot))
                {
                    position = spot;
                    if (Random.value < settings.foodClusterSameKind && TryFindType(neighbour.name, out FoodType sameType)) type = sameType;
                }
            }

            // Some piles are big or huge: more trips, drawn bigger.
            float roll = Random.value;
            int sizeFactor = roll < settings.foodHugePileChance ? 3 : roll < settings.foodHugePileChance + settings.foodBigPileChance ? 2 : 1;
            SpawnFood(type.Name, type.Prefab, position, type.Sprite, type.Width, type.Trips * sizeFactor, type.Kind, type.Load, upsideDown: false);
        }

        int CountNearNest()
        {
            int count = 0;
            float radius = game.Settings.foodNearRadius;
            foreach (FoodSource food in sources)
            {
                if (Vector2.Distance(food.Position, game.Surface.EntrancePoint) <= radius) count++;
            }
            return count;
        }

        /// <summary>A free spot between the nest's food-free ring and <see cref="GameSettings.foodNearRadius"/>.</summary>
        Vector2 RandomPointNearNest()
        {
            GameSettings settings = game.Settings;
            Vector2 hill = game.Surface.EntrancePoint;
            float min = settings.foodMinDistanceFromNest;
            float max = Mathf.Max(min + 1f, settings.foodNearRadius);
            for (int attempt = 0; attempt < 12; attempt++)
            {
                // Square root spreads the piles evenly over the ring instead of crowding its inner edge.
                float distance = Mathf.Sqrt(Random.Range(min * min, max * max));
                Vector2 spot = game.Surface.Clamp(hill + Random.insideUnitCircle.normalized * distance);
                if (Vector2.Distance(spot, hill) < min || game.Surface.IsBlocked(spot, 0.5f)) continue;
                return spot;
            }
            return game.Surface.RandomPoint(min);
        }

        /// <summary>An existing pile to grow a patch around; piles near the nest are likelier.</summary>
        FoodSource PickNeighbour()
        {
            float total = 0f;
            foreach (FoodSource food in sources) total += NeighbourWeight(food);
            float roll = Random.value * total;
            foreach (FoodSource food in sources)
            {
                roll -= NeighbourWeight(food);
                if (roll <= 0f) return food;
            }
            return sources.Count > 0 ? sources[sources.Count - 1] : null;
        }

        float NeighbourWeight(FoodSource food)
        {
            float distance = Vector2.Distance(food.Position, game.Surface.EntrancePoint);
            return 1f / (1f + distance / (game.Settings.foodNearRadius * 0.5f));
        }

        /// <summary>A free spot a little away from <paramref name="center"/>: inside the field, off the ant hill, not on another pile.</summary>
        bool TryFindSpotNear(Vector2 center, out Vector2 spot)
        {
            GameSettings settings = game.Settings;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                spot = game.Surface.Clamp(center + Random.insideUnitCircle.normalized * Random.Range(1.2f, settings.foodClusterSpread));
                if (Vector2.Distance(spot, game.Surface.EntrancePoint) < settings.foodMinDistanceFromNest) continue;
                if (game.Surface.IsBlocked(spot, 0.5f)) continue;

                bool crowded = false;
                foreach (FoodSource food in sources)
                {
                    if (Vector2.Distance(food.Position, spot) < 1f)
                    {
                        crowded = true;
                        break;
                    }
                }
                if (!crowded) return true;
            }
            spot = center;
            return false;
        }

        bool TryFindType(string typeName, out FoodType found)
        {
            foreach (FoodType type in foodTypes)
            {
                if (type.Name != typeName) continue;
                found = type;
                return true;
            }
            found = default;
            return false; // e.g. a carcass
        }

        FoodSource SpawnFood(string objectName, FoodSource prefab, Vector2 position, Sprite sprite, float width, int trips, FoodKind kind, int load, bool upsideDown)
        {
            FoodSource food = prefab != null ? Instantiate(prefab, transform, false) : new GameObject().AddComponent<FoodSource>();
            food.name = objectName;
            food.transform.SetParent(transform, false);
            food.transform.position = game.Surface.Clamp(position);

            SpriteRenderer visual = game.Art.FindOrCreateSprite("Visual", food.transform, sprite, SortingOrder.Food, out bool created);
            visual.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-40f, 40f));
            visual.flipY = upsideDown;
            if (upsideDown) visual.color = new Color(0.75f, 0.65f, 0.6f);
            if (created) GameArt.ScaleToWidth(visual, width);
            else food.PickRandomLook(visual);
            // Big and huge piles (more trips than their kind's usual) are drawn bigger.
            if (!upsideDown && TryFindType(objectName, out FoodType usual) && trips > usual.Trips)
                visual.transform.localScale *= Mathf.Min(1.8f, Mathf.Sqrt(trips / (float)usual.Trips));

            food.Init(this, visual, trips, kind, load);
            sources.Add(food);
            return food;
        }
    }
}
