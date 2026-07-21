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

        private void Awake()
        {
            ApplyThemeStyling();
        }

        private void Start()
        {
            scheduleManager = FindFirstObjectByType<ScheduleManager>();
            if (startBattleButton != null)
                startBattleButton.onClick.AddListener(OnStartBattle);

            GameManager.Instance?.ResetActionPoints();
            BuildActivityList();
            UpdateDisplay();
            ApplyThemeStyling();
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
                    ApplyActivityButtonStyle(button);
                }
            }
        }

        private void ApplyThemeStyling()
        {
            var theme = UITheme.Instance;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                UIStyler.EnsurePageBackground(canvas, "ScheduleBackground");

                var title = UIStyler.FindDeepChild(canvas.transform, "Title")?.GetComponent<TextMeshProUGUI>();
                if (title != null)
                {
                    UIStyler.SetTopRect(title.rectTransform, 48f, new Vector2(520f, 64f));
                    UIStyler.ApplyHeaderTextStyle(title, true);
                    title.color = theme.primary;
                    title.alignment = TextAlignmentOptions.Center;
                }

                var ddlList = UIStyler.FindDeepChild(canvas.transform, "DDLList") as RectTransform;
                if (ddlList != null)
                {
                    ddlList.anchorMin = new Vector2(1f, 0.5f);
                    ddlList.anchorMax = new Vector2(1f, 0.5f);
                    ddlList.pivot = new Vector2(1f, 0.5f);
                    ddlList.anchoredPosition = new Vector2(-120f, 10f);
                    ddlList.sizeDelta = new Vector2(320f, 360f);
                    UIStyler.EnsureVerticalLayout(ddlList, 10f, new RectOffset(14, 14, 18, 18));
                    UIStyler.EnsureBackdrop(ddlList, "DDLListPanel", new Vector2(52, 60), theme.surfaceTint);
                }

                var ddlTitle = UIStyler.FindDeepChild(canvas.transform, "DDLTitle")?.GetComponent<TextMeshProUGUI>();
                if (ddlTitle != null)
                {
                    ddlTitle.rectTransform.anchorMin = new Vector2(1f, 0.5f);
                    ddlTitle.rectTransform.anchorMax = new Vector2(1f, 0.5f);
                    ddlTitle.rectTransform.pivot = new Vector2(1f, 0.5f);
                    ddlTitle.rectTransform.anchoredPosition = new Vector2(-140f, 232f);
                    ddlTitle.rectTransform.sizeDelta = new Vector2(300f, 44f);
                    UIStyler.ApplyHeaderTextStyle(ddlTitle, false);
                    ddlTitle.fontSize = 22;
                    ddlTitle.color = theme.foreground;
                    ddlTitle.alignment = TextAlignmentOptions.Center;
                }
            }

            if (activityContainer is RectTransform activityRect)
            {
                activityRect.anchorMin = new Vector2(0.5f, 0.5f);
                activityRect.anchorMax = new Vector2(0.5f, 0.5f);
                activityRect.pivot = new Vector2(0.5f, 0.5f);
                activityRect.anchoredPosition = new Vector2(-230f, -10f);
                activityRect.sizeDelta = new Vector2(500f, 430f);
                UIStyler.EnsureVerticalLayout(activityRect, 12f, new RectOffset(120, 120, 18, 18));
                UIStyler.EnsureBackdrop(activityRect, "ActivityListPanel", new Vector2(56, 64), Color.white);
            }

            if (actionPointsText != null)
            {
                UIStyler.SetTopRect(actionPointsText.rectTransform, 118f, new Vector2(220f, 32f));
                UIStyler.ApplyTagStyle(actionPointsText.gameObject, theme.accent, theme.primary);
                actionPointsText.alignment = TextAlignmentOptions.Center;
            }

            if (statsText != null)
            {
                UIStyler.SetTopRect(statsText.rectTransform, 154f, new Vector2(440f, 54f));
                UIStyler.ApplyBodyTextStyle(statsText);
                statsText.color = theme.foreground;
                statsText.alignment = TextAlignmentOptions.Center;
                UIStyler.EnsureBackdrop(statsText.rectTransform, "StatsInfoCard", new Vector2(78, 44), theme.surfaceSoft, true);
            }

            if (startBattleButton != null)
            {
                UIStyler.SetCenteredRect(startBattleButton.GetComponent<RectTransform>(), new Vector2(0f, -455f), new Vector2(240f, 58f));
                UIStyler.ApplyGradientButtonStyle(startBattleButton);
            }
        }

        private void ApplyActivityButtonStyle(Button button)
        {
            if (button == null) return;

            UIStyler.ApplySecondaryButtonStyle(button);

            var image = button.GetComponent<Image>();
            if (image != null)
                image.color = Color.Lerp(UITheme.Instance.chart4, Color.white, 0.82f);

            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.color = UITheme.Instance.foreground;
                text.fontSize = 15;
                text.fontStyle = FontStyles.Bold;
                text.alignment = TextAlignmentOptions.Center;
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
