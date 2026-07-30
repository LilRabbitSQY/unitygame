using System.Collections.Generic;
using UnityEngine;
using FinalDefense.Core;
using FinalDefense.Data;

namespace FinalDefense.Event
{
    public enum EventPriority
    {
        Normal,     // 普通事件 - 可不做
        Important,  // 重要事件 - 不做有负面影响
        Urgent      // 紧急事件 - 必做，否则debuff
    }

    public enum EventDomain
    {
        Academic,   // 学术事件
        Social,     // 社交事件
        Daily       // 日常事件
    }

    [System.Serializable]
    public class GameEvent
    {
        public string eventId;
        public string title;
        public string description;
        public EventPriority priority;
        public EventDomain domain;
        public NPCId relatedNPC;
        public int turnsRemaining;
        public bool completed;

        public Dictionary<string, int> rewardOnComplete;
        public Dictionary<string, int> penaltyOnFail;
    }

    public class EventSystem : MonoBehaviour
    {
        public static EventSystem Instance { get; private set; }

        private List<GameEvent> activeEvents = new();
        private List<GameEvent> completedEvents = new();

        public IReadOnlyList<GameEvent> ActiveEvents => activeEvents;

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

        public void GenerateEventsForDay(int day)
        {
            int count = Random.Range(1, 3);
            for (int i = 0; i < count; i++)
            {
                var evt = CreateRandomEvent(day);
                activeEvents.Add(evt);
            }
        }

        private GameEvent CreateRandomEvent(int day)
        {
            var priorities = new[] { EventPriority.Normal, EventPriority.Normal, EventPriority.Important, EventPriority.Urgent };
            var domains = new[] { EventDomain.Academic, EventDomain.Social, EventDomain.Daily };
            var npcs = System.Enum.GetValues(typeof(NPCId));

            var priority = priorities[Random.Range(0, priorities.Length)];
            var domain = domains[Random.Range(0, domains.Length)];
            var npc = (NPCId)npcs.GetValue(Random.Range(0, npcs.Length));

            string title = GenerateEventTitle(domain, npc);

            var evt = new GameEvent
            {
                eventId = $"evt_{day}_{Random.Range(1000, 9999)}",
                title = title,
                description = GenerateEventDescription(domain, npc),
                priority = priority,
                domain = domain,
                relatedNPC = npc,
                turnsRemaining = priority == EventPriority.Urgent ? 1 : 3,
                completed = false,
                rewardOnComplete = new Dictionary<string, int> { { "favorability", 5 }, { "emotion", 2 } },
                penaltyOnFail = new Dictionary<string, int> { { "favorability", -5 }, { "emotion", -3 } }
            };

            return evt;
        }

        private string GenerateEventTitle(EventDomain domain, NPCId npc)
        {
            return domain switch
            {
                EventDomain.Academic => npc switch
                {
                    NPCId.Professor => "专业课老师布置了课题",
                    NPCId.Roommate => "室友邀请一起写论文",
                    _ => "一起去图书馆自习"
                },
                EventDomain.Social => npc switch
                {
                    NPCId.Bestie => "闺蜜约你逛街",
                    NPCId.ClubSenior => "社团活动需要帮忙",
                    NPCId.ChildhoodFriend => "竹马发来消息想聊天",
                    _ => "朋友聚餐邀请"
                },
                EventDomain.Daily => npc switch
                {
                    NPCId.Junior => "学弟请帮忙取快递",
                    NPCId.Crush => "偶遇Crush在食堂",
                    _ => "帮同学占座"
                },
                _ => "日常事件"
            };
        }

        private string GenerateEventDescription(EventDomain domain, NPCId npc)
        {
            return domain switch
            {
                EventDomain.Academic => "需要投入时间和精力完成学术任务，可能提升相关NPC的好感度和学术力。",
                EventDomain.Social => "社交活动邀请，参加可以提升好感度，不参加可能降低耐心值。",
                EventDomain.Daily => "日常小事，处理得当可以获得少量好感度提升。",
                _ => ""
            };
        }

        public void CompleteEvent(string eventId)
        {
            var evt = activeEvents.Find(e => e.eventId == eventId);
            if (evt == null) return;

            evt.completed = true;
            activeEvents.Remove(evt);
            completedEvents.Add(evt);

            ApplyRewards(evt);
        }

        public void TickEvents()
        {
            for (int i = activeEvents.Count - 1; i >= 0; i--)
            {
                var evt = activeEvents[i];
                evt.turnsRemaining--;
                if (evt.turnsRemaining <= 0 && !evt.completed)
                {
                    ApplyPenalty(evt);
                    activeEvents.RemoveAt(i);
                }
            }
        }

        private void ApplyRewards(GameEvent evt)
        {
            var npcMgr = NPC.NPCRelationshipManager.Instance;
            if (npcMgr != null)
            {
                var rel = npcMgr.GetRelationship(evt.relatedNPC);
                if (rel != null && evt.rewardOnComplete != null)
                {
                    if (evt.rewardOnComplete.TryGetValue("favorability", out int fav))
                        rel.ModifyFavorability(fav);
                }
            }

            var gm = GameManager.Instance;
            if (gm != null && evt.rewardOnComplete != null)
            {
                if (evt.rewardOnComplete.TryGetValue("emotion", out int emo))
                    gm.ModifyStat("emotion", emo);
            }
        }

        private void ApplyPenalty(GameEvent evt)
        {
            var npcMgr = NPC.NPCRelationshipManager.Instance;
            if (npcMgr != null)
            {
                var rel = npcMgr.GetRelationship(evt.relatedNPC);
                if (rel != null && evt.penaltyOnFail != null)
                {
                    if (evt.penaltyOnFail.TryGetValue("favorability", out int fav))
                        rel.ModifyFavorability(fav);
                }
            }

            var gm = GameManager.Instance;
            if (gm != null && evt.penaltyOnFail != null)
            {
                if (evt.penaltyOnFail.TryGetValue("emotion", out int emo))
                    gm.ModifyStat("emotion", emo);
            }

            if (evt.priority == EventPriority.Urgent)
            {
                var gmRef = GameManager.Instance;
                if (gmRef != null)
                    gmRef.TakeGPADamage(3);
            }
        }
    }
}
