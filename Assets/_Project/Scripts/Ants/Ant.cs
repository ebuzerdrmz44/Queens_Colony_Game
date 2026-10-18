using System.Collections.Generic;
using UnityEngine;

namespace AntColony
{
    /// <summary>What an ant is up to, in one word (shown when the mouse is over it).</summary>
    public enum AntState
    {
        Idle,
        Working,
        Fighting,
        Guarding,
        Moving,
        Fleeing,
    }

    public enum AntRole
    {
        Worker,
        Soldier,
        Builder,
        /// <summary>A soldier that spits acid from a distance (added last, so old saves keep their numbers).</summary>
        Spitter,
    }

    /// <summary>
    /// Shared behaviour of every ant: body and health set-up, walking along routes through the nest and
    /// across the surface, and switching between side-view art (nest) and top-down art (surface) when it
    /// goes through the entrance. Subclasses decide where to go (see <see cref="WorkerAnt"/> and <see cref="SoldierAnt"/>).
    /// </summary>
    public abstract class Ant : MonoBehaviour
    {
        const float HealthBarGap = 0.2f;

        // Smooth steering on the surface (see SteersSmoothly).
        /// <summary>How fast the ant can turn, in radians per second (faster when close to the target).</summary>
        const float TurnRate = 4f;
        /// <summary>Largest sideways drift of the wandering, in degrees.</summary>
        const float WanderAngle = 35f;
        const float WanderSpeed = 0.6f;
        /// <summary>Within this distance the ant walks straight at its target, so it always arrives.</summary>
        const float StraightDistance = 0.4f;

        // Running from enemies (see FleeFromEnemies).
        /// <summary>An enemy this close (body edge to body edge) makes the ant run.</summary>
        const float FleeDistance = 2.5f;
        /// <summary>The ant keeps running until every enemy is at least this far away.</summary>
        const float SafeDistance = 4.5f;
        /// <summary>Speed boost while running away.</summary>
        const float FleeSpeedFactor = 1.3f;

        static readonly List<Waypoint> PathBuffer = new List<Waypoint>();

        [Header("Art (empty slots use the game's ant art)")]
        [Tooltip("Frames in the nest, seen from the side.")]
        [SerializeField] CreatureSprites sideView = new CreatureSprites();
        [Tooltip("Frames on the surface, seen from above, drawn facing right.")]
        [SerializeField] CreatureSprites topView = new CreatureSprites();
        [Tooltip("Tick this if the side-view frames above look to the left instead of to the right.")]
        [SerializeField] bool sideViewFacesLeft;

        readonly Queue<Waypoint> route = new Queue<Waypoint>();
        HealthBar healthBar;
        CreatureFrames sideFrames;
        CreatureFrames topFrames;
        Vector2 heading;
        float wanderSeed;
        /// <summary>Which way the ant is going around something in its way (see <see cref="Surface.SteerAround"/>).</summary>
        int avoidSide;
        bool isFleeing;
        Mission mission = Mission.Auto;
        /// <summary>Where the last <see cref="GoTo"/> was heading, so the ant can pick its trip up again after fleeing.</summary>
        Vector2? destination;

        public abstract AntRole Role { get; }
        public Health Health { get; private set; }
        /// <summary>The job the player gave this ant in the Missions panel. Changing it takes effect at once.</summary>
        public Mission Mission
        {
            get => mission;
            set
            {
                if (mission == value) return;
                mission = value;
                OnMissionChanged();
            }
        }

        /// <summary>True while the ant is doing a job (not resting or waiting for work).</summary>
        public virtual bool IsBusy => false;
        public bool IsFleeing => isFleeing;

        /// <summary>The ant's own name, picked when it hatches.</summary>
        public string Name { get; private set; } = "";
        /// <summary>What it's up to in one word (shown when the mouse is over it).</summary>
        public virtual AntState Status => isFleeing ? AntState.Fleeing : IsBusy ? AntState.Working : AntState.Idle;
        /// <summary>What it's doing right now, in a few words (e.g. "carrying food home"); empty if nothing.</summary>
        public virtual string Activity => isFleeing ? "running from an enemy" : "";
        /// <summary>The middle of the body in the world (the side-view body stands on <see cref="Position"/>).</summary>
        public Vector2 BodyCenter => IsOnSurface ? Position : Position + new Vector2(0f, BodyHeight * 0.5f);

