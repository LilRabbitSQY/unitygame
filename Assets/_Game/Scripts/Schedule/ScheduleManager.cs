using UnityEngine;
using FinalDefense.Core;
using FinalDefense.Data;

namespace FinalDefense.Schedule
{
    public class ScheduleManager : MonoBehaviour
    {
        [SerializeField] private ActivityData[] availableActivities;
        [SerializeField] private bool skipDialogue;

        public ActivityData[] AvailableActivities => availableActivities ?? System.Array.Empty<ActivityData>();
        private bool leaving;

        public bool ExecuteActivity(ActivityData activity)
        {
            var gm = GameManager.Instance;
            if (gm == null || activity == null || leaving || gm.IsGameComplete) return false;
            if (!gm.SpendActionPoint(activity.actPointCost)) return false;

            if (activity.emotionDelta != 0) gm.ModifyStat("emotion", activity.emotionDelta);
            if (activity.strengthDelta != 0) gm.ModifyStat("strength", activity.strengthDelta);
            if (activity.eduPowerDelta != 0) gm.ModifyStat("eduPower", activity.eduPowerDelta);
            if (activity.determinationDelta != 0) gm.ModifyStat("determination", activity.determinationDelta);

            return true;
        }

        public void FinishSchedule()
        {
            if (leaving || GameManager.Instance == null || !GameManager.Instance.BeginBattle()) return;
            leaving = true;
            EventBus.ScheduleCompleted();
            if (skipDialogue || !Application.CanStreamedLevelBeLoaded(SceneLoader.DialogueScene))
                SceneLoader.LoadBattle();
            else
                SceneLoader.LoadDialogue();
        }

        public void GoToBattle()
        {
            SceneLoader.LoadBattle();
        }
    }
}
