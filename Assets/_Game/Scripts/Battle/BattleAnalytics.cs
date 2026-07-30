using System.Collections.Generic;
using UnityEngine;
using FinalDefense.Core;
using FinalDefense.Data;

namespace FinalDefense.Battle
{
    [System.Serializable]
    public class BattleAnalyticsData
    {
        public int groundTowersDeployed;
        public int highGroundTowersDeployed;
        public Dictionary<TowerType, int> towerTypeCount = new();
        public int totalCostSpent;
        public int totalCostEarned;
        public int enemiesKilled;
        public int enemiesLeaked;
        public float battleDuration;
        public float averageReactionTime;

        public float GroundRatio => (groundTowersDeployed + highGroundTowersDeployed) > 0
            ? (float)groundTowersDeployed / (groundTowersDeployed + highGroundTowersDeployed)
            : 0.5f;

        public float HighGroundRatio => 1f - GroundRatio;

        public float CostEfficiency => totalCostEarned > 0
            ? (float)totalCostSpent / totalCostEarned
            : 1f;

        public TowerType MostUsedTowerType
        {
            get
            {
                TowerType best = TowerType.Heavy;
                int bestCount = 0;
                foreach (var kvp in towerTypeCount)
                {
                    if (kvp.Value > bestCount)
                    {
                        bestCount = kvp.Value;
                        best = kvp.Key;
                    }
                }
                return best;
            }
        }
    }

    public class BattleAnalytics : MonoBehaviour
    {
        public static BattleAnalytics Instance { get; private set; }

        private BattleAnalyticsData currentData;
        private BattleAnalyticsData lastBattleData;
        private float battleStartTime;

        public BattleAnalyticsData LastBattleData => lastBattleData;

        private void Awake()
        {
            Instance = this;
            currentData = new BattleAnalyticsData();
            battleStartTime = Time.time;
        }

        private void OnEnable()
        {
            EventBus.OnEnemyKilled += OnEnemyKilled;
            EventBus.OnEnemyReachedEnd += OnEnemyLeaked;
            EventBus.OnCostChanged += OnCostChanged;
        }

        private void OnDisable()
        {
            EventBus.OnEnemyKilled -= OnEnemyKilled;
            EventBus.OnEnemyReachedEnd -= OnEnemyLeaked;
            EventBus.OnCostChanged -= OnCostChanged;
        }

        private void OnEnemyKilled(int _) => currentData.enemiesKilled++;
        private void OnEnemyLeaked(int _) => currentData.enemiesLeaked++;
        private void OnCostChanged(int current, int max) { }

        public void RecordTowerPlaced(TowerData data)
        {
            if (data.deployPosition == DeployPosition.HighGround)
                currentData.highGroundTowersDeployed++;
            else
                currentData.groundTowersDeployed++;

            if (!currentData.towerTypeCount.ContainsKey(data.towerType))
                currentData.towerTypeCount[data.towerType] = 0;
            currentData.towerTypeCount[data.towerType]++;
        }

        public void RecordCostSpent(int amount) => currentData.totalCostSpent += amount;
        public void RecordCostEarned(int amount) => currentData.totalCostEarned += amount;

        public void FinalizeBattle()
        {
            currentData.battleDuration = Time.time - battleStartTime;
            lastBattleData = currentData;
        }

        public BattleAnalyticsData GetCurrentData() => currentData;

        public string GenerateHalftimeReport()
        {
            var d = currentData;
            string report = $"战斗数据分析：\n";
            report += $"· 地面塔台占比: {Mathf.RoundToInt(d.GroundRatio * 100)}%\n";
            report += $"· 高台塔台占比: {Mathf.RoundToInt(d.HighGroundRatio * 100)}%\n";
            report += $"· 费用使用率: {Mathf.RoundToInt(d.CostEfficiency * 100)}%\n";
            report += $"· 已击杀: {d.enemiesKilled} / 已漏: {d.enemiesLeaked}\n";

            string prediction = "";
            if (d.HighGroundRatio < 0.3f)
                prediction = "下半场飞行单位比例将提升，建议补充高台对空单位";
            else if (d.GroundRatio < 0.3f)
                prediction = "下半场地面重装单位将增多，建议补充地面阻挡";
            else if (d.CostEfficiency > 0.8f)
                prediction = "下半场出怪量将增加，建议节约费用";
            else
                prediction = "下半场怪物整体属性将提升，保持当前策略";

            report += $"\n预测：{prediction}";
            return report;
        }
    }
}
