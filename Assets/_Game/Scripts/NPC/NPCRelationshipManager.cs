using System.Collections.Generic;
using UnityEngine;
using FinalDefense.Core;
using FinalDefense.Data;

namespace FinalDefense.NPC
{
    public class NPCRelationshipManager : MonoBehaviour
    {
        [SerializeField] private NPCData[] npcDataList;


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
            var campaign = GameManager.Instance?.Campaign;
            if (campaign == null) return NPCId.Roommate;
            var all = campaign.Snapshot.npcs;
            System.Array.Sort(all, (a, b) => a.favor != b.favor ? b.favor.CompareTo(a.favor) : a.reachedSequence.CompareTo(b.reachedSequence));
            foreach (var npc in campaign.Content.npcs)
                if (npc.id == all[0].npcId) return (NPCId)npc.legacyId;
            return NPCId.Roommate;
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
