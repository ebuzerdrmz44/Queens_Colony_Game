using System;
using System.Collections.Generic;
using UnityEngine;

namespace AntColony
{
    /// <summary>
    /// Every gameplay number lives here, so the game can be balanced from the Inspector
    /// (select the "Colony Game" object in the scene) without touching any code.
    ///
    /// Balance in short: a foraging trip takes about 20 s and brings home ~2.5 food (or 3 leaves, which a
    /// fungus garden turns into 6 food), so one worker earns about 7 food a minute while every ant eats
    /// 0.9 a minute. A worker pays for itself in about a minute; soldiers cost upkeep without earning,
    /// so the army has to be paid for by the workers and gardens. The first wave comes after ~5 minutes,
    /// which leaves time to learn the nest before having to fight.
    /// </summary>
    [Serializable]
    public class GameSettings
    {
        [Header("Surface (top-down)")]
        [Tooltip("The surface field goes from -worldHalfWidth to +worldHalfWidth. The nest entrance is in the middle.")]
        public float worldHalfWidth = 30f;
        public float worldHalfHeight = 20f;
        [Tooltip("Change this to get a different layout of trees, decorations and underground rock.")]
        public int worldSeed = 2024;

        [Header("Nest (side view, a grid of tiles)")]
        [Tooltip("The nest is its own scene. This is only where it sits in world space: far below the surface, " +
                 "so the two never show up in the same view.")]
        public float nestTopY = -150f;
        [Tooltip("Width of the nest in tiles. Odd numbers keep the entrance in the middle.")]
        public int nestWidth = 33;
        [Tooltip("Depth of the nest in tiles.")]
        public int nestDepth = 36;
        [Tooltip("Share of the underground that is hard rock (can't be dug).")]
        [Range(0f, 0.4f)] public float rockAmount = 0.12f;
        [Tooltip("Seconds for one builder to dig out one tile.")]
        public float digTime = 1.2f;

        [Header("Colony start")]
        public int startingFood = 40;
        public int startingWorkers = 4;
        public int startingSoldiers = 1;
        public int startingBuilders = 1;

        [Header("Queen")]
        public float queenMaxHealth = 100f;
        [Tooltip("Health the queen recovers per second while no enemy is at the entrance.")]
        public float queenRegenPerSecond = 0.5f;
        [Tooltip("Seconds between two eggs, while there are ants waiting to be raised.")]
        public float eggLayInterval = 3f;

        [Header("Upkeep")]
        [Tooltip("Food every adult ant eats per minute.")]
        public float antFoodPerMinute = 0.9f;
        public float queenFoodPerMinute = 1.5f;
        [Tooltip("Health lost per second by every ant while the colony has no food.")]
        public float starvationDamagePerSecond = 1f;

        [Header("Raising ants (egg -> larva -> adult)")]
        [Tooltip("Food the queen uses to lay one egg.")]
        public int eggCost = 3;
        [Tooltip("Seconds before an egg hatches into a larva.")]
        public float eggTime = 8f;
        [Tooltip("Food workers carry to a larva per feeding trip.")]
        public int foodPerFeeding = 2;
        public CasteCosts worker = new CasteCosts { larvaFood = 5, larvaTime = 15f };
        public CasteCosts soldier = new CasteCosts { larvaFood = 12, larvaTime = 25f };
        public CasteCosts builder = new CasteCosts { larvaFood = 7, larvaTime = 18f };
        public CasteCosts spitter = new CasteCosts { larvaFood = 14, larvaTime = 28f };

        [Header("Rooms in the nest")]
        [Tooltip("Food and leaves the queen's chamber holds on its own.")]
        public int baseStorage = 60;
        [Tooltip("Extra space for every Storage room.")]
        public int storageRoomCapacity = 40;
        public int storageRoomCost = 12;
        public int cradleCost = 6;
        public int gardenCost = 18;
        [Tooltip("Seconds for one builder to build a room.")]
        public float roomBuildTime = 4f;
        [Tooltip("Share of a room's cost given back when it is removed.")]
        [Range(0f, 1f)] public float roomRefund = 0.5f;

