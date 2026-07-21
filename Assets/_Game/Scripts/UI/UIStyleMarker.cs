using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FinalDefense.UI
{
    [ExecuteInEditMode]
    public class UIStyleMarker : MonoBehaviour
    {
        public enum StyleType
        {
            None,
            Card,
            Panel,
            PanelGradient,
            Sidebar,
            MutedPanel,
            PrimaryButton,
            SecondaryButton,
            GradientButton,
            HeaderH1,
            HeaderH2,
            BodyText,
            CaptionText,
            StatValue,
            Tag,
            ProgressBar,
            TowerCard,
        }

        public StyleType styleType = StyleType.None;
        public Color customAccentColor = Color.white;
        public bool applyOnStart = true;
        public bool applyInEditMode = false;

        private void Reset()
        {
            AutoDetectStyle();
        }

        private void Start()
        {
            if (applyOnStart && Application.isPlaying)
            {
                ApplyStyle();
            }
        }

        private void OnValidate()
        {
            if (applyInEditMode && !Application.isPlaying)
            {
                ApplyStyle();
            }
        }

        [ContextMenu("Auto Detect Style")]
        public void AutoDetectStyle()
        {
            string goName = gameObject.name.ToLower();

            if (GetComponent<Button>() != null)
            {
                if (goName.Contains("primary") || goName.Contains("main") || goName.Contains("cta") ||
                    goName.Contains("gradient") || goName.Contains("battle") || goName.Contains("start"))
                {
                    styleType = StyleType.GradientButton;
                }
                else if (goName.Contains("secondary") || goName.Contains("back") || goName.Contains("return"))
                {
                    styleType = StyleType.SecondaryButton;
                }
                else
                {
                    styleType = StyleType.PrimaryButton;
                }
            }
            else if (GetComponent<Image>() != null)
            {
                if (goName.Contains("sidebar"))
                {
                    styleType = StyleType.Sidebar;
                }
                else if (goName.Contains("muted"))
                {
                    styleType = StyleType.MutedPanel;
                }
                else if (goName.Contains("card"))
                {
                    styleType = StyleType.Card;
                }
                else if (goName.Contains("tower"))
                {
                    styleType = StyleType.TowerCard;
                }
                else if (goName.Contains("panel") || goName.Contains("container"))
                {
                    styleType = goName.Contains("header") ? StyleType.PanelGradient : StyleType.Panel;
                }
            }
            else if (GetComponent<TextMeshProUGUI>() != null)
            {
                if (goName.Contains("title") || goName.Contains("h1") || goName.Contains("header"))
                {
                    styleType = StyleType.HeaderH1;
                }
                else if (goName.Contains("subtitle") || goName.Contains("h2"))
                {
                    styleType = StyleType.HeaderH2;
                }
                else if (goName.Contains("value") || goName.Contains("stat") || goName.Contains("number"))
                {
                    styleType = StyleType.StatValue;
                }
                else if (goName.Contains("caption") || goName.Contains("label") || goName.Contains("small"))
                {
                    styleType = StyleType.CaptionText;
                }
                else
                {
                    styleType = StyleType.BodyText;
                }
            }
        }

        [ContextMenu("Apply Style")]
        public void ApplyStyle()
        {
            switch (styleType)
            {
                case StyleType.Card:
                    UIStyler.ApplyCardStyle(gameObject);
                    break;
                case StyleType.Panel:
                    UIStyler.ApplyPanelStyle(gameObject, false);
                    break;
                case StyleType.PanelGradient:
                    UIStyler.ApplyPanelStyle(gameObject, true);
                    break;
                case StyleType.Sidebar:
                    UIStyler.ApplySidebarStyle(gameObject);
                    break;
                case StyleType.MutedPanel:
                    UIStyler.ApplyMutedPanelStyle(gameObject);
                    break;
                case StyleType.PrimaryButton:
                    var btn1 = GetComponent<Button>();
                    if (btn1 != null) UIStyler.ApplyPrimaryButtonStyle(btn1);
                    break;
                case StyleType.SecondaryButton:
                    var btn2 = GetComponent<Button>();
                    if (btn2 != null) UIStyler.ApplySecondaryButtonStyle(btn2);
                    break;
                case StyleType.GradientButton:
                    var btn3 = GetComponent<Button>();
                    if (btn3 != null) UIStyler.ApplyGradientButtonStyle(btn3);
                    break;
                case StyleType.HeaderH1:
                    var text1 = GetComponent<TextMeshProUGUI>();
                    if (text1 != null) UIStyler.ApplyHeaderTextStyle(text1, true);
                    break;
                case StyleType.HeaderH2:
                    var text2 = GetComponent<TextMeshProUGUI>();
                    if (text2 != null) UIStyler.ApplyHeaderTextStyle(text2, false);
                    break;
                case StyleType.BodyText:
                    var text3 = GetComponent<TextMeshProUGUI>();
                    if (text3 != null) UIStyler.ApplyBodyTextStyle(text3);
                    break;
                case StyleType.CaptionText:
                    var text4 = GetComponent<TextMeshProUGUI>();
                    if (text4 != null) UIStyler.ApplyCaptionTextStyle(text4);
                    break;
                case StyleType.StatValue:
                    var text5 = GetComponent<TextMeshProUGUI>();
                    if (text5 != null) UIStyler.ApplyStatValueTextStyle(text5);
                    break;
                case StyleType.Tag:
                    UIStyler.ApplyTagStyle(gameObject, customAccentColor, Color.white);
                    break;
                case StyleType.TowerCard:
                    UIStyler.ApplyTowerCardStyle(gameObject, customAccentColor);
                    break;
            }
        }
    }
}
