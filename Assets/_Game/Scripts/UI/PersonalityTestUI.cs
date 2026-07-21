using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FinalDefense.Core;
using FinalDefense.Data;
using FinalDefense.Personality;

namespace FinalDefense.UI
{
    public class PersonalityTestUI : MonoBehaviour
    {
        [Header("Existing UI Styling")]
        [SerializeField] private bool styleExistingUI = true;

        [SerializeField] private TextMeshProUGUI questionText;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private Button[] optionButtons;
        [SerializeField] private TextMeshProUGUI[] optionTexts;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TextMeshProUGUI resultTypeText;
        [SerializeField] private TextMeshProUGUI resultDescText;
        [SerializeField] private Button continueButton;

        private PersonalityTestManager testManager;

        private void Awake()
        {
            if (styleExistingUI)
                ApplyThemeStyling();
        }

        private void Start()
        {
            testManager = FindFirstObjectByType<PersonalityTestManager>();
            if (resultPanel != null) resultPanel.SetActive(false);
            if (continueButton != null)
                continueButton.onClick.AddListener(OnContinue);

            for (int i = 0; i < optionButtons.Length; i++)
            {
                int index = i;
                if (optionButtons[i] != null)
                    optionButtons[i].onClick.AddListener(() => OnOptionSelected(index));
            }

            DisplayCurrentQuestion();

            if (styleExistingUI)
                ApplyThemeStyling();
        }

        private void DisplayCurrentQuestion()
        {
            if (testManager == null || testManager.IsComplete)
            {
                ShowResult();
                return;
            }

            var q = testManager.CurrentQuestion;
            if (questionText != null) questionText.text = q.questionText;
            if (progressText != null) progressText.text = $"{testManager.CurrentQuestionIndex + 1}/{testManager.TotalQuestions}";

            if (styleExistingUI)
                ApplyProgressStyle();

            for (int i = 0; i < optionButtons.Length && i < q.options.Length; i++)
            {
                if (optionTexts != null && i < optionTexts.Length && optionTexts[i] != null)
                    optionTexts[i].text = q.options[i];
                if (optionButtons[i] != null) optionButtons[i].gameObject.SetActive(true);
            }
        }

        private void ApplyThemeStyling()
        {
            var theme = UITheme.Instance;

            ApplyPageBackground();
            ApplyQuestionCard();
            ApplyProgressStyle();

            if (questionText != null)
            {
                UIStyler.SetCenteredRect(questionText.rectTransform, new Vector2(0f, 165f), new Vector2(560f, 96f));
                UIStyler.ApplyHeaderTextStyle(questionText, false);
                questionText.color = theme.foreground;
                questionText.fontSize = 24;
                questionText.alignment = TextAlignmentOptions.Center;
            }

            for (int i = 0; i < optionButtons.Length; i++)
            {
                ApplyOptionButtonStyle(optionButtons[i], i);
            }

            if (resultPanel != null)
            {
                UIStyler.SetCenteredRect(resultPanel.GetComponent<RectTransform>(), new Vector2(0f, 35f), new Vector2(620f, 360f));
                UIStyler.ApplyCardStyle(resultPanel);
            }

            if (resultTypeText != null)
            {
                UIStyler.SetCenteredRect(resultTypeText.rectTransform, new Vector2(0f, 118f), new Vector2(520f, 54f));
                UIStyler.ApplyHeaderTextStyle(resultTypeText, false);
                resultTypeText.color = theme.primary;
                resultTypeText.alignment = TextAlignmentOptions.Center;
            }

            if (resultDescText != null)
            {
                UIStyler.SetCenteredRect(resultDescText.rectTransform, new Vector2(0f, 24f), new Vector2(500f, 120f));
                UIStyler.ApplyBodyTextStyle(resultDescText);
                resultDescText.color = theme.mutedForeground;
                resultDescText.alignment = TextAlignmentOptions.Center;
            }

            if (continueButton != null)
            {
                UIStyler.SetCenteredRect(continueButton.GetComponent<RectTransform>(), new Vector2(0f, -150f), new Vector2(240f, 54f));
                UIStyler.ApplyGradientButtonStyle(continueButton);
            }
        }

        private void ApplyPageBackground()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            var background = canvas.transform.Find("PersonalityTestBackground") as RectTransform;
            if (background == null)
            {
                var bgGo = new GameObject("PersonalityTestBackground", typeof(RectTransform), typeof(Image));
                bgGo.transform.SetParent(canvas.transform, false);
                background = bgGo.GetComponent<RectTransform>();
                background.SetAsFirstSibling();
            }

            background.anchorMin = Vector2.zero;
            background.anchorMax = Vector2.one;
            background.offsetMin = Vector2.zero;
            background.offsetMax = Vector2.zero;

