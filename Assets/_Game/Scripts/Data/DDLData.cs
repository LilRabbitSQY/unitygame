using UnityEngine;

namespace FinalDefense.Data
{
    [CreateAssetMenu(menuName = "FinalDefense/DDL Data")]
    public class DDLData : ScriptableObject
    {
        public string ddlName;
        public string description;
        public int deadlineDays = 3;
        public float waveCountMultiplier = 1.5f;
        public int gpaPenalty = 10;
        public EnemyData associatedEnemy;
    }
}
