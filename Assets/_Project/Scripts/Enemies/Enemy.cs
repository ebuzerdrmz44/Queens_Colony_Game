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

        // Finding the way to the ant hill around rocks and logs (see Surface.FindEnemyRoute).
        const float ReplanEvery = 4f;
        const float WaypointReach = 0.4f;
        /// <summary>Barely moving this long: plan again. Much longer: scramble over whatever is in the way.</summary>
        const float StuckReplanTime = 2f;
        const float StuckScrambleTime = 10f;
        const float ScrambleTime = 3f;
        readonly System.Collections.Generic.List<Vector2> route = new System.Collections.Generic.List<Vector2>();
        int routeIndex;
        float replanTimer;
        Vector2 stuckCheckPoint;
        float stuckTime;
        float scrambleTimeLeft;
        BossSkills skills;

        public int WaveNumber { get; set; }
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
                WalkToward(entrance);
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

        /// <summary>
        /// One frame of walking to <paramref name="goal"/> along a planned route around boulders and logs (towers and
        /// walls get chewed instead, see Update). A stuck enemy plans again, and one stuck for long scrambles over the
        /// obstacle, so no enemy can hold up the end of a wave by hiding behind a rock.
        /// </summary>
        void WalkToward(Vector2 goal)
        {
            replanTimer -= Time.deltaTime;
            if (route.Count == 0 || replanTimer <= 0f)
            {
                game.Surface.FindEnemyRoute(Position, goal, route);
                routeIndex = 0;
                replanTimer = ReplanEvery;
            }
            if (routeIndex < route.Count - 1 && Vector2.Distance(Position, route[routeIndex]) < WaypointReach) routeIndex++;
            Vector2 waypoint = route[Mathf.Min(routeIndex, route.Count - 1)];

            float step = Mathf.Min(stats.speed * Time.deltaTime, Vector2.Distance(Position, waypoint));
            Vector2 next;
            if (scrambleTimeLeft > 0f)
            {
                // Over whatever is in the way.
                scrambleTimeLeft -= Time.deltaTime;
                next = Vector2.MoveTowards(Position, goal, stats.speed * Time.deltaTime);
            }
            else
            {
                Vector2 direction = game.Surface.SteerAround(Position, waypoint, Radius, includeStructures: false, ref avoidSide);
                next = game.Surface.PushOut(Position + direction * step, Radius, includeStructures: false);
            }
            animator.Face(next - Position);
            transform.position = next;

            if (Vector2.Distance(Position, stuckCheckPoint) > 0.3f)
            {
                stuckCheckPoint = Position;
                stuckTime = 0f;
                return;
            }
            stuckTime += Time.deltaTime;
            if (stuckTime > StuckScrambleTime)
            {
                scrambleTimeLeft = ScrambleTime;
                stuckTime = 0f;
            }
            else if (stuckTime > StuckReplanTime && replanTimer > 0.5f)
            {
                replanTimer = 0f; // plan again next frame
            }
        }

        void Bite(Vector2 targetPosition, Health targetHealth)
        {
            animator.Face(targetPosition - Position);
            if (attackCooldownLeft > 0f) return;

            animator.PlayAttack();
            targetHealth.TakeDamage(stats.damage, WaveNumber);
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
