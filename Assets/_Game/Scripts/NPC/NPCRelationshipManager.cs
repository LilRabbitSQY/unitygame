using System.Collections.Generic;
using UnityEngine;
using FinalDefense.Core;
using FinalDefense.Data;

namespace FinalDefense.NPC
{
    public class NPCRelationshipManager : MonoBehaviour
    {
        [SerializeField] private NPCData[] npcDataList;

        private Dictionary<NPCId, NPCRelationship> relationships = new();

        public static NPCRelationshipManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeRelationships();
        }

        private void InitializeRelationships()
        {
            relationships.Clear();
            if (npcDataList == null) return;
            foreach (var data in npcDataList)
            {
                relationships[data.npcId] = new NPCRelationship(data);
            }
        }

        public NPCRelationship GetRelationship(NPCId id)
        {
            return relationships.TryGetValue(id, out var rel) ? rel : null;
        }

        public NPCData GetNPCData(NPCId id)
        {
            if (npcDataList == null) return null;
            foreach (var data in npcDataList)
            {
                if (data.npcId == id) return data;
            }
            return null;
        }

        public BattleBuffs CalculateBattleBuffs()
        {
            var buffs = new BattleBuffs();

            foreach (var kvp in relationships)
            {
                var data = GetNPCData(kvp.Key);
                if (data == null) continue;

                float influence = (kvp.Value.favorability - 50f) / 100f;

                switch (data.battleInfluence)
                {
                    case NPCBattleInfluence.AllyAttack:
                        buffs.allyAttackMult += influence * 0.3f;
                        break;
                    case NPCBattleInfluence.AllyDefense:
                        buffs.allyDefenseMult += influence * 0.3f;
                        break;
                    case NPCBattleInfluence.AllyAttackSpeed:
                        buffs.allyAttackSpeedMult += influence * 0.3f;
                        break;
                    case NPCBattleInfluence.AllyCostRegen:
                        buffs.costRegenMult += influence * 0.3f;
                        break;
                    case NPCBattleInfluence.EnemyAttack:
                        buffs.enemyAttackMult -= influence * 0.2f;
                        break;
                    case NPCBattleInfluence.EnemyDefense:
                        buffs.enemyDefenseMult -= influence * 0.2f;
                        break;
                    case NPCBattleInfluence.TowerVariety:
                        buffs.towerVarietyBonus += Mathf.RoundToInt(influence * 2f);
                        break;
                    case NPCBattleInfluence.AllyCritRate:
                        buffs.critRate += influence * 0.15f;
                        break;
                }
            }

            return buffs;
        }

        public NPCId GetHighestFavorabilityNPC()
        {
            NPCId best = NPCId.Roommate;
            int bestVal = -1;
            foreach (var kvp in relationships)
            {
                if (kvp.Value.favorability > bestVal)
                {
                    bestVal = kvp.Value.favorability;
                    best = kvp.Key;
                }
            }
            return best;
        }

        public void OnBattleResult(bool victory)
        {
            int delta = victory ? 3 : -2;
            foreach (var kvp in relationships)
            {
                kvp.Value.ModifyFavorability(delta);
            }
        }
    }

    [System.Serializable]
    public struct BattleBuffs
    {
        public float allyAttackMult;
        public float allyDefenseMult;
        public float allyAttackSpeedMult;
        public float costRegenMult;
        public float enemyAttackMult;
        public float enemyDefenseMult;
        public int towerVarietyBonus;
        public float critRate;

        public float GetAllyAttackMultiplier() => 1f + allyAttackMult;
        public float GetAllyDefenseMultiplier() => 1f + allyDefenseMult;
        public float GetAllyAttackSpeedMultiplier() => 1f + allyAttackSpeedMult;
        public float GetCostRegenMultiplier() => 1f + costRegenMult;
        public float GetEnemyAttackMultiplier() => 1f + enemyAttackMult;
        public float GetEnemyDefenseMultiplier() => 1f + enemyDefenseMult;
    }
}
