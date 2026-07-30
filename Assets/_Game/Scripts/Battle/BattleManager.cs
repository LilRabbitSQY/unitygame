using UnityEngine;
using FinalDefense.Core;
using FinalDefense.NPC;

namespace FinalDefense.Battle
{
    public class BattleManager : MonoBehaviour
    {
        private bool battleEnded;
        private int goldEarned;
        private int totalEnemies;
        private int killedEnemies;
        private bool halftimeShown;

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

        public void SetTotalEnemies(int count)
        {
            totalEnemies = count;
        }

        private void OnEnemyReachedEnd(int gpaDamage)
        {
            if (battleEnded) return;
            killedEnemies++;
            if (GameManager.Instance != null)
                GameManager.Instance.TakeGPADamage(gpaDamage);
            CheckHalftime();
        }

        private void OnEnemyKilled(int reward)
        {
            goldEarned += reward;
            killedEnemies++;
            CheckHalftime();
        }

        private void CheckHalftime()
        {
            if (halftimeShown || totalEnemies <= 0) return;
            if (killedEnemies >= totalEnemies / 2)
            {
                halftimeShown = true;
                TriggerHalftimeJudgment();
            }
        }

        private void TriggerHalftimeJudgment()
        {
            var analytics = BattleAnalytics.Instance;
            if (analytics == null) return;

            string report = analytics.GenerateHalftimeReport();
            EventBus.ShowHalftimeReport(report);
            EventBus.HalftimeReached();
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
            var analytics = BattleAnalytics.Instance;
            if (analytics != null)
                analytics.FinalizeBattle();

            var gm = GameManager.Instance;
            if (gm == null)
            {
                Invoke(nameof(GoToResult), 2f);
                return;
            }

            gm.RecordBattleResult(victory);

            if (victory)
            {
                gm.AddGPA(5);
                gm.AddGold(goldEarned);
            }

            var npcMgr = NPCRelationshipManager.Instance;
            if (npcMgr != null)
                npcMgr.OnBattleResult(victory);

            Invoke(nameof(GoToResult), 2f);
        }

        private void GoToResult()
        {
            SceneLoader.LoadResult();
        }
    }
}
