using System.Collections.Generic;
using UnityEngine;

namespace AntColony
{
    /// <summary>
    /// A blob of acid that homes in on one enemy and hurts it on contact. Fizzles if the enemy dies first.
    /// It can be a prefab (Prefabs > Projectiles): the whole object flies and turns to face where it's going (its
    /// right / X axis forward), so children such as trail particles are carried along. When it lands, child particle
    /// systems and trail renderers are let go and fade out instead of vanishing.
    /// </summary>
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
        GameObject trail;
        Quaternion baseRotation = Quaternion.identity;

        /// <param name="hitEffect">The effect played where it hits (towers and spitters have their own).</param>
        /// <param name="trailEffect">An extra trail effect riding on the shot (Effects slot).</param>
        /// <param name="prefab">The shot to fly (Prefabs > Projectiles); null for the built-in blob of <paramref name="width"/>.</param>
        public static AcidShot Fire(ColonyGame game, Vector2 from, Enemy target, float damage,
            EffectKind hitEffect = EffectKind.AcidHit, float width = Width, float speed = Speed,
            EffectKind trailEffect = EffectKind.TowerShotTrail, GameObject prefab = null)
        {
            Transform root;
            if (prefab != null)
            {
                root = Instantiate(prefab, game.SurfaceRoot, false).transform;
                root.name = prefab.name;
                ColonyGame.RaiseToEffectsOrder(root.gameObject);
                ColonyGame.FollowTransformVelocity(root.gameObject);
            }
            else
            {
                SpriteRenderer blob = game.Art.CreateSprite("Acid Shot", game.SurfaceRoot, game.Art.AcidShot, SortingOrder.Projectiles);
                GameArt.ScaleToWidth(blob, width);
                root = blob.transform;
            }
            root.position = from;

            AcidShot shot = root.gameObject.AddComponent<AcidShot>();
            shot.game = game;
            shot.target = target;
            shot.damage = damage;
            shot.hitEffect = hitEffect;
            shot.speed = speed;
            shot.baseRotation = root.rotation;
            shot.Aim((Vector2)target.Position - from);
            shot.trail = game.AttachTrail(trailEffect, root);
            return shot;
        }

        /// <summary>Turns the shot (and everything on it) to face <paramref name="direction"/>.</summary>
        void Aim(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.000001f) return;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle) * baseRotation;
        }

        void Update()
        {
            if (target == null || target.Health.IsDead)
            {
                Finish();
                return;
            }

            Vector2 previous = transform.position;
            Vector2 next = Vector2.MoveTowards(previous, target.Position, speed * Time.deltaTime);
            transform.position = next;
            Aim(next - previous);

            if (Vector2.Distance(next, target.Position) <= HitDistance)
            {
                target.Health.TakeDamage(damage);
                game.PlayEffect(hitEffect, next, previous - next); // splashes back the way it came
                Finish();
            }
        }

        /// <summary>Lets go of anything that should fade out (trails, child particles), then removes the shot.</summary>
        void Finish()
        {
            game.DropTrail(trail);
            var children = new List<Transform>();
            foreach (Transform child in transform) children.Add(child);
            foreach (Transform child in children)
            {
                if (child.GetComponentInChildren<ParticleSystem>() != null || child.GetComponentInChildren<TrailRenderer>() != null)
                    game.DropTrail(child.gameObject);
            }
            Destroy(gameObject);
        }
    }
}
