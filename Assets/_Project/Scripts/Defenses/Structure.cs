using UnityEngine;

namespace AntColony
{
    public enum StructureKind
    {
        Wall,
        Tower,
    }

    /// <summary>
    /// A wall block or tower on the surface. It starts as a see-through blueprint that builders work on;
    /// once finished it blocks enemies (they have to chew through it), and a tower starts spitting acid.
    /// Damaged structures are repaired by idle builders.
    /// </summary>
    public class Structure : MonoBehaviour
    {
        static readonly Color BlueprintTint = new Color(1f, 1f, 1f, 0.45f);
        static readonly Color BuildBarColor = AutumnPalette.Gold;
        static readonly Color HealthBarColor = new Color(0.5f, 0.85f, 0.3f);

        ColonyGame game;
        SpriteRenderer visual;
        Color builtColor = Color.white;
        HealthBar buildBar;
        float buildTime;
        float buildProgress;
        float shotCooldownLeft;

        public StructureKind Kind { get; private set; }
        public float Radius { get; private set; }
        public Vector2 Position => transform.position;
        public bool IsBuilt { get; private set; }
        /// <summary>Only set once the structure is built.</summary>
        public Health Health { get; private set; }
        public bool NeedsWork => !IsBuilt || Health.Fraction < 1f;

        public void Init(ColonyGame colonyGame, StructureKind kind, Vector2 position, float radius)
        {
            game = colonyGame;
            Kind = kind;
            Radius = radius;
            transform.position = position;
            buildTime = kind == StructureKind.Wall ? game.Settings.wallBuildTime : game.Settings.towerBuildTime;

            Sprite sprite = kind == StructureKind.Wall ? game.Art.Wall : game.Art.Tower;
            visual = game.Art.FindOrCreateSprite("Visual", transform, sprite, SortingOrder.Structures, out bool created);
            if (created) GameArt.ScaleToWidth(visual, radius * 2f);
            builtColor = visual.color;
            visual.color = builtColor * BlueprintTint;

            buildBar = HealthBar.Create(game.Art, transform, radius + 0.2f, radius * 1.6f, BuildBarColor, alwaysVisible: true);
            buildBar.SetFraction(0f);
        }

        public float BuildProgress => buildProgress;

        /// <summary>Puts a loaded structure back: finished with its health, or a blueprint with its progress.</summary>
        public void Restore(bool built, float progress, float health)
        {
            if (built)
            {
                buildProgress = 1f;
                Complete();
                Health.SetCurrent(health);
                return;
            }
            buildProgress = Mathf.Clamp01(progress);
            buildBar.SetFraction(buildProgress);
        }

        /// <summary>A builder spends <paramref name="seconds"/> of work here: building, or repairing once built.</summary>
        public void AddWork(float seconds)
        {
            if (IsBuilt)
            {
                Health.Heal(seconds * game.Settings.repairPerSecond);
                return;
            }

            buildProgress = Mathf.Min(1f, buildProgress + seconds / buildTime);
            buildBar.SetFraction(buildProgress);
            if (buildProgress >= 1f) Complete();
        }

        void Complete()
        {
            IsBuilt = true;
            visual.color = builtColor;
            Destroy(buildBar.gameObject);
            game.PlayEffect(EffectKind.StructureBuilt, Position);
            game.Audio.Play(Kind == StructureKind.Wall ? Sound.WallBuilt : Sound.TowerBuilt, Position);

            float maxHealth = Kind == StructureKind.Wall ? game.Settings.wallHealth : game.Settings.towerHealth;
            HealthBar bar = HealthBar.Create(game.Art, transform, Radius + 0.2f, Radius * 1.6f, HealthBarColor);
            if (!TryGetComponent(out Health health)) health = gameObject.AddComponent<Health>();
            Health = health;
            Health.Init(maxHealth, bar, visual);
            Health.Died += () =>
            {
                game.Defenses.Remove(this);
                game.PlayEffect(EffectKind.StructureDestroyed, Position);
                Destroy(gameObject);
            };
        }

        void Update()
        {
            if (!IsBuilt || Kind != StructureKind.Tower) return;

            shotCooldownLeft -= Time.deltaTime;
            if (shotCooldownLeft > 0f) return;

            GameSettings settings = game.Settings;
            Enemy target = game.Waves.FindNearestEnemy(Position, Position, settings.towerRange);
            if (target == null) return;

            AcidShot.Fire(game, Position, target, settings.towerDamage, prefab: game.Prefabs.towerShot);
            game.Audio.Play(Sound.TowerFire, Position);
            shotCooldownLeft = settings.towerCooldown;
        }
    }
}