        static readonly string[] Names =
        {
            "Pip", "Hazel", "Bramble", "Acorn", "Clover", "Nutmeg", "Juniper", "Maple", "Thistle", "Fern",
            "Rowan", "Sorrel", "Pebble", "Moss", "Birch", "Willow", "Tansy", "Burdock", "Chestnut", "Sage",
            "Cricket", "Twig", "Barley", "Quill", "Ember", "Rusty", "Hickory", "Poppy", "Basil", "Fennel",
            "Aspen", "Bean", "Cedar", "Dill", "Elm", "Flint", "Ginger", "Holly", "Ivy", "Kip",
        };
        public Vector2 Position => transform.position;
        /// <summary>Size of the body for crowding and reach, in world units.</summary>
        public float Radius => BodyWidth * 0.38f;
        public bool IsOnSurface => Game.Surface.IsOnSurface(transform.position);

        protected ColonyGame Game { get; private set; }
        protected SpriteRenderer Body { get; private set; }
        /// <summary>Height of the side-view body; the top-down body is about as long as it is wide.</summary>
        protected float BodyHeight { get; private set; }
        protected UnitAnimator BodyAnimator { get; private set; }
        protected bool HasRoute => route.Count > 0;

        protected abstract float Speed { get; }
        /// <summary>
        /// True for ants that walk in gentle, slightly wandering curves on the surface (workers, builders)
        /// instead of straight lines. Soldiers keep straight lines so they react to orders at once.
        /// </summary>
        protected virtual bool SteersSmoothly => false;
        protected abstract float MaxHealth { get; }
        /// <summary>Width of the body in world units, for reach and crowding (and the body's size when built in code).</summary>
        public abstract float BodyWidth { get; }
        /// <summary>Colour of the body when it is built in code; a prefab's "Body" keeps its own colour.</summary>
        public abstract Color Tint { get; }

        public void Init(ColonyGame game, Vector2 spawnPoint)
        {
            Game = game;
            transform.position = spawnPoint;
            if (string.IsNullOrEmpty(Name)) Name = Names[Random.Range(0, Names.Length)];
            wanderSeed = Random.value * 100f;

            bool customSide = sideView != null && sideView.walk != null && System.Array.Exists(sideView.walk, sprite => sprite != null);
            sideFrames = GameArt.Frames(sideView, game.Art.AntSide);
            topFrames = GameArt.Frames(topView, game.Art.AntTop);
            bool facesLeft = customSide ? sideViewFacesLeft : game.Art.AntSideFacesLeft;

            Body = UnitBody.FindOrCreate(game.Art, transform, sideFrames.Walk[0], BodyWidth, SortingOrder.Ants, Tint);
            Body.sprite = sideFrames.Walk[0];
            BodyHeight = GameArt.Height(Body);
            BodyAnimator = UnitAnimator.Attach(transform, Body, BodyView.Side, sideFrames, facesLeft);

            healthBar = HealthBar.Create(game.Art, transform, BodyHeight + HealthBarGap, 0.8f, new Color(0.5f, 0.85f, 0.3f));
            if (!TryGetComponent(out Health health)) health = gameObject.AddComponent<Health>();
            Health = health;
            Health.Init(MaxHealth, healthBar, Body);
            Health.Died += HandleDeath;

            OnSpawned();
            UpdateView();
            BodyAnimator.Face(Random.value < 0.5f ? Vector2.left : Vector2.right);
        }

        /// <summary>Called once in <see cref="Init"/>, before the first <see cref="OnViewChanged"/>.</summary>
        protected virtual void OnSpawned() { }

        /// <summary>Called when the player gives the ant a new mission, so it can drop work that no longer fits.</summary>
        protected virtual void OnMissionChanged() { }

        /// <summary>Called when the ant switches between the nest (side view) and the surface (top-down).</summary>
        protected virtual void OnViewChanged(BodyView view) { }

        /// <summary>Called just before the ant is removed, so it can let go of anything it was holding.</summary>
        protected virtual void OnDeath() { }

