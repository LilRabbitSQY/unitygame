using UnityEngine;
using FinalDefense.Core;
using FinalDefense.Data;
using FinalDefense.NPC;

namespace FinalDefense.Enemy
{
    public class EnemyHealth : MonoBehaviour
    {
        private int currentHP;
        private int maxHP;
        private int defense;
        private int shieldHP;
        private int maxShieldHP;
        private int goldReward;
        private int costReward;
        private EnemyData enemyData;

        public float HPRatio => maxHP > 0 ? (float)currentHP / maxHP : 0f;
        public bool IsDead => currentHP <= 0;
        public int Defense => defense;
        public int ShieldHP => shieldHP;
        public bool HasShield => shieldHP > 0;

        public void Initialize(EnemyData data, float hpMultiplier)
        {
            enemyData = data;
            maxHP = Mathf.RoundToInt(data.maxHP * hpMultiplier);
            currentHP = maxHP;
            defense = data.defense;
            shieldHP = data.shieldHP;
            maxShieldHP = data.shieldHP;
            goldReward = data.goldReward;
            costReward = data.costReward;

            if (NPCRelationshipManager.Instance != null)
            {
                var buffs = NPCRelationshipManager.Instance.CalculateBattleBuffs();
                defense = Mathf.RoundToInt(defense * buffs.GetEnemyDefenseMultiplier());
            }
        }

        public void TakeDamage(int amount)
        {
            if (IsDead) return;

            if (shieldHP > 0)
            {
                if (amount >= shieldHP)
                {
                    amount -= shieldHP;
                    shieldHP = 0;
                }
                else
                {
                    shieldHP -= amount;
                    return;
                }
            }

            currentHP -= amount;
            if (currentHP <= 0)
            {
                currentHP = 0;
                Die();
            }
        }

        public void Heal(int amount)
        {
            if (IsDead) return;
            currentHP = Mathf.Min(currentHP + amount, maxHP);
        }

        private void Die()
        {
            if (enemyData != null && enemyData.enemyType == EnemyType.Splitter && enemyData.splitCount > 0 && enemyData.splitInto != null)
            {
                SpawnSplitEnemies();
            }

            EventBus.EnemyKilled(goldReward);
            EventBus.EnemyCostReward(costReward);
            Destroy(gameObject);
        }

        private void SpawnSplitEnemies()
        {
            for (int i = 0; i < enemyData.splitCount; i++)
            {
                Vector3 offset = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.3f, 0.3f), 0);
                Vector3 spawnPos = transform.position + offset;

                var go = new GameObject("SplitEnemy");
                go.transform.position = spawnPos;
                go.layer = LayerMask.NameToLayer("Enemy");

                var sr = go.AddComponent<SpriteRenderer>();
                sr.color = enemyData.splitInto.enemyColor;
                sr.sortingOrder = 10;
                var tex = new Texture2D(24, 24);
                var colors = new Color[24 * 24];
                for (int c = 0; c < colors.Length; c++) colors[c] = Color.white;
                tex.SetPixels(colors);
                tex.Apply();
                sr.sprite = Sprite.Create(tex, new Rect(0, 0, 24, 24), Vector2.one * 0.5f, 24);
                go.transform.localScale = Vector3.one * 0.5f;

                go.AddComponent<CircleCollider2D>().radius = 0.3f;
                var health = go.AddComponent<EnemyHealth>();
                var enemy = go.AddComponent<Enemy>();
                var attack = go.AddComponent<EnemyAttack>();

                enemy.Initialize(enemyData.splitInto, 1f);
            }
        }
    }
}
