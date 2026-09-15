using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FinalDefense.Core;
using FinalDefense.Shop;

namespace FinalDefense.UI
{
    public class ShopUI : MonoBehaviour
    {
        [SerializeField] private Transform itemContainer;
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private Button leaveButton;

        private ShopManager shopManager;
        private TextMeshProUGUI feedback;
        private readonly System.Collections.Generic.Dictionary<Button, BattleShopItem> itemButtons = new System.Collections.Generic.Dictionary<Button, BattleShopItem>();

        private void Awake()
        {
            ApplyThemeStyling();
        }

        private void Start()
        {
            shopManager = FindFirstObjectByType<ShopManager>();
            if (leaveButton != null)
                leaveButton.onClick.AddListener(OnLeave);

            BuildItemList();
            UpdateGold();
            ApplyThemeStyling();
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                feedback = UIBootstrap.CreateTextObject("ShopFeedback", canvas.transform, "下局自动携带每类道具 1 件；重试不重复消耗。", UITheme.Instance.bodyFontSize, UITheme.Instance.foreground, FontStyles.Normal);
                UIStyler.SetCenteredRect(feedback.rectTransform, new Vector2(0, -355), new Vector2(620, 44));
                feedback.alignment = TextAlignmentOptions.Center;
                feedback.raycastTarget = false;
            }
        }

        private void BuildItemList()
        {
            if (shopManager == null || itemContainer == null) return;

            foreach (var item in shopManager.AvailableItems)
            {
                var go = new GameObject(item.name);
                go.transform.SetParent(itemContainer, false);
                go.AddComponent<RectTransform>().sizeDelta = new Vector2(0, 86);
                var img = go.AddComponent<Image>();
                img.color = Color.white;

                var btn = go.AddComponent<Button>();
                var le = go.AddComponent<LayoutElement>();
                le.minHeight = 86;
                le.preferredHeight = 86;
                btn.targetGraphic = img;
                itemButtons[btn] = item;

                var txtGo = new GameObject("Text");
                txtGo.transform.SetParent(go.transform, false);
                var rt = txtGo.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(82, 10);
                rt.offsetMax = new Vector2(-18, -10);
                var tmp = txtGo.AddComponent<TextMeshProUGUI>();
                tmp.text = ItemLabel(item);
                tmp.fontSize = 15;
                tmp.raycastTarget = false;
                tmp.fontStyle = FontStyles.Bold;
                tmp.color = UITheme.Instance.foreground;
                tmp.alignment = TextAlignmentOptions.MidlineLeft;
                tmp.enableWordWrapping = true;

                UIStyler.ApplyTowerCardStyle(go, UITheme.Instance.chart3);
                var icon = UIBootstrap.CreateUIObject("ItemIcon", go.transform).AddComponent<Image>();
                var iconRect = icon.rectTransform;
                iconRect.anchorMin = iconRect.anchorMax = new Vector2(0, .5f);
                iconRect.anchoredPosition = new Vector2(44, 0);
                iconRect.sizeDelta = new Vector2(56, 56);
                icon.sprite = GameArt.Item(item.key);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                var capturedItem = item;
                btn.onClick.AddListener(() => OnBuyItem(capturedItem));
            }
        }

        private void ApplyThemeStyling()
        {
            var theme = UITheme.Instance;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                UIStyler.EnsurePageBackground(canvas, "ShopBackground");

                var title = UIStyler.FindDeepChild(canvas.transform, "Title")?.GetComponent<TextMeshProUGUI>();
                if (title != null)
                {
                    UIStyler.SetTopRect(title.rectTransform, 56f, new Vector2(520f, 64f));
                    UIStyler.ApplyHeaderTextStyle(title, true);
                    title.color = theme.primary;
                    title.alignment = TextAlignmentOptions.Center;
                }
            }

            if (itemContainer is RectTransform itemRect)
            {
                UIStyler.SetCenteredRect(itemRect, new Vector2(0f, -20f), new Vector2(620f, 560f));
                UIStyler.EnsureVerticalLayout(itemRect, 14f, new RectOffset(22, 22, 22, 22));
                UIStyler.EnsureBackdrop(itemRect, "ShopItemListPanel", new Vector2(64, 72), Color.white);
            }

            if (goldText != null)
            {
                UIStyler.SetTopRect(goldText.rectTransform, 128f, new Vector2(220f, 34f));
                UIStyler.ApplyTagStyle(goldText.gameObject, theme.surfaceStrong, theme.chart3);
                goldText.alignment = TextAlignmentOptions.Center;
            }

            if (leaveButton != null)
            {
                UIStyler.SetCenteredRect(leaveButton.GetComponent<RectTransform>(), new Vector2(0f, -440f), new Vector2(220f, 54f));
                UIStyler.ApplySecondaryButtonStyle(leaveButton);
                var label = leaveButton.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = GameManager.Instance != null && (GameManager.Instance.IsGameComplete || GameManager.Instance.CurrentDay >= GameManager.TotalDays) ? "查看总结" : "下一天";
            }
        }

        private void OnBuyItem(BattleShopItem item)
        {
            if (shopManager != null && shopManager.BuyItem(item))
            {
                UpdateGold();
                if (feedback != null) feedback.text = $"已获得 {item.name} ×1，下局自动携带。";
                if (GameManager.Instance.IsGameComplete) SceneLoader.LoadResult();
            }
            else if (feedback != null) feedback.text = "GPA不足、商品售罄或库存已满。";
        }

        private void UpdateGold()
        {
            var gm = GameManager.Instance;
            if (goldText != null && gm != null) goldText.text = $"GPA  {gm.CurrentGPA:0.##}";
            foreach (var entry in itemButtons)
            {
                var item = entry.Value;
                entry.Key.interactable = gm != null && gm.Phase == CampaignPhase.Shop && !gm.IsGameComplete && gm.CurrentGPAHundredths >= item.priceHundredths && gm.BattleItemCount(item.key) < 99 && gm.ShopStock(item.key) > 0;
                var text = entry.Key.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null) text.text = ItemLabel(item);
            }
        }

        private static string ItemLabel(BattleShopItem item)
        {
            var gm = GameManager.Instance;
            return $"{item.name}   ·   {item.Price:0.##} GPA\n{item.description}\n持有 {gm?.BattleItemCount(item.key) ?? 0}/99    剩余 {gm?.ShopStock(item.key) ?? 99} 件";
        }

        private void OnLeave()
        {
            shopManager?.LeaveShop();
        }
    }
}
