using System.Collections.Generic;
using UnityEngine;
using FinalDefense.Core;
using FinalDefense.Data;
using FinalDefense.NPC;

namespace FinalDefense.Battle
{
    public enum EndingType
    {
        RoommateRomance,    // 和室友在一起
        BestieRomance,      // 和闺蜜在一起
        CrushRomance,       // 和Crush在一起
        ChildhoodRomance,   // 和竹马在一起
        SeniorRomance,      // 和社团学长在一起
        JuniorRomance,      // 和同系学弟在一起
        TopStudent,         // GPA专业第一
        Delayed,            // 延毕
        Balanced            // 平衡人生
    }

    public class EndingSystem : MonoBehaviour
    {
        public static EndingSystem Instance { get; private set; }

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

        public EndingType DetermineEnding()
        {
            var gm = GameManager.Instance;
            var npcMgr = NPCRelationshipManager.Instance;
            if (gm == null) return EndingType.Balanced;

            if (gm.CurrentGPA < 30 || gm.BattlesLost > 5)
                return EndingType.Delayed;

            if (gm.CurrentGPA >= 90 && gm.CurrentEduPower >= gm.StatCap * 0.8f)
                return EndingType.TopStudent;

            if (npcMgr != null)
            {
                var highest = npcMgr.GetHighestFavorabilityNPC();
                var rel = npcMgr.GetRelationship(highest);
                if (rel != null && rel.favorability >= 80)
                {
                    return highest switch
                    {
                        NPCId.Roommate => EndingType.RoommateRomance,
                        NPCId.Bestie => EndingType.BestieRomance,
                        NPCId.Crush => EndingType.CrushRomance,
                        NPCId.ChildhoodFriend => EndingType.ChildhoodRomance,
                        NPCId.ClubSenior => EndingType.SeniorRomance,
                        NPCId.Junior => EndingType.JuniorRomance,
                        _ => EndingType.Balanced
                    };
                }
            }

            return EndingType.Balanced;
        }

        public string GetEndingTitle(EndingType ending)
        {
            return ending switch
            {
                EndingType.RoommateRomance => "我们的宿舍故事",
                EndingType.BestieRomance => "最好的我们",
                EndingType.CrushRomance => "心动的信号",
                EndingType.ChildhoodRomance => "青梅竹马的约定",
                EndingType.SeniorRomance => "学长带我飞",
                EndingType.JuniorRomance => "被学弟拿下了",
                EndingType.TopStudent => "专业第一！顶尖名校Offer已到",
                EndingType.Delayed => "延毕通知书",
                EndingType.Balanced => "平衡的大学生活，安逸毕业",
                _ => "毕业快乐"
            };
        }

        public string GetEndingDescription(EndingType ending)
        {
            return ending switch
            {
                EndingType.RoommateRomance => "四年的朝夕相处，从卷王竞争对手变成了最亲密的人。你们约定好了，研究生也要在一起。",
                EndingType.BestieRomance => "温柔的她一直在你身边，从闺蜜到恋人，水到渠成。",
                EndingType.CrushRomance => "外冷内热的学术天才终于对你敞开了心扉，原来他一直在用自己的方式守护你。",
                EndingType.ChildhoodRomance => "从小到大的陪伴，他的粘人终于得到了回应。",
                EndingType.SeniorRomance => "阳光大狗学长的热情感染了你，社团的故事变成了你们的故事。",
                EndingType.JuniorRomance => "害羞的学弟鼓起勇气表白了，你发现他的内敛下藏着最真挚的感情。",
                EndingType.TopStudent => "四年的努力没有白费！你以专业第一的成绩拿到了顶尖名校的研究生offer。",
                EndingType.Delayed => "挂科太多，只能延毕了……但也许这是重新来过的机会？",
                EndingType.Balanced => "没有极致的成绩，没有轰烈的爱情，但你过了一个充实的大学生活。这也挺好的。",
                _ => ""
            };
        }
    }
}
