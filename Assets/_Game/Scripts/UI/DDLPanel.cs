using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FinalDefense.Core;
using FinalDefense.DDL;

namespace FinalDefense.UI
{
    public class DDLPanel : MonoBehaviour
    {
        [SerializeField] private Transform ddlListContainer;
        [SerializeField] private TextMeshProUGUI ddlTitleText;

        private void Start()
        {
            ApplyPanelStyle();
            RefreshDisplay();
        }

        public void RefreshDisplay()
        {
            if (DDLManager.Instance == null) return;

            if (ddlTitleText != null)
                ddlTitleText.text = $"当前DDL (难度倍率: x{DDLManager.Instance.WaveMultiplier:F1})";

            if (ddlListContainer == null) return;
            foreach (Transform child in ddlListContainer)
                Destroy(child.gameObject);

            foreach (var ddl in DDLManager.Instance.ActiveDDLs)
            {
                if (ddl.completed) continue;
                var go = new GameObject("DDLItem");
                go.transform.SetParent(ddlListContainer, false);
                go.AddComponent<RectTransform>();
                var image = go.AddComponent<Image>();
                var le = go.AddComponent<UnityEngine.UI.LayoutElement>();
                le.minHeight = 44;
                le.preferredHeight = 44;
                UIStyler.ApplyMutedPanelStyle(go);
                image.color = ddl.remainingDays <= 1
                    ? Color.Lerp(UITheme.Instance.stateError, Color.white, 0.82f)
                    : UITheme.Instance.surfaceSoft;

                var textGo = new GameObject("Text");
                textGo.transform.SetParent(go.transform, false);
                var textRect = textGo.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(14, 6);
                textRect.offsetMax = new Vector2(-14, -6);
                var tmp = textGo.AddComponent<TextMeshProUGUI>();
                tmp.fontSize = 14;
                tmp.fontStyle = FontStyles.Bold;
                tmp.color = ddl.remainingDays <= 1 ? UITheme.Instance.stateError : UITheme.Instance.foreground;
                tmp.alignment = TextAlignmentOptions.MidlineLeft;
                tmp.text = $"{ddl.data.ddlName} - 剩余{ddl.remainingDays}天";
            }
        }

        private void ApplyPanelStyle()
        {
            if (ddlTitleText != null)
            {
                UIStyler.ApplyHeaderTextStyle(ddlTitleText, false);
                ddlTitleText.fontSize = 22;
                ddlTitleText.color = UITheme.Instance.foreground;
            }

            if (ddlListContainer is RectTransform listRect)
                UIStyler.EnsureBackdrop(listRect, "DDLPanelCard", new Vector2(48, 58), UITheme.Instance.surfaceTint);
        }
    }
}
