using UnityEngine;
using FinalDefense.Data;
using FinalDefense.Tower;
using FinalDefense.Battle;
using FinalDefense.NPC;

namespace FinalDefense.Enemy
{
    public class EnemyAttack : MonoBehaviour
    {
        private int attackDamage;
        private float attackSpeed;
        private float attackRange;
        private float attackTimer;
        private LayerMask towerLayer;
        private Enemy enemy;
        private Transform currentTarget;
        private bool canAttackWhileMoving;
        private float healAmount;
        private float healRange;
        private float healTimer;
        private EnemyType enemyType;

        public void Initialize(EnemyData data)
        {
            attackDamage = data.attackDamage;
            attackSpeed = data.attackSpeed;
            attackRange = data.attackRange;
            canAttackWhileMoving = data.canAttackWhileMoving;
            healAmount = data.healAmount;
            healRange = data.healRange;
            enemyType = data.enemyType;
            towerLayer = LayerMask.GetMask("Tower");
            enemy = GetComponent<Enemy>();

            if (NPCRelationshipManager.Instance != null)
            {
                var buffs = NPCRelationshipManager.Instance.CalculateBattleBuffs();
                attackDamage = Mathf.RoundToInt(attackDamage * buffs.GetEnemyAttackMultiplier());
            }
        }

        private void Update()
        {
            if (attackDamage <= 0 && enemyType != EnemyType.HealerMob) return;

            if (enemyType == EnemyType.HealerMob)
            {
                HandleHeal();
                return;
            }

            currentTarget = FindTowerTarget();

            if (currentTarget != null)
            {
                if (enemy != null && !canAttackWhileMoving) enemy.IsAttacking = true;
                attackTimer -= Time.deltaTime;
                if (attackTimer <= 0f)
                {
                    AttackTower();
                    attackTimer = attackSpeed;
                }
            }
            else
            {
                if (enemy != null && !canAttackWhileMoving) enemy.IsAttacking = false;
            }
        }

        private void HandleHeal()
        {
            healTimer -= Time.deltaTime;
            if (healTimer > 0) return;
            healTimer = 2f;

            var hits = Physics2D.OverlapCircleAll(transform.position, healRange, LayerMask.GetMask("Enemy"));
            foreach (var hit in hits)
            {
                if (hit.gameObject == gameObject) continue;
                var health = hit.GetComponent<EnemyHealth>();
                if (health != null && !health.IsDead)
                {
                    health.Heal(Mathf.RoundToInt(healAmount));
                }
            }
        }

        private Transform FindTowerTarget()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, attackRange, towerLayer);
            if (hits.Length == 0) return null;

            float minDist = float.MaxValue;
            Transform closest = null;
            foreach (var hit in hits)
            {
                var health = hit.GetComponent<TowerHealth>();
                if (health != null && !health.IsDowned)
                {
                    float dist = Vector2.Distance(transform.position, hit.transform.position);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        closest = hit.transform;
                    }
                }
            }
            return closest;
        }

        private void AttackTower()
        {
            if (currentTarget == null) return;
            var tower = currentTarget.GetComponent<Tower.Tower>();
            if (tower != null)
            {
                tower.TakeDamageFromEnemy(attackDamage);
            }
            else
            {
                var health = currentTarget.GetComponent<TowerHealth>();
                if (health != null && !health.IsDowned)
                {
                    int damage = DamageCalculator.Calculate(attackDamage, 0);
                    health.TakeDamage(damage);
                }
            }
        }
    }
}
