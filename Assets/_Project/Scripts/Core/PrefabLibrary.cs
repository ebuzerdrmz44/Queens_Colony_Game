using System;
using UnityEngine;

namespace AntColony
{
    /// <summary>
    /// The prefabs the game spawns, shown on the "Colony Game" object in the Inspector.
    /// "Ant Colony > Generate Prefabs" creates them in Assets/_Project/Prefabs and fills these slots.
    /// Edit the prefabs freely (sprites, scale, colour, extra components); any slot left empty is built in code instead.
    /// Enemy prefabs are set per enemy type, under Settings > Enemy Types.
    /// </summary>
    [Serializable]
    public class PrefabLibrary
    {
        [Header("Ants")]
        public WorkerAnt worker;
        public SoldierAnt soldier;
        public BuilderAnt builder;
        [Tooltip("The acid-spitting soldier. Empty: built in code.")]
        public SpitterAnt spitter;
        [Tooltip("Needs a child called \"Body\" with a SpriteRenderer.")]
        public GameObject queen;

        [Header("Projectiles (flying shots; their \"right\" / X axis points the way they fly)")]
        [Tooltip("The blob a spitter spits. Put a sprite and any trail particles in it as children: they move and turn with it. " +
                 "When it lands, child particle systems are let go and fade out. Empty: a small green blob.")]
        public GameObject spitterShot;
        [Tooltip("The shot an acid tower fires. Same rules as the spitter's. Empty: a green blob.")]
        public GameObject towerShot;

        [Header("Defenses")]
        public Structure wall;
        public Structure tower;

        [Header("Food on the surface")]
        public FoodSource leaf;
        public FoodSource berries;
        public FoodSource acorn;

        [Header("Overworld scenery (one is picked at random for each spot)")]
        [Tooltip("Used for the trees around the edge and the groves inside. The canopy is the child called \"Visual\"; it fades while units walk under it.")]
        public GameObject[] trees;
        public GameObject[] bushes;
        public GameObject[] boulders;
        public GameObject[] stumps;
        public GameObject[] logs;
        [Tooltip("Each flower patch uses one of these, so make one prefab per colour.")]
        public GameObject[] flowers;

        public Ant AntFor(AntRole role)
        {
            switch (role)
            {
                case AntRole.Worker: return worker;
                case AntRole.Soldier: return soldier;
                case AntRole.Spitter: return spitter;
                default: return builder;
            }
        }

        public Structure StructureFor(StructureKind kind) => kind == StructureKind.Wall ? wall : tower;
    }
}
