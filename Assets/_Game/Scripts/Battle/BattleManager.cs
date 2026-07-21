using UnityEngine;
using FinalDefense.Core;
using FinalDefense.DDL;

namespace FinalDefense.Battle
{
    public class BattleManager : MonoBehaviour
    {
        private bool battleEnded;
        private int goldEarned;

        private void OnEnable()
        {
            EventBus.OnEnemyReachedEnd += OnEnemyReachedEnd;
            EventBus.OnEnemyKilled += OnEnemyKilled;
            EventBus.OnBattleLost += OnBattleLost;
            EventBus.OnAllWavesCompleted += OnAllWavesCleared;
        }

        private void OnDisable()
        {
            EventBus.OnEnemyReachedEnd -= OnEnemyReachedEnd;
            EventBus.OnEnemyKilled -= OnEnemyKilled;
            EventBus.OnBattleLost -= OnBattleLost;
            EventBus.OnAllWavesCompleted -= OnAllWavesCleared;
        }

        private void OnEnemyReachedEnd(int gpaDamage)
        {
            if (battleEnded) return;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.TakeGPADamage(gpaDamage);
            }
        }

        private void OnEnemyKilled(int reward)
        {
            goldEarned += reward;
        }

        private void OnBattleLost()
        {
            if (battleEnded) return;
            battleEnded = true;
            EndBattle(false);
        }

        private void OnAllWavesCleared()
        {
            if (battleEnded) return;
            battleEnded = true;
            EventBus.BattleWon();
            EndBattle(true);
        }

        private void EndBattle(bool victory)
        {
            var gm = GameManager.Instance;
            if (gm == null)
            {
                Invoke(nameof(GoToResult), 2f);
                return;
            }

            if (victory)
            {
                gm.AddGPA(5);
                gm.AddGold(goldEarned);
                if (DDLManager.Instance != null)
                    DDLManager.Instance.OnBattleWon();
            }

            Invoke(nameof(GoToResult), 2f);
        }

        private void GoToResult()
        {
            SceneLoader.LoadResult();
        }
    }
}
