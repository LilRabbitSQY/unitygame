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
            // Synchronous canned replies are retired. UI must await GameManager.DialogueSession.
            return null;
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
