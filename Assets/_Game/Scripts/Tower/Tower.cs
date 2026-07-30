using UnityEngine;
using FinalDefense.Core;
using FinalDefense.Data;
using FinalDefense.Battle;
using FinalDefense.NPC;

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
        private int currentDefense;
        private float currentAttackSpeed;
        private BattleBuffs battleBuffs;

        public TowerData Data => data;
        public int CurrentLevel => currentLevel;
        public int CurrentDefense => currentDefense;

        public void Initialize(TowerData towerData)
        {
            data = towerData;
            enemyLayer = LayerMask.GetMask("Enemy");
            towerHealth = GetComponent<TowerHealth>();
            if (towerHealth != null)
                towerHealth.Initialize(towerData);

            currentDefense = towerData.defense;
            currentAttackSpeed = towerData.attackSpeed;

            if (NPCRelationshipManager.Instance != null)
                battleBuffs = NPCRelationshipManager.Instance.CalculateBattleBuffs();
        }

        private void Update()
        {
            if (data == null) return;
            if (towerHealth != null && towerHealth.IsDowned) return;

            float interval = data.attackInterval / GetEffectiveAttackSpeed();
            attackTimer -= Time.deltaTime;

            if (currentTarget == null || !currentTarget.gameObject.activeInHierarchy)
            {
                currentTarget = FindTarget();
            }

            if (currentTarget != null && attackTimer <= 0f)
            {
                Attack();
                attackTimer = interval;
            }
        }

        private float GetEffectiveAttackSpeed()
        {
            float speed = currentAttackSpeed;
            float levelBonus = 1f + (currentLevel - 1) * 0.1f;
            speed *= levelBonus;
            speed *= battleBuffs.GetAllyAttackSpeedMultiplier();
            return Mathf.Max(0.1f, speed);
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
                if (enemy == null) continue;

                if (data.deployPosition == DeployPosition.Ground && enemy.MoveData == MoveType.Air)
                    continue;

                if (data.towerType == TowerType.Sniper && enemy.MoveData == MoveType.Air)
                {
                    if (best == null || enemy.CurrentWaypointIndex > highestIndex)
                    {
                        highestIndex = enemy.CurrentWaypointIndex;
                        best = hit.transform;
                    }
                    continue;
                }

                if (enemy.CurrentWaypointIndex > highestIndex)
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
            var enemyHealth = currentTarget.GetComponent<Enemy.EnemyHealth>();
            if (enemyHealth == null) return;

            int enemyDef = enemyHealth.Defense;
            int actualDamage = DamageCalculator.Calculate(finalDamage, enemyDef);

            if (battleBuffs.critRate > 0 && Random.value < battleBuffs.critRate)
                actualDamage = Mathf.RoundToInt(actualDamage * 1.5f);

            if (data.projectilePrefab != null)
            {
                var go = Instantiate(data.projectilePrefab, transform.position, Quaternion.identity);
                var proj = go.GetComponent<Projectile>();
                if (proj != null)
                    proj.Initialize(currentTarget, actualDamage);
            }
            else
            {
                enemyHealth.TakeDamage(actualDamage);
            }

            if (data.towerType == TowerType.Vanguard && enemyHealth.IsDead)
            {
                var costSystem = Object.FindFirstObjectByType<DeployCostSystem>();
                if (costSystem != null)
                    costSystem.AddCost(1);
            }
        }

        public int GetDamage()
        {
            float levelMult = 1f;
            for (int i = 1; i < currentLevel; i++)
                levelMult *= data.upgradeDamageMultiplier;

            int baseDmg = Mathf.RoundToInt(data.damage * levelMult);
            baseDmg = Mathf.RoundToInt(baseDmg * battleBuffs.GetAllyAttackMultiplier());

            if (GameManager.Instance != null)
                baseDmg = Mathf.RoundToInt(baseDmg * (1f + GameManager.Instance.GetEduPowerBonus()));

            return baseDmg;
        }

        public void LevelUp()
        {
            if (currentLevel >= data.maxLevel) return;
            currentLevel++;
            currentDefense = Mathf.RoundToInt(data.defense * Mathf.Pow(data.upgradeDefenseMultiplier, currentLevel - 1));
            if (towerHealth != null)
            {
                int newMaxHP = Mathf.RoundToInt(data.maxHP * Mathf.Pow(data.upgradeHPMultiplier, currentLevel - 1));
                towerHealth.SetMaxHP(newMaxHP);
            }
            EventBus.TowerUpgraded(gameObject, currentLevel);
        }

        public void TakeDamageFromEnemy(int rawAttack)
        {
            int actualDamage = DamageCalculator.Calculate(rawAttack, currentDefense);
            if (towerHealth != null)
                towerHealth.TakeDamage(actualDamage);
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
