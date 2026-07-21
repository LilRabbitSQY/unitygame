using UnityEngine;
using FinalDefense.Core;

namespace FinalDefense.Battle
{
    public class DeployCostSystem : MonoBehaviour
    {
        [SerializeField] private int initialCost = 10;
        [SerializeField] private int maxCost = 100;
        [SerializeField] private float regenRate = 1f;

        public int CurrentCost { get; private set; }
        public int MaxCost => maxCost;

        private float regenTimer;

        private void Start()
        {
            CurrentCost = initialCost;
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
            if (regenTimer >= regenRate && CurrentCost < maxCost)
            {
                regenTimer -= regenRate;
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
            return true;
        }
    }
}
