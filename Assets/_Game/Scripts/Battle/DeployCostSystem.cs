using UnityEngine;
using FinalDefense.Core;
using FinalDefense.NPC;

namespace FinalDefense.Battle
{
    public class DeployCostSystem : MonoBehaviour
    {
        [SerializeField] private int initialCost = 10;
        [SerializeField] private int maxCost = 100;
        [SerializeField] private float baseRegenInterval = 1f;

        public int CurrentCost { get; private set; }
        public int MaxCost => maxCost;

        private float regenTimer;
        private float effectiveRegenInterval;

        private void Start()
        {
            CurrentCost = initialCost;
            effectiveRegenInterval = baseRegenInterval;

            if (NPCRelationshipManager.Instance != null)
            {
                var buffs = NPCRelationshipManager.Instance.CalculateBattleBuffs();
                effectiveRegenInterval = baseRegenInterval / buffs.GetCostRegenMultiplier();
            }

            EventBus.CostChanged(CurrentCost, maxCost);
            EventBus.OnEnemyCostReward += OnEnemyCostReward;
        }

        private void OnDestroy()
        {
            EventBus.OnEnemyCostReward -= OnEnemyCostReward;
        }

        private void Update()
        {
            regenTimer += Time.deltaTime;
            if (regenTimer >= effectiveRegenInterval && CurrentCost < maxCost)
            {
                regenTimer -= effectiveRegenInterval;
                CurrentCost = Mathf.Min(CurrentCost + 1, maxCost);
                EventBus.CostChanged(CurrentCost, maxCost);
            }
        }

        private void OnEnemyCostReward(int reward)
        {
            AddCost(reward);
        }

        public void AddCost(int amount)
        {
            CurrentCost = Mathf.Min(CurrentCost + amount, maxCost);
            EventBus.CostChanged(CurrentCost, maxCost);
        }

        public bool TrySpend(int amount)
        {
            if (CurrentCost < amount) return false;
            CurrentCost -= amount;
            EventBus.CostChanged(CurrentCost, maxCost);

            var analytics = BattleAnalytics.Instance;
            if (analytics != null)
                analytics.RecordCostSpent(amount);

            return true;
        }
    }
}
