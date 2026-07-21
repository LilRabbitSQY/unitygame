using UnityEngine;
using FinalDefense.Data;

namespace FinalDefense.Enemy
{
    public class EnemySkill : MonoBehaviour
    {
        private float currentSP;
        private float maxSP = 20f;
        private float spRegenRate = 1f;
        private int skillDamage = 15;
        private float skillRange = 2f;
        private bool initialized;

        public void Initialize(EnemyData data)
        {
            skillDamage = Mathf.RoundToInt(data.attackDamage * 1.5f);
            skillRange = data.attackRange * 1.5f;
            initialized = true;
        }

        private void Update()
        {
            if (!initialized) return;
            currentSP += spRegenRate * Time.deltaTime;
            if (currentSP >= maxSP)
            {
                ActivateSkill();
                currentSP = 0;
            }
        }

        private void ActivateSkill()
        {
            var hits = Physics2D.OverlapCircleAll(transform.position, skillRange, LayerMask.GetMask("Tower"));
            foreach (var hit in hits)
            {
                var health = hit.GetComponent<Tower.TowerHealth>();
                if (health != null && !health.IsDowned)
                {
                    health.TakeDamage(skillDamage);
                }
            }
        }
    }
}
