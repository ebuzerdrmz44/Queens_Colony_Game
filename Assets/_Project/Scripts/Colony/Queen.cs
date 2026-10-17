using System.Collections.Generic;
using UnityEngine;

namespace AntColony
{
    /// <summary>
    /// The queen's errands. She rests in her chamber; when the colony raises an ant she walks through the tunnels to a
    /// free cradle, lays the egg there (a stepped pixel-art birthing pose: settle, strain, push, pop) and walks home.
    /// The egg only appears in the cradle when it pops out. If the cradle is removed on the way, the egg goes back
    /// into the raise queue and its food is refunded.
    /// </summary>
    public class Queen : MonoBehaviour
    {
        enum State { Home, ToCradle, Birthing, ToHome }

        [Tooltip("Walking speed through the tunnels, in world units per second.")]
        [Min(0.1f)] public float walkSpeed = 0.9f;
        [Tooltip("Frames per second of the birthing pose.")]
        [Range(1f, 12f)] public float birthFps = 5f;
        [Tooltip("Where she stands to lay: her middle this far from the egg, as a fraction of her body width, so her abdomen is over it.")]
        [Range(0f, 0.6f)] public float rearOverEgg = 0.35f;

        /// <summary>
        /// The birthing pose, one entry per frame: pixels taller, pixels wider, pixels sideways, and degrees of tilt
        /// (head up, abdomen down). She arrives, turns her back to the cradle, squats with her abdomen down over it,
        /// strains with a shake, pushes, and stretches as the egg pops out; then she stands back up.
        /// </summary>
        static readonly (int taller, int wider, int shift, float tilt)[] BirthFrames =
        {
            (0, 0, 0, 0f),    // arrive
            (0, 0, 0, 0f),    // turn around (see TurnFrame)
            (-1, 1, 0, 6f),   // squat down
            (-2, 1, 0, 12f),
            (-2, 2, 0, 12f),
            (-2, 2, 1, 12f),  // strain
            (-2, 2, -1, 12f),
            (-2, 2, 1, 12f),
            (-2, 2, -1, 12f),
            (-3, 3, 0, 16f),  // push
            (1, -1, 0, 8f),   // pop: the egg drops out
            (0, 0, 0, 4f),    // stand up
            (0, 0, 0, 0f),
        };
        const int TurnFrame = 1;
        const int PopFrame = 10;

        ColonyGame game;
        Colony colony;
        UnitAnimator animator;
        readonly List<Waypoint> path = new List<Waypoint>();
        readonly Queue<Waypoint> route = new Queue<Waypoint>();
        Vector2 home;
        State state = State.Home;
        Room cradle;
        AntRole caste;
        float birthClock;
        int birthFrame = -1;

        /// <summary>Out on an errand: no new egg until she's back home.</summary>
        public bool IsBusy => state != State.Home;
        /// <summary>She has an egg that isn't in a cradle yet (saving puts it back in the raise queue).</summary>
        public bool IsCarryingEgg => state == State.ToCradle || (state == State.Birthing && birthFrame < PopFrame);
        public AntRole CarriedCaste => caste;

        public void Init(ColonyGame colonyGame, Colony owner, UnitAnimator body)
        {
            game = colonyGame;
            colony = owner;
            animator = body;
            home = transform.position;
        }

        /// <summary>Sends her to lay an egg of <paramref name="role"/> in <paramref name="room"/> (already paid for).</summary>
        public void LayEggIn(Room room, AntRole role)
        {
            cradle = room;
            caste = role;
            room.AwaitingEgg = true;

            // Stand beside the cradle on the side she comes from.
            Vector2 spot = room.StandPoint;
            float side = transform.position.x < spot.x ? -1f : 1f;
            float offset = animator.BodyWidth * rearOverEgg;
            if (!PlanRoute(spot + new Vector2(side * offset, 0f)) && !PlanRoute(spot))
            {
                // No way there (shouldn't happen): lay it at once, as before.
                room.AwaitingEgg = false;
                colony.PlaceEgg(room, role);
                cradle = null;
                return;
            }
            state = State.ToCradle;
        }

        void Update()
        {
            if (game == null) return;

            switch (state)
            {
                case State.ToCradle:
                    if (!CradleUsable()) GiveUp();
                    else if (Walk()) StartBirth();
                    break;

                case State.Birthing:
                    UpdateBirth();
                    break;

                case State.ToHome:
                    if (Walk())
                    {
                        state = State.Home;
                        animator.Face(Random.value < 0.5f ? Vector2.left : Vector2.right);
                    }
                    break;
            }
        }

        bool CradleUsable()
        {
            if (cradle == null || !cradle.IsBuilt || cradle.Brood != null) return false;
            foreach (Room room in game.Nest.Rooms)
            {
                if (room == cradle) return true; // still there (not removed)
            }
            return false;
        }

        void StartBirth()
        {
            state = State.Birthing;
            birthClock = 0f;
            birthFrame = -1;
            // Level out facing the cradle (the way she walked in); she turns her back to it on TurnFrame.
            float side = cradle.StandPoint.x - transform.position.x;
            if (Mathf.Abs(side) < 0.01f) side = animator.Facing.x;
            animator.Face(new Vector2(side < 0f ? -1f : 1f, 0f));
            animator.BeginPose();
        }

        void UpdateBirth()
        {
            if (birthFrame < PopFrame && !CradleUsable())
            {
                animator.EndPose();
                GiveUp();
                return;
            }

            birthClock += Time.deltaTime;
            int frame = Mathf.FloorToInt(birthClock * birthFps);
            if (frame == birthFrame) return;
            birthFrame = frame;

            if (frame >= BirthFrames.Length)
            {
                animator.EndPose();
                GoHome();
                return;
            }

            if (frame == TurnFrame)
            {
                // Back to the cradle, so the egg comes out of her abdomen over it.
                float away = transform.position.x - cradle.StandPoint.x;
                if (Mathf.Abs(away) < 0.01f) away = -animator.Facing.x;
                animator.Face(new Vector2(away < 0f ? -1f : 1f, 0f));
            }

            var pose = BirthFrames[frame];
            animator.Pose(pose.taller, pose.wider, pose.shift, pose.tilt);
            if (frame == PopFrame)
            {
                cradle.AwaitingEgg = false;
                colony.PlaceEgg(cradle, caste);
            }
        }

        /// <summary>The cradle is gone or taken: the egg goes back into the queue, refunded, and she heads home.</summary>
        void GiveUp()
        {
            if (cradle != null) cradle.AwaitingEgg = false;
            colony.ReturnEgg(caste);
            cradle = null;
            GoHome();
        }

        void GoHome()
        {
            cradle = null;
            state = PlanRoute(home) ? State.ToHome : State.Home;
            if (state == State.Home) transform.position = home;
        }

        bool PlanRoute(Vector2 destination)
        {
            route.Clear();
            if (!game.Nest.TryFindPath(transform.position, destination, path)) return false;
            foreach (Waypoint waypoint in path)
            {
                if (!waypoint.ThroughEntrance) route.Enqueue(waypoint);
            }
            return true;
        }

        /// <summary>Walks along the route. True once she has arrived.</summary>
        bool Walk()
        {
            if (route.Count == 0) return true;

            Vector2 position = transform.position;
            Vector2 target = route.Peek().Position;
            Vector2 next = Vector2.MoveTowards(position, target, walkSpeed * Time.deltaTime);
            animator.Face(target - position);
            transform.position = next;
            if (Vector2.Distance(next, target) < 0.01f) route.Dequeue();
            return route.Count == 0;
        }
    }
}
