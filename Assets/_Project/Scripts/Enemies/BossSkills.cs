using System.Collections.Generic;
using UnityEngine;

namespace AntColony
{
    /// <summary>
    /// The final boss's two big moves, taking turns with its normal walking and biting:
    /// - Earthquake: it rears up and slams the ground. Every ant and every wall or tower near it is hurt, and the
    ///   screen shakes. It only does this when something is in reach.
    /// - Mega spit: it aims at the nearest tower or wall (or else a group of ants) and spits a big glob of acid
    ///   that splashes where it lands, with a smaller shake.
    /// Before each move it stands still for a moment (the wind-up), with a blinking red circle on the ground showing
    /// where it will hit, so players can pull their ants out. Numbers are in Settings > Final boss.
    /// </summary>
    public class BossSkills : MonoBehaviour
    {
        enum Move { None, Quake, Spit }

        /// <summary>Wind-up pose frames per second (stepped, pixel-art timing).</summary>
        const float PoseFps = 6f;

        ColonyGame game;
        Enemy boss;
        UnitAnimator animator;
        SpriteRenderer warning;
        Move casting = Move.None;
        float castTime;
        int poseFrame = -1;
        Vector2 spitTarget;
        float quakeTimer;
        float spitTimer;

        /// <summary>The boss is winding up a move: it doesn't walk or bite meanwhile.</summary>
        public bool IsCasting => casting != Move.None;

        public void Init(ColonyGame colonyGame, Enemy owner, UnitAnimator body)
        {
            game = colonyGame;
            boss = owner;
            animator = body;
            GameSettings settings = game.Settings;
            quakeTimer = settings.bossQuakeEvery * 0.6f;
            spitTimer = settings.bossSpitEvery * 0.5f;

            warning = game.Art.CreateSprite("Boss Warning", game.SurfaceRoot, game.Art.RangeCircle, SortingOrder.OrderMarkers);
            warning.color = new Color(1f, 0.25f, 0.2f, 0.85f);
            warning.enabled = false;
        }

        void OnDestroy()
        {
            if (warning != null) Destroy(warning.gameObject);
        }

        void Update()
        {
            if (game == null || boss == null || boss.Health == null || boss.Health.IsDead) return;
            GameSettings settings = game.Settings;

            if (IsCasting)
            {
                UpdateCast(settings);
                return;
            }

            quakeTimer -= Time.deltaTime;
            spitTimer -= Time.deltaTime;

            if (settings.bossQuakeEvery > 0f && quakeTimer <= 0f)
            {
                quakeTimer = settings.bossQuakeEvery;
                if (AnythingNear(boss.Position, settings.bossQuakeRadius)) StartCast(Move.Quake, boss.Position, settings.bossQuakeRadius);
            }
            else if (settings.bossSpitEvery > 0f && spitTimer <= 0f)
            {
                spitTimer = settings.bossSpitEvery;
                if (TryPickSpitTarget(settings.bossSpitRange, out Vector2 target))
                {
                    spitTarget = target;
                    StartCast(Move.Spit, target, settings.bossSpitRadius);
                }
            }
        }

        void StartCast(Move move, Vector2 center, float radius)
        {
            casting = move;
            castTime = 0f;
            poseFrame = -1;
            if (move == Move.Spit) animator.Face(center - boss.Position);
            animator.BeginPose();

            warning.transform.position = center;
            GameArt.ScaleToWidth(warning, radius * 2f);
            warning.enabled = true;
        }

        void UpdateCast(GameSettings settings)
        {
            castTime += Time.deltaTime;
            float windUp = Mathf.Max(0.2f, settings.bossWindUp);

            // The warning circle blinks faster as the move comes.
            float blinkRate = castTime < windUp * 0.6f ? 3f : 8f;
            warning.enabled = Mathf.FloorToInt(castTime * blinkRate) % 2 == 0;

            int frame = Mathf.FloorToInt(castTime * PoseFps);
            if (frame != poseFrame)
            {
                poseFrame = frame;
                // Quake: crouch lower and wider, shaking; spit: rear up, swelling.
                bool shakeLeft = (frame & 1) == 0;
                if (casting == Move.Quake) animator.Pose(-2 - Mathf.Min(frame, 3), 2 + Mathf.Min(frame, 3), shakeLeft ? 1 : -1);
                else animator.Pose(Mathf.Min(frame, 4), 1 + Mathf.Min(frame, 3) / 2, 0);
            }

            if (castTime < windUp) return;

            if (casting == Move.Quake) Quake(settings);
            else Spit(settings);

            animator.EndPose();
            animator.PlayAttack();
            warning.enabled = false;
            casting = Move.None;
        }

