using UnityEngine;

namespace FinalDefense.Enemy
{
    public class EnemyHealthBar : MonoBehaviour
    {
        [SerializeField] private Transform barFill;
        private EnemyHealth health;

        private void Start()
        {
            health = GetComponentInParent<EnemyHealth>();
        }

        private void Update()
        {
            if (health == null || barFill == null) return;
            Vector3 scale = barFill.localScale;
            scale.x = health.HPRatio;
            barFill.localScale = scale;
        }
    }
}
