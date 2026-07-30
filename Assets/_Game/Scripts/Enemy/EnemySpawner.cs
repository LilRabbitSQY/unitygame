using System.Collections;
using UnityEngine;
using FinalDefense.Core;
using FinalDefense.Data;
using FinalDefense.Path;

namespace FinalDefense.Enemy
{
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private WaveData waveData;
        [SerializeField] private GameObject defaultEnemyPrefab;

        private int currentWaveIndex;
        private int enemiesAlive;
        private int totalEnemyCount;
        private bool spawning;

        public int CurrentWave => currentWaveIndex + 1;
        public int TotalWaves => waveData != null ? waveData.waves.Length : 0;
        public int TotalEnemyCount => totalEnemyCount;

        private void Start()
        {
            EventBus.OnEnemyKilled += OnEnemyDied;
            EventBus.OnEnemyReachedEnd += OnEnemyDied;
            CalculateTotalEnemies();

            var battleMgr = Object.FindFirstObjectByType<Battle.BattleManager>();
            if (battleMgr != null)
                battleMgr.SetTotalEnemies(totalEnemyCount);

            StartCoroutine(SpawnAllWaves());
        }

        private void OnDestroy()
        {
            EventBus.OnEnemyKilled -= OnEnemyDied;
            EventBus.OnEnemyReachedEnd -= OnEnemyDied;
        }

        private void CalculateTotalEnemies()
        {
            totalEnemyCount = 0;
            if (waveData == null) return;
            foreach (var wave in waveData.waves)
                totalEnemyCount += wave.count;
        }

        private void OnEnemyDied(int _)
        {
            enemiesAlive--;
        }

        private IEnumerator SpawnAllWaves()
        {
            if (waveData == null || waveData.waves.Length == 0) yield break;

            float hpMultiplier = 1f;
            if (GameManager.Instance != null)
                hpMultiplier = 1f + GameManager.Instance.GetDeterminationPenalty();

            for (currentWaveIndex = 0; currentWaveIndex < waveData.waves.Length; currentWaveIndex++)
            {
                var wave = waveData.waves[currentWaveIndex];
                yield return new WaitForSeconds(wave.delayBeforeWave);

                spawning = true;
                for (int i = 0; i < wave.count; i++)
                {
                    SpawnEnemy(wave.enemyType, hpMultiplier);
                    yield return new WaitForSeconds(wave.spawnInterval);
                }
                spawning = false;

                while (enemiesAlive > 0)
                {
                    yield return null;
                }

                EventBus.WaveCompleted();
            }

            EventBus.AllWavesCompleted();
        }

        private void SpawnEnemy(EnemyData data, float hpMultiplier)
        {
            if (PathManager.Instance == null || PathManager.Instance.WaypointCount == 0) return;

            Vector3 spawnPos = PathManager.Instance.GetWaypointPosition(0);
            if (data.moveType == MoveType.Air)
                spawnPos.y += 0.5f;

            GameObject prefab = data.prefab != null ? data.prefab : defaultEnemyPrefab;

            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, spawnPos, Quaternion.identity);
            }
            else
            {
                go = CreateFallbackEnemy(spawnPos, data);
            }

            go.layer = LayerMask.NameToLayer("Enemy");

            var enemy = go.GetComponent<Enemy>();
            if (enemy == null) enemy = go.AddComponent<Enemy>();

            var attack = go.GetComponent<EnemyAttack>();
            if (attack == null) attack = go.AddComponent<EnemyAttack>();

            var healthComp = go.GetComponent<EnemyHealth>();
            if (healthComp == null) healthComp = go.AddComponent<EnemyHealth>();

            var setup = go.GetComponent<EnemySetup>();
            if (setup == null) setup = go.AddComponent<EnemySetup>();
            setup.ApplyVisuals(data);

            enemy.Initialize(data, hpMultiplier);
            enemiesAlive++;
        }

        private GameObject CreateFallbackEnemy(Vector3 position, EnemyData data)
        {
            var go = new GameObject("Enemy_" + data.enemyName);
            go.transform.position = position;
            var sr = go.AddComponent<SpriteRenderer>();
            Texture2D tex = new Texture2D(32, 32);
            Color[] colors = new Color[32 * 32];
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
            tex.SetPixels(colors);
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), Vector2.one * 0.5f, 32);
            sr.color = data.enemyColor;
            sr.sortingOrder = 10;

            if (data.moveType == MoveType.Air)
                go.transform.localScale = Vector3.one * 0.6f;

            go.AddComponent<CircleCollider2D>().radius = 0.4f;
            go.AddComponent<EnemyHealth>();
            go.AddComponent<EnemyAttack>();
            return go;
        }
    }
}
