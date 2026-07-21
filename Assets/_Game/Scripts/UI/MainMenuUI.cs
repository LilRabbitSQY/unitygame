using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FinalDefense.Core;

namespace FinalDefense.UI
{
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private Button startButton;
        [SerializeField] private TextMeshProUGUI gpaText;
        [SerializeField] private TextMeshProUGUI gradeText;
        [SerializeField] private TextMeshProUGUI personalityText;
        [SerializeField] private TextMeshProUGUI titleText;

        private void Start()
        {
            startButton.onClick.AddListener(OnStartClicked);
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            if (gpaText != null) gpaText.text = $"GPA: {gm.CurrentGPA}";
            if (gradeText != null) gradeText.text = $"年级: {gm.CurrentGrade}  第{gm.CurrentDay}天";
            if (personalityText != null && gm.PersonalitySelected)
            {
                var config = gm.PersonalityConfigData;
                if (config != null)
                {
                    var stats = config.GetStats(gm.CurrentPersonality);
                    personalityText.text = $"类型: {stats.displayName}";
                }
            }
        }

        private void OnStartClicked()
        {
            var gm = GameManager.Instance;
            if (gm != null && !gm.PersonalitySelected)
            {
                SceneLoader.LoadPersonalityTest();
            }
            else
            {
                SceneLoader.LoadSchedule();
            }
        }
    }
}
