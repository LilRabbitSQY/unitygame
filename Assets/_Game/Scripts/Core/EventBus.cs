using System;
using UnityEngine;

namespace FinalDefense.Core
{
    public static class EventBus
    {
        // Battle events
        public static event Action<int> OnEnemyReachedEnd;
        public static event Action<int> OnEnemyKilled;
        public static event Action OnWaveCompleted;
        public static event Action OnAllWavesCompleted;
        public static event Action OnBattleLost;
        public static event Action OnBattleWon;

        // Economy
        public static event Action<int> OnGoldChanged;
        public static event Action<int, int> OnCostChanged; // current, max
        public static event Action<int> OnEnemyCostReward;

        // Tower events
        public static event Action<GameObject> OnTowerDowned;
        public static event Action<GameObject> OnTowerRedeployed;
        public static event Action<GameObject, int> OnTowerUpgraded; // tower, newLevel

        // Schedule events
        public static event Action OnScheduleCompleted;

        // DDL events
        public static event Action OnDDLExpired;

        // Skill events
        public static event Action<GameObject> OnSkillActivated;

        // AI Agent
        public static event Action OnAIAgentUsed;
        public static event Action<int> OnMisconductChanged;

        public static void EnemyReachedEnd(int gpaDamage) => OnEnemyReachedEnd?.Invoke(gpaDamage);
        public static void EnemyKilled(int reward) => OnEnemyKilled?.Invoke(reward);
        public static void WaveCompleted() => OnWaveCompleted?.Invoke();
        public static void AllWavesCompleted() => OnAllWavesCompleted?.Invoke();
        public static void BattleLost() => OnBattleLost?.Invoke();
        public static void BattleWon() => OnBattleWon?.Invoke();
        public static void GoldChanged(int currentGold) => OnGoldChanged?.Invoke(currentGold);
        public static void CostChanged(int current, int max) => OnCostChanged?.Invoke(current, max);
        public static void EnemyCostReward(int cost) => OnEnemyCostReward?.Invoke(cost);
        public static void TowerDowned(GameObject tower) => OnTowerDowned?.Invoke(tower);
        public static void TowerRedeployed(GameObject tower) => OnTowerRedeployed?.Invoke(tower);
        public static void TowerUpgraded(GameObject tower, int level) => OnTowerUpgraded?.Invoke(tower, level);
        public static void ScheduleCompleted() => OnScheduleCompleted?.Invoke();
        public static void DDLExpired() => OnDDLExpired?.Invoke();
        public static void SkillActivated(GameObject source) => OnSkillActivated?.Invoke(source);
        public static void AIAgentUsed() => OnAIAgentUsed?.Invoke();
        public static void MisconductChanged(int value) => OnMisconductChanged?.Invoke(value);

        public static void Clear()
        {
            OnEnemyReachedEnd = null;
            OnEnemyKilled = null;
            OnWaveCompleted = null;
            OnAllWavesCompleted = null;
            OnBattleLost = null;
            OnBattleWon = null;
            OnGoldChanged = null;
            OnCostChanged = null;
            OnEnemyCostReward = null;
            OnTowerDowned = null;
            OnTowerRedeployed = null;
            OnTowerUpgraded = null;
            OnScheduleCompleted = null;
            OnDDLExpired = null;
            OnSkillActivated = null;
            OnAIAgentUsed = null;
            OnMisconductChanged = null;
        }
    }
}
