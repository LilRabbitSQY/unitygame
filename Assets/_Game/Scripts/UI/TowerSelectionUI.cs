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
        }

        private void CreateButtons()
        {
            if (availableTowers == null || towerButtonPrefab == null) return;

            foreach (var tower in availableTowers)
            {
                var btnGo = Instantiate(towerButtonPrefab, buttonContainer);
                var btn = btnGo.GetComponent<Button>();
                var text = btnGo.GetComponentInChildren<TextMeshProUGUI>();

                if (text != null)
                    text.text = $"{tower.towerName}\n${tower.cost}";

                var img = btnGo.transform.Find("Icon")?.GetComponent<Image>();
                if (img != null && tower.icon != null)
                    img.sprite = tower.icon;

                var towerRef = tower;
                btn.onClick.AddListener(() => OnTowerSelected(towerRef));
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
