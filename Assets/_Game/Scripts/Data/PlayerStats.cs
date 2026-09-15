using UnityEngine;

namespace FinalDefense.Data
{
    [CreateAssetMenu(menuName = "FinalDefense/Player Stats")]
    public class PlayerStats : ScriptableObject
    {
        [Header("GPA")]
        public int initialGPA = 70;

        [Header("等级")]
        public int initialGrade = 1;

        [Header("属性初始值")]
        public int initialEmotion = 10;
        public int initialStrength = 10;
        public int initialEduPower = 10;
        public int initialDetermination = 5;
    }
}
