using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FinalDefense.Core;
using FinalDefense.Data;
using FinalDefense.Shop;

namespace FinalDefense.UI
{
    public class ShopUI : MonoBehaviour
    {
        [SerializeField] private Transform itemContainer;
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private Button leaveButton;

        private ShopManager shopManager;

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
        }

        private void BuildItemList()
        {
            if (shopManager == null || itemContainer == null) return;

            foreach (var item in shopManager.AvailableItems)
            {
                var go = new GameObject(item.itemName);
                go.transform.SetParent(itemContainer, false);
                go.AddComponent<RectTransform>();
                var img = go.AddComponent<Image>();
                img.color = Color.white;

                var btn = go.AddComponent<Button>();
                var le = go.AddComponent<LayoutElement>();
                le.minHeight = 86;
                le.preferredHeight = 86;

                var txtGo = new GameObject("Text");
                txtGo.transform.SetParent(go.transform, false);
                var rt = txtGo.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(18, 10);
                rt.offsetMax = new Vector2(-18, -10);
                var tmp = txtGo.AddComponent<TextMeshProUGUI>();
                tmp.text = $"{item.itemName} - {item.price}金\n{item.description}";
                tmp.fontSize = 15;
                tmp.fontStyle = FontStyles.Bold;
                tmp.color = UITheme.Instance.foreground;
                tmp.alignment = TextAlignmentOptions.MidlineLeft;
                tmp.enableWordWrapping = true;

                UIStyler.ApplyTowerCardStyle(go, UITheme.Instance.chart3);

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
            }
        }

        private void OnBuyItem(ShopItemData item)
        {
            if (shopManager.BuyItem(item))
            {
                UpdateGold();
            }
        }

        private void UpdateGold()
        {
            if (goldText != null && GameManager.Instance != null)
                goldText.text = $"金币: {GameManager.Instance.Gold}";
        }

        private void OnLeave()
        {
            shopManager?.LeaveShop();
        }
    }
}
