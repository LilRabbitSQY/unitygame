using System;
using System.Linq;
using FinalDefense.Combat;
using FinalDefense.Core;
using FinalDefense.Data;
using NUnit.Framework;
using UnityEngine;

namespace FinalDefense.Tests
{
    public sealed class CampaignStateTests
    {
        private const string SaveKey = "FinalDefense.Campaign.v1";
        private bool hadSave;
        private string savedProgress;
        private GameManager manager;
        private PersonalityConfig[] existingConfigs;

        [SetUp]
        public void SetUp()
        {
            hadSave = PlayerPrefs.HasKey(SaveKey);
            savedProgress = hadSave ? PlayerPrefs.GetString(SaveKey) : null;
            existingConfigs = Resources.FindObjectsOfTypeAll<PersonalityConfig>();
            try
            {
                foreach (var old in Resources.FindObjectsOfTypeAll<GameManager>())
                    if (old.gameObject.scene.IsValid()) UnityEngine.Object.DestroyImmediate(old.gameObject);
                PlayerPrefs.DeleteKey(SaveKey);
                manager = new GameObject("Campaign State Test Manager").AddComponent<GameManager>();
                // EditMode need not invoke a component's Awake. Initialize the real production object.
                manager.BeginNewGame();
                manager.SetPersonality(PersonalityType.NORM);
            }
            catch
            {
                CleanUpAndRestoreSave();
                throw;
            }
        }

        [TearDown]
        public void TearDown() => CleanUpAndRestoreSave();

        private void CleanUpAndRestoreSave()
        {
            try
            {
                if (manager != null) UnityEngine.Object.DestroyImmediate(manager.gameObject);
                manager = null;
                foreach (var config in Resources.FindObjectsOfTypeAll<PersonalityConfig>())
                    if (existingConfigs != null && !existingConfigs.Contains(config))
                        UnityEngine.Object.DestroyImmediate(config);
            }
            finally
            {
                if (hadSave) PlayerPrefs.SetString(SaveKey, savedProgress);
                else PlayerPrefs.DeleteKey(SaveKey);
                PlayerPrefs.Save();
            }
        }

        private void OpenShop()
        {
            Assert.That(manager.BeginBattle(LoadBattleConfiguration("Inventory test")), Is.True);
            Assert.That(manager.CompleteBattle(new BattleReport { won = false }), Is.True);
            Assert.That(manager.EnterShop(), Is.True);
        }

        private static BattleConfiguration LoadBattleConfiguration(string title = "Persistence test")
        {
            var configuration = JsonUtility.FromJson<BattleConfiguration>(Resources.Load<TextAsset>("Battle/Level1").text);
            configuration.towers = JsonUtility.FromJson<UnitCatalog>(Resources.Load<TextAsset>("Battle/Towers").text).units;
            configuration.title = title;
            return configuration;
        }

        private void RecreateManagerAndLoadProgress()
        {
            UnityEngine.Object.DestroyImmediate(manager.gameObject);
            manager = new GameObject("Cold Restored Campaign Manager").AddComponent<GameManager>();
            manager.InitializeStats();
            Assert.That(manager.LoadProgress(), Is.True, "A newly constructed manager must restore the saved campaign");
        }

        [Test]
        public void RepeatedOneTenthGpaPurchasesKeepExactHundredthsAndExhaustStock()
        {
            OpenShop();
            for (int purchase = 1; purchase <= 99; purchase++)
            {
                Assert.That(manager.BuyBattleItem("sparkling_focus"), Is.True, "Purchase " + purchase);
                Assert.That(manager.CurrentGPAHundredths, Is.EqualTo(10000 - purchase * 10));
                Assert.That(manager.CurrentGPA, Is.EqualTo((10000 - purchase * 10) / 100f));
                Assert.That(manager.BattleItemCount("sparkling_focus"), Is.EqualTo(purchase));
                Assert.That(manager.ShopStock("sparkling_focus"), Is.EqualTo(99 - purchase));
            }
            Assert.That(manager.BuyBattleItem("sparkling_focus"), Is.False);
            Assert.That(manager.CurrentGPAHundredths, Is.EqualTo(9010));
            Assert.That(manager.FinishShopping(), Is.True);
            var next = LoadBattleConfiguration();
            Assert.That(manager.BeginBattle(next), Is.True);
            manager.PrepareBattleItems(next);
            Assert.That(manager.BattleItemCount("sparkling_focus"), Is.EqualTo(98));
            Assert.That(manager.CompleteBattle(new BattleReport { won = false }), Is.True);
            Assert.That(manager.EnterShop(), Is.True);
            Assert.That(manager.BuyBattleItem("sparkling_focus"), Is.False, "Sold-out stock stays empty after inventory consumption");
        }

