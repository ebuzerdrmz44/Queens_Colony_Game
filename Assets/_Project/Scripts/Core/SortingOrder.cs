namespace AntColony
{
    /// <summary>Draw order of every layer in the game, from back to front.</summary>
    public static class SortingOrder
    {
        // Surface (top-down)
        public const int Meadow = -100;
        public const int PlayAreaEdge = -99;
        public const int Decorations = -15;
        public const int Shadows = -14;
        /// <summary>Bushes, boulders, stumps and logs: on the ground, under every unit.</summary>
        public const int Scenery = -13;
        public const int AntHill = -10;
        public const int Food = 0;
        public const int RallyFlag = 2;
        public const int OrderMarkers = 3;
        public const int Structures = 4;
        public const int SelectionRings = 8;

        // Nest (side view)
        public const int Underground = -50;
        public const int NestTiles = -45;
        public const int Rooms = -40;
        public const int DigMarkers = -38;
        public const int Brood = -36;
        public const int Queen = 5;

        // Both
        public const int Ants = 10;
        public const int Enemies = 12;
        public const int CarriedFood = 15;
        public const int Projectiles = 16;
        public const int BuildPreview = 17;
        public const int TreeCanopies = 18;
        public const int FallingLeaves = 20;
        /// <summary>Effect prefabs drawn lower than this are raised to it, so they show over the units they play on.</summary>
        public const int Effects = 25;
        public const int HealthBars = 30;
    }
}
