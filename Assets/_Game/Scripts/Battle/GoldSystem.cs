using UnityEngine;
using FinalDefense.Core;

namespace FinalDefense.Battle
{
    public class GoldSystem : MonoBehaviour
    {
        [SerializeField] private int startingGold = 100;

        public int CurrentGold { get; private set; }

        private void Start()
        {
            CurrentGold = startingGold;
            EventBus.GoldChanged(CurrentGold);
        }

        private void OnDestroy()
        {
        }

        private void AddGold(int amount)
        {
            CurrentGold += amount;
            EventBus.GoldChanged(CurrentGold);
        }

        public bool TrySpend(int amount)
        {
            if (CurrentGold < amount) return false;
            CurrentGold -= amount;
            EventBus.GoldChanged(CurrentGold);
            return true;
        }
    }
}
