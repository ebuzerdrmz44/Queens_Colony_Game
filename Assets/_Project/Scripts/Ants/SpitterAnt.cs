using UnityEngine;

namespace AntColony
{
    /// <summary>
    /// A soldier that fights from a distance: it keeps about <see cref="GameSettings.spitterRange"/> from its enemy and
    /// spits blobs of acid at it, backing away from enemies that get too close. It takes the same orders as a soldier
    /// (select it with the mouse, right-click to move or attack; E / Q) and guards a wider ring around its post.
    /// It is frail, so keep it behind the biters and the walls.
    /// </summary>
    public class SpitterAnt : SoldierAnt
    {
        /// <summary>Closer than this share of its range, it steps back before spitting again.</summary>
        const float BackOffShare = 0.45f;
        /// <summary>It walks in until the enemy is this share of its range away.</summary>
        const float ApproachShare = 0.85f;

        public override AntRole Role => AntRole.Spitter;
        protected override float Speed => Game.Settings.spitterSpeed;
        protected override float MaxHealth => Game.Settings.spitterHealth;
        public override float BodyWidth => 1.05f;
        public override Color Tint => new Color(0.62f, 0.85f, 0.45f);
        protected override float GuardRadius => Mathf.Max(Game.Settings.soldierGuardRadius, Game.Settings.spitterRange + 1.5f);

        protected override void Fight(Enemy enemy)
        {
            GameSettings settings = Game.Settings;
            float range = settings.spitterRange;
            Vector2 fromEnemy = Position - enemy.Position;
            float distance = fromEnemy.magnitude - enemy.Radius;
            Vector2 away = fromEnemy.sqrMagnitude > 0.0001f ? fromEnemy.normalized : Random.insideUnitCircle.normalized;

            if (distance > range)
            {
                // Walk in to spitting distance.
                StepTowards(enemy.Position + away * (enemy.Radius + range * ApproachShare));
                return;
            }
            if (distance < range * BackOffShare && attackCooldownLeft > 0f)
            {
                // Too close: back off while reloading.
                StepTowards(Position + away * 1.5f);
                return;
            }

            FaceTowards(enemy.Position);
            if (attackCooldownLeft > 0f) return;

            BodyAnimator.PlayAttack();
            Vector2 mouth = Position - away * (BodyWidth * 0.45f);
            Game.PlayEffect(EffectKind.SpitterSpit, mouth);
            AcidShot.Fire(Game, mouth, enemy, settings.spitterDamage, EffectKind.SpitterAcidHit, width: 0.22f, speed: 7f);
            attackCooldownLeft = settings.spitterCooldown;
        }
    }
}
