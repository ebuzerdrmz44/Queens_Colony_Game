namespace AntColony
{
    /// <summary>
    /// A job the player gives an ant from the Missions panel (M). New ants start on <see cref="Auto"/>,
    /// where they choose for themselves as before; any other mission makes them do only that.
    /// </summary>
    public enum Mission
    {
        /// <summary>The ant picks its own work (workers: feed larvae, otherwise stay in the nest; builders: any building).</summary>
        Auto,
        /// <summary>Worker: only feeds larvae.</summary>
        Nurse,
        /// <summary>Worker: gathers marked food that is eaten as it is (berries, acorns, defeated enemies), else other marked piles.</summary>
        Forage,
        /// <summary>Worker: gathers marked leaves for the fungus gardens, else other marked piles.</summary>
        Leaves,
        /// <summary>Builder: only digs tunnels and builds rooms in the nest.</summary>
        NestBuilder,
        /// <summary>Builder: only builds and repairs walls and towers on the surface.</summary>
        SurfaceBuilder,
    }

    /// <summary>Names, descriptions and which caste each <see cref="Mission"/> is for.</summary>
    public static class Missions
    {
        public static readonly Mission[] WorkerMissions = { Mission.Nurse, Mission.Forage, Mission.Leaves };
        public static readonly Mission[] BuilderMissions = { Mission.NestBuilder, Mission.SurfaceBuilder };

        public static AntRole RoleOf(Mission mission)
        {
            return mission == Mission.NestBuilder || mission == Mission.SurfaceBuilder ? AntRole.Builder : AntRole.Worker;
        }

        public static string NameOf(Mission mission)
        {
            switch (mission)
            {
                case Mission.Nurse: return "Larva feeders";
                case Mission.Forage: return "Food gatherers";
                case Mission.Leaves: return "Leaf cutters";
                case Mission.NestBuilder: return "Diggers";
                case Mission.SurfaceBuilder: return "Wall builders";
                default: return "Free";
            }
        }

        public static string DescriptionOf(Mission mission)
        {
            switch (mission)
            {
                case Mission.Nurse: return "Only carry food from storage to hungry larvae.";
                case Mission.Forage: return "Bring home berries, acorns and dead enemies from piles marked with Gather (6); marked leaves when no food is marked.";
                case Mission.Leaves: return "Bring home leaves from piles marked with Gather (6), for the gardens; marked food when no leaves are marked.";
                case Mission.NestBuilder: return "Only dig tunnels and build rooms in the nest.";
                case Mission.SurfaceBuilder: return "Only build and repair walls and towers outside.";
                default: return "Choose their own work. Free workers feed larvae and stay in the nest.";
            }
        }
    }
}
