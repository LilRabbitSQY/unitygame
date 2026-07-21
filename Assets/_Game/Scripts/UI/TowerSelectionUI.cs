using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FinalDefense.Data;
using FinalDefense.Tower;

namespace FinalDefense.UI
{
    public class TowerSelectionUI : MonoBehaviour
    {
        [SerializeField] private TowerData[] availableTowers;
        [SerializeField] private Transform buttonContainer;
        [SerializeField] private GameObject towerButtonPrefab;
        [SerializeField] private TowerPlacement towerPlacement;

        private void Start()
        {
            CreateButtons();
            ApplyTowerBarBackdrop();
        }

        private void CreateButtons()
        {
            if (availableTowers == null) return;

            foreach (var tower in availableTowers)
            {
                GameObject btnGo;
                Button btn;

                if (towerButtonPrefab != null)
                {
                    btnGo = Instantiate(towerButtonPrefab, buttonContainer);
                    btn = btnGo.GetComponent<Button>();
                    if (btn == null) btn = btnGo.AddComponent<Button>();
                }
                else
                {
                    btn = UIBootstrap.CreateButton($"TowerCard_{tower.towerName}", buttonContainer,
                        $"{tower.towerName}\n${tower.cost}", true);
                    btnGo = btn.gameObject;

                    var iconGo = UIBootstrap.CreateUIObject("Icon", btnGo.transform);
                    var iconImg = iconGo.AddComponent<Image>();
                    var iconRT = iconGo.GetComponent<RectTransform>();
                    iconRT.anchorMin = new Vector2(0, 0.5f);
                    iconRT.anchorMax = new Vector2(0, 0.5f);
                    iconRT.pivot = new Vector2(0, 0.5f);
                    iconRT.anchoredPosition = new Vector2(12, 0);
                    iconRT.sizeDelta = new Vector2(32, 32);
                }

                var text = btnGo.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null)
                {
                    text.text = $"{tower.towerName}\n${tower.cost}";
                    UIStyler.ApplyBodyTextStyle(text);
                    text.color = UITheme.Instance.foreground;
                    text.alignment = TextAlignmentOptions.Center;
                }

                var img = btnGo.transform.Find("Icon")?.GetComponent<Image>();
                if (img != null)
                {
                    if (tower.icon != null)
                        img.sprite = tower.icon;

                    img.color = GetTowerAccentColor(tower);
                }

                Color accentColor = GetTowerAccentColor(tower);
                UIStyler.ApplyTowerCardStyle(btnGo, accentColor);

                var towerRef = tower;
                btn.onClick.AddListener(() => OnTowerSelected(towerRef));
            }
        }

        private Color GetTowerAccentColor(TowerData tower)
        {
            if (tower.towerColor != Color.green && tower.towerColor != Color.white)
                return tower.towerColor;

            string nameLower = tower.towerName.ToLower();
            if (nameLower.Contains("笔记本") || nameLower.Contains("notebook"))
                return UITheme.Instance.towerNotebook;
            if (nameLower.Contains("计算器") || nameLower.Contains("calculator"))
                return UITheme.Instance.towerCalculator;
            if (nameLower.Contains("咖啡") || nameLower.Contains("coffee"))
                return UITheme.Instance.towerCoffeeCup;

            return UITheme.Instance.primary;
        }

        private void ApplyTowerBarBackdrop()
        {
            var containerRect = buttonContainer as RectTransform;
            if (containerRect == null || containerRect.parent == null) return;

            const string backdropName = "TowerBarIntegratedCard";
            var existing = containerRect.parent.Find(backdropName);
            GameObject backdropGo;

            if (existing != null)
            {
                backdropGo = existing.gameObject;
            }
            else
            {
                backdropGo = new GameObject(backdropName, typeof(RectTransform), typeof(Image));
                backdropGo.transform.SetParent(containerRect.parent, false);
                backdropGo.transform.SetSiblingIndex(Mathf.Max(0, containerRect.GetSiblingIndex()));
            }

            var backdropRect = backdropGo.GetComponent<RectTransform>();
            backdropRect.anchorMin = containerRect.anchorMin;
            backdropRect.anchorMax = containerRect.anchorMax;
            backdropRect.pivot = containerRect.pivot;
            backdropRect.anchoredPosition = containerRect.anchoredPosition;
            backdropRect.sizeDelta = new Vector2(
                Mathf.Max(Mathf.Abs(containerRect.sizeDelta.x) + 44f, 420f),
                Mathf.Max(Mathf.Abs(containerRect.sizeDelta.y) + 24f, 76f));

            UIStyler.ApplyMutedPanelStyle(backdropGo);

            var image = backdropGo.GetComponent<Image>();
            if (image != null)
            {
                image.color = Color.Lerp(UITheme.Instance.accent, Color.white, 0.45f);
                image.raycastTarget = false;
            }
        }

        private void OnTowerSelected(TowerData tower)
        {
            if (towerPlacement != null)
            {
                towerPlacement.SelectTower(tower);
            }
        }
    }
}
