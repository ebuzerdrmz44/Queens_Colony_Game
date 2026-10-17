using System;

namespace AntColony
{
    public enum EnemyKind
    {
        Beetle,
        Spider,
        /// <summary>The final wave's boss (looks like a giant red ant until it has its own art).</summary>
        Boss,
    }

    /// <summary>Numbers for one kind of enemy. Edited in the Inspector under Settings > Enemy Types.</summary>
    [Serializable]
    public class EnemyStats
    {
        public string name = "Enemy";
        [UnityEngine.Tooltip("Prefab to spawn for this enemy (made by \"Ant Colony > Generate Prefabs\"). Empty = built in code.")]
        public Enemy prefab;
        public EnemyKind kind = EnemyKind.Beetle;
        [UnityEngine.Tooltip("The first wave this enemy can appear in.")]
        public int firstWave = 1;
        public float speed = 1f;
        public float health = 50f;
        public float damage = 5f;
        [UnityEngine.Tooltip("Gap between the enemy's body and its victim's body at which it can bite.")]
        public float attackRange = 0.4f;
        public float attackCooldown = 1f;
        public float bodyWidth = 1f;
        [UnityEngine.Tooltip("Food units left behind when this enemy is defeated.")]
        public int foodWhenKilled = 3;
    }
}
