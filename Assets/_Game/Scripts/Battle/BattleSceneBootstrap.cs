using UnityEngine;
using FinalDefense.Core;
using FinalDefense.Data;
using FinalDefense.Tower;
using FinalDefense.NPC;

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
                gmGo.AddComponent<GameManager>();
            }

            if (BattleAnalytics.Instance == null)
            {
                var go = new GameObject("BattleAnalytics");
                go.AddComponent<BattleAnalytics>();
            }

            if (NPCRelationshipManager.Instance == null)
            {
                var go = new GameObject("NPCRelationshipManager");
                go.AddComponent<NPCRelationshipManager>();
            }

            if (FindFirstObjectByType<BattleManager>() == null)
            {
                var go = new GameObject("BattleManager");
                go.AddComponent<BattleManager>();
            }

            if (FindFirstObjectByType<DeployCostSystem>() == null)
            {
                var go = new GameObject("DeployCostSystem");
                go.AddComponent<DeployCostSystem>();
            }

            if (FindFirstObjectByType<AIAgentSystem>() == null)
            {
                var go = new GameObject("AIAgentSystem");
                go.AddComponent<AIAgentSystem>();
            }
        }
    }
}
