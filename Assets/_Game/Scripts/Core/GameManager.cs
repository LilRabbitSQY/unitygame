using UnityEngine;
using FinalDefense.Data;
using FinalDefense.Combat;
using FinalDefense.Shop;
using System.Collections.Generic;
using System.Linq;

namespace FinalDefense.Core
{
    public enum CampaignPhase { Personality, Schedule, Battle, Result, Shop, Complete }

    public class GameManager : Singleton<GameManager>
    {
        [SerializeField] private PlayerStats playerStats;
        [SerializeField] private PersonalityConfig personalityConfig;

        public PlayerStats Stats => playerStats;
        public PersonalityConfig PersonalityConfigData => personalityConfig;

        private int gpaHundredths;
        public float CurrentGPA => gpaHundredths / 100f;
        public int CurrentGPAHundredths => gpaHundredths;
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

        public const int TotalDays = 28;
        private const string SaveKey = "FinalDefense.Campaign.v1";
        public CampaignPhase Phase { get; private set; } = CampaignPhase.Personality;
        public BattleReport LastBattleReport { get; private set; }
        public BattleConfiguration CurrentBattleConfiguration { get; private set; }
        public bool HasSavedGame => PlayerPrefs.HasKey(SaveKey);
        private bool dayWon;
        private bool initialized;
        private readonly Dictionary<string, int> battleInventory = new Dictionary<string, int>();
        private readonly Dictionary<string, int> purchasedItems = new Dictionary<string, int>();
        private bool battleItemsPrepared;
        private string[] equippedBattleItems = System.Array.Empty<string>();
        public string LastBattleDrop { get; private set; }
        public IReadOnlyList<string> NextBattleItems => battleItemsPrepared ? equippedBattleItems : BattleItemDefs.All.Where(item => BattleItemCount(item.key) > 0).Select(item => item.key).ToArray();

        public int CurrentSemester => Mathf.Clamp((CurrentDay - 1) / 3 + 1, 1, 8);
        public int CurrentRound => ((CurrentDay - 1) % 3) + 1;
        public int StatCap => CurrentGrade * 10 + 10;

        public int BattlesWon { get; private set; }
        public int BattlesLost { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this) return;
            if (personalityConfig == null) personalityConfig = ScriptableObject.CreateInstance<PersonalityConfig>();
            if (!initialized) InitializeStats();
        }

