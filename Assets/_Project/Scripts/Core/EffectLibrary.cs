using System;
using UnityEngine;

namespace AntColony
{
    /// <summary>Moments in the game that can play a visual effect (see <see cref="EffectLibrary"/>).</summary>
    public enum EffectKind
    {
        SoldierBite,
        EnemyBite,
        QueenHit,
        AcidHit,
        AntDied,
        EnemyDied,
        FoodPickedUp,
        FoodStored,
        LarvaFed,
        EggLaid,
        AntHatched,
        GardenMadeFood,
        TileDug,
        DigHit,
        StoneFound,
        TreasureFound,
        RoomBuilt,
        StructureBuilt,
        StructureDestroyed,
        SpitterSpit,
        SpitterAcidHit,
        BossArrived,
        BossSummon,
        BossDied,
        BossQuake,
        BossMegaSpit,
        BossMegaSpitHit,
        SpitterShotTrail,
        TowerShotTrail,
        BossGlobTrail,
    }

    /// <summary>
    /// Effect prefabs (particle systems, sprite animations, sounds...) shown on the "Colony Game" object in the Inspector.
    /// Each one is spawned where its moment happens and removed once its particle systems finish
    /// (or after <see cref="FallbackLifetime"/> seconds if it has none). Empty slots play nothing.
    /// </summary>
    [Serializable]
    public class EffectLibrary
    {
        /// <summary>Seconds an effect without particle systems (or with a looping one) stays before it is removed.</summary>
        public const float FallbackLifetime = 2f;

        [Header("Fighting")]
        [Tooltip("A soldier bites an enemy. Spawned at the enemy.")]
        public GameObject soldierBite;
        [Tooltip("An enemy bites an ant, wall or tower. Spawned at the victim.")]
        public GameObject enemyBite;
        [Tooltip("An enemy at the ant hill hurts the queen. Spawned at the ant hill.")]
        public GameObject queenHit;
        [Tooltip("A tower's acid shot hits an enemy.")]
        public GameObject acidHit;
        public GameObject antDied;
        public GameObject enemyDied;

        [Header("Colony")]
        [Tooltip("A worker takes a load from a food pile.")]
        public GameObject foodPickedUp;
        [Tooltip("A worker brings a load home to storage.")]
        public GameObject foodStored;
        [Tooltip("A worker feeds a larva. Spawned at the cradle.")]
        public GameObject larvaFed;
        [Tooltip("The queen lays an egg into a cradle.")]
        public GameObject eggLaid;
        [Tooltip("A larva grows into an adult ant.")]
        public GameObject antHatched;
        [Tooltip("A fungus garden turns a leaf into food.")]
        public GameObject gardenMadeFood;

        [Header("Building")]
        [Tooltip("A builder finishes digging a tile.")]
        public GameObject tileDug;
        [Tooltip("Every strike of a builder on the soil it is digging. Empty: small built-in dirt chips.")]
        public GameObject digHit;
        [Tooltip("Digging gives stone.")]
        public GameObject stoneFound;
        [Tooltip("Digging uncovers a seed cache or a grub.")]
        public GameObject treasureFound;
        public GameObject roomBuilt;
        [Tooltip("A wall or tower is finished.")]
        public GameObject structureBuilt;
        [Tooltip("A wall or tower is destroyed.")]
        public GameObject structureDestroyed;

        [Header("Spitters")]
        [Tooltip("A spitter spits a blob of acid. Spawned at its mouth.")]
        public GameObject spitterSpit;
        [Tooltip("A spitter's acid hits an enemy.")]
        public GameObject spitterAcidHit;
        [Tooltip("Rides along on each blob a spitter spits (a child of it, so it moves and turns with it), and fades out " +
                 "after it lands. Simulation Space World leaves a trail behind; Local keeps the particles on the blob.")]
        public GameObject spitterShotTrail;

        [Header("Towers")]
        [Tooltip("Rides along on each acid shot a tower fires.")]
        public GameObject towerShotTrail;

        [Header("Final boss")]
        [Tooltip("The boss walks onto the meadow.")]
        public GameObject bossArrived;
        [Tooltip("The boss calls in helpers. Spawned at the boss.")]
        public GameObject bossSummon;
        public GameObject bossDied;
        [Tooltip("The boss slams the ground (earthquake). Spawned at the boss.")]
        public GameObject bossQuake;
        [Tooltip("The boss spits its mega glob. Spawned at its mouth.")]
        public GameObject bossMegaSpit;
        [Tooltip("The mega glob lands (splash). Spawned where it lands.")]
        public GameObject bossMegaSpitHit;
        [Tooltip("Rides along on the boss's mega glob.")]
        public GameObject bossGlobTrail;

        public GameObject PrefabFor(EffectKind kind)
        {
            switch (kind)
            {
                case EffectKind.SoldierBite: return soldierBite;
                case EffectKind.EnemyBite: return enemyBite;
                case EffectKind.QueenHit: return queenHit;
                case EffectKind.AcidHit: return acidHit;
                case EffectKind.AntDied: return antDied;
                case EffectKind.EnemyDied: return enemyDied;
                case EffectKind.FoodPickedUp: return foodPickedUp;
                case EffectKind.FoodStored: return foodStored;
                case EffectKind.LarvaFed: return larvaFed;
                case EffectKind.EggLaid: return eggLaid;
                case EffectKind.AntHatched: return antHatched;
                case EffectKind.GardenMadeFood: return gardenMadeFood;
                case EffectKind.TileDug: return tileDug;
                case EffectKind.DigHit: return digHit;
                case EffectKind.StoneFound: return stoneFound;
                case EffectKind.TreasureFound: return treasureFound;
                case EffectKind.RoomBuilt: return roomBuilt;
                case EffectKind.StructureBuilt: return structureBuilt;
                case EffectKind.StructureDestroyed: return structureDestroyed;
                case EffectKind.SpitterSpit: return spitterSpit;
                case EffectKind.SpitterAcidHit: return spitterAcidHit;
                case EffectKind.BossArrived: return bossArrived;
                case EffectKind.BossSummon: return bossSummon;
                case EffectKind.BossDied: return bossDied;
                case EffectKind.BossQuake: return bossQuake;
                case EffectKind.BossMegaSpit: return bossMegaSpit;
                case EffectKind.BossMegaSpitHit: return bossMegaSpitHit;
                case EffectKind.SpitterShotTrail: return spitterShotTrail;
                case EffectKind.TowerShotTrail: return towerShotTrail;
                case EffectKind.BossGlobTrail: return bossGlobTrail;
                default: return null;
            }
        }

        /// <summary>How long an effect needs to finish: its longest non-looping particle system, plus that system's particle lifetime.</summary>
        public static float LifetimeOf(GameObject effect)
        {
            float longest = 0f;
            foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>())
            {
                ParticleSystem.MainModule main = particles.main;
                if (main.loop) return FallbackLifetime;
                longest = Mathf.Max(longest, main.startDelay.constantMax + main.duration + main.startLifetime.constantMax);
            }
            return longest > 0f ? longest : FallbackLifetime;
        }
    }
}
