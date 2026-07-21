using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FinalDefense.Core;
using FinalDefense.Enemy;
using FinalDefense.Battle;

namespace FinalDefense.UI
{
    public class BattleHUD : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private TextMeshProUGUI gpaText;
        [SerializeField] private TextMeshProUGUI waveText;
        [SerializeField] private Button speedButton;

        private bool doubleSpeed;
        private EnemySpawner spawner;

        private void Start()
        {
            spawner = FindFirstObjectByType<EnemySpawner>();
            EventBus.OnCostChanged += UpdateCost;
            EventBus.OnEnemyReachedEnd += OnEnemyReached;
            EventBus.OnWaveCompleted += UpdateWave;

            if (speedButton != null)
                speedButton.onClick.AddListener(ToggleSpeed);

            UpdateGPA();
            UpdateWave();
        }

        private void OnDestroy()
        {
            EventBus.OnCostChanged -= UpdateCost;
            EventBus.OnEnemyReachedEnd -= OnEnemyReached;
            EventBus.OnWaveCompleted -= UpdateWave;
        }

        private void OnEnemyReached(int _) => UpdateGPA();

        private void UpdateCost(int current, int max)
        {
            if (costText != null) costText.text = $"费用: {current}/{max}";
        }

        private void UpdateGPA()
        {
            if (gpaText != null && GameManager.Instance != null)
                gpaText.text = $"GPA: {GameManager.Instance.CurrentGPA}";
        }

        private void UpdateWave()
        {
            if (waveText != null && spawner != null)
                waveText.text = $"波次: {spawner.CurrentWave}/{spawner.TotalWaves}";
        }

        private void ToggleSpeed()
        {
            doubleSpeed = !doubleSpeed;
            Time.timeScale = doubleSpeed ? 2f : 1f;
            if (speedButton != null)
            {
                var txt = speedButton.GetComponentInChildren<TextMeshProUGUI>();
                if (txt != null) txt.text = doubleSpeed ? "2x" : "1x";
            }
        }
    }
}
