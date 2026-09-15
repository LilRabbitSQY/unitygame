using UnityEngine;

namespace FinalDefense.Data
{
    public enum NPCId
    {
        Roommate,       // 室友
        Bestie,         // 闺蜜
        Crush,          // Crush
        ChildhoodFriend,// 竹马
        ClubSenior,     // 社团学长
        Junior,         // 同系学弟
        Professor,      // 专业课老师
        Dean            // 院长
    }

    public enum NPCBattleInfluence
    {
        AllyAttack,         // 我方攻击力增益/减益
        AllyDefense,        // 我方防御力增益/减益
        AllyAttackSpeed,    // 我方攻击速度增益/减益
        AllyCostRegen,      // 我方费用恢复增益/减益
        EnemyAttack,        // 敌方攻击力增/减
        EnemyDefense,       // 敌方防御力增/减
        TowerVariety,       // 可部署塔台种类多/少
        AllyCritRate         // 我方暴击率
    }

    [CreateAssetMenu(menuName = "FinalDefense/NPC Data")]
    public class NPCData : ScriptableObject
    {
        [Header("基础信息")]
        public NPCId npcId;
        public string npcName;
        public string gender;
        public string personalityKeywords;
        [TextArea] public string description;

        [Header("战斗影响")]
        public NPCBattleInfluence battleInfluence;
        public string battleStyle;

        [Header("初始数值")]
        public int initialFavorability = 50;
        public int initialMeritScore = 50;
        public int initialPatience = 50;
        public int initialAcademicPower = 50;
    }

    [System.Serializable]
    public class NPCRelationship
    {
        public NPCId npcId;
        public int favorability;
        public int meritScore;
        public int patience;
        public int academicPower;

        public NPCRelationship() { }

        public NPCRelationship(NPCData data)
        {
            npcId = data.npcId;
            favorability = data.initialFavorability;
            meritScore = data.initialMeritScore;
            patience = data.initialPatience;
            academicPower = data.initialAcademicPower;
        }

        public void ModifyFavorability(int delta)
        {
            favorability = Mathf.Clamp(favorability + delta, 0, 100);
        }

        public void ModifyMeritScore(int delta)
        {
            meritScore = Mathf.Clamp(meritScore + delta, 0, 100);
        }

        public void ModifyPatience(int delta)
        {
            patience = Mathf.Clamp(patience + delta, 0, 100);
        }

        public void ModifyAcademicPower(int delta)
        {
            academicPower = Mathf.Clamp(academicPower + delta, 0, 100);
        }
    }
}