        [Header("Fungus gardens")]
        [Tooltip("Seconds a garden takes to turn one leaf into food.")]
        public float gardenCycleTime = 5f;
        public int gardenFoodPerLeaf = 2;

        [Header("Workers")]
        public float workerSpeed = 2.2f;
        public float workerHealth = 20f;

        [Header("Soldiers")]
        public float soldierSpeed = 2.6f;
        public float soldierHealth = 40f;
        public float soldierDamage = 7f;
        [Tooltip("Gap between the soldier's body and the enemy's body at which it can bite.")]
        public float soldierAttackRange = 0.5f;
        public float soldierAttackCooldown = 0.8f;
        [Tooltip("A soldier standing guard attacks enemies that come this close to its post.")]
        public float soldierGuardRadius = 6f;

        [Header("Spitters (acid soldiers)")]
        public float spitterSpeed = 2.3f;
        public float spitterHealth = 24f;
        [Tooltip("Damage of one acid blob.")]
        public float spitterDamage = 6f;
        [Tooltip("Distance from which a spitter spits; it backs off from enemies closer than half of it.")]
        public float spitterRange = 4.5f;
        public float spitterCooldown = 1.4f;

        [Header("Final boss")]
        [Tooltip("Comes with the last wave. Give it a prefab when its art is ready.")]
        public EnemyStats boss = new EnemyStats
        {
            name = "Red Ant Warlord", kind = EnemyKind.Boss, firstWave = 999,
            speed = 0.55f, health = 900f, damage = 18f, attackRange = 0.6f, attackCooldown = 1.6f,
            bodyWidth = 3.2f, foodWhenKilled = 40,
        };
        [Tooltip("Seconds between the boss calling in helpers (0 = never).")]
        public float bossSummonEvery = 12f;
        [Tooltip("Helpers per call (the first enemy type in the list).")]
        public int bossSummonCount = 2;
        [Tooltip("Seconds the boss stands still, winding up, before an earthquake or mega spit (a red circle shows where it hits).")]
        public float bossWindUp = 1.2f;
        [Tooltip("Seconds between earthquakes (0 = never). It only slams when ants or defences are in reach.")]
        public float bossQuakeEvery = 11f;
        public float bossQuakeRadius = 4.5f;
        [Tooltip("Damage to each ant in the quake.")]
        public float bossQuakeDamage = 14f;
        [Tooltip("Damage to each wall and tower in the quake.")]
        public float bossQuakeStructureDamage = 35f;
        [Tooltip("Seconds between mega spits (0 = never). It aims at the nearest tower or wall, else at a group of ants.")]
        public float bossSpitEvery = 7f;
        public float bossSpitRange = 9f;
        [Tooltip("Splash radius where the glob lands.")]
        public float bossSpitRadius = 1.8f;
        public float bossSpitDamage = 30f;
        [Tooltip("How hard the screen shakes, in world units (earthquake; the spit's landing shakes half as much).")]
        public float bossShake = 0.35f;

        [Header("Builders")]
        public float builderSpeed = 2.0f;
        public float builderHealth = 18f;
        [Tooltip("Health a builder restores per second when repairing.")]
        public float repairPerSecond = 10f;

        [Header("Stone and digging")]
        public int startingStone = 6;
        [Tooltip("Share of the underground that is stone veins: dug like soil but twice as slow, and each gives stonePerVeinTile.")]
        [Range(0f, 0.3f)] public float stoneVeinAmount = 0.08f;
        public int stonePerVeinTile = 3;
        [Tooltip("Chance that an ordinary soil tile gives 1 stone when dug.")]
        [Range(0f, 1f)] public float stoneChancePerSoil = 0.3f;
        [Tooltip("Chance that a soil tile hides a seed cache (food). Doubles toward the bottom of the nest.")]
        [Range(0f, 0.3f)] public float seedCacheChance = 0.04f;
        public int seedCacheFoodMin = 5;
        public int seedCacheFoodMax = 12;
        [Tooltip("Chance that a soil tile hides a fat grub (food). Doubles toward the bottom of the nest.")]
        [Range(0f, 0.3f)] public float grubChance = 0.03f;
        public int grubFood = 4;

