using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AntColony
{
    /// <summary>
    /// Sends waves of enemies in from the edges of the surface. Each wave has more enemies,
    /// with more health, than the last. Surviving the final wave wins the game.
    /// </summary>
    public class WaveSpawner : MonoBehaviour
    {
        const float DelayBetweenSpawns = 1.5f;
        /// <summary>Seconds into the final wave before the boss walks in.</summary>
        const float BossDelay = 5f;

        readonly List<Enemy> enemies = new List<Enemy>();
        ColonyGame game;
        float timeUntilNextWave;
        int enemiesWaitingToSpawn;
        bool bossOnTheWay;

        public IReadOnlyList<Enemy> Enemies => enemies;
        public int CurrentWave { get; private set; }
        /// <summary>The final boss while it lives (null before it comes and once it's beaten).</summary>
        public Enemy Boss { get; private set; }
        public int TotalWaves => game.Settings.wavesToWin;
        public float TimeUntilNextWave => timeUntilNextWave;
        public bool HasMoreWaves => CurrentWave < TotalWaves;
        /// <summary>Enemies are on the field or still arriving.</summary>
        public bool IsWaveActive => enemies.Count > 0 || enemiesWaitingToSpawn > 0;

        void LateUpdate()
        {
            // An enemy destroyed without going through RemoveEnemy must not hold up the end of a wave.
            enemies.RemoveAll(enemy => enemy == null);
        }
        public bool AllWavesDefeated => !HasMoreWaves && enemiesWaitingToSpawn == 0 && enemies.Count == 0;

        public bool IsNestUnderAttack
        {
            get
            {
                foreach (Enemy enemy in enemies)
                {
                    if (enemy.IsAttackingNest) return true;
                }
                return false;
            }
        }

        public void Init(ColonyGame colonyGame)
        {
            game = colonyGame;
            timeUntilNextWave = game.Settings.firstWaveDelay;
        }

        void Update()
        {
            if (!HasMoreWaves) return;
            if (game.Tutorial != null && game.Tutorial.HoldsWaves) return; // the first wave waits for the tutorial

            timeUntilNextWave -= Time.deltaTime;
            if (timeUntilNextWave <= 0f) StartNextWave();
        }

        /// <summary>For tests: pretend <paramref name="wave"/> waves are done, with the next one due in <paramref name="seconds"/>.</summary>
        public void SkipTo(int wave, float seconds)
        {
            CurrentWave = Mathf.Clamp(wave, 0, TotalWaves - 1);
            timeUntilNextWave = seconds;
        }

        /// <summary>Starts the next wave right away (also used by the "Call next wave" button).</summary>
        public void StartNextWave()
        {
            if (!HasMoreWaves) return;

            CurrentWave++;
            timeUntilNextWave = game.Settings.timeBetweenWaves;

            int count = game.Settings.enemiesInFirstWave + (CurrentWave - 1) * game.Settings.extraEnemiesPerWave;
            StartCoroutine(SpawnWave(CurrentWave, Mathf.Max(1, count)));

            string finalNote = HasMoreWaves ? "" : " It's the final wave!";
            game.ShowMessage($"Wave {CurrentWave} is coming!{finalNote}");
            if (!HasMoreWaves) StartCoroutine(BossArrives());
        }

        public void RemoveEnemy(Enemy enemy)
        {
            enemies.Remove(enemy);
            if (enemy == Boss) Boss = null;
        }

        /// <summary>The final wave's boss walks in from the edge a few seconds after the wave starts.</summary>
        IEnumerator BossArrives()
        {
            enemiesWaitingToSpawn++; // the game isn't won while the boss is still on its way
            bossOnTheWay = true;
            yield return new WaitForSeconds(BossDelay);
            bossOnTheWay = false;
            enemiesWaitingToSpawn--;
            SpawnBoss(game.Surface.RandomEdgePoint(), 1f);
        }

        Enemy SpawnBoss(Vector2 point, float healthMultiplier)
        {
            EnemyStats stats = game.Settings.boss;
            Boss = SpawnEnemyAt(stats, healthMultiplier, point);
            game.PlayEffect(EffectKind.BossArrived, point);
            game.ShowMessage($"The {stats.name} is coming! Defend the queen!");
            return Boss;
        }

        /// <summary>Helpers the boss calls in: the first enemy type, around <paramref name="near"/>.</summary>
        public void SpawnHelpers(Vector2 near, int count)
        {
            List<EnemyStats> types = game.Settings.enemyTypes;
            if (types == null || types.Count == 0) return;
            float healthMultiplier = 1f + (CurrentWave - 1) * game.Settings.enemyHealthGrowthPerWave;
            for (int i = 0; i < count; i++)
                SpawnEnemyAt(types[0], healthMultiplier, game.Surface.Clamp(near + Random.insideUnitCircle.normalized * 1.8f));
        }

        /// <summary>The enemy closest to <paramref name="from"/> among those within <paramref name="radius"/> of <paramref name="center"/>.</summary>
        public Enemy FindNearestEnemy(Vector2 from, Vector2 center, float radius)
        {
            Enemy nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (Enemy enemy in enemies)
            {
                if (Vector2.Distance(enemy.Position, center) > radius) continue;

                float distance = Vector2.Distance(enemy.Position, from);
                if (distance < nearestDistance)
                {
                    nearest = enemy;
                    nearestDistance = distance;
                }
            }
            return nearest;
        }

        /// <summary>The enemy under a clicked point, or null.</summary>
        public Enemy FindEnemyAt(Vector2 point, float pickRadius)
        {
            return FindNearestEnemy(point, point, pickRadius);
        }

        public bool IsEnemyNear(Vector2 point, float distance)
        {
            foreach (Enemy enemy in enemies)
            {
                if (Vector2.Distance(enemy.Position, point) <= distance) return true;
            }
            return false;
        }

        IEnumerator SpawnWave(int wave, int count)
        {
            enemiesWaitingToSpawn += count;
            float healthMultiplier = 1f + (wave - 1) * game.Settings.enemyHealthGrowthPerWave;

            for (int i = 0; i < count; i++)
            {
                EnemyStats stats = PickEnemyType(wave);
                if (stats != null) SpawnEnemy(stats, healthMultiplier);
                enemiesWaitingToSpawn--;

                yield return new WaitForSeconds(DelayBetweenSpawns);
            }
        }

        /// <summary>A random enemy type that is allowed in this wave.</summary>
        EnemyStats PickEnemyType(int wave)
        {
            List<EnemyStats> allTypes = game.Settings.enemyTypes;
            if (allTypes == null || allTypes.Count == 0)
            {
                Debug.LogWarning("No enemy types are set up in the Colony Game settings.");
                return null;
            }

            var allowed = allTypes.FindAll(type => type.firstWave <= wave);
            if (allowed.Count == 0) allowed = allTypes;
            return allowed[Random.Range(0, allowed.Count)];
        }

        void SpawnEnemy(EnemyStats stats, float healthMultiplier)
        {
            SpawnEnemyAt(stats, healthMultiplier, game.Surface.RandomEdgePoint());
        }

        Enemy SpawnEnemyAt(EnemyStats stats, float healthMultiplier, Vector2 spawnPoint)
        {
            Enemy enemy;
            if (stats.prefab != null)
            {
                enemy = Instantiate(stats.prefab, transform, false);
                enemy.name = stats.name;
            }
            else
            {
                var enemyObject = new GameObject(stats.name);
                enemyObject.transform.SetParent(transform, false);
                enemy = enemyObject.AddComponent<Enemy>();
            }
            enemy.Init(game, stats, healthMultiplier, spawnPoint);
            enemies.Add(enemy);
            return enemy;
        }

        // ---------- Saving ----------

        public void Write(SaveData data)
        {
            data.wave = CurrentWave;
            data.timeUntilNextWave = timeUntilNextWave;
            data.enemiesWaitingToSpawn = enemiesWaitingToSpawn - (bossOnTheWay ? 1 : 0);
            data.bossOnTheWay = bossOnTheWay;
            List<EnemyStats> types = game.Settings.enemyTypes;
            foreach (Enemy enemy in enemies)
            {
                data.enemies.Add(new EnemySave
                {
                    type = enemy == Boss ? -1 : Mathf.Max(0, types.IndexOf(enemy.Stats)), // -1: the boss
                    position = enemy.Position,
                    health = enemy.Health.Current,
                    healthMultiplier = enemy.HealthMultiplier,
                });
            }
        }

        public void Read(SaveData data)
        {
            CurrentWave = data.wave;
            timeUntilNextWave = data.timeUntilNextWave;
            List<EnemyStats> types = game.Settings.enemyTypes;
            if (types == null || types.Count == 0) return;

            foreach (EnemySave saved in data.enemies)
            {
                if (saved.type < 0)
                {
                    SpawnBoss(saved.position, saved.healthMultiplier).Health.SetCurrent(saved.health);
                    continue;
                }
                EnemyStats stats = types[Mathf.Clamp(saved.type, 0, types.Count - 1)];
                Enemy enemy = SpawnEnemyAt(stats, saved.healthMultiplier, saved.position);
                enemy.Health.SetCurrent(saved.health);
            }
            // Enemies of the current wave that hadn't arrived yet still come.
            if (data.enemiesWaitingToSpawn > 0) StartCoroutine(SpawnWave(Mathf.Max(1, CurrentWave), data.enemiesWaitingToSpawn));
            if (data.bossOnTheWay) StartCoroutine(BossArrives());
        }
    }
}
