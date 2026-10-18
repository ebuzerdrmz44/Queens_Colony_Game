using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AntColony
{
    /// <summary>
    /// Everything a saved game needs, written to one JSON file. The surface scenery and the nest's rock are rebuilt
    /// from the world seed, so only what the player changed is stored: the nest tiles, rooms and brood, the ants,
    /// resources, walls and towers, food piles and the waves. Each part of the game writes and reads its own section
    /// (see the Write/Read methods on <see cref="Nest"/>, <see cref="Colony"/>, <see cref="Defenses"/>,
    /// <see cref="FoodSpawner"/> and <see cref="WaveSpawner"/>).
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;
        const string FileName = "colony-save.json";

        public int version = CurrentVersion;
        public int worldSeed;
        public string savedAt;
        public bool onSurface;
        public bool tutorialActive;
        public int tutorialStep = -1;
        public bool tutorialGoalStarted;
        public bool tutorialGoalCompleted;
        public int tutorialSignals;

        // Colony
        public float food;
        public int leaves;
        public int stone;
        public float queenHealth;
        public float eggTimer;
        public Vector2 rallyPoint;
        public List<int> raiseQueue = new List<int>();
        public List<AntSave> ants = new List<AntSave>();

        // Nest
        public int nestWidth;
        public int nestHeight;
        public int[] tiles;
        public int[] buried;
        public bool[] digOrders;
        public List<RoomSave> rooms = new List<RoomSave>();

        // Surface
        public List<StructureSave> structures = new List<StructureSave>();
        public List<FoodSave> foodPiles = new List<FoodSave>();

        // Waves
        public bool hasWaveReports;
        public bool partialWaveHistory;
        public int waveHistoryUnknownThrough;
        public List<WaveReportRecord> waveReports = new List<WaveReportRecord>();
        public List<PendingWaveSave> pendingWaveSpawns = new List<PendingWaveSave>();
        public int wave;
        public float timeUntilNextWave;
        public int enemiesWaitingToSpawn;
        /// <summary>The final boss was about to walk in.</summary>
        public bool bossOnTheWay;
        public List<EnemySave> enemies = new List<EnemySave>();

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);
        public static bool Exists => File.Exists(FilePath);

        public void WriteToDisk()
        {
            savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            Directory.CreateDirectory(Application.persistentDataPath);
            string temporary = FilePath + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonUtility.ToJson(this));
                // Keep the previous complete save if writing or replacing fails.
                if (File.Exists(FilePath)) File.Replace(temporary, FilePath, FilePath + ".bak");
                else File.Move(temporary, FilePath);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        /// <summary>The saved game, or null (with the reason) if there is none or it can't be read.</summary>
        public static SaveData ReadFromDisk(out string problem)
        {
            problem = null;
            if (!Exists)
            {
                problem = "No saved game yet.";
                return null;
            }

            try
            {
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
                if (data == null || data.version != CurrentVersion)
                {
                    problem = "The saved game is from another version of the game.";
                    return null;
                }
                return data;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not read {FilePath}: {exception.Message}");
                problem = "The saved game could not be read.";
                return null;
            }
        }
    }

    [Serializable]
    public class AntSave
    {
        public int role;
        public int mission;
        public Vector2 position;
        public float health;
    }

    [Serializable]
    public class RoomSave
    {
        public int type;
        public int originX;
        public int originY;
        public bool built;
        public float buildProgress;
        public int paidCost;
        public float gardenTimer;
        public bool hasBrood;
        public int broodCaste;
        public int broodStage;
        public float broodTime;
        public int broodFed;
    }

    [Serializable]
    public class StructureSave
    {
        public int kind;
        public Vector2 position;
        public bool built;
        public float buildProgress;
        public float health;
    }

    [Serializable]
    public class FoodSave
    {
        public string name;
        public int kind;
        public Vector2 position;
        public int unitsLeft;
        public int startingUnits;
        public int load;
        public bool marked;
    }

    [Serializable]
    public class EnemySave
    {
        public int wave;
        public int type;
        public Vector2 position;
        public float health;
        public float healthMultiplier;
    }
    [Serializable]
    public class PendingWaveSave
    {
        public int wave;
        public int count;
    }

    [Serializable]
    public class WaveReportRecord
    {
        public int wave;
        public bool started;
        public bool completed;
        public bool partial;
        public int spawned;
        public int kills;
        public int workersLost;
        public int soldiersLost;
        public int buildersLost;
        public int spittersLost;
        public int starved;
        public int TotalLosses => workersLost + soldiersLost + buildersLost + spittersLost;

        // Role ordinals are the same stable values used by AntSave (worker, soldier, builder, spitter).
        public void RecordLoss(int role, bool starvation)
        {
            switch (role)
            {
                case 0: workersLost++; break;
                case 1: soldiersLost++; break;
                case 2: buildersLost++; break;
                case 3: spittersLost++; break;
                default: return;
            }
            if (starvation) starved++;
        }
    }

}
