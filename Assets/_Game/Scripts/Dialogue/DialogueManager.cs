using System.Collections.Generic;
using UnityEngine;
using FinalDefense.Core;
using FinalDefense.Data;
using FinalDefense.NPC;

namespace FinalDefense.Dialogue
{
    [System.Serializable]
    public class DialogueMessage
    {
        public bool isPlayer;
        public string content;
        public float timestamp;
    }

    [System.Serializable]
    public class DialogueResult
    {
        public int favorabilityDelta;
        public int meritScoreDelta;
        public int patienceDelta;
        public int academicPowerDelta;
        public string npcResponse;
    }

    public class DialogueManager : MonoBehaviour
    {
        public static DialogueManager Instance { get; private set; }

        private NPCId currentNPC;
        private int decisionPoints;
        private List<DialogueMessage> currentConversation = new();
        private int remainingPatience;

        public NPCId CurrentNPC => currentNPC;
        public int DecisionPoints => decisionPoints;
        public IReadOnlyList<DialogueMessage> Conversation => currentConversation;
        public bool IsInDialogue { get; private set; }
        public int RemainingPatience => remainingPatience;

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

        public void StartDialogue(NPCId npc, int points)
        {
            currentNPC = npc;
            decisionPoints = points;
            currentConversation.Clear();
            IsInDialogue = true;

            var npcMgr = NPCRelationshipManager.Instance;
            if (npcMgr != null)
            {
                var rel = npcMgr.GetRelationship(npc);
                remainingPatience = rel != null ? rel.patience / 10 + 3 : 5;
            }
            else
            {
                remainingPatience = 5;
            }
        }

        public DialogueResult SendMessage(string playerMessage)
        {
            if (!IsInDialogue || remainingPatience <= 0) return null;

            currentConversation.Add(new DialogueMessage
            {
                isPlayer = true,
                content = playerMessage,
                timestamp = Time.time
            });

            remainingPatience--;

            var result = ProcessDialogue(playerMessage);

            currentConversation.Add(new DialogueMessage
            {
                isPlayer = false,
                content = result.npcResponse,
                timestamp = Time.time
            });

            ApplyDialogueResult(result);

            if (remainingPatience <= 0)
                EndDialogue();

            return result;
        }

        private DialogueResult ProcessDialogue(string message)
        {
            var result = new DialogueResult();
            var npcMgr = NPCRelationshipManager.Instance;
            var npcData = npcMgr?.GetNPCData(currentNPC);

            float pointsBonus = decisionPoints / 100f;
            int baseFavorChange = Mathf.RoundToInt(Random.Range(1, 5) * (1f + pointsBonus));

            bool isPositive = AnalyzeSentiment(message);

            if (isPositive)
            {
                result.favorabilityDelta = baseFavorChange;
                result.patienceDelta = 1;
            }
            else
            {
                result.favorabilityDelta = -baseFavorChange / 2;
                result.patienceDelta = -2;
            }

            if (ContainsAcademicContent(message))
            {
                result.academicPowerDelta = Random.Range(1, 3);
                result.meritScoreDelta = Random.Range(0, 2);
            }

            result.npcResponse = GenerateNPCResponse(npcData, isPositive);
            return result;
        }

        private bool AnalyzeSentiment(string message)
        {
            string[] positiveWords = { "好", "棒", "厉害", "谢谢", "喜欢", "赞", "加油", "支持", "哈哈", "帮" };
            string[] negativeWords = { "烦", "讨厌", "不", "差", "垃圾", "无聊", "懒", "算了" };

            int score = 0;
            foreach (var w in positiveWords)
                if (message.Contains(w)) score++;
            foreach (var w in negativeWords)
                if (message.Contains(w)) score--;

            return score >= 0;
        }

        private bool ContainsAcademicContent(string message)
        {
            string[] academicWords = { "学习", "论文", "作业", "考试", "实验", "课", "研究", "期末", "GPA", "绩点" };
            foreach (var w in academicWords)
                if (message.Contains(w)) return true;
            return false;
        }

        private string GenerateNPCResponse(NPCData npcData, bool positive)
        {
            if (npcData == null) return "嗯嗯，我知道了。";

            string[] responses = npcData.npcId switch
            {
                NPCId.Roommate => positive
                    ? new[] { "一起冲GPA！", "今晚图书馆见？", "我的笔记可以借你看看。" }
                    : new[] { "我还要学习，下次再说。", "别打扰我了好吗。", "你自己看着办吧。" },
                NPCId.Bestie => positive
                    ? new[] { "最好的你！", "我一直都在呀~", "周末一起去吃好吃的！" }
                    : new[] { "怎么了嘛，别不开心啦。", "你是不是有什么心事？", "我不太舒服……" },
                NPCId.Crush => positive
                    ? new[] { "嗯。", "……谢谢。", "有空可以一起去图书馆。" }
                    : new[] { "……", "我先走了。", "别这样。" },
                NPCId.ChildhoodFriend => positive
                    ? new[] { "嘿嘿你终于回我了！", "想你了~什么时候见面？", "一直在等你消息！" }
                    : new[] { "你不理我我会哭的……", "哼，算了。", "又忙着不理我？" },
                NPCId.ClubSenior => positive
                    ? new[] { "太好了！下次社团活动一起来！", "你真靠谱！", "有你在就放心了！" }
                    : new[] { "没事，忙就下次！", "理解理解~", "那我找别人帮忙吧。" },
                NPCId.Junior => positive
                    ? new[] { "谢谢学姐……", "学姐人真好。", "我会努力的！" }
                    : new[] { "对不起打扰了……", "那我自己想办法。", "学姐一定很忙吧……" },
                NPCId.Professor => positive
                    ? new[] { "不错，继续保持。", "你的课题方向选得很好。", "下次课上见。" }
                    : new[] { "注意你的学术态度。", "ddl快到了。", "希望下次能看到进步。" },
                NPCId.Dean => positive
                    ? new[] { "很好，这个学期的表现不错。", "继续努力。", "有什么需要帮助的可以来找我。" }
                    : new[] { "要注意平衡学习和生活。", "下次注意。", "这样下去会影响毕业的。" },
                _ => new[] { "好的。", "知道了。", "嗯。" }
            };

            return responses[Random.Range(0, responses.Length)];
        }

        private void ApplyDialogueResult(DialogueResult result)
        {
            var npcMgr = NPCRelationshipManager.Instance;
            if (npcMgr == null) return;

            var rel = npcMgr.GetRelationship(currentNPC);
            if (rel == null) return;

            rel.ModifyFavorability(result.favorabilityDelta);
            rel.ModifyMeritScore(result.meritScoreDelta);
            rel.ModifyPatience(result.patienceDelta);
            rel.ModifyAcademicPower(result.academicPowerDelta);
        }

        public void EndDialogue()
        {
            IsInDialogue = false;
        }

        public void SetDecisionPoints(int points)
        {
            decisionPoints = Mathf.Clamp(points, 0, 100);
        }
    }
}
