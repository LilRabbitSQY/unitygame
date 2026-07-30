using UnityEngine;
using FinalDefense.Core;
using FinalDefense.Data;

namespace FinalDefense.Battle
{
    public class AIAgentSystem : MonoBehaviour
    {
        [SerializeField] private int maxUses = 3;
        [SerializeField] private int costToActivate = 30;

        private int usesRemaining;

        public int UsesRemaining => usesRemaining;
        public bool CanUse => usesRemaining > 0;
        public int CostToActivate => costToActivate;

        private void Start()
        {
            usesRemaining = maxUses;
        }

        public bool ActivateAIAgent()
        {
            if (!CanUse) return false;

            var costSystem = Object.FindFirstObjectByType<DeployCostSystem>();
            if (costSystem == null || costSystem.CurrentCost < costToActivate) return false;

            costSystem.TrySpend(costToActivate);
            usesRemaining--;

            var gm = GameManager.Instance;
            if (gm == null) return false;

            ExecuteSkillByPersonality(gm.CurrentPersonality);
            EventBus.AIAgentUsed();
            return true;
        }

        private void ExecuteSkillByPersonality(PersonalityType type)
        {
            var enemies = Object.FindObjectsByType<Enemy.Enemy>(FindObjectsSortMode.None);

            switch (type)
            {
                case PersonalityType.LMAO: // 全场嘲讽 - 怪物短暂混乱
                    foreach (var e in enemies)
                    {
                        e.IsAttacking = true;
                        StartCoroutine(ReleaseAfterDelay(e, 3f));
                    }
                    break;

                case PersonalityType.ASAP: // 加班模式 - 费用恢复大幅提升
                    var costSys = Object.FindFirstObjectByType<DeployCostSystem>();
                    if (costSys != null) costSys.AddCost(20);
                    break;

                case PersonalityType.ZZZZ: // 时间暂停 - 暂停所有怪物
                    foreach (var e in enemies)
                    {
                        e.ApplySlow(0f, 4f);
                    }
                    break;

                case PersonalityType.QWWQ: // 求情信 - 降低所有怪物攻击力
                    foreach (var e in enemies)
                    {
                        var health = e.GetComponent<Enemy.EnemyHealth>();
                        if (health != null)
                            health.TakeDamage(Mathf.RoundToInt(health.Defense * 0.3f));
                    }
                    break;

                case PersonalityType.SSCI: // 数据分析 - 揭示弱点+暴击率提升
                    foreach (var e in enemies)
                    {
                        e.RevealStealth();
                        var health = e.GetComponent<Enemy.EnemyHealth>();
                        if (health != null && health.HasShield)
                            health.TakeDamage(health.ShieldHP);
                    }
                    break;

                case PersonalityType.COOK: // 能量补给 - 全场塔台回满血
                    var towers = Object.FindObjectsByType<Tower.TowerHealth>(FindObjectsSortMode.None);
                    foreach (var t in towers)
                    {
                        if (!t.IsDowned)
                            t.Heal(t.MaxHP);
                    }
                    break;

                case PersonalityType.IDLE: // 摸鱼大法 - 降低所有怪物移速
                    foreach (var e in enemies)
                    {
                        e.ApplySlow(0.3f, 5f);
                    }
                    break;

                case PersonalityType.NORM: // 全能觉醒 - 全场塔台属性提升(通过治疗模拟)
                    var allTowers = Object.FindObjectsByType<Tower.TowerHealth>(FindObjectsSortMode.None);
                    foreach (var t in allTowers)
                    {
                        if (!t.IsDowned)
                            t.Heal(Mathf.RoundToInt(t.MaxHP * 0.5f));
                    }
                    foreach (var e in enemies)
                    {
                        var health = e.GetComponent<Enemy.EnemyHealth>();
                        if (health != null)
                            health.TakeDamage(10);
                    }
                    break;
            }
        }

        private System.Collections.IEnumerator ReleaseAfterDelay(Enemy.Enemy enemy, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (enemy != null) enemy.IsAttacking = false;
        }
    }
}
