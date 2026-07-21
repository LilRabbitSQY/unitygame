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

        private void Start()
        {
            shopManager = FindFirstObjectByType<ShopManager>();
            if (leaveButton != null)
                leaveButton.onClick.AddListener(OnLeave);

            BuildItemList();
            UpdateGold();
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
                img.color = new Color(0.3f, 0.5f, 0.3f, 1f);

                var btn = go.AddComponent<Button>();
                var le = go.AddComponent<LayoutElement>();
                le.minHeight = 70;
                le.preferredHeight = 70;

                var txtGo = new GameObject("Text");
                txtGo.transform.SetParent(go.transform, false);
                var rt = txtGo.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(5, 5);
                rt.offsetMax = new Vector2(-5, -5);
                var tmp = txtGo.AddComponent<TextMeshProUGUI>();
                tmp.text = $"{item.itemName} - {item.price}金\n{item.description}";
                tmp.fontSize = 13;
                tmp.alignment = TextAlignmentOptions.Center;

                var capturedItem = item;
                btn.onClick.AddListener(() => OnBuyItem(capturedItem));
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
