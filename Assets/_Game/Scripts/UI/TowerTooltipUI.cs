using FinalDefense.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FinalDefense.UI
{
    public class TowerTooltipUI : MonoBehaviour
    {
        private const float MaxTextWidth = 340f;
        private const float Padding = 14f;
        private const float ScreenMargin = 12f;

        private static readonly Vector2 PointerOffset = new Vector2(20f, -18f);
        private static TowerTooltipUI instance;

        private Canvas canvas;
        private RectTransform rectTransform;
        private TextMeshProUGUI bodyText;

        public static void Show(TowerData towerData, Vector2 screenPosition)
        {
            if (towerData == null) return;

            EnsureInstance();
            instance.SetTower(towerData);
            instance.gameObject.SetActive(true);
            instance.MoveTo(screenPosition);
        }

        public static void Move(Vector2 screenPosition)
        {
            if (instance == null || !instance.gameObject.activeSelf) return;
            instance.MoveTo(screenPosition);
        }

        public static void Hide()
        {
            if (instance == null) return;
            instance.gameObject.SetActive(false);
        }

        private static void EnsureInstance()
        {
            if (instance != null) return;

            Canvas targetCanvas = FindFirstObjectByType<Canvas>();
            if (targetCanvas == null)
            {
                var canvasGo = new GameObject("RuntimeCanvas");
                targetCanvas = canvasGo.AddComponent<Canvas>();
                targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGo.AddComponent<CanvasScaler>();
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            var go = new GameObject("TowerTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup), typeof(TowerTooltipUI));
            go.transform.SetParent(targetCanvas.transform, false);

            instance = go.GetComponent<TowerTooltipUI>();
            instance.Initialize(targetCanvas);
            go.SetActive(false);
        }

        private void Initialize(Canvas targetCanvas)
        {
            canvas = targetCanvas;
            rectTransform = GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.one * 0.5f;
            rectTransform.anchorMax = Vector2.one * 0.5f;
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.sizeDelta = new Vector2(320f, 160f);

            var background = GetComponent<Image>();
            background.color = new Color(0.05f, 0.06f, 0.1f, 0.94f);
            background.raycastTarget = false;

            var canvasGroup = GetComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(transform, false);

            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(Padding, Padding);
            textRect.offsetMax = new Vector2(-Padding, -Padding);

            bodyText = textGo.GetComponent<TextMeshProUGUI>();
            bodyText.fontSize = 20f;
            bodyText.color = Color.white;
            bodyText.enableWordWrapping = true;
            bodyText.raycastTarget = false;
        }

        private void SetTower(TowerData towerData)
        {
            bodyText.text =
                $"<b>{towerData.towerName}</b>\n" +
                $"{GetTowerSummary(towerData)}\n" +
                $"部署费用: {towerData.deployCost}    商店价格: {towerData.cost}\n" +
                $"伤害: {towerData.damage}    射程: {towerData.attackRange:0.#}\n" +
                $"攻击间隔: {towerData.attackInterval:0.##}秒    生命: {towerData.maxHP}\n" +
                "<color=#A9C8FF>点击后跟随鼠标，再点地图确认放置。右键/Esc取消。</color>";

            Vector2 preferred = bodyText.GetPreferredValues(bodyText.text, MaxTextWidth, 0f);
            rectTransform.sizeDelta = new Vector2(MaxTextWidth + Padding * 2f, preferred.y + Padding * 2f);
            rectTransform.SetAsLastSibling();
        }

        private string GetTowerSummary(TowerData towerData)
        {
            if (towerData.attackInterval <= 0.9f)
                return "高频输出塔，适合清理低血量小怪。";

            if (towerData.attackRange >= 3f)
                return "远距离输出塔，适合放在路线转角覆盖更多路径。";

            if (towerData.maxHP >= 350)
                return "高生命防线，适合放在前排拖住敌人。";

            return "均衡型防御塔，适合补足防线空位。";
        }

        private void MoveTo(Vector2 screenPosition)
        {
            if (canvas == null || rectTransform == null) return;

            var canvasRect = canvas.transform as RectTransform;
            if (canvasRect == null) return;

            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition + PointerOffset, uiCamera, out Vector2 localPoint))
            {
                rectTransform.anchoredPosition = ClampToCanvas(localPoint, canvasRect.rect, rectTransform.sizeDelta);
            }
        }

        private Vector2 ClampToCanvas(Vector2 localPoint, Rect canvasRect, Vector2 tooltipSize)
        {
            float minX = canvasRect.xMin + ScreenMargin;
            float maxX = canvasRect.xMax - tooltipSize.x - ScreenMargin;
            float minY = canvasRect.yMin + tooltipSize.y + ScreenMargin;
            float maxY = canvasRect.yMax - ScreenMargin;

            if (maxX < minX) maxX = minX;
            if (maxY < minY) maxY = minY;

            localPoint.x = Mathf.Clamp(localPoint.x, minX, maxX);
            localPoint.y = Mathf.Clamp(localPoint.y, minY, maxY);
            return localPoint;
        }
    }
}
