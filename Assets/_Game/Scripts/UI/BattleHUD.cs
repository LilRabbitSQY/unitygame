using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FinalDefense.Core;
using FinalDefense.Enemy;
using FinalDefense.Battle;

namespace FinalDefense.UI
{
    public class BattleHUD : MonoBehaviour
    {
        [Header("Existing UI Styling")]
        [SerializeField] private bool styleExistingUI = true;

        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private TextMeshProUGUI gpaText;
        [SerializeField] private TextMeshProUGUI waveText;
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private Button speedButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Button shopButton;

        private bool doubleSpeed;
        private EnemySpawner spawner;

        private void Awake()
        {
            RemoveGeneratedOverlay();

            if (styleExistingUI)
                ApplyThemeStyling();
        }

        private void Start()
        {
            spawner = FindFirstObjectByType<EnemySpawner>();
            EventBus.OnCostChanged += UpdateCost;
            EventBus.OnEnemyReachedEnd += OnEnemyReached;
            EventBus.OnWaveCompleted += UpdateWave;
            EventBus.OnGoldChanged += UpdateGold;

            if (speedButton != null)
                speedButton.onClick.AddListener(ToggleSpeed);

            if (backButton != null)
                backButton.onClick.AddListener(OnBackClicked);

            if (shopButton != null)
                shopButton.onClick.AddListener(OnShopClicked);

            UpdateGPA();
            UpdateWave();
            UpdateGoldDisplay();
        }

        private void OnDestroy()
        {
            EventBus.OnCostChanged -= UpdateCost;
            EventBus.OnEnemyReachedEnd -= OnEnemyReached;
            EventBus.OnWaveCompleted -= UpdateWave;
            EventBus.OnGoldChanged -= UpdateGold;
        }

        private void ApplyThemeStyling()
        {
            var theme = UITheme.Instance;

            if (costText != null)
            {
                SetHudTextRect(costText, new Vector2(-360f, -54f), new Vector2(190f, 52f));
                ApplyInGameStatText(costText, theme.foreground);
            }
            if (gpaText != null)
            {
                SetHudTextRect(gpaText, new Vector2(-120f, -54f), new Vector2(190f, 52f));
                ApplyInGameStatText(gpaText, theme.chart2);
            }
            if (waveText != null)
            {
                SetHudTextRect(waveText, new Vector2(120f, -54f), new Vector2(190f, 52f));
                ApplyInGameStatText(waveText, theme.primary);
            }
            if (goldText != null)
            {
                SetHudTextRect(goldText, new Vector2(360f, -54f), new Vector2(190f, 52f));
                ApplyInGameStatText(goldText, theme.chart3);
            }
            if (speedButton != null)
            {
                SetHudButtonRect(speedButton, new Vector2(-90f, 44f), new Vector2(86f, 42f));
                UIStyler.ApplySecondaryButtonStyle(speedButton);
            }
            if (backButton != null)
            {
                SetHudButtonRect(backButton, new Vector2(-186f, 44f), new Vector2(86f, 42f));
                UIStyler.ApplySecondaryButtonStyle(backButton);
            }
            if (shopButton != null)
            {
                SetHudButtonRect(shopButton, new Vector2(186f, 44f), new Vector2(112f, 42f));
                UIStyler.ApplyGradientButtonStyle(shopButton);
            }
        }

        private void SetHudTextRect(TextMeshProUGUI text, Vector2 anchoredPosition, Vector2 size)
        {
            if (text == null) return;

            var rect = text.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private void SetHudButtonRect(Button button, Vector2 anchoredPosition, Vector2 size)
        {
            if (button == null) return;

            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private void ApplyInGameStatText(TextMeshProUGUI text, Color color)
        {
            text.color = color;
            text.fontSize = Mathf.Max(text.fontSize, 18);
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.lineSpacing = -10f;

            var shadow = text.GetComponent<Shadow>();
            if (shadow == null)
                shadow = text.gameObject.AddComponent<Shadow>();

            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);

            CreateStatBackdrop(text, color);
        }

        private void CreateStatBackdrop(TextMeshProUGUI text, Color accentColor)
        {
            var rect = text.GetComponent<RectTransform>();
            if (rect == null || rect.parent == null) return;

            string backdropName = $"{text.gameObject.name}_IntegratedCard";
            var existing = rect.parent.Find(backdropName);
            GameObject backdropGo;

            if (existing != null)
            {
                backdropGo = existing.gameObject;
            }
            else
            {
                backdropGo = new GameObject(backdropName, typeof(RectTransform), typeof(Image));
                backdropGo.transform.SetParent(rect.parent, false);
                backdropGo.transform.SetSiblingIndex(Mathf.Max(0, rect.GetSiblingIndex()));
            }

            var backdropRect = backdropGo.GetComponent<RectTransform>();
            backdropRect.anchorMin = rect.anchorMin;
            backdropRect.anchorMax = rect.anchorMax;
            backdropRect.pivot = rect.pivot;
            backdropRect.anchoredPosition = rect.anchoredPosition;

            float width = Mathf.Max(Mathf.Abs(rect.sizeDelta.x) + 52f, 118f);
            float height = Mathf.Max(Mathf.Abs(rect.sizeDelta.y) + 28f, 58f);
            backdropRect.sizeDelta = new Vector2(width, height);

            UIStyler.ApplyCardStyle(backdropGo, false);

            var image = backdropGo.GetComponent<Image>();
            if (image != null)
            {
                image.color = Color.Lerp(accentColor, Color.white, 0.9f);
                image.raycastTarget = false;
            }

            var outline = backdropGo.GetComponent<Outline>();
            if (outline != null)
                outline.effectColor = Color.Lerp(accentColor, Color.white, 0.45f);
        }

        private void RemoveGeneratedOverlay()
        {
            var generatedRoot = GameObject.Find("GeneratedBattleUI");
            if (generatedRoot == null) return;

            if (Application.isPlaying)
                Destroy(generatedRoot);
            else
                DestroyImmediate(generatedRoot);
        }

        private void OnEnemyReached(int _) => UpdateGPA();

        private void UpdateCost(int current, int max)
        {
            if (costText != null) costText.text = $"费用\n{current}/{max}";
        }

        private void UpdateGold(int gold)
        {
            UpdateGoldDisplay();
        }

        private void UpdateGoldDisplay()
        {
            if (goldText != null && GameManager.Instance != null)
            {
                goldText.text = $"金币\n{GameManager.Instance.Gold}";
            }
        }

        private void UpdateGPA()
        {
            if (gpaText != null && GameManager.Instance != null)
                gpaText.text = $"GPA\n{GameManager.Instance.CurrentGPA}";
        }

        private void UpdateWave()
        {
            if (waveText != null && spawner != null)
                waveText.text = $"波次\n{spawner.CurrentWave}/{spawner.TotalWaves}";
        }

        private void ToggleSpeed()
        {
            doubleSpeed = !doubleSpeed;
            Time.timeScale = doubleSpeed ? 2f : 1f;
            if (speedButton != null)
            {
                var txt = speedButton.GetComponentInChildren<TextMeshProUGUI>();
                if (txt != null) txt.text = doubleSpeed ? "2x" : "1x";
            }
        }

        private void OnBackClicked()
        {
            SceneLoader.LoadSchedule();
        }

        private void OnShopClicked()
        {
            SceneLoader.LoadShop();
        }
    }
}
