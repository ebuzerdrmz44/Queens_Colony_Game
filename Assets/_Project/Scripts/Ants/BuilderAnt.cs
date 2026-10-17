using UnityEngine;

namespace AntColony
{
    /// <summary>
    /// Builders do the colony's construction. A free builder looks for work where it is first (the nest or the
    /// surface), then in the other area:
    /// - in the nest: dig out tiles the player marked, then finish room blueprints,
    /// - on the surface: finish wall and tower blueprints, then repair damaged ones.
    /// With nothing to do it waits in the queen's chamber.
    /// </summary>
    public class BuilderAnt : Ant
    {
        enum JobKind
        {
            None,
            Dig,
            Room,
            Structure,
        }

        /// <summary>How often the builder "hammers" (plays its attack animation) while working.</summary>
        const float HammerInterval = 0.5f;
        /// <summary>Gap between the builder and a surface structure while working on it.</summary>
        const float WorkGap = 0.15f;
        const float JobCheckInterval = 0.5f;

        JobKind job;
        Vector2Int digTile;
        Room room;
        Structure structure;
        float hammerTimeLeft;
        float jobCheckTimeLeft;
        bool resting;

        public override AntRole Role => AntRole.Builder;
        public override bool IsBusy => job != JobKind.None;
        protected override float Speed => Game.Settings.builderSpeed;
        protected override float MaxHealth => Game.Settings.builderHealth;
        protected override bool SteersSmoothly => true;
        public override float BodyWidth => 0.9f;
        public override Color Tint => new Color(0.8f, 0.95f, 0.65f);

        void Update()
        {
            if (FleeFromEnemies()) return;

            if (!JobStillValid())
            {
                EndJob();
                jobCheckTimeLeft -= Time.deltaTime;
                if (jobCheckTimeLeft <= 0f)
                {
                    jobCheckTimeLeft = JobCheckInterval;
                    FindJob();
                }

                if (job == JobKind.None)
                {
                    RestInQueenRoom();
                    return;
                }
            }

            switch (job)
            {
                case JobKind.Dig:
                    if (!FollowRoute()) return;
                    if (Hammer(Game.Nest.TileCenter(digTile))) ChipSoil();
                    if (Game.Nest.AddDigWork(digTile, Time.deltaTime)) EndJob();
                    break;

                case JobKind.Room:
                    if (!FollowRoute()) return;
                    Hammer(room.StandPoint + Vector2.up * 0.5f);
                    room.AddWork(Time.deltaTime);
                    break;

                case JobKind.Structure:
                    UpdateStructureJob();
                    break;
            }
        }

        void FindJob()
        {
            bool found;
            if (Mission == Mission.NestBuilder) found = TryNestJob();
            else if (Mission == Mission.SurfaceBuilder) found = TrySurfaceJob();
            else found = IsOnSurface ? TrySurfaceJob() || TryNestJob() : TryNestJob() || TrySurfaceJob();
            if (found) resting = false;
        }

        bool TryNestJob()
        {
            Nest nest = Game.Nest;
            if (nest.TryFindDigJob(Position, out Vector2Int tile, out Vector2Int standTile))
            {
                if (GoTo(nest.StandPoint(standTile)))
                {
                    job = JobKind.Dig;
                    digTile = tile;
                    return true;
                }
                nest.ReleaseDig(tile);
            }

            Room blueprint = nest.FindRoomJob(Position);
            if (blueprint != null && GoTo(blueprint.StandPoint))
            {
                job = JobKind.Room;
                room = blueprint;
                return true;
            }
            return false;
        }

        bool TrySurfaceJob()
        {
            Structure found = Game.Defenses.FindJob(Position);
            if (found == null) return false;

            job = JobKind.Structure;
            structure = found;
            if (!IsOnSurface) GoTo(WorkSpot(found));
            return true;
        }

        void UpdateStructureJob()
        {
            if (!IsOnSurface)
            {
                // Still in the nest: climb out toward the job.
                if (!HasRoute) GoTo(WorkSpot(structure));
                FollowRoute();
                return;
            }

            ClearRoute();
            float gap = Vector2.Distance(Position, structure.Position) - Radius - structure.Radius;
            if (gap > WorkGap * 2f)
            {
                StepTowards(WorkSpot(structure));
                return;
            }

            Hammer(structure.Position);
            structure.AddWork(Time.deltaTime);
        }

        protected override void OnMissionChanged()
        {
            jobCheckTimeLeft = 0f; // look for fitting work this frame
        }

        bool JobStillValid()
        {
            // A new mission from the player ends work that no longer fits it.
            if (Mission == Mission.NestBuilder && job == JobKind.Structure) return false;
            if (Mission == Mission.SurfaceBuilder && (job == JobKind.Dig || job == JobKind.Room)) return false;

            switch (job)
            {
                case JobKind.Dig: return Game.Nest.HasDigOrder(digTile);
                case JobKind.Room: return room != null && !room.IsBuilt && !room.IsRemoved;
                case JobKind.Structure: return structure != null && structure.NeedsWork;
                default: return false;
            }
        }

        void EndJob()
        {
            if (job == JobKind.Dig) Game.Nest.ReleaseDig(digTile);
            job = JobKind.None;
            room = null;
            structure = null;
        }

        /// <summary>Faces the work and strikes it every <see cref="HammerInterval"/>. True on the frame of a strike.</summary>
        bool Hammer(Vector2 target)
        {
            FaceTowards(target);
            hammerTimeLeft -= Time.deltaTime;
            if (hammerTimeLeft > 0f) return false;

            hammerTimeLeft = HammerInterval;
            BodyAnimator.PlayAttack();
            return true;
        }

        static readonly Color[] SoilChips = { AutumnPalette.Dirt, AutumnPalette.DirtLight, AutumnPalette.DirtDark };
        static readonly Color[] StoneChips = { AutumnPalette.Stone, AutumnPalette.StoneDark, AutumnPalette.Dirt };

        /// <summary>Bits of soil fly off the face of the tile being dug, back toward the builder.</summary>
        void ChipSoil()
        {
            Vector2 tileCenter = Game.Nest.TileCenter(digTile);
            Vector2 towardBuilder = Position + Vector2.up * 0.25f - tileCenter;
            Vector2 face = tileCenter + Vector2.ClampMagnitude(towardBuilder, Nest.TileSize * 0.45f);
            if (Game.PlayEffect(EffectKind.DigHit, face)) return;

            bool stony = Game.Nest.TileAt(digTile) == NestTile.StoneVein;
            DirtChips.Burst(Game, face, towardBuilder, stony ? StoneChips : SoilChips);
        }

        /// <summary>The point on a surface structure's edge nearest to the builder.</summary>
        Vector2 WorkSpot(Structure target)
        {
            Vector2 fromStructure = IsOnSurface ? Position - target.Position : Game.Surface.EntrancePoint - target.Position;
            if (fromStructure.sqrMagnitude < 0.0001f) fromStructure = Vector2.down;
            return target.Position + fromStructure.normalized * (target.Radius + Radius + WorkGap);
        }

        void RestInQueenRoom()
        {
            if (!resting)
            {
                resting = true;
                GoTo(Game.Colony.RandomQueenRoomPoint());
            }
            FollowRoute();
        }

        protected override void OnDeath()
        {
            EndJob();
        }
    }
}
