using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FinalDefense.Core;
using FinalDefense.Data;
using FinalDefense.Schedule;

namespace FinalDefense.UI
{
    public class ScheduleUI : MonoBehaviour
    {
        [SerializeField] private Transform activityContainer;
        [SerializeField] private GameObject activityButtonPrefab;
        [SerializeField] private TextMeshProUGUI actionPointsText;
        [SerializeField] private TextMeshProUGUI statsText;
        [SerializeField] private Button startBattleButton;

        private ScheduleManager scheduleManager;

        private void Start()
        {
            scheduleManager = FindFirstObjectByType<ScheduleManager>();
            if (startBattleButton != null)
                startBattleButton.onClick.AddListener(OnStartBattle);

            GameManager.Instance?.ResetActionPoints();
            BuildActivityList();
            UpdateDisplay();
        }

        private void BuildActivityList()
        {
            if (scheduleManager == null || activityContainer == null) return;

            foreach (var activity in scheduleManager.AvailableActivities)
            {
                GameObject btnGo;
                if (activityButtonPrefab != null)
                    btnGo = Instantiate(activityButtonPrefab, activityContainer);
                else
                {
                    btnGo = new GameObject(activity.activityName);
                    btnGo.transform.SetParent(activityContainer, false);
                    btnGo.AddComponent<RectTransform>();
                    var img = btnGo.AddComponent<Image>();
                    img.color = new Color(0.2f, 0.4f, 0.6f, 1f);
                    var btn = btnGo.AddComponent<Button>();
                    var txtGo = new GameObject("Text");
                    txtGo.transform.SetParent(btnGo.transform, false);
                    var rt = txtGo.AddComponent<RectTransform>();
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    var tmp = txtGo.AddComponent<TextMeshProUGUI>();
                    tmp.text = $"{activity.activityName}\n{activity.description}";
                    tmp.fontSize = 14;
                    tmp.alignment = TextAlignmentOptions.Center;

                    var le = btnGo.AddComponent<UnityEngine.UI.LayoutElement>();
                    le.minHeight = 60;
                    le.preferredHeight = 60;
                }

                var button = btnGo.GetComponent<Button>();
                if (button != null)
                {
                    var act = activity;
                    button.onClick.AddListener(() => OnActivityClicked(act));
                }
            }
        }

        private void OnActivityClicked(ActivityData activity)
        {
            if (scheduleManager.ExecuteActivity(activity))
            {
                UpdateDisplay();
            }
        }

        private void UpdateDisplay()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            if (actionPointsText != null)
                actionPointsText.text = $"行动点: {gm.ActionPoints}/{gm.MaxActionPoints}";

            if (statsText != null)
                statsText.text = $"心情: {gm.CurrentEmotion}  体力: {gm.CurrentStrength}\n学习力: {gm.CurrentEduPower}  决心: {gm.CurrentDetermination}";
        }

        private void OnStartBattle()
        {
            scheduleManager?.FinishSchedule();
        }
    }
}