        [Test]
        public void InventoryCapsAtNinetyNineAndRejectsInvalidOrUnpayablePurchases()
        {
            Assert.That(manager.GrantBattleItem("espresso_focus", 1000), Is.True);
            Assert.That(manager.BattleItemCount("espresso_focus"), Is.EqualTo(99));
            Assert.That(manager.GrantBattleItem("espresso_focus"), Is.False);
            Assert.That(manager.GrantBattleItem("makeup_sample", 1), Is.True);
            Assert.That(manager.GrantBattleItem("makeup_sample", int.MaxValue), Is.True);
            Assert.That(manager.BattleItemCount("makeup_sample"), Is.EqualTo(99));
            Assert.That(manager.GrantBattleItem("unknown"), Is.False);
            Assert.That(manager.GrantBattleItem("gentle_focus", 0), Is.False);
            Assert.That(manager.BuyBattleItem("sparkling_focus"), Is.False, "Cannot buy before Shop");
            OpenShop();
            Assert.That(manager.BuyBattleItem("espresso_focus"), Is.False, "Inventory full");
            Assert.That(manager.CurrentGPAHundredths, Is.EqualTo(10000));
            Assert.That(manager.BuyBattleItem("signed_fan_art"), Is.False, "Drop items are not sold");
            Assert.That(manager.BuyBattleItem("unknown"), Is.False);
            Assert.That(manager.ShopStock(null), Is.Zero);
        }

        [Test]
        public void InsufficientBalanceDoesNotChargeAndSpendingLastGpaEndsTheCampaign()
        {
            OpenShop();
            manager.TakeGPADamage(99);
            Assert.That(manager.BuyBattleItem("endurance_focus"), Is.True);
            Assert.That(manager.CurrentGPAHundredths, Is.EqualTo(20));
            Assert.That(manager.BuyBattleItem("gentle_focus"), Is.False);
            Assert.That(manager.CurrentGPAHundredths, Is.EqualTo(20));
            Assert.That(manager.BattleItemCount("gentle_focus"), Is.Zero);
            Assert.That(manager.BuyBattleItem("espresso_focus"), Is.True);
            Assert.That(manager.CurrentGPAHundredths, Is.Zero);
            Assert.That(manager.Phase, Is.EqualTo(CampaignPhase.Complete));
            Assert.That(manager.IsGameComplete, Is.True);
            Assert.That(manager.BuyBattleItem("sparkling_focus"), Is.False);
            Assert.That(manager.EnterShop(), Is.False);
            Assert.That(manager.BeginBattle(), Is.False);
        }

        [Test]
        public void PrepareConsumesOnceAcrossRestartRetryAndRestoreThenConsumesOnNextDay()
        {
            manager.GrantBattleItem("espresso_focus", 3);
            manager.GrantBattleItem("coffee_coupon", 2);
            var config = LoadBattleConfiguration("Same day loadout");
            Assert.That(manager.BeginBattle(config), Is.True);
            manager.PrepareBattleItems(config);
            CollectionAssert.AreEquivalent(new[] { "espresso_focus", "coffee_coupon" }, config.battleItems);
            Assert.That(manager.BattleItemCount("espresso_focus"), Is.EqualTo(2));
            Assert.That(manager.BattleItemCount("coffee_coupon"), Is.EqualTo(1));
            manager.PrepareBattleItems(config);
            var restarted = LoadBattleConfiguration();
            Assert.That(manager.BeginBattle(restarted), Is.True);
            manager.PrepareBattleItems(restarted);
            Assert.That(manager.BattleItemCount("espresso_focus"), Is.EqualTo(2));
            CollectionAssert.AreEquivalent(config.battleItems, restarted.battleItems);
            Assert.That(manager.CompleteBattle(new BattleReport { won = false, protectionLost = 1 }), Is.True);
            Assert.That(manager.TryRetryBattle(), Is.True);
            manager.PrepareBattleItems(restarted);
            Assert.That(manager.BattleItemCount("espresso_focus"), Is.EqualTo(2));
            Assert.That(manager.RetryCount, Is.EqualTo(1));
            manager.SaveProgress();
            manager.InitializeStats();
            Assert.That(manager.LoadProgress(), Is.True);
            manager.PrepareBattleItems(manager.CurrentBattleConfiguration);
            Assert.That(manager.BattleItemCount("espresso_focus"), Is.EqualTo(2));
            CollectionAssert.AreEquivalent(config.battleItems, manager.CurrentBattleConfiguration.battleItems);
            Assert.That(manager.CompleteBattle(new BattleReport { won = false }), Is.True);
            Assert.That(manager.EnterShop(), Is.True);
            Assert.That(manager.FinishShopping(), Is.True);
            Assert.That(manager.CurrentDay, Is.EqualTo(2));
            var next = LoadBattleConfiguration();
            Assert.That(manager.BeginBattle(next), Is.True);
            manager.PrepareBattleItems(next);
            Assert.That(manager.BattleItemCount("espresso_focus"), Is.EqualTo(1));
            Assert.That(manager.BattleItemCount("coffee_coupon"), Is.Zero);
            CollectionAssert.AreEquivalent(config.battleItems, next.battleItems);
        }

