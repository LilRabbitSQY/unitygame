using UnityEngine;
using FinalDefense.Data;

namespace FinalDefense.Core
{
    public class GameManager : Singleton<GameManager>
    {
        [SerializeField] private PlayerStats playerStats;
        [SerializeField] private PersonalityConfig personalityConfig;

        public PlayerStats Stats => playerStats;
        public PersonalityConfig PersonalityConfigData => personalityConfig;

        public int CurrentGPA { get; private set; }
        public int CurrentGrade { get; private set; }
        public int CurrentEmotion { get; private set; }
        public int CurrentStrength { get; private set; }
        public int CurrentEduPower { get; private set; }
        public int CurrentDetermination { get; private set; }
        public int ActionPoints { get; private set; }
        public int MaxActionPoints => 4 + CurrentGrade;
        public PersonalityType CurrentPersonality { get; private set; }
        public bool PersonalitySelected { get; private set; }
        public int CurrentDay { get; private set; } = 1;
        public int Gold { get; private set; }
        public int RetryCount { get; private set; }

        public int CurrentSemester => Mathf.Clamp((CurrentDay - 1) / 3 + 1, 1, 8);
        public int CurrentRound => ((CurrentDay - 1) % 3) + 1;
        public int StatCap => CurrentGrade * 10 + 10;

        public int BattlesWon { get; private set; }
        public int BattlesLost { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            if (!PersonalitySelected)
                InitializeStats();
        }

        public void InitializeStats()
        {
            if (playerStats != null)
            {
                CurrentGPA = playerStats.initialGPA;
                CurrentGrade = playerStats.initialGrade;
                CurrentEmotion = playerStats.initialEmotion;
                CurrentStrength = playerStats.initialStrength;
                CurrentEduPower = playerStats.initialEduPower;
                CurrentDetermination = playerStats.initialDetermination;
            }
            else
            {
                CurrentGPA = 100;
                CurrentGrade = 1;
                CurrentEmotion = 10;
                CurrentStrength = 10;
                CurrentEduPower = 10;
                CurrentDetermination = 5;
            }
            ActionPoints = MaxActionPoints;
            CurrentDay = 1;
            Gold = 0;
            RetryCount = 0;
            BattlesWon = 0;
            BattlesLost = 0;
        }

        public void SetPersonality(PersonalityType type)
        {
            CurrentPersonality = type;
            PersonalitySelected = true;
            if (personalityConfig != null)
            {
                var stats = personalityConfig.GetStats(type);
                CurrentEmotion = Mathf.Min(stats.emotion, StatCap);
                CurrentStrength = Mathf.Min(stats.strength, StatCap);
                CurrentEduPower = Mathf.Min(stats.eduPower, StatCap);
                CurrentDetermination = Mathf.Min(stats.determination, StatCap);
            }
        }

        public void TakeGPADamage(int amount)
        {
            CurrentGPA = Mathf.Max(0, CurrentGPA - amount);
            if (CurrentGPA <= 0)
            {
                EventBus.BattleLost();
            }
        }

        public void AddGPA(int amount)
        {
            CurrentGPA = Mathf.Min(100, CurrentGPA + amount);
        }

        public void ModifyStat(string stat, int delta)
        {
            int cap = StatCap;
            switch (stat)
            {
                case "emotion":
                    CurrentEmotion = Mathf.Clamp(CurrentEmotion + delta, 0, cap);
                    break;
                case "strength":
                    CurrentStrength = Mathf.Clamp(CurrentStrength + delta, 0, cap);
                    break;
                case "eduPower":
                    CurrentEduPower = Mathf.Clamp(CurrentEduPower + delta, 0, cap);
                    break;
                case "determination":
                    CurrentDetermination = Mathf.Clamp(CurrentDetermination + delta, 0, cap);
                    break;
            }
        }

        public bool SpendActionPoint(int cost = 1)
        {
            if (ActionPoints < cost) return false;
            ActionPoints -= cost;
            return true;
        }

        public void ResetActionPoints()
        {
            ActionPoints = MaxActionPoints;
        }

        public void AdvanceDay()
        {
            CurrentDay++;
            if (CurrentDay > 3 && (CurrentDay - 1) % 6 == 0)
            {
                GradeUp();
            }
        }

        public void RecordBattleResult(bool won)
        {
            if (won) BattlesWon++;
            else BattlesLost++;
        }

        public void AddGold(int amount)
        {
            Gold += amount;
        }

        public bool SpendGold(int amount)
        {
            if (Gold < amount) return false;
            Gold -= amount;
            return true;
        }

        public void IncrementRetry()
        {
            RetryCount++;
        }

        public void GradeUp()
        {
            if (CurrentGrade < 4) CurrentGrade++;
        }

        public float GetEduPowerBonus() => CurrentEduPower * 0.01f;
        public float GetDeterminationPenalty() => CurrentDetermination * 0.015f;
        public float GetEmotionDebuffReduction() => CurrentEmotion * 0.02f;
        public float GetStrengthActionBonus() => CurrentStrength * 0.01f;

        public bool IsGameComplete => CurrentSemester > 8;
    }
}