        [Header("Walls")]
        [Tooltip("Stone for one wall block (walls are placed as a line of blocks).")]
        public int wallCost = 2;
        [Tooltip("Seconds for one builder to finish a wall block (more builders are faster).")]
        public float wallBuildTime = 3f;
        public float wallHealth = 100f;

        [Header("Towers")]
        [Tooltip("Food for one tower.")]
        public int towerCost = 12;
        [Tooltip("Stone for one tower.")]
        public int towerStoneCost = 10;
        public float towerBuildTime = 10f;
        public float towerHealth = 150f;
        public float towerRange = 5.5f;
        public float towerDamage = 6f;
        public float towerCooldown = 1f;

        [Header("Food on the surface")]
        public int startingFoodSources = 10;
        public int maxFoodSources = 18;
        public float foodSpawnInterval = 4f;
        public float foodMinDistanceFromNest = 5f;
        [Tooltip("Chance that a new pile is a leaf (leaves need a fungus garden) rather than a berry or acorn.")]
        [Range(0f, 1f)] public float leafChance = 0.55f;
        [Tooltip("Chance that a new pile grows next to an existing one (making food patches) instead of at a random spot.")]
        [Range(0f, 1f)] public float foodClusterChance = 0.75f;
        [Tooltip("How far from its neighbour a pile in a patch appears.")]
        public float foodClusterSpread = 2.6f;
        [Tooltip("Chance that a pile in a patch is the same kind as its neighbour (a leaf patch, a berry patch...).")]
        [Range(0f, 1f)] public float foodClusterSameKind = 0.7f;
        [Tooltip("\"Near the nest\" means within this distance of the ant hill.")]
        public float foodNearRadius = 14f;
        [Tooltip("Share of new piles that appear near the nest (the rest anywhere on the meadow).")]
        [Range(0f, 1f)] public float foodNearShare = 0.75f;
        [Tooltip("At least this many piles are kept near the nest: if there are fewer, the next one appears there.")]
        public int foodMinNearPiles = 4;
        [Tooltip("Chance that a new pile is big (twice the food, drawn bigger).")]
        [Range(0f, 1f)] public float foodBigPileChance = 0.3f;
        [Tooltip("Chance that a new pile is huge (three times the food).")]
        [Range(0f, 1f)] public float foodHugePileChance = 0.1f;

        [Header("Enemy waves")]
        public float firstWaveDelay = 310f;
        public float timeBetweenWaves = 75f;
        public int wavesToWin = 10;
        public int enemiesInFirstWave = 2;
        public int extraEnemiesPerWave = 1;
        [Tooltip("0.08 means enemies get 8% more health every wave.")]
        public float enemyHealthGrowthPerWave = 0.08f;

        public List<EnemyStats> enemyTypes = new List<EnemyStats>
        {
            new EnemyStats
            {
                name = "Beetle", kind = EnemyKind.Beetle, firstWave = 1,
                speed = 0.9f, health = 40f, damage = 3f, attackCooldown = 1.2f,
                bodyWidth = 1.1f, foodWhenKilled = 3,
            },
            new EnemyStats
            {
                name = "Spider", kind = EnemyKind.Spider, firstWave = 4,
                speed = 1.7f, health = 28f, damage = 6f, attackCooldown = 0.9f,
                bodyWidth = 1.25f, foodWhenKilled = 2,
            },
        };

        public CasteCosts CostsFor(AntRole role)
        {
            switch (role)
            {
                case AntRole.Worker: return worker;
                case AntRole.Soldier: return soldier;
                case AntRole.Spitter: return spitter;
                default: return builder;
            }
        }

        /// <summary>Total food to raise one ant of this caste: the egg plus everything its larva eats.</summary>
        public int RaiseCost(AntRole role) => eggCost + CostsFor(role).larvaFood;
    }

    /// <summary>What a larva of one caste needs before it becomes an adult.</summary>
    [Serializable]
    public class CasteCosts
    {
        [Tooltip("Food workers must bring the larva.")]
        public int larvaFood = 5;
        [Tooltip("Seconds the larva needs to grow (it only grows while it has been fed enough).")]
        public float larvaTime = 15f;
    }
}
