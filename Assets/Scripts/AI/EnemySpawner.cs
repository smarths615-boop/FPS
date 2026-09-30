using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using FPS.Combat;
using FPS.Core;

namespace FPS.AI
{
    /// <summary>
    /// PVE wave director. Spawns enemies at designated points on the NavMesh, escalates
    /// difficulty per wave, and reports remaining hostiles to the HUD.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Setup")]
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private List<Transform> spawnPoints = new List<Transform>();
        [SerializeField] private int maxAlive = 6;

        [Header("Waves")]
        [SerializeField] private int enemiesPerWave = 3;
        [SerializeField] private int waveGrowth = 1;
        [SerializeField] private float interWaveDelay = 6f;
        [SerializeField] private float baseDamage = 11f;
        [SerializeField] private float damageGrowth = 1.6f;

        private readonly List<EnemyAI> _alive = new List<EnemyAI>();
        private int _wave = 0;
        private float _nextWaveTime;
        private bool _running;

        public int Wave => _wave;
        public int AliveCount => _alive.Count;
        public int RemainingThisWave { get; private set; }

        public event System.Action<int, int> WaveStarted;      // wave, count
        public event System.Action<int> WaveCleared;
        public event System.Action<EnemyAI> EnemyKilled;

        public void Begin()
        {
            _running = true;
            _wave = 0;
            _nextWaveTime = Time.time + 2f;
        }

        public void Stop() => _running = false;

        private void Update()
        {
            if (!_running) return;

            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                if (_alive[i] == null || _alive[i].HealthRef == null || _alive[i].HealthRef.IsDead)
                {
                    if (_alive[i] != null) EnemyKilled?.Invoke(_alive[i]);
                    _alive.RemoveAt(i);
                }
            }

            if (_alive.Count == 0)
            {
                if (RemainingThisWave <= 0 && _nextWaveTime <= 0f)
                {
                    WaveCleared?.Invoke(_wave);
                }
                if (Time.time >= _nextWaveTime) SpawnWave();
            }
        }

        private void SpawnWave()
        {
            _wave++;
            RemainingThisWave = Mathf.Min(enemiesPerWave + (_wave - 1) * waveGrowth, 24);
            float dmg = baseDamage + (_wave - 1) * damageGrowth;
            _nextWaveTime = 0f;

            for (int i = 0; i < RemainingThisWave; i++)
            {
                SpawnOne(dmg);
                if (_alive.Count >= maxAlive) break;
            }

            RemainingThisWave = _alive.Count;
            WaveStarted?.Invoke(_wave, RemainingThisWave);
        }

        private void SpawnOne(float damage)
        {
            if (enemyPrefab == null) return;
            if (spawnPoints.Count == 0)
            {
                Debug.LogWarning("[EnemySpawner] No spawn points assigned.");
                return;
            }

            Transform sp = spawnPoints[Random.Range(0, spawnPoints.Count)];
            Vector3 pos = sp.position;
            if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                pos = hit.position;

            var go = Instantiate(enemyPrefab, pos, sp.rotation);
            var ai = go.GetComponent<EnemyAI>();
            if (ai != null)
            {
                ai.Configure(damage);
                ai.SetHome(pos);
            }
            _alive.Add(ai);
        }

        /// <summary>Allows the map to register its spawn markers at build time.</summary>
        public void AddSpawnPoint(Transform t)
        {
            if (t != null && !spawnPoints.Contains(t)) spawnPoints.Add(t);
        }

        public void ClearAllEnemies()
        {
            for (int i = _alive.Count - 1; i >= 0; i--)
                if (_alive[i] != null) Destroy(_alive[i].gameObject);
            _alive.Clear();
            RemainingThisWave = 0;
            _nextWaveTime = Time.time + interWaveDelay;
        }
    }
}
