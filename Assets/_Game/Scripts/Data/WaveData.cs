using UnityEngine;

namespace FinalDefense.Data
{
    [CreateAssetMenu(menuName = "FinalDefense/Wave Data")]
    public class WaveData : ScriptableObject
    {
        public WaveEntry[] waves;
    }

    [System.Serializable]
    public class WaveEntry
    {
        public EnemyData enemyType;
        public int count = 5;
        public float spawnInterval = 1f;
        public float delayBeforeWave = 3f;
    }
}
