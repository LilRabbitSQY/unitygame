using UnityEngine;

namespace FinalDefense.Data
{
    [CreateAssetMenu(menuName = "FinalDefense/Tower Data")]
    public class TowerData : ScriptableObject
    {
        public string towerName;
        public int cost;
        public int deployCost = 5;
        public float attackRange = 2.5f;
        public float attackInterval = 1f;
        public int damage = 10;
        public int maxHP = 200;
        public float redeployCooldown = 15f;
        public int redeployCost = 3;
        public int maxLevel = 3;
        public float upgradeHPMultiplier = 1.5f;
        public float upgradeDamageMultiplier = 1.5f;
        public GameObject prefab;
        public GameObject projectilePrefab;
        public Sprite icon;
        public Color towerColor = Color.green;
    }
}
