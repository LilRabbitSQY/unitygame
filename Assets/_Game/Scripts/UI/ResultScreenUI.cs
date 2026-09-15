using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FinalDefense.Core;
using FinalDefense.Battle;

namespace FinalDefense.UI
{
    public class ResultScreenUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI resultText;
        [SerializeField] private TextMeshProUGUI gpaText;
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private Button returnButton;
        [SerializeField] private Button shopButton;
        [SerializeField] private Button retryButton;

        private static bool lastBattleWon;
        private bool leaving;

        public static void SetResult(bool won)
        {
            lastBattleWon = won;
        }

        private void Awake()
        {
            ApplyThemeStyling();
        }

        private void OnEnable()
        {
            EventBus.OnBattleWon += OnWon;
            EventBus.OnBattleLost += OnLost;
        }

        private void OnDisable()
        {
            EventBus.OnBattleWon -= OnWon;
            EventBus.OnBattleLost -= OnLost;
        }

        private void OnWon() => lastBattleWon = true;
        private void OnLost() => lastBattleWon = false;

        private void Start()
        {
            Time.timeScale = 1f;
            var gm = GameManager.Instance;
            var report = gm?.LastBattleReport;
            if (report != null) lastBattleWon = report.won;
            bool ending = gm != null && gm.IsGameComplete;
            if (resultText != null)
                resultText.text = ending ? "游戏结束" : lastBattleWon ? "考试通过！" : "挂科了...";
            if (gm != null)
            {
                if (gpaText != null) gpaText.text = $"当前 GPA: {gm.CurrentGPA:0.##}";
                if (goldText != null) goldText.text = ending ? $"金币: {gm.Gold}" : $"获得金币: {((report != null && report.won) ? report.goldEarned : 0)}";
            }
            if (returnButton != null) returnButton.onClick.AddListener(OnReturnClicked);
            if (shopButton != null)
            {
                shopButton.gameObject.SetActive(!ending);
                shopButton.onClick.AddListener(OnShopClicked);
                var label = shopButton.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = lastBattleWon ? "去商店" : "结束今天 · 去商店";
            }
            if (retryButton != null)
            {
                retryButton.gameObject.SetActive(!ending && !lastBattleWon);
                retryButton.onClick.AddListener(OnRetryClicked);
                var label = retryButton.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = gm == null || gm.RetryCount == 0 ? "免费重试" : "重试 (-5 GPA)";
                retryButton.interactable = gm != null && (gm.RetryCount == 0 || gm.CurrentGPA > 5);
            }
            ApplyThemeStyling();
            if (!ending && !lastBattleWon && shopButton != null)
                UIStyler.SetCenteredRect(shopButton.GetComponent<RectTransform>(), new Vector2(0, -220), new Vector2(220, 54));

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var summary = UIBootstrap.CreateTextObject("BattleSummary", canvas.transform, "", UITheme.Instance.bodyFontSize, UITheme.Instance.mutedForeground, FontStyles.Normal);
            UIStyler.SetCenteredRect(summary.rectTransform, new Vector2(0, -330), new Vector2(620, 140));
            summary.alignment = TextAlignmentOptions.Center;
            summary.raycastTarget = false;
            if (ending)
            {
                summary.text = $"第 {gm.CurrentDay}/{GameManager.TotalDays} 天  ·  胜利 {gm.BattlesWon} 次  ·  失败 {gm.BattlesLost} 次\n" + (gm.CurrentGPA > 0 ? "已完成全部日程。" : "GPA 已耗尽。");
                var restart = UIBootstrap.CreateButton("EndingRestart", canvas.transform, "新游戏");
                UIStyler.SetCenteredRect(restart.GetComponent<RectTransform>(), new Vector2(140, -150), new Vector2(220, 54));
                restart.onClick.AddListener(() => { gm.BeginNewGame(); BattleSceneBootstrap.NextBattle = null; SceneLoader.LoadPersonalityTest(); });
            }
            else if (report != null)
            {
                summary.text = $"第 {gm.CurrentDay}/{GameManager.TotalDays} 天  ·  持有金币 {gm.Gold}\n用时 {report.duration:0.0}s  ·  击败 {report.killed}/{report.spawned}  ·  漏怪 {report.leaked}\n部署 {report.deployed}  ·  升级 {report.upgrades}  ·  花费 {report.costSpent} COST\nGPA -{report.protectionLost}{(report.won ? $"，胜利 +{5 + report.bonusGpa}" : "")}";
                if (!string.IsNullOrEmpty(gm.LastBattleDrop)) summary.text += "\n战利品：" + FinalDefense.Shop.BattleItemDefs.Name(gm.LastBattleDrop) + " ×1";
            }
        }

