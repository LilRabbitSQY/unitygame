using UnityEngine;
using FinalDefense.Core;
using FinalDefense.Data;

namespace FinalDefense.Schedule
{
    public class ScheduleManager : MonoBehaviour
    {
        [SerializeField] private ActivityData[] availableActivities;

        public ActivityData[] AvailableActivities => availableActivities;

        public bool ExecuteActivity(ActivityData activity)
        {
            var gm = GameManager.Instance;
            if (gm == null) return false;
            if (!gm.SpendActionPoint(activity.actPointCost)) return false;

            if (activity.emotionDelta != 0) gm.ModifyStat("emotion", activity.emotionDelta);
            if (activity.strengthDelta != 0) gm.ModifyStat("strength", activity.strengthDelta);
            if (activity.eduPowerDelta != 0) gm.ModifyStat("eduPower", activity.eduPowerDelta);
            if (activity.determinationDelta != 0) gm.ModifyStat("determination", activity.determinationDelta);

            return true;
        }

        public void FinishSchedule()
        {
            EventBus.ScheduleCompleted();
            SceneLoader.LoadBattle();
        }
    }
}