        void Quake(GameSettings settings)
        {
            Vector2 center = boss.Position;
            game.PlayEffect(EffectKind.BossQuake, center);
            game.CameraRig.Shake(settings.bossShake, 0.7f);
            HurtAround(center, settings.bossQuakeRadius, settings.bossQuakeDamage, settings.bossQuakeStructureDamage);
        }

        void Spit(GameSettings settings)
        {
            Vector2 mouth = boss.Position + (spitTarget - boss.Position).normalized * boss.Radius;
            game.PlayEffect(EffectKind.BossMegaSpit, mouth);
            BossGlob.Throw(game, mouth, spitTarget, landed: point =>
            {
                game.PlayEffect(EffectKind.BossMegaSpitHit, point);
                game.CameraRig.Shake(settings.bossShake * 0.5f, 0.35f);
                HurtAround(point, settings.bossSpitRadius, settings.bossSpitDamage * 0.5f, settings.bossSpitDamage);
            });
        }

        /// <summary>Hurts every ant on the surface and every wall or tower within <paramref name="radius"/>.</summary>
        void HurtAround(Vector2 center, float radius, float antDamage, float structureDamage)
        {
            var ants = new List<Ant>(game.Colony.Ants);
            foreach (Ant ant in ants)
            {
                if (ant == null || !ant.IsOnSurface) continue;
                if (Vector2.Distance(ant.Position, center) <= radius + ant.Radius) ant.Health.TakeDamage(antDamage);
            }

            var structures = new List<Structure>(game.Defenses.Structures);
            foreach (Structure structure in structures)
            {
                if (structure == null || !structure.IsBuilt || structure.Health == null) continue;
                if (Vector2.Distance(structure.Position, center) <= radius + structure.Radius) structure.Health.TakeDamage(structureDamage);
            }
        }

        bool AnythingNear(Vector2 center, float radius)
        {
            foreach (Ant ant in game.Colony.Ants)
            {
                if (ant != null && ant.IsOnSurface && Vector2.Distance(ant.Position, center) <= radius) return true;
            }
            foreach (Structure structure in game.Defenses.Structures)
            {
                if (structure != null && structure.IsBuilt && Vector2.Distance(structure.Position, center) <= radius) return true;
            }
            return false;
        }

        /// <summary>The nearest built tower or wall in range; otherwise the surface ant with the most other ants around it.</summary>
        bool TryPickSpitTarget(float range, out Vector2 target)
        {
            target = default;
            float best = range;
            bool found = false;
            foreach (Structure structure in game.Defenses.Structures)
            {
                if (structure == null || !structure.IsBuilt) continue;
                float distance = Vector2.Distance(structure.Position, boss.Position);
                // Towers first: they count as a bit closer.
                if (structure.Kind == StructureKind.Tower) distance -= 2f;
                if (distance < best)
                {
                    best = distance;
                    target = structure.Position;
                    found = true;
                }
            }
            if (found) return true;

            int mostNeighbours = -1;
            foreach (Ant ant in game.Colony.Ants)
            {
                if (ant == null || !ant.IsOnSurface || Vector2.Distance(ant.Position, boss.Position) > range) continue;
                int neighbours = 0;
                foreach (Ant other in game.Colony.Ants)
                {
                    if (other != null && other.IsOnSurface && Vector2.Distance(other.Position, ant.Position) < 1.8f) neighbours++;
                }
                if (neighbours > mostNeighbours)
                {
                    mostNeighbours = neighbours;
                    target = ant.Position;
                    found = true;
                }
            }
            return found;
        }
    }
}