        /// <summary>
        /// Plans a route to <paramref name="destination"/> through the nest's tunnels and the entrance as needed.
        /// Returns false (and stays put) if the destination can't be reached.
        /// </summary>
        protected bool GoTo(Vector2 destination)
        {
            route.Clear();
            this.destination = destination;
            if (!Game.Nest.TryFindPath(transform.position, destination, PathBuffer))
            {
                // Tunnels filled around the ant may have shut it in a pocket: climb out to the nearest open tunnel and retry.
                if (IsOnSurface || !Game.Nest.TryFindWayOut(transform.position, out Vector2 freeSpot)) return false;
                Relocate(freeSpot);
                if (!Game.Nest.TryFindPath(transform.position, destination, PathBuffer)) return false;
            }

            foreach (Waypoint waypoint in PathBuffer) route.Enqueue(waypoint);
            return true;
        }

        protected void ClearRoute()
        {
            route.Clear();
            destination = null;
        }

        /// <summary>
        /// On the surface, runs from any enemy that comes close: straight away from it, bending toward the ant hill
        /// when the hill is not behind the enemy, until the enemy is well behind. Then the ant resumes its trip.
        /// Call it first in Update and skip the rest of the frame while it returns true.
        /// </summary>
        protected bool FleeFromEnemies()
        {
            if (!IsOnSurface || Game.Waves == null)
            {
                isFleeing = false;
                return false;
            }

            float lookDistance = (isFleeing ? SafeDistance : FleeDistance) + Radius;
            Enemy enemy = Game.Waves.FindNearestEnemy(Position, Position, lookDistance + 1.5f);
            if (enemy == null || Vector2.Distance(Position, enemy.Position) - enemy.Radius > lookDistance)
            {
                if (isFleeing)
                {
                    isFleeing = false;
                    // Pick the trip up again from here.
                    if (destination.HasValue) GoTo(destination.Value);
                }
                return false;
            }

            isFleeing = true;
            Vector2 away = Position - enemy.Position;
            away = away.sqrMagnitude > 0.0001f ? away.normalized : Random.insideUnitCircle.normalized;
            Vector2 toHill = Game.Surface.EntrancePoint - Position;
            if (toHill.sqrMagnitude > 1f && Vector2.Dot(toHill.normalized, away) > 0f)
                away = (away + toHill.normalized * 0.6f).normalized;

            Vector2 next = Game.Surface.PushOut(Position + away * Speed * FleeSpeedFactor * Time.deltaTime, Radius, includeStructures: true);
            BodyAnimator.Face(next - Position);
            heading = away;
            transform.position = next;
            return true;
        }

        /// <summary>Corners of a route count as reached this close.</summary>
        const float CornerReach = 0.3f;
        /// <summary>Barely moving for this long while following a route means stuck: plan a new route.</summary>
        const float StuckTime = 1.2f;
        const float StuckMove = 0.25f;
        Vector2 stuckCheckPoint;
        float stuckTimer;

        /// <summary>
        /// True (and a fresh route is planned from here) if the ant has barely moved for a while, e.g. pinned against
        /// a wall that was built after it planned its route, or shoved into a corner by the crowd.
        /// </summary>
        bool CheckStuck()
        {
            if (Vector2.Distance(Position, stuckCheckPoint) > StuckMove)
            {
                stuckCheckPoint = Position;
                stuckTimer = 0f;
                return false;
            }

            stuckTimer += Time.deltaTime;
            if (stuckTimer < StuckTime || !destination.HasValue) return false;

            stuckTimer = 0f;
            stuckCheckPoint = Position;
            Vector2 goal = destination.Value;
            transform.position = Game.Surface.PushOut(Position, Radius, includeStructures: true);
            GoTo(goal);
            return true;
        }

        /// <summary>Puts the ant somewhere else at once (e.g. out of a tunnel that was filled in) and forgets its route.</summary>
        public void Relocate(Vector2 position)
        {
            transform.position = position;
            route.Clear();
        }

