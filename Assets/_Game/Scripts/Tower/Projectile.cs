using UnityEngine;

namespace FinalDefense.Tower
{
    public class Projectile : MonoBehaviour
    {
        private Transform target;
        private int damage;
        private float speed = 10f;

        public void Initialize(Transform target, int damage)
        {
            this.target = target;
            this.damage = damage;
        }

        private void Update()
        {
            if (target == null)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 dir = (target.position - transform.position).normalized;
            transform.position += dir * speed * Time.deltaTime;

            if (Vector3.Distance(transform.position, target.position) < 0.15f)
            {
                HitTarget();
            }
        }

        private void HitTarget()
        {
            if (target != null)
            {
                var health = target.GetComponent<Enemy.EnemyHealth>();
                if (health != null)
                {
                    health.TakeDamage(damage);
                }
            }
            Destroy(gameObject);
        }
    }
}
