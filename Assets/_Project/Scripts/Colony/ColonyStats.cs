using System.Collections.Generic;
using UnityEngine;

namespace AntColony
{
    /// <summary>Adds up amounts over the last minute, so the HUD can show rates like "food per minute".</summary>
    public class RollingRate
    {
        const float Window = 60f;

        readonly Queue<(float time, float amount)> events = new Queue<(float, float)>();
        float total;
        float startTime = -1f; // set on first use

        public void Add(float amount)
        {
            StartClock();
            if (amount == 0f) return;
            events.Enqueue((Time.time, amount));
            total += amount;
        }

        /// <summary>Amount per minute over the last minute (or since the start, early in the game).</summary>
        public float PerMinute
        {
            get
            {
                StartClock();
                Trim();
                float span = Mathf.Clamp(Time.time - startTime, 10f, Window);
                return total * 60f / span;
            }
        }

        void StartClock()
        {
            if (startTime < 0f) startTime = Time.time;
        }

        void Trim()
        {
            while (events.Count > 0 && events.Peek().time < Time.time - Window)
                total -= events.Dequeue().amount;
        }
    }

    /// <summary>The colony's numbers that help the player see what to improve. Fed by ants, gardens and upkeep.</summary>
    public class ColonyStats
    {
        const int TripsRemembered = 12;

        readonly Queue<float> tripTimes = new Queue<float>();

        /// <summary>Food brought home from the surface.</summary>
        public RollingRate ForagedFood { get; } = new RollingRate();
        /// <summary>Leaves brought home from the surface.</summary>
        public RollingRate ForagedLeaves { get; } = new RollingRate();
        /// <summary>Food made by fungus gardens.</summary>
        public RollingRate GardenFood { get; } = new RollingRate();
        /// <summary>Food eaten by adult ants and the queen.</summary>
        public RollingRate Upkeep { get; } = new RollingRate();
        /// <summary>Food spent on eggs, larvae, rooms and defenses.</summary>
        public RollingRate Spent { get; } = new RollingRate();
        /// <summary>Food or leaves that didn't fit in storage and were dropped.</summary>
        public RollingRate Wasted { get; } = new RollingRate();

        public float IncomePerMinute => ForagedFood.PerMinute + GardenFood.PerMinute;
        /// <summary>Income minus upkeep: what's left over for growing the colony.</summary>
        public float NetPerMinute => IncomePerMinute - Upkeep.PerMinute;

        /// <summary>Average seconds of a foraging trip (nest to food and back), or 0 before the first trip.</summary>
        public float AverageTripTime
        {
            get
            {
                if (tripTimes.Count == 0) return 0f;
                float sum = 0f;
                foreach (float time in tripTimes) sum += time;
                return sum / tripTimes.Count;
            }
        }

        public void AddTrip(float seconds)
        {
            tripTimes.Enqueue(seconds);
            while (tripTimes.Count > TripsRemembered) tripTimes.Dequeue();
        }
    }
}
