using UnityEngine;
using FinalDefense.Core;

namespace FinalDefense.Battle
{
    public class AIAgentSystem : MonoBehaviour
    {
        [SerializeField] private int maxUses = 3;
        [SerializeField] private int gpaCost = 10;
        [SerializeField] private int misconductGain = 25;

        private int usesRemaining;

        public int UsesRemaining => usesRemaining;
        public bool CanUse => usesRemaining > 0;

        private void Start()
        {
            usesRemaining = maxUses;
        }

        public bool ActivateAIAgent()
        {
            if (!CanUse) return false;

            var gm = GameManager.Instance;
            if (gm == null) return false;

            usesRemaining--;

            var enemies = Object.FindObjectsByType<Enemy.Enemy>(FindObjectsSortMode.None);
            foreach (var enemy in enemies)
            {
                var health = enemy.GetComponent<Enemy.EnemyHealth>();
                if (health != null) health.TakeDamage(99999);
            }

            gm.TakeGPADamage(gpaCost);
            gm.AddMisconduct(misconductGain);
            EventBus.AIAgentUsed();

            return true;
        }
    }
}
