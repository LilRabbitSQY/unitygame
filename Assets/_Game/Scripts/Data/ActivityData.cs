using UnityEngine;

namespace FinalDefense.Data
{
    [CreateAssetMenu(menuName = "FinalDefense/Activity Data")]
    public class ActivityData : ScriptableObject
    {
        public string activityName;
        public string description;
        public int actPointCost = 1;
        public int emotionDelta;
        public int strengthDelta;
        public int eduPowerDelta;
        public int determinationDelta;
    }
}
