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
            var snapshot = GameManager.Instance?.Campaign?.Snapshot;
            if (snapshot == null) return null;
            var def = GameManager.Instance.Campaign.Content.npcs;
            foreach (var npc in def)
                if (npc.legacyId == (int)id && npc.romance)
                    foreach (var n in snapshot.npcs)
                        if (n.npcId == npc.id) return new NPCRelationship { npcId = id, favorability = n.favor, meritScore = n.gpa / 100 };
            return null;
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
            return new BattleBuffs();
        }

        public NPCId GetHighestFavorabilityNPC()
        {
            NPCId best = NPCId.Roommate;
            int bestVal = -1;
            foreach (NPCId id in System.Enum.GetValues(typeof(NPCId)))
            {
                var rel = GetRelationship(id);
                if (rel != null && rel.favorability > bestVal)
                {
                    bestVal = rel.favorability;
                    best = id;
                }
            }
            return best;
        }

        public void OnBattleResult(bool victory)
        {
            // Campaign settlement owns all relationship effects.
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