        /// <summary>Walks along the current route. Returns true once the destination is reached.</summary>
        protected bool FollowRoute()
        {
            if (route.Count == 0) return true;
            if (IsOnSurface && CheckStuck()) return false;

            Waypoint next = route.Peek();
            // A corner on the way counts as reached when close (the crowd nudges ants, so they rarely land exactly on it).
            if (!next.ThroughEntrance && route.Count > 1 && Vector2.Distance(Position, next.Position) < CornerReach)
            {
                route.Dequeue();
                return false;
            }
            if (next.ThroughEntrance)
            {
                PassThroughEntrance(next.Position);
                route.Dequeue();
            }
            else if (StepTowards(next.Position))
            {
                route.Dequeue();
            }
            return route.Count == 0;
        }

        /// <summary>Moves one frame's worth of distance toward a point. Returns true once the point is reached.</summary>
        protected bool StepTowards(Vector2 target)
        {
            Vector2 position = transform.position;
            float distance = Vector2.Distance(position, target);
            bool onSurface = IsOnSurface;

            if (onSurface)
            {
                // A target inside a boulder or tower counts as reached at its edge.
                float arrival = Game.Surface.ArrivalDistance(target, Radius, includeStructures: true);
                if (arrival > 0f && distance <= arrival) return true;
                if (SteersSmoothly && distance > StraightDistance) return SteerTowards(position, target);
            }

            Vector2 next;
            if (onSurface)
            {
                Vector2 direction = Game.Surface.SteerAround(position, target, Radius, includeStructures: true, ref avoidSide);
                bool clear = distance < 0.0001f || Vector2.Dot(direction, (target - position) / distance) > 0.999f;
                next = clear ? Vector2.MoveTowards(position, target, Speed * Time.deltaTime)
                             : position + direction * Mathf.Min(Speed * Time.deltaTime, distance);
                next = Game.Surface.PushOut(next, Radius, includeStructures: true);
            }
            else
            {
                next = Vector2.MoveTowards(position, target, Speed * Time.deltaTime);
            }

            BodyAnimator.Face(next - position);
            transform.position = next;
            return next == target;
        }

        /// <summary>
        /// Turns gradually toward the target while drifting a little from side to side, like a real ant
        /// following a scent. The drift fades out near the target and the turning gets sharper, so it never circles.
        /// </summary>
        bool SteerTowards(Vector2 position, Vector2 target)
        {
            float distance = Vector2.Distance(position, target);
            Vector2 around = Game.Surface.SteerAround(position, target, Radius, includeStructures: true, ref avoidSide);

            float drift = (Mathf.PerlinNoise(wanderSeed, Time.time * WanderSpeed) - 0.5f) * 2f * WanderAngle;
            drift *= Mathf.Clamp01((distance - StraightDistance) / 3f);
            Vector2 desired = Quaternion.Euler(0f, 0f, drift) * around;

            if (heading.sqrMagnitude < 0.5f) heading = desired;
            float turn = TurnRate * Mathf.Max(1f, 2f / distance) * Time.deltaTime;
            heading = Vector3.RotateTowards(heading, desired, turn, 0f);

            Vector2 next = Game.Surface.PushOut(position + heading * Mathf.Min(Speed * Time.deltaTime, distance), Radius, includeStructures: true);
            BodyAnimator.Face(heading);
            transform.position = next;
            return false;
        }

        protected void FaceTowards(Vector2 point)
        {
            BodyAnimator.Face(point - Position);
        }

        /// <summary>Pops out at the other end of the nest entrance, moving into that area's scene.</summary>
        void PassThroughEntrance(Vector2 exit)
        {
            transform.position = exit;
            heading = Vector2.zero;
            transform.SetParent(Game.AreaRoot(exit), true);
            UpdateView();
        }

        void UpdateView()
        {
            bool onSurface = IsOnSurface;
            BodyView view = onSurface ? BodyView.TopDown : BodyView.Side;
            BodyAnimator.SetView(view, onSurface ? topFrames : sideFrames);

            // The side-view body stands on the ant's position; the top-down body is centred on it.
            float barHeight = onSurface ? BodyWidth * 0.5f + HealthBarGap : BodyHeight + HealthBarGap;
            healthBar.transform.localPosition = new Vector3(0f, barHeight, 0f);
            OnViewChanged(view);
        }

        void HandleDeath()
        {
            Game.PlayEffect(EffectKind.AntDied, Position);
            OnDeath();
            Game.Colony.RemoveAnt(this);
            Destroy(gameObject);
        }
    }
}
