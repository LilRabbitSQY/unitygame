using UnityEngine;
using FinalDefense.Core;

namespace FinalDefense.Battle
{
    public class RetrySystem : MonoBehaviour
    {
        public static bool LastBattleWon { get; set; }

        public bool CanFreeRetry()
        {
            var gm = GameManager.Instance;
            return gm != null && gm.RetryCount == 0;
        }

        public void Retry()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            if (!CanFreeRetry())
            {
                gm.TakeGPADamage(5);
            }
            gm.IncrementRetry();
            SceneLoader.LoadBattle();
        }
    }
}
