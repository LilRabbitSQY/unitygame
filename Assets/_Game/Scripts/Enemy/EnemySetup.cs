using UnityEngine;
using FinalDefense.Data;

namespace FinalDefense.Enemy
{
    public class EnemySetup : MonoBehaviour
    {
        private void Start()
        {
            gameObject.layer = LayerMask.NameToLayer("Enemy");
        }

        public void ApplyVisuals(EnemyData data)
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null && data != null)
            {
                sr.color = data.enemyColor;
                sr.sortingOrder = 10;
            }

            float scale = data.maxHP > 80 ? 1.8f : data.maxHP > 30 ? 1.4f : 1.0f;
            transform.localScale = Vector3.one * scale;
        }
    }
}
