using UnityEngine;
using FinalDefense.Core;
using FinalDefense.Data;

namespace FinalDefense.Tower
{
    public class Tower : MonoBehaviour
    {
        private TowerData data;
        private float attackTimer;
        private Transform currentTarget;
        private LayerMask enemyLayer;
        private TowerHealth towerHealth;
        private int currentLevel = 1;

        public TowerData Data => data;
        public int CurrentLevel => currentLevel;

        public void Initialize(TowerData towerData)
        {
            data = towerData;
            enemyLayer = LayerMask.GetMask("Enemy");
            towerHealth = GetComponent<TowerHealth>();
            if (towerHealth != null)
                towerHealth.Initialize(towerData);
        }

        private void Update()
        {
            if (data == null) return;
            if (towerHealth != null && towerHealth.IsDowned) return;

            attackTimer -= Time.deltaTime;

            if (currentTarget == null || !currentTarget.gameObject.activeInHierarchy)
            {
                currentTarget = FindTarget();
            }

            if (currentTarget != null && attackTimer <= 0f)
            {
                Attack();
                attackTimer = data.attackInterval;
            }
        }

        private Transform FindTarget()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, data.attackRange, enemyLayer);
            if (hits.Length == 0) return null;

            Transform best = null;
            int highestIndex = -1;

            foreach (var hit in hits)
            {
                var enemy = hit.GetComponent<Enemy.Enemy>();
                if (enemy != null && enemy.CurrentWaypointIndex > highestIndex)
                {
                    highestIndex = enemy.CurrentWaypointIndex;
                    best = hit.transform;
                }
            }
            return best;
        }

        private void Attack()
        {
            if (currentTarget == null) return;

            int finalDamage = GetDamage();

            if (data.projectilePrefab != null)
            {
                var go = Instantiate(data.projectilePrefab, transform.position, Quaternion.identity);
                var proj = go.GetComponent<Projectile>();
                if (proj != null)
                {
                    proj.Initialize(currentTarget, finalDamage);
                }
            }
            else
            {
                var health = currentTarget.GetComponent<Enemy.EnemyHealth>();
                if (health != null)
                {
                    health.TakeDamage(finalDamage);
                }
            }
        }

        public int GetDamage()
        {
            float levelMult = 1f;
            for (int i = 1; i < currentLevel; i++)
                levelMult *= data.upgradeDamageMultiplier;

            int baseDmg = Mathf.RoundToInt(data.damage * levelMult);
            if (GameManager.Instance != null)
                baseDmg = Mathf.RoundToInt(baseDmg * (1f + GameManager.Instance.GetEduPowerBonus()));
            return baseDmg;
        }

        public void LevelUp()
        {
            if (currentLevel >= data.maxLevel) return;
            currentLevel++;
            if (towerHealth != null)
            {
                int newMaxHP = Mathf.RoundToInt(data.maxHP * Mathf.Pow(data.upgradeHPMultiplier, currentLevel - 1));
                towerHealth.SetMaxHP(newMaxHP);
            }
            EventBus.TowerUpgraded(gameObject, currentLevel);
        }

        private void OnDrawGizmosSelected()
        {
            if (data != null)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
                Gizmos.DrawWireSphere(transform.position, data.attackRange);
            }
        }
    }
}
