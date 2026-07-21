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

            if (resultText != null)
                resultText.text = lastBattleWon ? "考试通过！" : "挂科了...";

            var gm = GameManager.Instance;
            if (gm != null)
            {
                if (gpaText != null) gpaText.text = $"当前 GPA: {gm.CurrentGPA}";
                if (goldText != null) goldText.text = $"获得金币: {gm.Gold}";
            }

            if (returnButton != null)
                returnButton.onClick.AddListener(OnReturnClicked);

            if (shopButton != null)
            {
                shopButton.gameObject.SetActive(lastBattleWon);
                shopButton.onClick.AddListener(OnShopClicked);
            }

            if (retryButton != null)
            {
                retryButton.gameObject.SetActive(!lastBattleWon);
                retryButton.onClick.AddListener(OnRetryClicked);
                var retryText = retryButton.GetComponentInChildren<TextMeshProUGUI>();
                if (retryText != null && gm != null)
                {
                    retryText.text = gm.RetryCount == 0 ? "免费重试" : "重试 (-5 GPA)";
                }
            }

            ApplyThemeStyling();
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
                UIStyler.EnsureBackdrop(gpaText.rectTransform, "ResultGPACard", new Vector2(80, 42), theme.surfaceSoft, true);
            }

            if (goldText != null)
            {
                UIStyler.SetCenteredRect(goldText.rectTransform, new Vector2(0f, 0f), new Vector2(360f, 48f));
                UIStyler.ApplyStatValueTextStyle(goldText);
                goldText.color = theme.chart3;
                goldText.alignment = TextAlignmentOptions.Center;
                UIStyler.EnsureBackdrop(goldText.rectTransform, "ResultGoldCard", new Vector2(80, 42), theme.surfaceSoft, true);
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
            SceneLoader.LoadShop();
        }

        private void OnRetryClicked()
        {
            var retry = FindFirstObjectByType<RetrySystem>();
            if (retry != null)
            {
                retry.Retry();
            }
            else
            {
                var gm = GameManager.Instance;
                if (gm != null)
                {
                    if (gm.RetryCount > 0) gm.TakeGPADamage(5);
                    gm.IncrementRetry();
                }
                SceneLoader.LoadBattle();
            }
        }
    }
}
