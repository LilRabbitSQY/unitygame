using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FinalDefense.Core;
using FinalDefense.Data;
using FinalDefense.Dialogue;
using FinalDefense.NPC;

namespace FinalDefense.UI
{
    public class DialogueUI : MonoBehaviour
    {
        [Header("NPC信息")]
        [SerializeField] private TextMeshProUGUI npcNameText;
        [SerializeField] private TextMeshProUGUI npcKeywordsText;
        [SerializeField] private Image npcAvatarImage;

        [Header("数值显示")]
        [SerializeField] private TextMeshProUGUI favorabilityText;
        [SerializeField] private TextMeshProUGUI meritScoreText;
        [SerializeField] private TextMeshProUGUI patienceText;
        [SerializeField] private TextMeshProUGUI academicPowerText;

        [Header("对话区")]
        [SerializeField] private Transform messageContainer;
        [SerializeField] private GameObject playerMessagePrefab;
        [SerializeField] private GameObject npcMessagePrefab;
        [SerializeField] private ScrollRect scrollRect;

        [Header("输入区")]
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private Button sendButton;
        [SerializeField] private Button endDialogueButton;

        [Header("决策点")]
        [SerializeField] private Slider decisionPointSlider;
        [SerializeField] private TextMeshProUGUI decisionPointText;

        [Header("NPC选择")]
        [SerializeField] private GameObject npcSelectionPanel;
        [SerializeField] private Button[] npcButtons;

        private void Start()
        {
            if (sendButton != null)
                sendButton.onClick.AddListener(OnSendClicked);
            if (endDialogueButton != null)
                endDialogueButton.onClick.AddListener(OnEndDialogueClicked);
            if (decisionPointSlider != null)
                decisionPointSlider.onValueChanged.AddListener(OnDecisionPointChanged);

            SetupNPCButtons();
            ShowNPCSelection();
        }

        private void SetupNPCButtons()
        {
            if (npcButtons == null) return;
            var npcs = System.Enum.GetValues(typeof(NPCId));
            for (int i = 0; i < npcButtons.Length && i < npcs.Length; i++)
            {
                int index = i;
                NPCId npcId = (NPCId)npcs.GetValue(i);
                npcButtons[i].onClick.AddListener(() => OnNPCSelected(npcId));

                var text = npcButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                if (text != null) text.text = GetNPCDisplayName(npcId);
            }
        }

        private void ShowNPCSelection()
        {
            if (npcSelectionPanel != null)
                npcSelectionPanel.SetActive(true);
        }

        private void OnNPCSelected(NPCId npcId)
        {
            if (npcSelectionPanel != null)
                npcSelectionPanel.SetActive(false);

            int points = decisionPointSlider != null ? (int)decisionPointSlider.value : 50;
            DialogueManager.Instance?.StartDialogue(npcId, points);
            EventBus.DialogueStarted(npcId);

            UpdateNPCInfo(npcId);
            UpdateStats(npcId);
        }

        private void OnSendClicked()
        {
            if (inputField == null || string.IsNullOrEmpty(inputField.text)) return;

            string message = inputField.text;
            inputField.text = "";

            var result = DialogueManager.Instance?.SendMessage(message);
            if (result == null) return;

            AddMessageBubble(message, true);
            AddMessageBubble(result.npcResponse, false);
            UpdateStats(DialogueManager.Instance.CurrentNPC);

            if (patienceText != null)
                patienceText.text = $"耐心: {DialogueManager.Instance.RemainingPatience}";
        }

        private void OnEndDialogueClicked()
        {
            DialogueManager.Instance?.EndDialogue();
            EventBus.DialogueEnded();
            SceneLoader.LoadBattle();
        }

        private void OnDecisionPointChanged(float value)
        {
            if (decisionPointText != null)
                decisionPointText.text = $"决策点: {(int)value}/100";
            DialogueManager.Instance?.SetDecisionPoints((int)value);
        }

        private void AddMessageBubble(string text, bool isPlayer)
        {
            if (messageContainer == null) return;

            var prefab = isPlayer ? playerMessagePrefab : npcMessagePrefab;
            if (prefab == null)
            {
                var go = new GameObject(isPlayer ? "PlayerMsg" : "NPCMsg");
                go.transform.SetParent(messageContainer);
                var tmp = go.AddComponent<TextMeshProUGUI>();
                tmp.text = (isPlayer ? "[你] " : "[NPC] ") + text;
                tmp.fontSize = 14;
                tmp.color = isPlayer ? Color.white : new Color(0.8f, 1f, 0.8f);
                return;
            }

            var msgGo = Instantiate(prefab, messageContainer);
            var msgText = msgGo.GetComponentInChildren<TextMeshProUGUI>();
            if (msgText != null) msgText.text = text;

            if (scrollRect != null)
                Canvas.ForceUpdateCanvases();
        }

        private void ClearMessages()
        {
            if (messageContainer == null) return;
            foreach (Transform child in messageContainer)
                Destroy(child.gameObject);
        }

        private void UpdateNPCInfo(NPCId npcId)
        {
            if (npcNameText != null) npcNameText.text = GetNPCDisplayName(npcId);
            if (npcKeywordsText != null)
            {
                var npcMgr = NPCRelationshipManager.Instance;
                var data = npcMgr?.GetNPCData(npcId);
                npcKeywordsText.text = data?.personalityKeywords ?? "";
            }
        }

        private void UpdateStats(NPCId npcId)
        {
            var npcMgr = NPCRelationshipManager.Instance;
            if (npcMgr == null) return;

            var rel = npcMgr.GetRelationship(npcId);
            if (rel == null) return;

            if (favorabilityText != null) favorabilityText.text = $"好感度: {rel.favorability}";
            if (meritScoreText != null) meritScoreText.text = $"优绩度: {rel.meritScore}";
            if (patienceText != null) patienceText.text = $"耐心值: {rel.patience}";
            if (academicPowerText != null) academicPowerText.text = $"学术力: {rel.academicPower}";
        }

        private string GetNPCDisplayName(NPCId id)
        {
            return id switch
            {
                NPCId.Roommate => "室友",
                NPCId.Bestie => "闺蜜",
                NPCId.Crush => "Crush",
                NPCId.ChildhoodFriend => "竹马",
                NPCId.ClubSenior => "社团学长",
                NPCId.Junior => "同系学弟",
                NPCId.Professor => "专业课老师",
                NPCId.Dean => "院长",
                _ => "NPC"
            };
        }
    }
}
