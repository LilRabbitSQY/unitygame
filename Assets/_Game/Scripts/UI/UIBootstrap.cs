using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace FinalDefense.UI
{
    [DefaultExecutionOrder(-100)]
    public class UIBootstrap : MonoBehaviour
    {
        [Header("Canvas Setup")]
        [SerializeField] private Canvas mainCanvas;
        [SerializeField] private bool autoSetupCanvas = true;

        [Header("Camera")]
        [SerializeField] private Camera uiCamera;

        [Header("Background")]
        [SerializeField] private bool setCameraBackground = true;

        private static UIBootstrap _instance;
        public static UIBootstrap Instance => _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            if (autoSetupCanvas)
            {
                SetupCanvas();
            }

            if (setCameraBackground && uiCamera != null)
            {
                uiCamera.backgroundColor = UITheme.Instance.background;
            }

            ApplyThemeToAllUI();
        }

        private void SetupCanvas()
        {
            if (mainCanvas == null)
            {
                mainCanvas = FindFirstObjectByType<Canvas>();
            }

            if (mainCanvas == null)
            {
                var canvasGo = new GameObject("MainCanvas");
                mainCanvas = canvasGo.AddComponent<Canvas>();
                canvasGo.AddComponent<CanvasScaler>();
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            mainCanvas.sortingOrder = 100;

            var scaler = mainCanvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<EventSystem>();
                esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
        }

        [ContextMenu("Apply Theme To All UI")]
        public void ApplyThemeToAllUI()
        {
            ApplyThemeToTransform(transform);
        }

        private void ApplyThemeToTransform(Transform parent)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                ApplyThemeToElement(child.gameObject);
                ApplyThemeToTransform(child);
            }
        }

        private void ApplyThemeToElement(GameObject go)
        {
            ApplyButtonStyle(go);
            ApplyPanelStyleByTag(go);
            ApplyTextStyle(go);
        }

        private void ApplyButtonStyle(GameObject go)
        {
            var button = go.GetComponent<Button>();
            if (button == null) return;

            string goName = go.name.ToLower();

            if (goName.Contains("primary") || goName.Contains("main") || goName.Contains("cta") ||
                goName.Contains("gradient") || goName.Contains("battle") || goName.Contains("start"))
            {
                UIStyler.ApplyGradientButtonStyle(button);
            }
            else if (goName.Contains("secondary") || goName.Contains("back") || goName.Contains("return"))
            {
                UIStyler.ApplySecondaryButtonStyle(button);
            }
            else if (goName.Contains("danger") || goName.Contains("destructive"))
            {
                UIStyler.ApplyPrimaryButtonStyle(button);
                var img = button.GetComponent<Image>();
                if (img != null) img.color = UITheme.Instance.destructive;
            }
        }

        private void ApplyPanelStyleByTag(GameObject go)
        {
            string goName = go.name.ToLower();

            if (goName.Contains("card"))
            {
                UIStyler.ApplyCardStyle(go);
            }
            else if (goName.Contains("panel") || goName.Contains("container"))
            {
                bool useGradient = goName.Contains("header") || goName.Contains("hero");
                UIStyler.ApplyPanelStyle(go, useGradient);
            }
            else if (goName.Contains("sidebar"))
            {
                UIStyler.ApplySidebarStyle(go);
            }
            else if (goName.Contains("muted"))
            {
                UIStyler.ApplyMutedPanelStyle(go);
            }
        }

        private void ApplyTextStyle(GameObject go)
        {
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (tmp == null) return;

            string goName = go.name.ToLower();

            if (goName.Contains("title") || goName.Contains("h1") || goName.Contains("header"))
            {
                UIStyler.ApplyHeaderTextStyle(tmp, true);
            }
            else if (goName.Contains("subtitle") || goName.Contains("h2"))
            {
                UIStyler.ApplyHeaderTextStyle(tmp, false);
            }
            else if (goName.Contains("value") || goName.Contains("stat") || goName.Contains("number"))
            {
                UIStyler.ApplyStatValueTextStyle(tmp);
            }
            else if (goName.Contains("caption") || goName.Contains("label") || goName.Contains("small"))
            {
                UIStyler.ApplyCaptionTextStyle(tmp);
            }
            else
            {
                UIStyler.ApplyBodyTextStyle(tmp);
            }
        }

        public void CreateDefaultBattleHUD(Transform parent)
        {
            var theme = UITheme.Instance;

            var header = CreateUIObject("HeaderPanel", parent);
            UIStyler.ApplyPanelStyle(header, true);
            var headerRT = header.GetComponent<RectTransform>();
            headerRT.anchorMin = new Vector2(0, 1);
            headerRT.anchorMax = new Vector2(1, 1);
            headerRT.pivot = new Vector2(0.5f, 1);
            headerRT.sizeDelta = new Vector2(-40, 120);
            headerRT.anchoredPosition = new Vector2(0, -20);

            CreateStatCard(header.transform, "GPA", "86", "还剩4次失误空间", theme.towerCalculator, new Vector2(20, 20), new Vector2(200, -20));
            CreateStatCard(header.transform, "金币", "128", "下一座塔可立即部署", theme.chart3, new Vector2(240, 20), new Vector2(200, -20));
            CreateStatCard(header.transform, "波次", "3/5", "论述题Boss即将出现", theme.towerNotebook, new Vector2(460, 20), new Vector2(200, -20));
        }

        private GameObject CreateStatCard(Transform parent, string label, string value, string subtitle, Color accentColor, Vector2 anchoredPos, Vector2 size)
        {
            var card = CreateUIObject($"StatCard_{label}", parent);
            UIStyler.ApplyCardStyle(card);
            var rt = card.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var labelText = CreateTextObject("Label", card.transform, label, 12, UITheme.Instance.mutedForeground, FontStyles.Bold);
            var labelRT = labelText.GetComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0, 1);
            labelRT.anchorMax = new Vector2(1, 1);
            labelRT.pivot = new Vector2(0.5f, 1);
            labelRT.sizeDelta = new Vector2(-32, 20);
            labelRT.anchoredPosition = new Vector2(0, -16);
            labelText.characterSpacing = 0.16f;

            var valueText = CreateTextObject("Value", card.transform, value, 28, UITheme.Instance.foreground, FontStyles.Bold);
            var valueRT = valueText.GetComponent<RectTransform>();
            valueRT.anchorMin = new Vector2(0, 0.5f);
            valueRT.anchorMax = new Vector2(1, 0.5f);
            valueRT.pivot = new Vector2(0.5f, 0.5f);
            valueRT.sizeDelta = new Vector2(-32, 36);
            valueRT.anchoredPosition = new Vector2(0, 0);

            var subText = CreateTextObject("Subtitle", card.transform, subtitle, 14, UITheme.Instance.mutedForeground, FontStyles.Normal);
            var subRT = subText.GetComponent<RectTransform>();
            subRT.anchorMin = new Vector2(0, 0);
            subRT.anchorMax = new Vector2(1, 0);
            subRT.pivot = new Vector2(0.5f, 0);
            subRT.sizeDelta = new Vector2(-32, 20);
            subRT.anchoredPosition = new Vector2(0, 16);

            return card;
        }

        public static GameObject CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.localScale = Vector3.one;
            rt.localPosition = Vector3.zero;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(100, 100);
            return go;
        }

        public static TextMeshProUGUI CreateTextObject(string name, Transform parent, string text, int fontSize, Color color, FontStyles style)
        {
            var go = CreateUIObject(name, parent);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.enableWordWrapping = true;
            return tmp;
        }

        public static Button CreateButton(string name, Transform parent, string text, bool isPrimary = true)
        {
            var go = CreateUIObject(name, parent);
            var image = go.AddComponent<Image>();
            var button = go.AddComponent<Button>();

            var textGo = CreateTextObject("Text", go.transform, text, 15, Color.white, FontStyles.Bold);
            var textRT = textGo.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(16, 8);
            textRT.offsetMax = new Vector2(-16, -8);
            textGo.alignment = TextAlignmentOptions.Center;

            if (isPrimary)
            {
                UIStyler.ApplyGradientButtonStyle(button);
            }
            else
            {
                UIStyler.ApplySecondaryButtonStyle(button);
            }

            return button;
        }
    }
}
