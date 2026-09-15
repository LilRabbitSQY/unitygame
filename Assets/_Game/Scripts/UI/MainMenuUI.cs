using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FinalDefense.Core;
using FinalDefense.Battle;

namespace FinalDefense.UI
{
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private Button startButton;
        [SerializeField] private TextMeshProUGUI gpaText;
        [SerializeField] private TextMeshProUGUI gradeText;
        [SerializeField] private TextMeshProUGUI personalityText;
        [SerializeField] private TextMeshProUGUI titleText;

        private void Awake()
        {
            ApplyThemeStyling();
        }

        private void Start()
        {
            if (startButton != null)
                startButton.onClick.AddListener(OnStartClicked);
            var gm = GameManager.Instance;
            if (gm != null && !gm.PersonalitySelected && gm.HasSavedGame) gm.LoadProgress();
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var newGame = UIBootstrap.CreateButton("NewGame", canvas.transform, "新游戏", false);
                UIStyler.SetCenteredRect(newGame.GetComponent<RectTransform>(), new Vector2(0, -215), new Vector2(260, 58));
                newGame.onClick.AddListener(OnNewGame);
                newGame.gameObject.SetActive(gm != null && (gm.PersonalitySelected || gm.IsGameComplete));
            }
            UpdateDisplay();
        }

        private void ApplyThemeStyling()
        {
            var theme = UITheme.Instance;
            var canvas = GetComponentInParent<Canvas>();

            if (canvas != null)
            {
                UIStyler.EnsurePageBackground(canvas, "MainMenuBackground");
                if (titleText == null)
                    titleText = UIStyler.FindDeepChild(canvas.transform, "TitleText")?.GetComponent<TextMeshProUGUI>();
            }

            if (titleText != null)
            {
                UIStyler.SetTopRect(titleText.rectTransform, 80f, new Vector2(640f, 82f));
                UIStyler.ApplyHeaderTextStyle(titleText, true);
                titleText.color = theme.primary;
                titleText.alignment = TextAlignmentOptions.Center;
            }
            if (gpaText != null)
            {
                UIStyler.SetCenteredRect(gpaText.rectTransform, new Vector2(0f, 155f), new Vector2(240f, 58f));
                UIStyler.ApplyStatValueTextStyle(gpaText);
                gpaText.color = theme.chart2;
                gpaText.alignment = TextAlignmentOptions.Center;
                UIStyler.EnsureBackdrop(gpaText.rectTransform, "GPAStatCard", new Vector2(80, 46), Color.white);
            }
            if (gradeText != null)
            {
                UIStyler.SetCenteredRect(gradeText.rectTransform, new Vector2(0f, 70f), new Vector2(420f, 54f));
                UIStyler.ApplyHeaderTextStyle(gradeText, false);
                gradeText.color = theme.foreground;
                gradeText.alignment = TextAlignmentOptions.Center;
            }
            if (personalityText != null)
            {
                UIStyler.SetCenteredRect(personalityText.rectTransform, new Vector2(0f, -10f), new Vector2(420f, 44f));
                UIStyler.ApplyCaptionTextStyle(personalityText);
                personalityText.color = theme.mutedForeground;
                personalityText.alignment = TextAlignmentOptions.Center;
                UIStyler.EnsureBackdrop(personalityText.rectTransform, "PersonalityInfoCard", new Vector2(96, 42), theme.surfaceSoft, true);
            }
            if (startButton != null)
            {
                UIStyler.SetCenteredRect(startButton.GetComponent<RectTransform>(), new Vector2(0f, -135f), new Vector2(260f, 58f));
                UIStyler.ApplyGradientButtonStyle(startButton);
            }
        }

        private void UpdateDisplay()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            if (gpaText != null) gpaText.text = $"{gm.CurrentGPA:0.##}";
            if (gradeText != null) gradeText.text = gm.IsGameComplete ? "游戏结束" : $"{gm.CurrentGrade}年级  第{gm.CurrentDay}/{GameManager.TotalDays}天";
            if (startButton != null)
            {
                var label = startButton.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = gm.IsGameComplete ? "查看总结" : gm.PersonalitySelected ? "继续游戏" : "开始游戏";
            }
            if (personalityText != null && !gm.PersonalitySelected) personalityText.text = "尚未选择人格";
            if (personalityText != null && gm.PersonalitySelected)
            {
                var config = gm.PersonalityConfigData;
                if (config != null)
                {
                    var stats = config.GetStats(gm.CurrentPersonality);
                    personalityText.text = stats.displayName;
                }
            }
        }

        private void OnNewGame()
        {
            GameManager.Instance?.BeginNewGame();
            BattleSceneBootstrap.NextBattle = null;
            SceneLoader.LoadPersonalityTest();
        }

        private void OnStartClicked()
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.PersonalitySelected) { SceneLoader.LoadPersonalityTest(); return; }
            if (gm.IsGameComplete || gm.Phase == CampaignPhase.Result) { SceneLoader.LoadResult(); return; }
            if (gm.Phase == CampaignPhase.Shop) { SceneLoader.LoadShop(); return; }
            if (gm.Phase == CampaignPhase.Battle)
            {
                BattleSceneBootstrap.NextBattle = gm.CurrentBattleConfiguration;
                SceneLoader.LoadBattle(); return;
            }
            SceneLoader.LoadSchedule();
        }
    }
}