        [Test]
        public void AtomicResultRejectsDuplicateRewardsAndDefeatChargesActualProtection()
        {
            Assert.That(manager.BeginBattle(), Is.True);
            var defeat = new BattleReport { won = false, protectionLost = 3, goldEarned = 99 };
            Assert.That(manager.CompleteBattle(defeat), Is.True);
            Assert.That(manager.CurrentGPAHundredths, Is.EqualTo(9700));
            Assert.That(manager.Gold, Is.Zero, "Defeat does not award kill gold");
            Assert.That(manager.BattlesLost, Is.EqualTo(1));
            Assert.That(manager.CompleteBattle(defeat), Is.False);
            Assert.That(manager.BeginBattle(), Is.False, "Result cannot bypass retry");
            Assert.That(manager.TryRetryBattle(), Is.True);
            Assert.That(manager.CurrentGPAHundredths, Is.EqualTo(9700), "First retry is free");
            var victory = new BattleReport { won = true, protectionLost = 1, bonusGpa = 1, goldEarned = 41 };
            Assert.That(manager.CompleteBattle(victory), Is.True);
            Assert.That(manager.CurrentGPAHundredths, Is.EqualTo(10000));
            Assert.That(manager.Gold, Is.EqualTo(41));
            Assert.That(manager.BattlesWon, Is.EqualTo(1));
            Assert.That(manager.BattleItemCount("signed_fan_art"), Is.EqualTo(1));
            Assert.That(manager.CompleteBattle(victory), Is.False);
            Assert.That(manager.TryRetryBattle(), Is.False);
            Assert.That(manager.Gold, Is.EqualTo(41));
            Assert.That(manager.BattleItemCount("signed_fan_art"), Is.EqualTo(1));
        }

        [Test]
        public void ExhaustedGpaEndsImmediatelyBeforeAnyVictoryBonusCanReviveIt()
        {
            manager.TakeGPADamage(99);
            Assert.That(manager.BeginBattle(), Is.True);
            Assert.That(manager.CompleteBattle(new BattleReport { won = true, protectionLost = 2, bonusGpa = 1, goldEarned = 41 }), Is.True);
            Assert.That(manager.CurrentGPAHundredths, Is.Zero);
            Assert.That(manager.Phase, Is.EqualTo(CampaignPhase.Complete));
            Assert.That(manager.IsGameComplete, Is.True);
            Assert.That(manager.Gold, Is.Zero);
            Assert.That(manager.LastBattleDrop, Is.Null);
            Assert.That(manager.TryRetryBattle(), Is.False);
            Assert.That(manager.EnterShop(), Is.False);
        }

