using UnityEngine;
using FinalDefense.Core;
using FinalDefense.Data;

namespace FinalDefense.Tower
{
    public class TowerHealth : MonoBehaviour
    {
        private int maxHP;
        private int currentHP;
        private float redeployCooldown;
        private int redeployCost;
        private bool isDowned;
        private float downTimer;
        private SpriteRenderer spriteRenderer;
        private Color originalColor;
        private Tower tower;

        public bool IsDowned => isDowned;
        public int CurrentHP => currentHP;
        public int MaxHP => maxHP;
        public float RedeployProgress => isDowned ? (downTimer / redeployCooldown) : 0f;

        public void Initialize(TowerData data)
        {
            maxHP = data.maxHP;
            currentHP = maxHP;
            redeployCooldown = data.redeployCooldown;
            redeployCost = data.redeployCost;
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null) originalColor = spriteRenderer.color;
            tower = GetComponent<Tower>();
        }

        public void TakeDamage(int damage)
        {
            if (isDowned) return;
            currentHP = Mathf.Max(0, currentHP - damage);
            if (currentHP <= 0)
            {
                Down();
            }
        }

        private void Down()
        {
            isDowned = true;
            downTimer = 0f;
            if (spriteRenderer != null)
                spriteRenderer.color = new Color(0.3f, 0.3f, 0.3f, 0.6f);
            EventBus.TowerDowned(gameObject);
        }

        private void Update()
        {
            if (!isDowned) return;
            downTimer += Time.deltaTime;
            if (downTimer >= redeployCooldown)
            {
                TryRedeploy();
            }
        }

        private void TryRedeploy()
        {
            var costSystem = Object.FindFirstObjectByType<Battle.DeployCostSystem>();
            if (costSystem != null && costSystem.TrySpend(redeployCost))
            {
                Redeploy();
            }
        }

        public void Redeploy()
        {
            isDowned = false;
            currentHP = maxHP;
            downTimer = 0f;
            if (spriteRenderer != null)
                spriteRenderer.color = originalColor;
            EventBus.TowerRedeployed(gameObject);
        }

        public void Heal(int amount)
        {
            if (isDowned) return;
            currentHP = Mathf.Min(currentHP + amount, maxHP);
        }

        public void SetMaxHP(int newMax)
        {
            maxHP = newMax;
            currentHP = Mathf.Min(currentHP, maxHP);
        }
    }
}
