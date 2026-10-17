using UnityEngine;

namespace AntColony
{
    /// <summary>
    /// An enemy walks across the surface toward the ant hill. It stops to bite any ant in reach,
    /// chews through any wall or tower in its way, and once it reaches the entrance it attacks the queen until it is killed.
    /// </summary>
    public class Enemy : MonoBehaviour
    {
        /// <summary>How close to the entrance an enemy must get to start hurting the queen.</summary>
        const float EntranceReach = 1.2f;

        [Header("Art (empty slots use the game's art for this enemy kind)")]
        [Tooltip("Seen from above, drawn facing right.")]
        [SerializeField] CreatureSprites sprites = new CreatureSprites();

        ColonyGame game;
        EnemyStats stats;
        SpriteRenderer body;
        UnitAnimator animator;
        float attackCooldownLeft;
        int avoidSide;
        float summonTimer;
        BossSkills skills;

        public bool IsBoss => stats != null && stats.kind == EnemyKind.Boss;

        public Health Health { get; private set; }
        public Vector2 Position => transform.position;
        /// <summary>Size of the body for crowding and reach, in world units.</summary>
        public float Radius => stats.bodyWidth * 0.4f;
        public bool IsAttackingNest { get; private set; }

        public EnemyStats Stats => stats;
        public float HealthMultiplier { get; private set; }

        public void Init(ColonyGame colonyGame, EnemyStats enemyStats, float healthMultiplier, Vector2 spawnPoint)
        {
            HealthMultiplier = healthMultiplier;
            game = colonyGame;
            stats = enemyStats;
            transform.position = spawnPoint;

            CreatureFrames frames = GameArt.Frames(sprites, game.Art.EnemyFrames(stats.kind));
            body = UnitBody.FindOrCreate(game.Art, transform, frames.Walk[0], stats.bodyWidth, SortingOrder.Enemies, Color.white);
            animator = UnitAnimator.Attach(transform, body, BodyView.TopDown, frames);
            HealthBar bar = HealthBar.Create(game.Art, transform, stats.bodyWidth * 0.5f + 0.2f, IsBoss ? 2.4f : 0.9f, new Color(0.9f, 0.25f, 0.2f));
            // Until the boss has its own art it is a giant dark-red ant.
            if (IsBoss && stats.prefab == null) body.color = new Color(0.8f, 0.22f, 0.16f);
            summonTimer = game.Settings.bossSummonEvery;
            if (IsBoss)
            {
                if (!TryGetComponent(out skills)) skills = gameObject.AddComponent<BossSkills>();
                skills.Init(game, this, animator);
            }

            if (!TryGetComponent(out Health health)) health = gameObject.AddComponent<Health>();
            Health = health;
            Health.Init(stats.health * healthMultiplier, bar, body);
            Health.Died += HandleDeath;

            animator.Face(game.Surface.EntrancePoint - spawnPoint);
        }

        void Update()
        {
            attackCooldownLeft -= Time.deltaTime;
            if (IsBoss) UpdateSummons();
            if (skills != null && skills.IsCasting) return; // standing still, winding up a big move

            Ant victim = game.Colony.FindNearestSurfaceAnt(Position, Radius, stats.attackRange);
            if (victim != null)
            {
                IsAttackingNest = false;
                Bite(victim.Position, victim.Health);
                return;
            }

            Vector2 entrance = game.Surface.EntrancePoint;
            Structure blocking = game.Defenses.FindBlocking(Position, Radius, entrance - Position, stats.attackRange);
            if (blocking != null)
            {
                IsAttackingNest = false;
                Bite(blocking.Position, blocking.Health);
                return;
            }

            if (Vector2.Distance(entrance, Position) > EntranceReach)
            {
                IsAttackingNest = false;
                animator.Face(entrance - Position);
                // Around boulders and logs (towers and walls get chewed instead, see above).
                Vector2 direction = game.Surface.SteerAround(Position, entrance, Radius, includeStructures: false, ref avoidSide);
                Vector2 next = Position + direction * Mathf.Min(stats.speed * Time.deltaTime, Vector2.Distance(Position, entrance));
                transform.position = game.Surface.PushOut(next, Radius, includeStructures: false);
                return;
            }

            IsAttackingNest = true;
            animator.Face(entrance - Position);
            if (attackCooldownLeft <= 0f)
            {
                animator.PlayAttack();
                game.Colony.DamageQueen(stats.damage);
                game.PlayEffect(EffectKind.QueenHit, entrance);
                attackCooldownLeft = stats.attackCooldown;
            }
        }

        void Bite(Vector2 targetPosition, Health targetHealth)
        {
            animator.Face(targetPosition - Position);
            if (attackCooldownLeft > 0f) return;

            animator.PlayAttack();
            targetHealth.TakeDamage(stats.damage);
            game.PlayEffect(EffectKind.EnemyBite, targetPosition);
            attackCooldownLeft = stats.attackCooldown;
        }

        /// <summary>The boss calls helpers in every so often.</summary>
        void UpdateSummons()
        {
            float every = game.Settings.bossSummonEvery;
            if (every <= 0f) return;

            summonTimer -= Time.deltaTime;
            if (summonTimer > 0f) return;
            summonTimer = every;
            animator.PlayAttack();
            game.PlayEffect(EffectKind.BossSummon, Position);
            game.Waves.SpawnHelpers(Position, game.Settings.bossSummonCount);
        }

        void HandleDeath()
        {
            if (IsBoss)
            {
                game.PlayEffect(EffectKind.BossDied, Position);
                game.ShowMessage($"The {stats.name} is defeated!");
            }
            game.PlayEffect(EffectKind.EnemyDied, Position);
            game.Audio.Play(stats.kind == EnemyKind.Spider ? Sound.EnemyDeath : Sound.EnemyDeathHeavy, Position);
            game.Waves.RemoveEnemy(this);
            game.Food.SpawnCarcass(Position, body.sprite, stats.bodyWidth, stats.foodWhenKilled);
            Destroy(gameObject);
        }
    }
}
