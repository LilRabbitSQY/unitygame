using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FinalDefense.Core;

namespace FinalDefense.UI
{
    public class HalftimePanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TextMeshProUGUI reportText;
        [SerializeField] private Button continueButton;

        private void OnEnable()
        {
            EventBus.OnHalftimeReport += ShowReport;
            if (continueButton != null)
                continueButton.onClick.AddListener(HidePanel);
            if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        private void OnDisable()
        {
            EventBus.OnHalftimeReport -= ShowReport;
        }

        private void ShowReport(string report)
        {
            if (panelRoot != null) panelRoot.SetActive(true);
            if (reportText != null) reportText.text = report;
            Time.timeScale = 0f;
        }

        private void HidePanel()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            Time.timeScale = 1f;
        }
    }
}
