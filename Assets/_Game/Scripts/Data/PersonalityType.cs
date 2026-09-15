using UnityEngine;

namespace FinalDefense.Data
{
    public enum PersonalityType
    {
        LMAO,  // 乐子人
        ASAP,  // 打工人
        ZZZZ,  // 睡美人
        QWWQ,  // 求求人
        SSCI,  // 学术人
        COOK,  // 干饭人
        IDLE,  // 摸鱼人
        NORM   // 均衡人
    }

    [System.Serializable]
    public struct PersonalityStats
    {
        public PersonalityType type;
        public string displayName;
        public string description;
        public int emotion;
        public int strength;
        public int eduPower;
        public int determination;
    }

}
