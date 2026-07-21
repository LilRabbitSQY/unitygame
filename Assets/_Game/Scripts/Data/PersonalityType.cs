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

    [CreateAssetMenu(menuName = "FinalDefense/Personality Config")]
    public class PersonalityConfig : ScriptableObject
    {
        public PersonalityStats[] personalities = new PersonalityStats[]
        {
            new PersonalityStats { type = PersonalityType.LMAO, displayName = "乐子人", description = "上大学是为了啥？图个乐子！", emotion = 17, strength = 13, eduPower = 7, determination = 9 },
            new PersonalityStats { type = PersonalityType.ASAP, displayName = "打工人", description = "好的老师，晚上给您发过去。", emotion = 9, strength = 9, eduPower = 13, determination = 11 },
            new PersonalityStats { type = PersonalityType.ZZZZ, displayName = "睡美人", description = "早八？让我再睡一会……", emotion = 13, strength = 17, eduPower = 3, determination = 3 },
            new PersonalityStats { type = PersonalityType.QWWQ, displayName = "求求人", description = "教授，求求，捞捞，呜呜……", emotion = 11, strength = 11, eduPower = 7, determination = 7 },
            new PersonalityStats { type = PersonalityType.SSCI, displayName = "学术人", description = "师兄，为什么你的实验结果没法复现？", emotion = 11, strength = 9, eduPower = 17, determination = 15 },
            new PersonalityStats { type = PersonalityType.COOK, displayName = "干饭人", description = "上课好累，买份麦当劳安慰下自己。", emotion = 15, strength = 15, eduPower = 7, determination = 9 },
            new PersonalityStats { type = PersonalityType.IDLE, displayName = "摸鱼人", description = "最后一排那位同学，你打音游把前桌震醒了。", emotion = 15, strength = 15, eduPower = 7, determination = 9 },
            new PersonalityStats { type = PersonalityType.NORM, displayName = "均衡人", description = "一手抓绩点一手抓论文一手抓实习……你只有两只手？", emotion = 10, strength = 10, eduPower = 10, determination = 10 },
        };

        public PersonalityStats GetStats(PersonalityType type)
        {
            foreach (var p in personalities)
            {
                if (p.type == type) return p;
            }
            return personalities[personalities.Length - 1];
        }
    }
}
