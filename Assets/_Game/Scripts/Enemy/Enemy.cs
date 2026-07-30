using UnityEngine;
using FinalDefense.Core;
using FinalDefense.Data;
using FinalDefense.Path;

namespace FinalDefense.Enemy
{
    public class Enemy : MonoBehaviour
    {
        private float moveSpeed;
        private float baseMoveSpeed;
        private int gpaDamage;
        private int currentWaypointIndex;
        private bool isAttacking;
        private MoveType moveType;
        private EnemyType enemyType;
        private bool isStealthed;
        private float stealthTimer;
        private float stealthDuration;
        private EnemyHealth health;
        private float slowMultiplier = 1f;
        private float slowTimer;

        public int CurrentWaypointIndex => currentWaypointIndex;
        public bool IsAttacking { get => isAttacking; set => isAttacking = value; }
        public float MoveSpeed => moveSpeed;
        public MoveType MoveData => moveType;
        public EnemyType EnemyTypeData => enemyType;
        public bool IsStealthed => isStealthed;

        public void Initialize(EnemyData data, float hpMultiplier)
        {
            baseMoveSpeed = data.moveSpeed;
            moveSpeed = baseMoveSpeed;
            gpaDamage = data.gpaDamage;
            moveType = data.moveType;
            enemyType = data.enemyType;
            currentWaypointIndex = 0;
            stealthDuration = data.stealthDuration;

            health = GetComponent<EnemyHealth>();
            if (health != null)
                health.Initialize(data, hpMultiplier);

            var attack = GetComponent<EnemyAttack>();
            if (attack != null)
                attack.Initialize(data);

            if (enemyType == EnemyType.Stealth)
            {
                isStealthed = true;
                stealthTimer = stealthDuration;
            }
        }

        private void Update()
        {
            if (PathManager.Instance == null) return;

            HandleStealth();
            HandleSlow();

            if (isAttacking && enemyType != EnemyType.Ranger) return;

            Transform target = PathManager.Instance.GetWaypoint(currentWaypointIndex);
            if (target == null)
            {
                ReachEnd();
                return;
            }

            float effectiveSpeed = moveSpeed * slowMultiplier;
            Vector3 direction = (target.position - transform.position).normalized;
            transform.position += direction * effectiveSpeed * Time.deltaTime;

            if (Vector3.Distance(transform.position, target.position) < 0.1f)
            {
                currentWaypointIndex++;
                if (currentWaypointIndex >= PathManager.Instance.WaypointCount)
                {
                    ReachEnd();
                }
            }
        }

        private void HandleStealth()
        {
            if (!isStealthed) return;
            stealthTimer -= Time.deltaTime;
            if (stealthTimer <= 0)
            {
                isStealthed = false;
                var sr = GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 1f);
            }
        }

        private void HandleSlow()
        {
            if (slowTimer > 0)
            {
                slowTimer -= Time.deltaTime;
                if (slowTimer <= 0)
                    slowMultiplier = 1f;
            }
        }

        public void ApplySlow(float multiplier, float duration)
        {
            slowMultiplier = Mathf.Min(slowMultiplier, multiplier);
            slowTimer = Mathf.Max(slowTimer, duration);
        }

        public void RevealStealth()
        {
            isStealthed = false;
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 1f);
        }

        private void ReachEnd()
        {
            EventBus.EnemyReachedEnd(gpaDamage);
            Destroy(gameObject);
        }
    }
}
