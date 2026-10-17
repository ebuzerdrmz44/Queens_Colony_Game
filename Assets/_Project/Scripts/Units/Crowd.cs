using System.Collections.Generic;
using UnityEngine;

namespace AntColony
{
    /// <summary>
    /// Keeps fighters on the surface (soldiers and enemies) from standing inside each other. After everyone has
    /// moved, overlapping bodies are pushed apart and slightly sideways, so two creatures meeting head-on slide
    /// past each other instead of locking up, and a group attacking one enemy spreads into a ring around it.
    /// Workers and builders are left out: they all walk the same roads and would jam them, so they pass over
    /// everything like real ant traffic. Finished walls and towers push enemies out too.
    /// Runs before <see cref="UnitAnimator"/>, so a small push doesn't look like a step.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class Crowd : MonoBehaviour
    {
        /// <summary>Fraction of an overlap fixed per frame; less than 1 keeps the push soft.</summary>
        const float Softness = 0.5f;
        /// <summary>Ants this close to the ant hill don't push, so they can all get in and out of the entrance.</summary>
        const float EntranceFreeZone = 1.4f;
        /// <summary>How much of each push goes sideways, so blocked creatures walk around each other.</summary>
        const float Sidestep = 0.6f;

        struct Body
        {
            public Transform Transform;
            public Vector2 Position;
            public float Radius;
            public bool IsEnemy;
        }

        readonly List<Body> bodies = new List<Body>();
        ColonyGame game;

        public void Init(ColonyGame colonyGame)
        {
            game = colonyGame;
        }

        void LateUpdate()
        {
            if (game.Colony == null || Time.deltaTime <= 0f) return;

            Gather();
            SeparateBodies();
            PushEnemiesOutOfStructures();

            // Nobody ends up inside a boulder; soldiers are kept out of towers too (enemies chew those).
            foreach (Body body in bodies)
                body.Transform.position = game.Surface.PushOut(body.Position, body.Radius, includeStructures: !body.IsEnemy);
        }

        void Gather()
        {
            bodies.Clear();
            Vector2 entrance = game.Surface.EntrancePoint;

            foreach (Ant ant in game.Colony.Ants)
            {
                if (!(ant is SoldierAnt)) continue; // soldiers and spitters
                if (!ant.IsOnSurface || Vector2.Distance(ant.Position, entrance) < EntranceFreeZone) continue;
                bodies.Add(new Body { Transform = ant.transform, Position = ant.Position, Radius = ant.Radius });
            }

            foreach (Enemy enemy in game.Waves.Enemies)
                bodies.Add(new Body { Transform = enemy.transform, Position = enemy.Position, Radius = enemy.Radius, IsEnemy = true });
        }

        void SeparateBodies()
        {
            for (int i = 0; i < bodies.Count; i++)
            {
                for (int j = i + 1; j < bodies.Count; j++)
                {
                    Body a = bodies[i];
                    Body b = bodies[j];

                    Vector2 offset = b.Position - a.Position;
                    float minDistance = a.Radius + b.Radius;
                    float distanceSquared = offset.sqrMagnitude;
                    if (distanceSquared >= minDistance * minDistance) continue;

                    float distance = Mathf.Sqrt(distanceSquared);
                    // Exactly on top of each other: pick a direction that is stable for this pair.
                    Vector2 direction = distance > 0.0001f ? offset / distance : PairDirection(i, j);
                    float amount = (minDistance - distance) * 0.5f * Softness;
                    // Each side also steps to its own right, so the pair turns around each other.
                    Vector2 sideways = new Vector2(-direction.y, direction.x) * (amount * Sidestep);
                    Vector2 push = direction * amount;

                    a.Position -= push + sideways;
                    b.Position += push + sideways;
                    bodies[i] = a;
                    bodies[j] = b;
                }
            }
        }

        void PushEnemiesOutOfStructures()
        {
            IReadOnlyList<Structure> structures = game.Defenses.Structures;
            for (int i = 0; i < bodies.Count; i++)
            {
                Body body = bodies[i];
                if (!body.IsEnemy) continue;

                foreach (Structure structure in structures)
                {
                    if (!structure.IsBuilt) continue;

                    Vector2 offset = body.Position - structure.Position;
                    float minDistance = body.Radius + structure.Radius;
                    float distance = offset.magnitude;
                    if (distance >= minDistance) continue;

                    Vector2 direction = distance > 0.0001f ? offset / distance : Vector2.up;
                    body.Position = structure.Position + direction * minDistance; // structures don't budge
                }
                bodies[i] = body;
            }
        }

        static Vector2 PairDirection(int i, int j)
        {
            float angle = (i * 7 + j * 13) * 0.7f;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
    }
}