        public void InitializeStats()
        {
            if (playerStats != null)
            {
                gpaHundredths = playerStats.initialGPA * 100;
                CurrentGrade = playerStats.initialGrade;
                CurrentEmotion = playerStats.initialEmotion;
                CurrentStrength = playerStats.initialStrength;
                CurrentEduPower = playerStats.initialEduPower;
                CurrentDetermination = playerStats.initialDetermination;
            }
            else
            {
                gpaHundredths = 10000;
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
            CurrentPersonality = PersonalityType.NORM;
            PersonalitySelected = false;
            Phase = CampaignPhase.Personality;
            LastBattleReport = null;
            CurrentBattleConfiguration = null;
            dayWon = false;
            battleInventory.Clear(); purchasedItems.Clear();
            battleItemsPrepared = false; equippedBattleItems = System.Array.Empty<string>(); LastBattleDrop = null;
            initialized = true;
        }

        public void BeginNewGame()
        {
            InitializeStats();
            PlayerPrefs.DeleteKey(SaveKey);
            SaveProgress();
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
            Phase = CampaignPhase.Schedule;
            SaveProgress();
        }

        public void TakeGPADamage(int amount)
        {
            gpaHundredths = Mathf.Max(0, gpaHundredths - Mathf.Max(0, amount) * 100);
            if (CurrentGPA <= 0)
            {
                Phase = CampaignPhase.Complete;
                EventBus.BattleLost();
            }
            SaveProgress();
        }

        public void AddGPA(int amount)
        {
            gpaHundredths = Mathf.Clamp(gpaHundredths + amount * 100, 0, 10000);
            SaveProgress();
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
            SaveProgress();
        }

        public bool SpendActionPoint(int cost = 1)
        {
            if (cost < 0 || Phase != CampaignPhase.Schedule || ActionPoints < cost) return false;
            ActionPoints -= cost;
            SaveProgress();
            return true;
        }

        public void ResetActionPoints()
        {
            ActionPoints = MaxActionPoints;
            SaveProgress();
        }

        public void AdvanceDay()
        {
            if (IsGameComplete || Phase != CampaignPhase.Shop) return;
            if (CurrentDay >= TotalDays || CurrentGPA <= 0)
            {
                Phase = CampaignPhase.Complete;
                SaveProgress();
                return;
            }
            CurrentDay++;
            if (CurrentDay > 3 && (CurrentDay - 1) % 6 == 0) GradeUp();
            RetryCount = 0;
            dayWon = false;
            LastBattleReport = null;
            CurrentBattleConfiguration = null;
            battleItemsPrepared = false; equippedBattleItems = System.Array.Empty<string>(); LastBattleDrop = null;
            ActionPoints = MaxActionPoints;
            Phase = CampaignPhase.Schedule;
            SaveProgress();
        }

        public bool BeginBattle(BattleConfiguration configuration = null)
        {
            if (IsGameComplete || dayWon || Phase == CampaignPhase.Result || Phase == CampaignPhase.Shop) return false;
            // A new day's Schedule has no resumable battle, even if old JSON created an empty object for null.
            if (Phase == CampaignPhase.Schedule || Phase == CampaignPhase.Personality)
                CurrentBattleConfiguration = null;
            if (configuration != null) CurrentBattleConfiguration = configuration;
            LastBattleReport = null;
            Phase = CampaignPhase.Battle;
            SaveProgress();
            return true;
        }

        public bool CompleteBattle(BattleReport report)
        {
            if (report == null || IsGameComplete || dayWon || Phase != CampaignPhase.Battle) return false;
            LastBattleReport = report;
            // Current campaign uses one protection point lost = one integer GPA point.
            gpaHundredths = Mathf.Max(0, gpaHundredths - Mathf.Max(0, report.protectionLost) * 100);
            if (gpaHundredths > 0 && report.won)
                gpaHundredths = Mathf.Min(10000, gpaHundredths + (5 + Mathf.Max(0, report.bonusGpa)) * 100);
            LastBattleDrop = null;
            if (report.won)
            {
                BattlesWon++;
                dayWon = true;
                if (gpaHundredths > 0)
                {
                    Gold += Mathf.Max(0, report.goldEarned);
                    // Authored drop rates are not provided. Use a documented, reproducible daily rotation.
                    string drop = BattleItemDefs.All[(CurrentDay - 1) % 8].key;
                    if (BattleItemCount(drop) < 99)
                    { battleInventory[drop] = BattleItemCount(drop) + 1; LastBattleDrop = drop; }
                }
            }
            else BattlesLost++;
            Phase = CurrentGPA <= 0 ? CampaignPhase.Complete : CampaignPhase.Result;
            SaveProgress();
            return true;
        }

        public void RecordBattleResult(bool won) => CompleteBattle(new BattleReport { won = won });

        public bool TryRetryBattle()
        {
            if (IsGameComplete || Phase != CampaignPhase.Result || dayWon || LastBattleReport == null || LastBattleReport.won) return false;
            int price = RetryCount == 0 ? 0 : 5;
            if (CurrentGPA <= price) return false;
            gpaHundredths -= price * 100;
            RetryCount++;
            Phase = CampaignPhase.Battle;
            return BeginBattle();
        }

        public bool EnterShop()
        {
            if (IsGameComplete || LastBattleReport == null || (Phase != CampaignPhase.Result && Phase != CampaignPhase.Shop)) return false;
            Phase = CampaignPhase.Shop;
            SaveProgress();
            return true;
        }

        public bool FinishShopping()
        {
            if (Phase != CampaignPhase.Shop) return false;
            AdvanceDay();
            return true;
        }

        public int BattleItemCount(string key) => key != null && battleInventory.TryGetValue(key, out int count) ? count : 0;
        public int ShopStock(string key) => BattleItemDefs.Find(key)?.priceHundredths > 0 ? Mathf.Max(0, 99 - (purchasedItems.TryGetValue(key, out int count) ? count : 0)) : 0;
        public bool GrantBattleItem(string key, int count = 1)
        {
            if (BattleItemDefs.Find(key) == null || count <= 0 || BattleItemCount(key) >= 99) return false;
            battleInventory[key] = BattleItemCount(key) + Mathf.Min(99 - BattleItemCount(key), count);
            SaveProgress(); return true;
        }
        public bool BuyBattleItem(string key)
        {
            var item = BattleItemDefs.Find(key);
            if (Phase != CampaignPhase.Shop || IsGameComplete || item == null || item.priceHundredths <= 0
                || gpaHundredths < item.priceHundredths || ShopStock(key) <= 0 || BattleItemCount(key) >= 99) return false;
            gpaHundredths -= item.priceHundredths;
            battleInventory[key] = BattleItemCount(key) + 1;
            purchasedItems[key] = (purchasedItems.TryGetValue(key, out int purchased) ? purchased : 0) + 1;
            if (gpaHundredths <= 0) Phase = CampaignPhase.Complete;
            SaveProgress(); return true;
        }
        public void PrepareBattleItems(BattleConfiguration configuration)
        {
            if (configuration == null || Phase != CampaignPhase.Battle) return;
            if (!battleItemsPrepared)
            {
                equippedBattleItems = BattleItemDefs.All.Where(item => BattleItemCount(item.key) > 0).Select(item => item.key).ToArray();
                foreach (string key in equippedBattleItems) battleInventory[key]--;
                battleItemsPrepared = true;
            }
            configuration.battleItems = (string[])equippedBattleItems.Clone();
            CurrentBattleConfiguration = configuration;
            SaveProgress();
        }

        public void AddGold(int amount)
        {
            Gold = Mathf.Max(0, Gold + amount);
            SaveProgress();
        }

        public bool SpendGold(int amount)
        {
            if (amount < 0 || Gold < amount) return false;
            Gold -= amount;
            SaveProgress();
            return true;
        }

        public void IncrementRetry()
        {
            RetryCount++;
            SaveProgress();
        }

        public void GradeUp()
        {
            if (CurrentGrade < 4) CurrentGrade++;
        }

        public float GetEduPowerBonus() => CurrentEduPower * 0.01f;
        public float GetDeterminationPenalty() => CurrentDetermination * 0.015f;
        public float GetEmotionDebuffReduction() => CurrentEmotion * 0.02f;
        public float GetStrengthActionBonus() => CurrentStrength * 0.01f;

        public bool IsGameComplete => Phase == CampaignPhase.Complete || CurrentDay > TotalDays || CurrentGPA <= 0;

        public static bool IsUsableBattleConfiguration(BattleConfiguration configuration)
        {
            if (configuration == null || configuration.path == null || configuration.path.Any(cell => cell == null)
                || configuration.ground == null || configuration.ground.Any(cell => cell == null)
                || configuration.highGround == null || configuration.highGround.Any(cell => cell == null)
                || configuration.towers == null || configuration.towers.Length == 0 || configuration.towers.Any(unit => unit == null)
                || configuration.enemies == null || configuration.enemies.Length == 0 || configuration.enemies.Any(unit => unit == null)
                || configuration.groups == null || configuration.groups.Length == 0 || configuration.groups.Any(group => group == null))
                return false;
            try
            {
                BattleSimulation.ValidateConfiguration(configuration);
                return true;
            }
            catch (System.ArgumentException) { return false; }
        }

        [System.Serializable]
        private sealed class ProgressData
        {
            public int version = 2, gpa, gpaHundredths, grade, emotion, strength, eduPower, determination, actionPoints, day, gold, retries, won, lost;
            public bool personalitySelected, dayWon, hasReport, reportWon;
            public PersonalityType personality;
            public CampaignPhase phase;
            public int spawned, killed, leaked, protectionLost, bonusGpa, costSpent, costEarned, goldEarned, upgrades, deployed;
            public float duration, damage, healing;
            public BattleConfiguration battle;
            public string lastBattleDrop;
            public bool itemsPrepared;
            public string[] equippedItems;
            public ItemSave[] inventory;
        }
        [System.Serializable] private sealed class ItemSave { public string key; public int count, purchased; }

        public void SaveProgress()
        {
            if (!initialized) return;
            var r = LastBattleReport;
            var save = new ProgressData { gpa = Mathf.RoundToInt(CurrentGPA), gpaHundredths = gpaHundredths, grade = CurrentGrade, emotion = CurrentEmotion,
                strength = CurrentStrength, eduPower = CurrentEduPower, determination = CurrentDetermination,
                actionPoints = ActionPoints, day = CurrentDay, gold = Gold, retries = RetryCount,
                won = BattlesWon, lost = BattlesLost, personalitySelected = PersonalitySelected,
                personality = CurrentPersonality, phase = Phase, dayWon = dayWon, battle = CurrentBattleConfiguration,
                hasReport = r != null, reportWon = r != null && r.won,
                itemsPrepared = battleItemsPrepared, equippedItems = equippedBattleItems, lastBattleDrop = LastBattleDrop,
                inventory = BattleItemDefs.All.Select(item => new ItemSave { key = item.key, count = BattleItemCount(item.key), purchased = purchasedItems.TryGetValue(item.key, out int purchased) ? purchased : 0 }).ToArray(),
                spawned = r?.spawned ?? 0, killed = r?.killed ?? 0, leaked = r?.leaked ?? 0, protectionLost = r?.protectionLost ?? 0, bonusGpa = r?.bonusGpa ?? 0,
                costSpent = r?.costSpent ?? 0, costEarned = r?.costEarned ?? 0, goldEarned = r?.goldEarned ?? 0,
                upgrades = r?.upgrades ?? 0, deployed = r?.deployed ?? 0, duration = r?.duration ?? 0,
                damage = r?.damage ?? 0, healing = r?.healing ?? 0 };
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(save));
            PlayerPrefs.Save();
        }