            var image = background.GetComponent<Image>();
            image.color = UITheme.Instance.background;
            image.raycastTarget = false;
        }

        private void ApplyQuestionCard()
        {
            if (questionText == null) return;

            var canvas = questionText.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            var card = canvas.transform.Find("PersonalityQuestionCard") as RectTransform;
            if (card == null)
            {
                var cardGo = new GameObject("PersonalityQuestionCard", typeof(RectTransform), typeof(Image));
                cardGo.transform.SetParent(canvas.transform, false);
                card = cardGo.GetComponent<RectTransform>();
                card.SetSiblingIndex(1);
                UIStyler.ApplyCardStyle(cardGo);
            }

            card.anchorMin = new Vector2(0.5f, 0.5f);
            card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = Vector2.zero;
            card.sizeDelta = new Vector2(620, 460);

            var image = card.GetComponent<Image>();
            if (image != null)
                image.raycastTarget = false;
        }

        private void ApplyProgressStyle()
        {
            if (progressText == null) return;

            progressText.color = UITheme.Instance.primary;
            progressText.fontSize = 14;
            progressText.fontStyle = FontStyles.Bold;
            progressText.alignment = TextAlignmentOptions.Center;

            var rect = progressText.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 250f);
            rect.sizeDelta = new Vector2(76, 28);

            CreateProgressBackdrop(rect);
        }

        private void CreateProgressBackdrop(RectTransform progressRect)
        {
            if (progressRect == null || progressRect.parent == null) return;

            const string backdropName = "ProgressText_Backdrop";
            var existing = progressRect.parent.Find(backdropName);
            GameObject backdropGo;

            if (existing != null)
            {
                backdropGo = existing.gameObject;
            }
            else
            {
                backdropGo = new GameObject(backdropName, typeof(RectTransform), typeof(Image));
                backdropGo.transform.SetParent(progressRect.parent, false);
                backdropGo.transform.SetSiblingIndex(Mathf.Max(0, progressRect.GetSiblingIndex()));
            }

            var backdropRect = backdropGo.GetComponent<RectTransform>();
            backdropRect.anchorMin = progressRect.anchorMin;
            backdropRect.anchorMax = progressRect.anchorMax;
            backdropRect.pivot = progressRect.pivot;
            backdropRect.anchoredPosition = progressRect.anchoredPosition;
            backdropRect.sizeDelta = new Vector2(96, 34);

            UIStyler.ApplyRoundedRect(backdropGo, UITheme.Instance.radiusPill);

            var image = backdropGo.GetComponent<Image>();
            if (image != null)
            {
                image.color = UITheme.Instance.accent;
                image.raycastTarget = false;
            }
        }

        private void ApplyOptionButtonStyle(Button button, int index)
        {
            if (button == null) return;

            UIStyler.ApplySecondaryButtonStyle(button);
            UIStyler.SetCenteredRect(button.GetComponent<RectTransform>(), new Vector2(0f, 70f - index * 78f), new Vector2(520f, 58f));

            var image = button.GetComponent<Image>();
            if (image != null)
            {
                var colors = new[]
                {
                    UITheme.Instance.accent,
                    Color.Lerp(UITheme.Instance.chart2, Color.white, 0.86f),
                    Color.Lerp(UITheme.Instance.chart4, Color.white, 0.86f),
                    Color.Lerp(UITheme.Instance.chart3, Color.white, 0.86f)
                };
                image.color = colors[Mathf.Clamp(index, 0, colors.Length - 1)];
            }

            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.color = UITheme.Instance.foreground;
                text.fontSize = 16;
                text.fontStyle = FontStyles.Bold;
                text.alignment = TextAlignmentOptions.Center;
            }
        }

        private void OnOptionSelected(int index)
        {
            testManager.AnswerQuestion(index);
            if (testManager.IsComplete)
                ShowResult();
            else
                DisplayCurrentQuestion();
        }

        private void ShowResult()
        {
            var resultType = testManager.GetResult();
            GameManager.Instance?.SetPersonality(resultType);

            if (resultPanel != null) resultPanel.SetActive(true);
            foreach (var btn in optionButtons)
                if (btn != null) btn.gameObject.SetActive(false);
            if (questionText != null) questionText.gameObject.SetActive(false);

            var config = GameManager.Instance?.PersonalityConfigData;
            if (config != null)
            {
                var stats = config.GetStats(resultType);
                if (resultTypeText != null) resultTypeText.text = $"你是: {stats.displayName} ({resultType})";
                if (resultDescText != null) resultDescText.text = stats.description;
            }
            else
            {
                if (resultTypeText != null) resultTypeText.text = $"你是: {resultType}";
            }
        }

        private void OnContinue()
        {
            SceneLoader.LoadMainMenu();
        }
    }
}
