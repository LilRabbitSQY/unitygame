using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FinalDefense.Data;
using FinalDefense.Tower;

namespace FinalDefense.UI
{
    public class TowerButton : MonoBehaviour
    {
        [SerializeField] private TowerData towerData;
        [SerializeField] private TowerPlacement towerPlacement;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private Image iconImage;

        private Button button;

        private void Start()
        {
            button = GetComponent<Button>();
            if (button != null)
                button.onClick.AddListener(OnClicked);

            if (towerData != null)
            {
                if (nameText != null) nameText.text = towerData.towerName;
                if (costText != null) costText.text = $"${towerData.cost}";
                if (iconImage != null && towerData.icon != null)
                    iconImage.sprite = towerData.icon;
                else if (iconImage != null)
                    iconImage.color = towerData.towerColor;
            }
        }

        private void OnClicked()
        {
            if (towerPlacement != null && towerData != null)
            {
                towerPlacement.SelectTower(towerData);
            }
        }
    }
}