        [Test]
        public void VersionOneIntegerGpaMigratesAndVersionTwoRoundTripsExactState()
        {
            PlayerPrefs.SetString(SaveKey, "{\"version\":1,\"gpa\":87,\"grade\":1,\"emotion\":11,\"strength\":12,\"eduPower\":13,\"determination\":7,\"actionPoints\":3,\"day\":2,\"gold\":9,\"retries\":0,\"won\":1,\"lost\":0,\"personalitySelected\":true,\"personality\":0,\"phase\":1}");
            Assert.That(manager.LoadProgress(), Is.True);
            Assert.That(manager.CurrentGPAHundredths, Is.EqualTo(8700));
            Assert.That(manager.CurrentDay, Is.EqualTo(2));
            Assert.That(manager.CurrentEmotion, Is.EqualTo(11));
            Assert.That(manager.ActionPoints, Is.EqualTo(3));
            manager.GrantBattleItem("coffee_coupon", 2);
            var config = LoadBattleConfiguration("Persisted configuration");
            Assert.That(manager.BeginBattle(config), Is.True);
            manager.PrepareBattleItems(config);
            var report = new BattleReport { won = true, protectionLost = 2, bonusGpa = 1, killed = 41, spawned = 41,
                goldEarned = 41, duration = 175.5f, costSpent = 67, upgrades = 4, deployed = 2, damage = 1234.5f, healing = 78 };
            Assert.That(manager.CompleteBattle(report), Is.True);
            Assert.That(manager.EnterShop(), Is.True);
            Assert.That(manager.BuyBattleItem("sparkling_focus"), Is.True);
            Assert.That(manager.CurrentGPAHundredths, Is.EqualTo(9090));
            manager.SaveProgress();
            string roundTripJson = PlayerPrefs.GetString(SaveKey);
            Assert.That(roundTripJson, Does.Contain("\"version\":2"));
            manager.InitializeStats();
            Assert.That(manager.LoadProgress(), Is.True);
            Assert.That(manager.CurrentGPAHundredths, Is.EqualTo(9090));
            Assert.That(manager.CurrentDay, Is.EqualTo(2));
            Assert.That(manager.Phase, Is.EqualTo(CampaignPhase.Shop));
            Assert.That(manager.Gold, Is.EqualTo(50));
            Assert.That(manager.ActionPoints, Is.EqualTo(3));
            Assert.That(manager.BattlesWon, Is.EqualTo(2));
            Assert.That(manager.BattleItemCount("coffee_coupon"), Is.EqualTo(1));
            Assert.That(manager.BattleItemCount("sparkling_focus"), Is.EqualTo(1));
            Assert.That(manager.ShopStock("sparkling_focus"), Is.EqualTo(98));
            CollectionAssert.AreEquivalent(new[] { "coffee_coupon" }, manager.NextBattleItems);
            Assert.That(manager.CurrentBattleConfiguration.title, Is.EqualTo("Persisted configuration"));
            Assert.That(manager.LastBattleReport.protectionLost, Is.EqualTo(2));
            Assert.That(manager.LastBattleReport.bonusGpa, Is.EqualTo(1));
            Assert.That(manager.LastBattleReport.killed, Is.EqualTo(41));
            Assert.That(manager.LastBattleReport.duration, Is.EqualTo(175.5f));
            Assert.That(manager.LastBattleDrop, Is.EqualTo("makeup_sample"));
            manager.SaveProgress();
            Assert.That(PlayerPrefs.GetString(SaveKey), Is.EqualTo(roundTripJson));
        }

        [Test]
        public void ScheduleWithNullBattleSurvivesColdRestoreAndStartsTheNextDaysRealMap()
        {
            var firstDay = LoadBattleConfiguration();
            Assert.That(manager.BeginBattle(firstDay), Is.True);
            manager.PrepareBattleItems(firstDay);
            Assert.That(manager.CompleteBattle(new BattleReport { won = true, goldEarned = 41 }), Is.True);
            Assert.That(manager.EnterShop(), Is.True);
            foreach (var key in new[] { "espresso_focus", "sparkling_focus", "gentle_focus", "endurance_focus" })
                Assert.That(manager.BuyBattleItem(key), Is.True);
            Assert.That(manager.FinishShopping(), Is.True);
            Assert.That(manager.Phase, Is.EqualTo(CampaignPhase.Schedule));
            Assert.That(manager.CurrentBattleConfiguration, Is.Null);
            manager.SaveProgress(); // Exercise production JsonUtility serialization of the null nested class.

            RecreateManagerAndLoadProgress();
            Assert.That(manager.CurrentDay, Is.EqualTo(2));
            Assert.That(manager.CurrentGPAHundredths, Is.EqualTo(9860));
            Assert.That(manager.Phase, Is.EqualTo(CampaignPhase.Schedule));
            Assert.That(manager.CurrentBattleConfiguration, Is.Null, "JsonUtility's empty inline class must not become a cached battle");
            Assert.That(manager.NextBattleItems.Count, Is.EqualTo(5));
            Assert.That(manager.BeginBattle(), Is.True, "The Schedule-to-Battle handoff runs before Bootstrap");
            Assert.That(manager.CurrentBattleConfiguration, Is.Null);
            var next = LoadBattleConfiguration();
            Assert.That(manager.BeginBattle(next), Is.True);
            manager.PrepareBattleItems(next);
            Assert.That(next.battleItems.Length, Is.EqualTo(5));
            Assert.That(manager.BattleItemCount("espresso_focus"), Is.Zero);
            var simulation = new BattleSimulation(next);
            Assert.That(simulation.TotalEnemies, Is.EqualTo(41));
            simulation.Tick(3);
            Assert.That(simulation.Time, Is.GreaterThan(2.9f));
            Assert.That(simulation.Report.spawned, Is.GreaterThan(0));
        }

