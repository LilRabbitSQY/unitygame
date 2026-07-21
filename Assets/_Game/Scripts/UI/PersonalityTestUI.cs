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
        [SerializeField] private TextMeshProUGUI questionText;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private Button[] optionButtons;
        [SerializeField] private TextMeshProUGUI[] optionTexts;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TextMeshProUGUI resultTypeText;
        [SerializeField] private TextMeshProUGUI resultDescText;
        [SerializeField] private Button continueButton;

        private PersonalityTestManager testManager;

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

            for (int i = 0; i < optionButtons.Length && i < q.options.Length; i++)
            {
                if (optionTexts != null && i < optionTexts.Length && optionTexts[i] != null)
                    optionTexts[i].text = q.options[i];
                if (optionButtons[i] != null) optionButtons[i].gameObject.SetActive(true);
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
