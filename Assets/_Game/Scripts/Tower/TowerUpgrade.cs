using UnityEngine;
using FinalDefense.Core;
using FinalDefense.Data;
using FinalDefense.Battle;

namespace FinalDefense.Tower
{
    public class TowerUpgrade : MonoBehaviour
    {
        private Tower tower;
        private TowerData data;

        private void Awake()
        {
            tower = GetComponent<Tower>();
        }

        public bool CanUpgrade()
        {
            if (tower == null || tower.Data == null) return false;
            if (tower.CurrentLevel >= tower.Data.maxLevel) return false;

            int sameTypeCount = 0;
            var allTowers = Object.FindObjectsByType<Tower>(FindObjectsSortMode.None);
            foreach (var t in allTowers)
            {
                if (t == tower) continue;
                if (t.Data == tower.Data && t.CurrentLevel == tower.CurrentLevel)
                    sameTypeCount++;
            }
            return sameTypeCount >= 1;
        }

        public bool TryUpgrade()
        {
            if (!CanUpgrade()) return false;

            var costSystem = Object.FindFirstObjectByType<DeployCostSystem>();
            if (costSystem != null && !costSystem.TrySpend(tower.Data.deployCost)) return false;

            var allTowers = Object.FindObjectsByType<Tower>(FindObjectsSortMode.None);
            foreach (var t in allTowers)
            {
                if (t == tower) continue;
                if (t.Data == tower.Data && t.CurrentLevel == tower.CurrentLevel)
                {
                    Object.Destroy(t.gameObject);
                    break;
                }
            }

            tower.LevelUp();
            UpdateVisuals();
            return true;
        }

        private void UpdateVisuals()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                float scale = 0.7f + (tower.CurrentLevel - 1) * 0.15f;
                transform.localScale = Vector3.one * scale;
            }
        }
    }
}