        private void ApplyThemeStyling()
        {
            var theme = UITheme.Instance;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
                UIStyler.EnsurePageBackground(canvas, "ResultBackground");

            if (resultText != null)
            {
                UIStyler.SetCenteredRect(resultText.rectTransform, new Vector2(0f, 210f), new Vector2(520f, 76f));
                UIStyler.ApplyHeaderTextStyle(resultText, true);
                resultText.color = lastBattleWon ? theme.stateSuccess : theme.stateError;
                resultText.alignment = TextAlignmentOptions.Center;
                UIStyler.EnsureBackdrop(resultText.rectTransform, "ResultHeroCard", new Vector2(160, 120), Color.white);
            }

            if (gpaText != null)
            {
                UIStyler.SetCenteredRect(gpaText.rectTransform, new Vector2(0f, 70f), new Vector2(360f, 48f));
                UIStyler.ApplyStatValueTextStyle(gpaText);
                gpaText.color = theme.chart2;
                gpaText.alignment = TextAlignmentOptions.Center;
                UIStyler.EnsureBackdrop(gpaText.rectTransform, "ResultGPACard", new Vector2(80, 12), theme.surfaceSoft, true);
            }

            if (goldText != null)
            {
                UIStyler.SetCenteredRect(goldText.rectTransform, new Vector2(0f, 0f), new Vector2(360f, 48f));
                UIStyler.ApplyStatValueTextStyle(goldText);
                goldText.color = theme.chart3;
                goldText.alignment = TextAlignmentOptions.Center;
                UIStyler.EnsureBackdrop(goldText.rectTransform, "ResultGoldCard", new Vector2(80, 12), theme.surfaceSoft, true);
            }

            if (returnButton != null)
            {
                UIStyler.SetCenteredRect(returnButton.GetComponent<RectTransform>(), new Vector2(-140f, -150f), new Vector2(220f, 54f));
                UIStyler.ApplySecondaryButtonStyle(returnButton);
            }

            if (shopButton != null)
            {
                UIStyler.SetCenteredRect(shopButton.GetComponent<RectTransform>(), new Vector2(140f, -150f), new Vector2(220f, 54f));
                UIStyler.ApplyGradientButtonStyle(shopButton);
            }

            if (retryButton != null)
            {
                UIStyler.SetCenteredRect(retryButton.GetComponent<RectTransform>(), new Vector2(140f, -150f), new Vector2(220f, 54f));
                UIStyler.ApplyGradientButtonStyle(retryButton);
            }
        }

        private void OnReturnClicked()
        {
            SceneLoader.LoadMainMenu();
        }

        private void OnShopClicked()
        {
            if (leaving) return;
            var gm = GameManager.Instance;
            if (gm != null && gm.EnterShop()) { leaving = true; SceneLoader.LoadShop(); }
        }

        private void OnRetryClicked()
        {
            if (leaving) return;
            var gm = GameManager.Instance;
            if (gm == null || !gm.TryRetryBattle()) return;
            leaving = true;
            BattleSceneBootstrap.NextBattle = gm.CurrentBattleConfiguration;
            SceneLoader.LoadBattle();
        }
    }
}
