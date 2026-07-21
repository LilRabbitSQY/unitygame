using UnityEngine;
using FinalDefense.Core;
using FinalDefense.Data;

namespace FinalDefense.Enemy
{
    public class EnemyHealth : MonoBehaviour
    {
        private int currentHP;
        private int maxHP;
        private int goldReward;
        private int costReward;

        public float HPRatio => maxHP > 0 ? (float)currentHP / maxHP : 0f;
        public bool IsDead => currentHP <= 0;

        public void Initialize(EnemyData data, float hpMultiplier)
        {
            maxHP = Mathf.RoundToInt(data.maxHP * hpMultiplier);
            currentHP = maxHP;
            goldReward = data.goldReward;
            costReward = data.costReward;
        }

        public void TakeDamage(int amount)
        {
            if (IsDead) return;
            currentHP -= amount;
            if (currentHP <= 0)
            {
                currentHP = 0;
                Die();
            }
        }

        private void Die()
        {
            EventBus.EnemyKilled(goldReward);
            EventBus.EnemyCostReward(costReward);
            Destroy(gameObject);
        }
    }
}
