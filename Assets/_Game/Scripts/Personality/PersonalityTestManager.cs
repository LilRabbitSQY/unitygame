using UnityEngine;
using FinalDefense.Core;
using FinalDefense.Data;

namespace FinalDefense.Personality
{
    public class PersonalityTestManager : MonoBehaviour
    {
        [System.Serializable]
        public struct Question
        {
            public string questionText;
            public string[] options; // 4 options
            public int[] emotionScores;
            public int[] strengthScores;
            public int[] eduPowerScores;
            public int[] determinationScores;
        }

        private Question[] questions;
        private int currentQuestion;
        private int totalEmotion;
        private int totalStrength;
        private int totalEduPower;
        private int totalDetermination;

        public int CurrentQuestionIndex => currentQuestion;
        public int TotalQuestions => questions.Length;
        public Question CurrentQuestion => questions[currentQuestion];
        public bool IsComplete => currentQuestion >= questions.Length;

        private void Awake()
        {
            InitializeQuestions();
        }

        private void InitializeQuestions()
        {
            questions = new Question[]
            {
                new Question {
                    questionText = "期末周的早晨，闹钟响了，你的第一反应是？",
                    options = new[] { "关掉继续睡", "立刻起床去图书馆", "看看群里有没有人签到", "订外卖犒劳自己" },
                    emotionScores = new[] { 2, 0, 1, 3 },
                    strengthScores = new[] { 3, 1, 1, 2 },
                    eduPowerScores = new[] { 0, 3, 2, 0 },
                    determinationScores = new[] { 0, 3, 1, 1 }
                },
                new Question {
                    questionText = "DDL还有三天，你现在会？",
                    options = new[] { "先打把游戏放松一下", "立刻开始写", "找同学要参考资料", "制定详细计划表" },
                    emotionScores = new[] { 3, 0, 2, 1 },
                    strengthScores = new[] { 2, 2, 1, 1 },
                    eduPowerScores = new[] { 0, 2, 2, 3 },
                    determinationScores = new[] { 0, 3, 1, 3 }
                },
                new Question {
                    questionText = "考试前一晚，你通常在？",
                    options = new[] { "已经睡了", "通宵复习", "和室友互相抽问", "焦虑地刷手机" },
                    emotionScores = new[] { 2, 0, 2, 1 },
                    strengthScores = new[] { 3, 0, 1, 1 },
                    eduPowerScores = new[] { 1, 3, 2, 0 },
                    determinationScores = new[] { 0, 3, 2, 0 }
                },
                new Question {
                    questionText = "遇到不会的题，你的反应是？",
                    options = new[] { "空着不写", "死磕到底", "问ChatGPT", "去找教授答疑" },
                    emotionScores = new[] { 2, 0, 2, 1 },
                    strengthScores = new[] { 2, 1, 2, 1 },
                    eduPowerScores = new[] { 0, 2, 2, 3 },
                    determinationScores = new[] { 0, 3, 1, 2 }
                },
                new Question {
                    questionText = "小组作业中，你通常担任什么角色？",
                    options = new[] { "划水摸鱼", "全权负责", "协调沟通", "专做PPT" },
                    emotionScores = new[] { 3, 0, 2, 2 },
                    strengthScores = new[] { 2, 0, 2, 2 },
                    eduPowerScores = new[] { 0, 3, 1, 2 },
                    determinationScores = new[] { 0, 3, 2, 1 }
                },
                new Question {
                    questionText = "拿到一个很低的分数，你会？",
                    options = new[] { "无所谓，及格就行", "痛定思痛猛学", "去找教授捞分", "发朋友圈吐槽" },
                    emotionScores = new[] { 3, 0, 1, 3 },
                    strengthScores = new[] { 2, 1, 1, 2 },
                    eduPowerScores = new[] { 0, 3, 1, 0 },
                    determinationScores = new[] { 0, 3, 2, 0 }
                },
                new Question {
                    questionText = "周末时光，你首选？",
                    options = new[] { "睡到自然醒", "去健身房", "自习室学一天", "和朋友出去吃好吃的" },
                    emotionScores = new[] { 2, 1, 0, 3 },
                    strengthScores = new[] { 3, 3, 0, 2 },
                    eduPowerScores = new[] { 0, 0, 3, 0 },
                    determinationScores = new[] { 0, 2, 3, 1 }
                },
                new Question {
                    questionText = "图书馆没位置了，你会？",
                    options = new[] { "回宿舍学", "去咖啡店", "干脆不学了", "等别人走" },
                    emotionScores = new[] { 1, 2, 3, 0 },
                    strengthScores = new[] { 1, 2, 3, 1 },
                    eduPowerScores = new[] { 2, 2, 0, 2 },
                    determinationScores = new[] { 2, 2, 0, 3 }
                },
                new Question {
                    questionText = "你对「卷」这个词的态度是？",
                    options = new[] { "离我远点", "适度即可", "不卷怎么活", "卷别人不如卷自己" },
                    emotionScores = new[] { 3, 2, 0, 1 },
                    strengthScores = new[] { 2, 2, 0, 2 },
                    eduPowerScores = new[] { 0, 1, 3, 2 },
                    determinationScores = new[] { 0, 1, 3, 2 }
                },
                new Question {
                    questionText = "毕业后最想做的事？",
                    options = new[] { "Gap year 游世界", "立刻找工作", "继续深造读研", "先躺一个月再说" },
                    emotionScores = new[] { 3, 1, 0, 3 },
                    strengthScores = new[] { 2, 2, 0, 3 },
                    eduPowerScores = new[] { 0, 1, 3, 0 },
                    determinationScores = new[] { 1, 3, 3, 0 }
                }
            };
        }

        public void AnswerQuestion(int optionIndex)
        {
            if (IsComplete) return;
            var q = questions[currentQuestion];
            totalEmotion += q.emotionScores[optionIndex];
            totalStrength += q.strengthScores[optionIndex];
            totalEduPower += q.eduPowerScores[optionIndex];
            totalDetermination += q.determinationScores[optionIndex];
            currentQuestion++;
        }

        public PersonalityType GetResult()
        {
            int maxEmo = totalEmotion;
            int maxStr = totalStrength;
            int maxEdu = totalEduPower;
            int maxDet = totalDetermination;

            if (maxStr >= maxEmo && maxStr >= maxEdu && maxStr >= maxDet)
            {
                if (maxEmo > maxEdu) return PersonalityType.ZZZZ;
                return PersonalityType.COOK;
            }
            if (maxEmo >= maxStr && maxEmo >= maxEdu && maxEmo >= maxDet)
            {
                if (maxStr > maxEdu) return PersonalityType.IDLE;
                return PersonalityType.LMAO;
            }
            if (maxEdu >= maxEmo && maxEdu >= maxStr && maxEdu >= maxDet)
            {
                if (maxDet > maxEmo) return PersonalityType.SSCI;
                return PersonalityType.ASAP;
            }
            if (maxDet >= maxEmo && maxDet >= maxStr && maxDet >= maxEdu)
            {
                return PersonalityType.SSCI;
            }

            int total = maxEmo + maxStr + maxEdu + maxDet;
            float avg = total / 4f;
            bool balanced = Mathf.Abs(maxEmo - avg) < 3 && Mathf.Abs(maxStr - avg) < 3;
            if (balanced) return PersonalityType.NORM;

            return PersonalityType.QWWQ;
        }
    }
}