        public bool LoadProgress()
        {
            if (!HasSavedGame) return false;
            ProgressData saved;
            try { saved = JsonUtility.FromJson<ProgressData>(PlayerPrefs.GetString(SaveKey)); }
            catch (System.ArgumentException) { return false; }
            if (saved == null || (saved.version != 1 && saved.version != 2) || saved.day < 1 || saved.day > TotalDays || saved.grade < 1 || saved.grade > 4
                || !System.Enum.IsDefined(typeof(CampaignPhase), saved.phase)
                || ((saved.phase == CampaignPhase.Result || saved.phase == CampaignPhase.Shop) && !saved.hasReport)) return false;
            gpaHundredths = Mathf.Clamp(saved.version == 1 ? saved.gpa * 100 : saved.gpaHundredths, 0, 10000); CurrentGrade = saved.grade;
            CurrentEmotion = Mathf.Clamp(saved.emotion, 0, StatCap); CurrentStrength = Mathf.Clamp(saved.strength, 0, StatCap);
            CurrentEduPower = Mathf.Clamp(saved.eduPower, 0, StatCap); CurrentDetermination = Mathf.Clamp(saved.determination, 0, StatCap);
            ActionPoints = Mathf.Clamp(saved.actionPoints, 0, MaxActionPoints); CurrentDay = saved.day;
            Gold = Mathf.Max(0, saved.gold); RetryCount = Mathf.Max(0, saved.retries);
            BattlesWon = Mathf.Max(0, saved.won); BattlesLost = Mathf.Max(0, saved.lost);
            PersonalitySelected = saved.personalitySelected; CurrentPersonality = saved.personality;
            Phase = saved.phase; dayWon = saved.dayWon;
            // JsonUtility serializes inline managed classes by value: a null field can return as
            // a non-null empty BattleConfiguration. Nullness alone is not a valid resume marker.
            bool resumablePhase = Phase == CampaignPhase.Battle || Phase == CampaignPhase.Result || Phase == CampaignPhase.Shop;
            CurrentBattleConfiguration = resumablePhase && IsUsableBattleConfiguration(saved.battle) ? saved.battle : null;
            battleItemsPrepared = saved.itemsPrepared;
            equippedBattleItems = (saved.equippedItems ?? System.Array.Empty<string>()).Where(key => BattleItemDefs.Find(key) != null).Distinct().ToArray();
            LastBattleDrop = saved.lastBattleDrop;
            battleInventory.Clear(); purchasedItems.Clear();
            foreach (var item in saved.inventory ?? System.Array.Empty<ItemSave>())
                if (item != null && BattleItemDefs.Find(item.key) != null)
                { battleInventory[item.key] = Mathf.Clamp(item.count, 0, 99); purchasedItems[item.key] = Mathf.Clamp(item.purchased, 0, 99); }
            LastBattleReport = saved.hasReport ? new BattleReport { won = saved.reportWon, spawned = saved.spawned,
                killed = saved.killed, leaked = saved.leaked, protectionLost = saved.protectionLost, bonusGpa = saved.bonusGpa, costSpent = saved.costSpent, costEarned = saved.costEarned,
                goldEarned = saved.goldEarned, upgrades = saved.upgrades, deployed = saved.deployed,
                duration = saved.duration, damage = saved.damage, healing = saved.healing } : null;
            return true;
        }
    }
}
