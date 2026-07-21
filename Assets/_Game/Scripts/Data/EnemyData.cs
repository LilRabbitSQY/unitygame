using UnityEngine;

namespace FinalDefense.Data
{
    [CreateAssetMenu(menuName = "FinalDefense/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        public string enemyName;
        public int maxHP = 20;
        public float moveSpeed = 2f;
        public int gpaDamage = 2;
        public int goldReward = 10;
        public int costReward = 2;
        public int attackDamage = 5;
        public float attackSpeed = 1.5f;
        public float attackRange = 1.2f;
        public GameObject prefab;
        public Color enemyColor = Color.red;
    }
}
