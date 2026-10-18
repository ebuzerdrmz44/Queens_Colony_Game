using UnityEngine;

namespace AntColony
{
    /// <summary>
    /// Soldiers follow the player's orders on the surface (select them with the mouse, see <see cref="SoldierSelection"/>):
    /// - Guard: stand at a post and fight any enemy that comes within the guard radius of it (the default),
    /// - Move: walk to a point, ignoring enemies on the way, then guard there,
    /// - Attack: chase one enemy until it is dead, then guard where the fight ended.
    /// New soldiers climb out of the nest and guard a spot near the rally flag.
    /// Fighters close in on a ring around their enemy; <see cref="Crowd"/> spreads them along it,
    /// so several soldiers surround the enemy instead of piling onto one spot.
    /// </summary>
    public class SoldierAnt : Ant
    {
        enum Order
        {
            Guard,
            Move,
            Attack,
        }

        const float PostSpread = 1.5f;
        /// <summary>A guard gives up chasing an enemy that pulls it this far past the guard radius.</summary>
        const float LeashExtra = 3f;
        /// <summary>Close enough to a move target or guard post: soldiers get pushed around, so they never land exactly on it.</summary>
        const float ArriveDistance = 0.35f;
        /// <summary>Seconds without getting closer before a soldier stops where it is (the spot is crowded or blocked).</summary>
        const float GiveUpTime = 1f;

        Order order;
        Vector2 post;
        Enemy target;
        protected float attackCooldownLeft;
        /// <summary>When it last fought, so the status can say "fighting" for a moment after each blow.</summary>
        float lastFightTime = -10f;
        SpriteRenderer selectionRing;
        float bestDistance = float.MaxValue;
        float noProgressTime;

        public override AntRole Role => AntRole.Soldier;
        public bool IsSelected { get; private set; }
        protected override float Speed => Game.Settings.soldierSpeed;
        protected override float MaxHealth => Game.Settings.soldierHealth;
        public override float BodyWidth => 1.15f;
        public override Color Tint => new Color(0.55f, 0.42f, 0.42f);

        public void OrderMove(Vector2 point)
        {
            order = Order.Move;
            post = Game.Surface.Clamp(point);
            target = null;
            ResetProgress();
            ClearRoute();
        }

        public void OrderAttack(Enemy enemy)
        {
            order = Order.Attack;
            target = enemy;
            ClearRoute();
        }

        public override AntState Status
        {
            get
            {
                if (Time.time - lastFightTime < 0.5f) return AntState.Fighting;
                if (!IsOnSurface || order == Order.Move) return AntState.Moving;
                return AntState.Guarding;
            }
        }

        public override string Activity
        {
            get
            {
                switch (Status)
                {
                    case AntState.Fighting: return order == Order.Attack ? "attacking its target" : "fighting off an enemy";
                    case AntState.Moving: return IsOnSurface ? "marching to its spot" : "climbing out of the nest";
                    default: return IsSelected ? "guarding (selected)" : "guarding its post";
                }
            }
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            selectionRing.enabled = selected;
        }

        protected override void OnSpawned()
        {
            // Each soldier keeps its own spot near the flag so they don't stand on top of each other.
            post = Game.Surface.Clamp(Game.Colony.RallyPoint + Random.insideUnitCircle * PostSpread);

            selectionRing = Game.Art.FindOrCreateSprite("Selection Ring", transform, Game.Art.SelectionRing, SortingOrder.SelectionRings, out bool created);
            if (created) selectionRing.color = AutumnPalette.Selection;
            selectionRing.enabled = false;
        }

        protected override void OnViewChanged(BodyView view)
        {
            // A flat oval at the feet in the nest, a full circle around the body from above.
            float width = BodyWidth * 1.15f;
            GameArt.ScaleToWidth(selectionRing, width);
            if (view == BodyView.Side)
            {
                selectionRing.transform.localScale = new Vector3(selectionRing.transform.localScale.x, selectionRing.transform.localScale.y * 0.35f, 1f);
                selectionRing.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            }
            else
            {
                selectionRing.transform.localPosition = Vector3.zero;
            }
        }

        void Update()
        {
            attackCooldownLeft -= Time.deltaTime;

            if (!IsOnSurface)
            {
                // Still in the nest: climb out first.
                if (!HasRoute) GoTo(order == Order.Attack && target != null ? target.Position : post);
                FollowRoute();
                return;
            }

            ClearRoute();

            switch (order)
            {
                case Order.Attack:
                    if (target == null || target.Health.IsDead)
                    {
                        order = Order.Guard;
                        post = Position;
                        break;
                    }
                    lastFightTime = Time.time;
                    Fight(target);
                    break;

                case Order.Move:
                    if (MoveToPost()) order = Order.Guard;
                    break;

                case Order.Guard:
                    Guard();
                    break;
            }
        }

        void Guard()
        {
            GameSettings settings = Game.Settings;
            Enemy enemy = Game.Waves.FindNearestEnemy(Position, post, GuardRadius);

            bool tooFarFromPost = Vector2.Distance(Position, post) > GuardRadius + LeashExtra;
            if (enemy == null || tooFarFromPost)
            {
                MoveToPost();
                return;
            }

            lastFightTime = Time.time;
            Fight(enemy);
        }

        /// <summary>
        /// Walks to the post. Returns true once there, or once the soldier stops getting closer (another body is in
        /// the way); the post then moves to where it stands, so crowded soldiers settle instead of shoving forever.
        /// </summary>
        bool MoveToPost()
        {
            float distance = Vector2.Distance(Position, post);
            if (distance <= ArriveDistance)
            {
                ResetProgress();
                return true;
            }

            if (distance < bestDistance - 0.05f)
            {
                bestDistance = distance;
                noProgressTime = 0f;
            }
            else
            {
                noProgressTime += Time.deltaTime;
                if (noProgressTime > GiveUpTime)
                {
                    post = Position;
                    ResetProgress();
                    return true;
                }
            }

            StepTowards(post);
            return false;
        }

        void ResetProgress()
        {
            bestDistance = float.MaxValue;
            noProgressTime = 0f;
        }

        /// <summary>Enemies this close to the post get attacked.</summary>
        protected virtual float GuardRadius => Game.Settings.soldierGuardRadius;

        /// <summary>One frame of fighting <paramref name="enemy"/>: close in and bite.</summary>
        protected virtual void Fight(Enemy enemy)
        {
            Vector2 fromEnemy = Position - enemy.Position;
            float gap = fromEnemy.magnitude - Radius - enemy.Radius;
            if (gap > Game.Settings.soldierAttackRange)
            {
                // Head for the nearest point of the ring around the enemy, not its centre.
                Vector2 side = fromEnemy.sqrMagnitude > 0.0001f ? fromEnemy.normalized : Random.insideUnitCircle.normalized;
                StepTowards(enemy.Position + side * (Radius + enemy.Radius + Game.Settings.soldierAttackRange * 0.5f));
                return;
            }

            FaceTowards(enemy.Position);
            if (attackCooldownLeft <= 0f)
            {
                BodyAnimator.PlayAttack();
                enemy.Health.TakeDamage(Game.Settings.soldierDamage);
                Game.PlayEffect(EffectKind.SoldierBite, enemy.Position);
                attackCooldownLeft = Game.Settings.soldierAttackCooldown;
            }
        }
    }
}
