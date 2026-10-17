using UnityEngine;

namespace AntColony
{
    /// <summary>A blob of acid that homes in on one enemy and hurts it on contact. Fizzles if the enemy dies first.</summary>
    public class AcidShot : MonoBehaviour
    {
        const float Speed = 9f;
        const float HitDistance = 0.25f;
        const float Width = 0.3f;

        ColonyGame game;
        Enemy target;
        float damage;

        EffectKind hitEffect = EffectKind.AcidHit;
        float speed = Speed;

        /// <param name="hitEffect">The effect played where it hits (towers and spitters have their own).</param>
        public static AcidShot Fire(ColonyGame game, Vector2 from, Enemy target, float damage,
            EffectKind hitEffect = EffectKind.AcidHit, float width = Width, float speed = Speed)
        {
            SpriteRenderer blob = game.Art.CreateSprite("Acid Shot", game.SurfaceRoot, game.Art.AcidShot, SortingOrder.Projectiles);
            GameArt.ScaleToWidth(blob, width);
            blob.transform.position = from;

            AcidShot shot = blob.gameObject.AddComponent<AcidShot>();
            shot.game = game;
            shot.target = target;
            shot.damage = damage;
            shot.hitEffect = hitEffect;
            shot.speed = speed;
            return shot;
        }

        void Update()
        {
            if (target == null || target.Health.IsDead)
            {
                Destroy(gameObject);
                return;
            }

            Vector2 next = Vector2.MoveTowards(transform.position, target.Position, speed * Time.deltaTime);
            transform.position = next;

            if (Vector2.Distance(next, target.Position) <= HitDistance)
            {
                target.Health.TakeDamage(damage);
                game.PlayEffect(hitEffect, next);
                Destroy(gameObject);
            }
        }
    }
}
