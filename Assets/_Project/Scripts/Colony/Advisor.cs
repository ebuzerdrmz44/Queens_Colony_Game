using System.Collections.Generic;
using UnityEngine;

namespace AntColony
{
    /// <summary>
    /// Looks at the colony's numbers once a second and writes short tips about what is holding production
    /// back, most urgent first. Shown in the HUD so the player learns how to optimise the nest.
    /// </summary>
    public class Advisor
    {
        public enum Urgency
        {
            Info,
            Warning,
            Danger,
        }

        public readonly struct Tip
        {
            public readonly string Text;
            public readonly Urgency Urgency;

            public Tip(string text, Urgency urgency)
            {
                Text = text;
                Urgency = urgency;
            }
        }

        const float RefreshInterval = 1f;
        const float SlowTripSeconds = 30f;
        const int LeafPileWarning = 12;

        readonly ColonyGame game;
        readonly List<Tip> tips = new List<Tip>();
        float refreshTimeLeft;

        public IReadOnlyList<Tip> Tips => tips;

        public Advisor(ColonyGame game)
        {
            this.game = game;
        }

        public void Update(float deltaTime)
        {
            refreshTimeLeft -= deltaTime;
            if (refreshTimeLeft > 0f) return;
            refreshTimeLeft = RefreshInterval;
            Refresh();
        }

        void Refresh()
        {
            tips.Clear();
            Colony colony = game.Colony;
            Nest nest = game.Nest;
            GameSettings settings = game.Settings;
            ColonyStats stats = colony.Stats;

            if (colony.IsStarving)
                Add(Urgency.Danger, "Starving! Ants are losing health. Bring food home.");

            float net = stats.NetPerMinute;
            if (net < 0f && Time.timeSinceLevelLoad > 20f)
                Add(Urgency.Warning, Loc.F("Eating more than you gather ({0:+0;-0}/min). Raise workers or build gardens.", net));

            if (colony.FreeSpace < colony.StorageCapacity * 0.1f)
                Add(Urgency.Warning, "Storage full: new food is wasted. Build a Storage room.");

            int gardens = nest.CountBuilt(RoomType.Garden);
            if (colony.Leaves >= LeafPileWarning)
            {
                string advice = gardens == 0
                    ? "No garden: leaves can't become food. Build a Fungus Garden."
                    : Loc.F("Leaves piling up ({0}). Build another Fungus Garden.", colony.Leaves);
                Add(Urgency.Warning, advice);
            }
            else if (gardens > 0 && colony.Leaves == 0 && stats.ForagedLeaves.PerMinute < 1f && Time.timeSinceLevelLoad > 40f)
            {
                Add(Urgency.Info, "Gardens idle: no leaves coming in.");
            }

            if (colony.RaiseQueue.Count > 0)
            {
                if (colony.FindFreeCradle() == null)
                    Add(Urgency.Warning, "All cradles taken: the queen can't lay eggs. Build a Cradle.");
                else if (colony.Food < settings.eggCost)
                    Add(Urgency.Warning, Loc.F("The queen needs {0} food to lay an egg.", settings.eggCost));
            }

            int hungryLarvae = 0;
            foreach (Brood brood in colony.Brood)
            {
                if (brood.IsWaitingForFood) hungryLarvae++;
            }
            if (hungryLarvae > 0)
            {
                string reason = colony.Food < settings.foodPerFeeding ? "no food in storage" : "all workers busy";
                Add(Urgency.Warning, Loc.F(hungryLarvae == 1 ? "{0} hungry larva: {1}." : "{0} hungry larvae: {1}.", hungryLarvae, Loc.T(reason)));
            }

            float trip = stats.AverageTripTime;
            if (trip > SlowTripSeconds)
                Add(Urgency.Info, Loc.F("Slow trips ({0:0}s). Build Storage near the entrance.", trip));

            int builders = colony.CountAnts(AntRole.Builder) + colony.CountQueued(AntRole.Builder);
            bool buildWork = nest.DigOrderCount > 0 || HasUnbuiltRoom() || HasUnbuiltDefense();
            if (buildWork && builders == 0)
                Add(Urgency.Warning, "No builder: raise one (3) to dig and build.");

            if (nest.UnreachableDigOrders > 0)
                Add(Urgency.Info, "Some dig marks don't touch a tunnel yet.");

            int gatherers = colony.CountOnMission(Mission.Forage, AntRole.Worker) + colony.CountOnMission(Mission.Leaves, AntRole.Worker);
            if (gatherers == 0 && colony.CountAnts(AntRole.Worker) > 0)
                Add(Urgency.Warning, "Nobody gathers food: put workers on Food gatherers or Leaf cutters (M).");
            else if (gatherers > 0 && game.Gather.MarkedCount == 0)
                Add(Urgency.Warning, "Gatherers are waiting: mark food piles with Gather (6).");

            int idle = colony.CountWorkers(WorkerJob.Idle);
            if (idle > 0 && colony.IsStorageFull)
                Add(Urgency.Info, Loc.F(idle == 1 ? "{0} idle worker: storage full." : "{0} idle workers: storage full.", idle));
            else if (idle > 1 && game.Food.Sources.Count == 0)
                Add(Urgency.Info, Loc.F("{0} idle workers: no food outside right now.", idle));

            if (tips.Count == 0)
                Add(Urgency.Info, "All good! Grow: more workers, gardens, storage.");

            tips.Sort((a, b) => b.Urgency.CompareTo(a.Urgency));
        }

        bool HasUnbuiltRoom()
        {
            foreach (Room room in game.Nest.Rooms)
            {
                if (!room.IsBuilt) return true;
            }
            return false;
        }

        bool HasUnbuiltDefense()
        {
            foreach (Structure structure in game.Defenses.Structures)
            {
                if (!structure.IsBuilt) return true;
            }
            return false;
        }

        void Add(Urgency urgency, string text)
        {
            tips.Add(new Tip(text, urgency));
        }
    }
}