        [Test]
        public void FormalRosterAndFiveSuppliesSurviveColdRestoreWithoutLosingWavesOrConsumingTwice()
        {
            string[] supplies = { "signed_fan_art", "espresso_focus", "sparkling_focus", "gentle_focus", "endurance_focus" };
            foreach (var key in supplies) Assert.That(manager.GrantBattleItem(key, 2), Is.True);
            var map = LoadBattleConfiguration();
            var enemies = JsonUtility.FromJson<UnitCatalog>(Resources.Load<TextAsset>("Battle/Enemies").text).units;
            var configuration = BattleFactory.CreateCampaignDuel(map, map.towers, enemies, map.towers.Take(8).Select(unit => unit.id));
            Assert.That(manager.BeginBattle(configuration), Is.True);
            manager.PrepareBattleItems(configuration);
            string before = JsonUtility.ToJson(configuration);
            manager.SaveProgress();

            RecreateManagerAndLoadProgress();
            var restored = manager.CurrentBattleConfiguration;
            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.towers.Length, Is.EqualTo(8));
            Assert.That(restored.enemies.Length, Is.EqualTo(8));
            Assert.That(restored.groups.Sum(group => group.count), Is.EqualTo(41));
            Assert.That(restored.groups.Select(group => group.wave).Distinct().Count(), Is.EqualTo(4));
            Assert.That(restored.towers.All(unit => unit.skill != null && !string.IsNullOrEmpty(unit.skill.id)), Is.True);
            Assert.That(restored.enemies.All(unit => unit.skill != null && !string.IsNullOrEmpty(unit.skill.id)), Is.True);
            Assert.That(JsonUtility.ToJson(restored), Is.EqualTo(before), "All cells, unit numbers, skill parameters and wave times survive Unity serialization");
            CollectionAssert.AreEquivalent(supplies, restored.battleItems);
            Assert.That(manager.BeginBattle(restored), Is.True);
            manager.PrepareBattleItems(restored);
            foreach (var key in supplies) Assert.That(manager.BattleItemCount(key), Is.EqualTo(1), key + " is not consumed twice");
            var simulation = new BattleSimulation(restored);
            Assert.That(simulation.TotalEnemies, Is.EqualTo(41));
            simulation.Tick(3);
            Assert.That(simulation.Report.spawned, Is.GreaterThan(0));
        }

        [TestCase("null")]
        [TestCase("{}")]
        public void LegacyEmptyBattleCacheIsDiscardedWithoutLosingCampaignOrEquippedInventory(string battleJson)
        {
            string[] supplies = { "signed_fan_art", "espresso_focus", "sparkling_focus", "gentle_focus", "endurance_focus" };
            string equippedJson = "[\"" + string.Join("\",\"", supplies) + "\"]";
            string inventoryJson = "[" + string.Join(",", supplies.Select(key => "{\"key\":\"" + key + "\",\"count\":1,\"purchased\":" + (key == "signed_fan_art" ? 0 : 2) + "}")) + "]";
            PlayerPrefs.SetString(SaveKey, "{\"version\":2,\"gpaHundredths\":9860,\"grade\":1,\"day\":2,\"personalitySelected\":true,\"phase\":2,\"battle\":" + battleJson
                + ",\"itemsPrepared\":true,\"equippedItems\":" + equippedJson + ",\"inventory\":" + inventoryJson + "}");
            RecreateManagerAndLoadProgress();
            Assert.That(manager.CurrentBattleConfiguration, Is.Null);
            Assert.That(manager.CurrentDay, Is.EqualTo(2));
            Assert.That(manager.CurrentGPAHundredths, Is.EqualTo(9860));
            Assert.That(manager.Phase, Is.EqualTo(CampaignPhase.Battle));
            foreach (var key in supplies) Assert.That(manager.BattleItemCount(key), Is.EqualTo(1));
            Assert.That(manager.ShopStock("espresso_focus"), Is.EqualTo(97));
            var fallback = LoadBattleConfiguration();
            Assert.That(manager.BeginBattle(fallback), Is.True);
            manager.PrepareBattleItems(fallback);
            CollectionAssert.AreEquivalent(supplies, fallback.battleItems);
            foreach (var key in supplies) Assert.That(manager.BattleItemCount(key), Is.EqualTo(1), key + " survives empty-cache recovery without another consumption");
            Assert.DoesNotThrow(() => new BattleSimulation(fallback));
        }
    }
}
