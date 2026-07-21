using UnityEngine;
using FinalDefense.Data;
using FinalDefense.Tower;

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

        public void Initialize(EnemyData data)
        {
            attackDamage = data.attackDamage;
            attackSpeed = data.attackSpeed;
            attackRange = data.attackRange;
            towerLayer = LayerMask.GetMask("Tower");
            enemy = GetComponent<Enemy>();
        }

        private void Update()
        {
            if (attackDamage <= 0) return;

            currentTarget = FindTowerTarget();

            if (currentTarget != null)
            {
                if (enemy != null) enemy.IsAttacking = true;
                attackTimer -= Time.deltaTime;
                if (attackTimer <= 0f)
                {
                    AttackTower();
                    attackTimer = attackSpeed;
                }
            }
            else
            {
                if (enemy != null) enemy.IsAttacking = false;
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
            var health = currentTarget.GetComponent<TowerHealth>();
            if (health != null && !health.IsDowned)
            {
                health.TakeDamage(attackDamage);
            }
        }
    }
}
