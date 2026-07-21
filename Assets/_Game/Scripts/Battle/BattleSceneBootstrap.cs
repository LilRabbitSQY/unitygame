using UnityEngine;
using UnityEngine.Tilemaps;
using FinalDefense.Core;
using FinalDefense.Data;
using FinalDefense.Tower;

namespace FinalDefense.Battle
{
    public class BattleSceneBootstrap : MonoBehaviour
    {
        [SerializeField] private TowerData[] availableTowers;
        [SerializeField] private TowerPlacement towerPlacement;

        private void Start()
        {
            if (GameManager.Instance == null)
            {
                var gmGo = new GameObject("GameManager");
                var gm = gmGo.AddComponent<GameManager>();
            }
        }
    }
}
