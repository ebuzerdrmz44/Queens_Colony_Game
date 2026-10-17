using UnityEngine;

namespace AntColony
{
    public enum BroodStage
    {
        Egg,
        Larva,
    }

    /// <summary>
    /// A future ant lying in a cradle. The egg hatches into a larva on its own; the larva then needs workers to
    /// bring it food (see <see cref="WorkerAnt"/>) and only grows while it has been fed enough. Once fully grown
    /// and fed, it becomes an adult of the caste it was laid for.
    /// </summary>
    public class Brood
    {
        const float SquirmInterval = 0.45f;

        readonly GameSettings settings;
        readonly CasteCosts costs;
        readonly SpriteRenderer sprite;
        readonly HealthBar progressBar;
        readonly Sprite[] larvaFrames;
        readonly Sprite eggSprite;
        float stageTime;
        float squirmTime;

        public AntRole Caste { get; }
        public BroodStage Stage { get; private set; }
        public Room Cradle { get; }
        public int FoodFed { get; private set; }
        public int FoodNeeded => costs.larvaFood;
        /// <summary>A worker is already on the way with food.</summary>
        public bool HasNurse { get; set; }
        public bool IsHungry => Stage == BroodStage.Larva && FoodFed < FoodNeeded;
        /// <summary>True while a larva has caught up with what it was fed and can't grow until it gets more.</summary>
        public bool IsWaitingForFood => Stage == BroodStage.Larva && GrowthFraction >= FedFraction && IsHungry;
        public bool IsReadyToHatch => Stage == BroodStage.Larva && FoodFed >= FoodNeeded && stageTime >= costs.larvaTime;

        /// <summary>0..1 over the whole life: egg first, then larva.</summary>
        public float Progress
        {
            get
            {
                float eggShare = settings.eggTime / (settings.eggTime + costs.larvaTime);
                return Stage == BroodStage.Egg
                    ? eggShare * Mathf.Clamp01(stageTime / settings.eggTime)
                    : eggShare + (1f - eggShare) * GrowthFraction;
            }
        }

        float GrowthFraction => Mathf.Clamp01(stageTime / costs.larvaTime);
        float FedFraction => FoodNeeded > 0 ? (float)FoodFed / FoodNeeded : 1f;

        public Brood(ColonyGame game, Room cradle, AntRole caste)
        {
            settings = game.Settings;
            costs = settings.CostsFor(caste);
            Caste = caste;
            Cradle = cradle;
            Stage = BroodStage.Egg;
            eggSprite = game.Art.Egg;
            larvaFrames = game.Art.Larva;

            var root = new GameObject($"{caste} Brood").transform;
            root.SetParent(game.Nest.transform, false);
            root.position = cradle.StandPoint;

            sprite = game.Art.CreateSprite("Body", root, eggSprite, SortingOrder.Brood);
            GameArt.ScaleToWidth(sprite, 0.5f);
            sprite.transform.localPosition = new Vector3(0f, GameArt.PivotHeight(sprite) + 0.05f, 0f);

            progressBar = HealthBar.Create(game.Art, root, 0.85f, 0.7f, CasteColor(caste), alwaysVisible: true);
            progressBar.SetFraction(0f);
        }

        public float StageTime => stageTime;

        /// <summary>Puts a loaded larva or egg back where it was.</summary>
        public void Restore(BroodStage stage, float time, int foodFed)
        {
            if (stage == BroodStage.Larva) BecomeLarva();
            stageTime = time;
            FoodFed = Mathf.Clamp(foodFed, 0, FoodNeeded);
            progressBar.SetFraction(Progress);
        }

        public void Feed(int amount)
        {
            FoodFed = Mathf.Min(FoodNeeded, FoodFed + amount);
        }

        public void Update(float deltaTime)
        {
            if (Stage == BroodStage.Egg)
            {
                stageTime += deltaTime;
                if (stageTime >= settings.eggTime) BecomeLarva();
            }
            else if (GrowthFraction < FedFraction)
            {
                // Larvae only grow as far as their food lets them.
                stageTime = Mathf.Min(costs.larvaTime, stageTime + deltaTime);
                Squirm(deltaTime);
            }

            progressBar.SetFraction(Progress);
        }

        public void Destroy()
        {
            Object.Destroy(sprite.transform.parent.gameObject);
        }

        void BecomeLarva()
        {
            Stage = BroodStage.Larva;
            stageTime = 0f;
            sprite.sprite = larvaFrames[0];
            GameArt.ScaleToWidth(sprite, 0.75f);
            sprite.transform.localPosition = new Vector3(0f, GameArt.PivotHeight(sprite) + 0.05f, 0f);
        }

        void Squirm(float deltaTime)
        {
            squirmTime += deltaTime;
            int frame = Mathf.FloorToInt(squirmTime / SquirmInterval) % larvaFrames.Length;
            sprite.sprite = larvaFrames[frame];
        }

        /// <summary>The progress bar shows which caste is growing.</summary>
        public static Color CasteColor(AntRole caste)
        {
            switch (caste)
            {
                case AntRole.Worker: return AutumnPalette.Gold;
                case AntRole.Soldier: return new Color(0.9f, 0.35f, 0.3f);
                case AntRole.Spitter: return AutumnPalette.Acid;
                default: return new Color(0.6f, 0.9f, 0.5f);
            }
        }
    }
}
