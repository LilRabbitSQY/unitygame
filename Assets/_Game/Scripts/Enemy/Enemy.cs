using UnityEngine;
using FinalDefense.Core;
using FinalDefense.Data;
using FinalDefense.Path;

namespace FinalDefense.Enemy
{
    public class Enemy : MonoBehaviour
    {
        private float moveSpeed;
        private int gpaDamage;
        private int currentWaypointIndex;
        private bool isAttacking;

        public int CurrentWaypointIndex => currentWaypointIndex;
        public bool IsAttacking { get => isAttacking; set => isAttacking = value; }
        public float MoveSpeed => moveSpeed;

        private EnemyHealth health;

        public void Initialize(EnemyData data, float hpMultiplier)
        {
            moveSpeed = data.moveSpeed;
            gpaDamage = data.gpaDamage;
            currentWaypointIndex = 0;

            health = GetComponent<EnemyHealth>();
            if (health != null)
            {
                health.Initialize(data, hpMultiplier);
            }

            var attack = GetComponent<EnemyAttack>();
            if (attack != null)
            {
                attack.Initialize(data);
            }
        }

        private void Update()
        {
            if (PathManager.Instance == null) return;
            if (isAttacking) return;

            Transform target = PathManager.Instance.GetWaypoint(currentWaypointIndex);
            if (target == null)
            {
                ReachEnd();
                return;
            }

            Vector3 direction = (target.position - transform.position).normalized;
            transform.position += direction * moveSpeed * Time.deltaTime;

            if (Vector3.Distance(transform.position, target.position) < 0.1f)
            {
                currentWaypointIndex++;
                if (currentWaypointIndex >= PathManager.Instance.WaypointCount)
                {
                    ReachEnd();
                }
            }
        }

        private void ReachEnd()
        {
            EventBus.EnemyReachedEnd(gpaDamage);
            Destroy(gameObject);
        }
    }
}
