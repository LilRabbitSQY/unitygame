using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using IOPath = System.IO.Path;
using System.Linq;
using FinalDefense.Battle;
using FinalDefense.Combat;
using FinalDefense.Core;
using FinalDefense.Personality;
using FinalDefense.Schedule;
using FinalDefense.Shop;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FinalDefense.Tests
{
    // Opt-in, real Unity acceptance journey. Load MainMenu once, then reach all
    // subsequent scenes through rendered UI. Only battle time is accelerated;
    // stats, wallet, day, skill state, position and result are never injected.
    public sealed class ReleaseFlowTests
    {
        private string output;
        private readonly List<string> evidence = new List<string>();
        private readonly HashSet<Scene> capturedScenes = new HashSet<Scene>();
        private readonly List<string> screenshotNames = new List<string>();
        private float previousTimeScale;
        private string initialPersistedSave;

        [UnitySetUp]
        public IEnumerator StartFromTheRealEntryScene()
        {
            output = Environment.GetEnvironmentVariable("FINAL_DEFENSE_RELEASE_CAPTURE_DIR");
            if (string.IsNullOrEmpty(output)) Assert.Ignore("Set FINAL_DEFENSE_RELEASE_CAPTURE_DIR to run the full graphical release acceptance journey.");
            string testName=TestContext.CurrentContext.Test.Name;
            if(testName.StartsWith("RepeatedRealDefeats")) output=IOPath.Combine(output,"early-defeat");
            else if(testName.StartsWith("StartedBattleRestart")) output=IOPath.Combine(output,"forfeit");
            else if(testName.StartsWith("ColdStartFromSerialized")) output=IOPath.Combine(output,"cross-process");
            Directory.CreateDirectory(output);
            evidence.Clear(); capturedScenes.Clear(); screenshotNames.Clear();
            initialPersistedSave=PlayerPrefs.GetString("FinalDefense.Campaign.v1", "");
            previousTimeScale = Time.timeScale;
            Time.timeScale = 1;
            // This creates a fresh test process state. The later new-game reset
            // is tested exclusively through UI, with the same live GameManager.
            foreach (var manager in Object.FindObjectsByType<GameManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(manager.gameObject);
            yield return new WaitForEndOfFrame();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExecuteMenuItem("Window/General/Game");
#endif
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return Ready("MainMenu");
        }

        [UnityTearDown]
        public IEnumerator SaveEvidenceAndCleanTestState()
        {
            if (!string.IsNullOrEmpty(output))
                File.WriteAllLines(IOPath.Combine(output, "journey-actions.txt"), evidence);
            Time.timeScale = previousTimeScale;
            var view = Object.FindAnyObjectByType<BattleView>();
            if (view != null) view.Simulation.Paused = true;
            yield return new WaitForEndOfFrame();
        }

        private void Record(string text)
        {
            string entry = DateTime.UtcNow.ToString("O") + " " + text;
            if (evidence.Count == 0) File.WriteAllText(IOPath.Combine(output, "journey-actions.txt"), entry + Environment.NewLine);
            else File.AppendAllText(IOPath.Combine(output, "journey-actions.txt"), entry + Environment.NewLine);
            evidence.Add(entry);
        }

        private static Button FindButton(string name)
        {
            var candidates = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(button => button.name == name && button.gameObject.activeInHierarchy).ToArray();
            Assert.That(candidates.Length, Is.EqualTo(1), "Expected one visible production button: " + name);
            return candidates[0];
        }

        private static bool TryPointer(Button button, out Vector2 position, out string blockedBy)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var canvas = button.GetComponentInParent<Canvas>();
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            blockedBy = "no UI hit";
            // Test real points in the button, including a free edge when two
            // battlefield units partially overlap. Never dispatch through cover.
            foreach (var offset in new[] { Vector2.zero, new Vector2(.3f, 0), new Vector2(-.3f, 0), new Vector2(0, .3f), new Vector2(0, -.3f) })
            {
                var local = new Vector3(rect.rect.center.x + offset.x * rect.rect.width, rect.rect.center.y + offset.y * rect.rect.height, 0);
                position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(local));
                if(position.x<0||position.y<0||position.x>=Screen.width||position.y>=Screen.height)
                { blockedBy="point outside the real game window";continue; }
                var pointer = new PointerEventData(EventSystem.current) { position = position };
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, hits);
                if (hits.Count > 0)
                {
                    blockedBy = hits[0].gameObject.name;
                    if (hits[0].gameObject.GetComponentInParent<Button>() == button) return true;
                }
            }
            position = Vector2.zero;
            return false;
        }

        private IEnumerator WaitForPointer(string name)
        {
            float deadline=Time.realtimeSinceStartup+3;
            var button=FindButton(name);
            while(!TryPointer(button,out _,out _)&&Time.realtimeSinceStartup<deadline) yield return new WaitForEndOfFrame();
            Assert.That(TryPointer(button,out _,out var cover),Is.True,"Rendered control must become the topmost hit: "+name+"; cover="+cover);
        }

        private void Click(string name, bool allowDisabled = false)
        {
            var button = FindButton(name);
            if (!allowDisabled) Assert.That(button.IsInteractable(), Is.True, "UI action must be available: " + name);
            Assert.That(TryPointer(button, out var position, out var blockedBy), Is.True,
                "Actual screen pointer must hit " + name + "; covering object: " + blockedBy);
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = position, pressPosition = position, button = PointerEventData.InputButton.Left,
                pointerPress = button.gameObject, rawPointerPress = button.gameObject
            };
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Record("POINTER " + SceneManager.GetActiveScene().name + " / " + name + " @ " + position);
        }

        private IEnumerator Ready(string sceneName)
        {
            float deadline = Time.realtimeSinceStartup + 15;
            while (SceneManager.GetActiveScene().name != sceneName && Time.realtimeSinceStartup < deadline) yield return new WaitForEndOfFrame();
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(sceneName), "UI navigation should reach its intended scene");
            yield return new WaitForSecondsRealtime(.15f);
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            Assert.That(EventSystem.current, Is.Not.Null);
            Assert.That(Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None).Length, Is.EqualTo(1), "Navigation preserves one live campaign manager");
            OriginalUIAssertions.AssertScene(sceneName);
            Record("SCENE " + sceneName + " day=" + GameManager.Instance.CurrentDay);
        }

        private IEnumerator Capture(string name)
        {
            yield return new WaitForSecondsRealtime(.15f);
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            if (capturedScenes.Add(SceneManager.GetActiveScene()) &&
                SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Metal)
            {
                LogAssert.Expect(LogType.Warning, "Ignoring depth surface load action as it is memoryless");
                LogAssert.Expect(LogType.Warning, "Ignoring depth surface store action as it is memoryless");
            }
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            Assert.That(texture, Is.Not.Null);
            string filename = name + ".png";
            File.WriteAllBytes(IOPath.Combine(output, filename), texture.EncodeToPNG());
            OriginalUIAssertions.RecordTextLayout(IOPath.Combine(output,name+".layout.json"));
            Object.Destroy(texture);
            screenshotNames.Add(filename);
            Record("SCREENSHOT " + filename + " " + Screen.width + "x" + Screen.height);
        }

        private IEnumerator NewGameThroughPersonality(bool capture = true)
        {
            bool canReset=Object.FindObjectsByType<Button>().Any(button=>button.name=="NewGame"&&button.gameObject.activeInHierarchy);
            if(!canReset) Assert.That(GameManager.Instance.PersonalitySelected,Is.False,"Fresh entry uses the original Start button");
            Click(canReset?"NewGame":"StartButton");
            yield return Ready("PersonalityTest");
            var manager = Object.FindAnyObjectByType<PersonalityTestManager>();
            Assert.That(manager, Is.Not.Null);
            if (capture) yield return Capture("02-personality-question");
            for (int index = 0; index < manager.TotalQuestions; index++)
            {
                Assert.That(manager.CurrentQuestionIndex, Is.EqualTo(index));
                Click("Option_0");
                yield return new WaitForEndOfFrame();
            }
            Assert.That(manager.IsComplete, Is.True);
            Assert.That(GameManager.Instance.PersonalitySelected, Is.True);
            Assert.That(GameManager.Instance.PersonalityConfigData, Is.Not.Null, "The selected personality has its real authored configuration");
            if (capture) yield return Capture("03-personality-result");
            Click("ContinueBtn");
            yield return Ready("MainMenu");
            Click("StartButton");
            yield return Ready("Schedule");
        }

        private IEnumerator UseSchedule(int day, bool capture = true)
        {
            var gm = GameManager.Instance;
            Assert.That(gm.CurrentDay, Is.EqualTo(day));
            Assert.That(gm.ActionPoints, Is.EqualTo(gm.MaxActionPoints), "A new day receives its authored action points");
            var manager = Object.FindAnyObjectByType<ScheduleManager>();
            Assert.That(manager, Is.Not.Null);
            var activity = manager.AvailableActivities.First(item => item.actPointCost > 0 && item.actPointCost <= gm.ActionPoints);
            int before = gm.ActionPoints;
            Click(activity.activityName);
            yield return new WaitForEndOfFrame();
            Assert.That(gm.ActionPoints, Is.EqualTo(before - activity.actPointCost));
            if (day == 1)
            {
                while (gm.ActionPoints >= activity.actPointCost)
                {
                    Click(activity.activityName); yield return new WaitForEndOfFrame();
                }
                int exhausted = gm.ActionPoints;
                Click(activity.activityName, true); yield return new WaitForEndOfFrame();
                Assert.That(gm.ActionPoints, Is.EqualTo(exhausted), "An exhausted schedule cannot spend below zero");
                if (capture) yield return Capture("04-schedule-actions");
            }
            Click("StartBattle");
            yield return Ready("Battle");
            Assert.That(gm.CurrentDay, Is.EqualTo(day));
            yield return ConfirmCampaignRoster(capture && day == 1);
        }

        private static readonly string[] CampaignRoster = {
            "counter_shield", "counter_store", "pursuit_strike", "pursuit_support",
            "poison_amp", "burn_burst", "heal_aura", "heal_support"
        };

        private IEnumerator ConfirmCampaignRoster(bool capture)
        {
            var pickers = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(button => button.name.StartsWith("Pick ")).ToArray();
            if (pickers.Length == 0) yield break;
            var catalog = JsonUtility.FromJson<UnitCatalog>(Resources.Load<TextAsset>("Battle/Towers").text).units;
            // Production now defaults to this documented, validated eight-character roster.
            // The separate smoke test verifies selection changes and rejection of seven.
            if (capture) yield return Capture("14-campaign-roster");
            var priorView=Object.FindAnyObjectByType<BattleView>();
            Click("Confirm Roster");
            float deadline=Time.realtimeSinceStartup+10;
            while((Object.FindAnyObjectByType<BattleView>()==null||Object.FindAnyObjectByType<BattleView>()==priorView)&&Time.realtimeSinceStartup<deadline) yield return new WaitForEndOfFrame();
            Assert.That(Object.FindAnyObjectByType<BattleView>(),Is.Not.SameAs(priorView),"Confirmed roster reloads the battle scene");
            yield return Ready("Battle");
            var sim = Object.FindAnyObjectByType<BattleView>().Simulation;
            Assert.That(sim.Config.towers.Length, Is.EqualTo(8)); Assert.That(sim.Config.enemies.Length, Is.EqualTo(8));
            Assert.That(sim.Config.groups.Sum(group => group.count), Is.EqualTo(41));
            Assert.That(sim.Config.enemies.All(enemy => !CampaignRoster.Contains(enemy.skill.id)), Is.True, "Enemy identities are the eight unselected characters");
            Assert.That(sim.Config.enemies.All(enemy => !string.IsNullOrEmpty(enemy.skill.id)), Is.True, "The campaign uses real enemy skills");
            Record("ROSTER " + string.Join(",", CampaignRoster));
        }

        private IEnumerator EnsureCardVisible(string id)
        {
            var card = FindButton("Card " + id);
            int pages=Object.FindAnyObjectByType<BattleView>().Simulation.Config.towers.Length+1;
            for (int attempt = 0; attempt < pages; attempt++)
            {
                if (OriginalUIAssertions.VisibleCardFraction((RectTransform)card.transform)>=.9f&&TryPointer(card, out _, out _)) yield break;
                Click("Next Cards");
                yield return new WaitForSecondsRealtime(.05f);
            }
            Assert.Fail("Card remains outside the actual scrolling viewport: " + id);
        }

        private IEnumerator DragCardToCell(string id, int x, int y)
        {
            yield return EnsureCardVisible(id);
            var card = FindButton("Card " + id);
            var cell = FindButton("Cell " + x + "," + y);
            Assert.That(TryPointer(card, out var begin, out _), Is.True);
            Assert.That(TryPointer(cell, out var end, out _), Is.True);
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = begin, pressPosition = begin, pointerDrag = card.gameObject,
                pointerPress = card.gameObject, button = PointerEventData.InputButton.Left
            };
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.beginDragHandler);
            pointer.position = end;
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.endDragHandler);
            Record("DRAG " + id + " -> " + x + "," + y);
            yield return new WaitForEndOfFrame();
        }

        private IEnumerator AdvanceBattleTo(float target)
        {
            var sim = Object.FindAnyObjectByType<BattleView>().Simulation;
            Assert.That(sim.Paused, Is.False, "Time acceleration is only permitted after the player starts battle");
            if (target > sim.Time) sim.Tick(target - sim.Time);
            yield return new WaitForSecondsRealtime(.12f);
            Record("BATTLE_TIME " + sim.Time.ToString("F2") + " kills=" + sim.Report.killed + " leaks=" + sim.Report.leaked);
        }

        private int deploymentFacing;
        private IEnumerator DeployThroughUI(string id, int x, int y, int direction, bool drag = false)
        {
            yield return EnsureCardVisible("tower_" + id);
            Click("Card tower_" + id);
            while (deploymentFacing != direction) { Click("Rotate"); deploymentFacing = (deploymentFacing + 1) % 4; }
            if (drag) yield return DragCardToCell("tower_" + id,x,y);
            else { Click("Cell " + x + "," + y); yield return new WaitForEndOfFrame(); }
            var u=Object.FindAnyObjectByType<BattleView>().Simulation.Towers.SingleOrDefault(unit => unit.definition.id == "tower_" + id && unit.x == x && unit.y == y);
            Assert.That(u, Is.Not.Null, "UI deployment created the requested real unit");
            Assert.That(u.facingX, Is.EqualTo(direction == 0 ? 1 : direction == 2 ? -1 : 0));
            Assert.That(u.facingY, Is.EqualTo(direction == 3 ? 1 : direction == 1 ? -1 : 0));
        }
        private IEnumerator UpgradeThroughUI(UnitState unit, int level)
        {
            Click("Unit " + unit.id); yield return new WaitForEndOfFrame(); Click("Upgrade"); yield return new WaitForEndOfFrame();
            Assert.That(unit.level, Is.EqualTo(level), "UI upgrade paid the actual authored cost");
        }
        private IEnumerator WaitForCost(int price)
        {
            var sim=Object.FindAnyObjectByType<BattleView>().Simulation;
            int limit=9000;
            while(sim.Cost+.0001f<price&&!sim.Finished&&limit-->0) sim.Tick(BattleSimulation.StepSeconds);
            yield return new WaitForEndOfFrame();
            Assert.That(sim.Finished,Is.False,"Battle must remain active while earning the next deployment");
            Assert.That(sim.Cost+.0001f,Is.GreaterThanOrEqualTo(price),"The next UI action uses legally earned COST");
            Record("BUDGET t="+sim.Time.ToString("F2")+" cost="+sim.Cost.ToString("F2")+" next="+price);
        }
        private IEnumerator FinishBattleWithLegalRedeployments()
        {
            var sim=Object.FindAnyObjectByType<BattleView>().Simulation; int frames=0;
            while(!sim.Finished&&sim.Time<450)
            {
                sim.Tick(BattleSimulation.StepSeconds);
                var ready=sim.Towers.FirstOrDefault(u=>u.downed&&u.alive&&sim.Time>=u.reviveAt&&sim.Cost>=u.definition.deployCost);
                if(ready!=null&&!sim.Finished)
                {
                    yield return new WaitForEndOfFrame();Click("Unit "+ready.id);yield return new WaitForEndOfFrame();Click("Revive");yield return new WaitForEndOfFrame();
                    Assert.That(ready.downed,Is.False,"Actual redeploy button restores a unit only after its authored cooldown and payment");
                    Record("REDEPLOY t="+sim.Time.ToString("F2")+" unit="+ready.definition.id+" at="+ready.x+","+ready.y);
                }
                if(++frames%60==0) yield return new WaitForEndOfFrame();
            }
            yield return new WaitForSecondsRealtime(.12f);
        }
        private IEnumerator WinAuthoredBattle(bool capture)
        {
            var sim = Object.FindAnyObjectByType<BattleView>().Simulation;
            int walletBefore = GameManager.Instance.Gold;
            double gpaBefore = GameManager.Instance.CurrentGPA;
            Assert.That(sim.TotalEnemies, Is.EqualTo(41)); Assert.That(sim.Towers, Is.Empty);
            Assert.That(sim.Time, Is.EqualTo(0)); Assert.That(sim.Cost, Is.EqualTo(sim.Config.initialCost));
            Assert.That(sim.Config.towers.Length, Is.EqualTo(8));
            if (GameManager.Instance.CurrentDay > 1)
            {
                Assert.That(sim.Config.battleItems, Does.Contain("espresso_focus"), "The purchased water is equipped next day");
                Assert.That(GameManager.Instance.BattleItemCount("espresso_focus"), Is.EqualTo(0), "Exactly one purchased water has been consumed");
                bool paused=sim.Paused;Click("Supplies");yield return new WaitForEndOfFrame();
                Assert.That(sim.Paused, Is.True); Assert.That(GameObject.Find("Supply espresso_focus"), Is.Not.Null);
                if(GameManager.Instance.CurrentDay==2) yield return Capture("16-equipped-supplies");
                yield return WaitForPointer("Close Supplies");
                Click("Close Supplies");yield return new WaitForEndOfFrame();Assert.That(sim.Paused,Is.EqualTo(paused));
            }
            deploymentFacing=0;
            yield return DeployThroughUI("counter_shield",8,7,2,true);
            yield return DeployThroughUI("pursuit_strike",7,6,3);
            var guard=sim.Towers.Single(u=>u.SkillId=="counter_shield");
            Click("Start");
            yield return WaitForCost(15); yield return DeployThroughUI("poison_amp",6,2,1);
            if(capture){yield return AdvanceBattleTo(15);Click("Start");yield return Capture("07-active-battle");Click("Start");}
            yield return WaitForCost(8); yield return DeployThroughUI("pursuit_strike",9,6,3);
            yield return WaitForCost(16); yield return DeployThroughUI("heal_aura",8,6,3);
            var healer=sim.Towers.Single(u=>u.SkillId=="heal_aura");
            yield return WaitForCost(10); yield return DeployThroughUI("counter_shield",9,7,2);
            yield return WaitForCost(15); yield return DeployThroughUI("pursuit_support",6,6,0);
            yield return WaitForCost(12); yield return UpgradeThroughUI(guard,2);
            yield return WaitForCost(15); yield return UpgradeThroughUI(guard,3);
            yield return WaitForCost(17); yield return UpgradeThroughUI(healer,2);
            yield return WaitForCost(18); yield return UpgradeThroughUI(healer,3);
            yield return FinishBattleWithLegalRedeployments();
            Assert.That(sim.Finished && sim.Report.won, Is.True, "The real selected-eight strategy wins against all named skill enemies");
            Assert.That(sim.Report.killed + sim.Report.leaked, Is.EqualTo(41));
            Assert.That(sim.Protection, Is.EqualTo(10), "All protection survives the complete named-enemy battle");
            Assert.That(sim.Report.killed,Is.EqualTo(41)); Assert.That(sim.Report.leaked,Is.EqualTo(0));
            Assert.That(sim.Report.deployed, Is.EqualTo(7)); Assert.That(sim.Report.upgrades, Is.EqualTo(4));
            Assert.That(sim.Report.costSpent, Is.InRange(144,194), "Authored deployments, upgrades and any necessary cooldown redeployments are paid");
            Assert.That(GameManager.Instance.Gold, Is.EqualTo(walletBefore + sim.Report.goldEarned), "Actual kill gold settles once");
            Assert.That(GameManager.Instance.CurrentGPA, Is.EqualTo(Math.Min(100, gpaBefore - sim.Report.protectionLost + 5 + sim.Report.bonusGpa)).Within(.0001), "GPA settles actual losses, victory and equipped bonus exactly once");
            if(capture) yield return Capture("08-battle-victory");
            Record("WIN day="+GameManager.Instance.CurrentDay+" killed="+sim.Report.killed+" leaked="+sim.Report.leaked+" protection="+sim.Protection+" cost="+sim.Report.costSpent+" GPA="+GameManager.Instance.CurrentGPAHundredths);
            Click("Next"); yield return Ready("Result");
        }

        private IEnumerator ResumeResultThroughMainMenu()
        {
            var gm = GameManager.Instance;
            int gold = gm.Gold, wins = gm.BattlesWon, losses = gm.BattlesLost;
            double gpa = gm.CurrentGPA;
            Click("ReturnButton"); yield return Ready("MainMenu");
            Click("StartButton"); yield return Ready("Result");
            Assert.That(gm.Gold, Is.EqualTo(gold), "Returning to a pending result never grants gold again");
            Assert.That(gm.CurrentGPA, Is.EqualTo(gpa), "Returning to a pending result never reapplies GPA gain/loss");
            Assert.That(gm.BattlesWon, Is.EqualTo(wins)); Assert.That(gm.BattlesLost, Is.EqualTo(losses));
        }

        private IEnumerator ShopThenNextDay(int day)
        {
            var gm = GameManager.Instance;
            int wonBefore = gm.BattlesWon, walletBefore = gm.Gold;
            Click("ShopButton");
            yield return Ready("Shop");
            Assert.That(gm.BattlesWon, Is.EqualTo(wonBefore), "Entering the shop cannot count the victory twice");
            Assert.That(gm.Gold, Is.EqualTo(walletBefore), "Opening another scene cannot award the battle gold twice");
            var manager = Object.FindAnyObjectByType<ShopManager>();
            var item = manager.AvailableItems.First(candidate => candidate.key == "espresso_focus");
            int gpaBefore = gm.CurrentGPAHundredths;
            int stockBefore = gm.ShopStock(item.key), countBefore = gm.BattleItemCount(item.key);
            Click(item.name); yield return new WaitForEndOfFrame();
            Assert.That(gm.CurrentGPAHundredths, Is.EqualTo(gpaBefore - item.priceHundredths), "A shop click spends exactly 20 hundredths of GPA");
            Assert.That(gm.BattleItemCount(item.key), Is.EqualTo(countBefore + 1), "The purchased supply enters inventory");
            Assert.That(gm.ShopStock(item.key), Is.EqualTo(stockBefore - 1));
            Assert.That(gm.Gold, Is.EqualTo(walletBefore), "The GPA shop does not spend kill gold");
            Record("PURCHASE " + item.key + " GPA=" + gm.CurrentGPAHundredths + " inventory=" + gm.BattleItemCount(item.key));
            if (day == 1) yield return Capture("10-shop-purchase");
            Click("LeaveBtn");
            if (day < GameManager.TotalDays)
            {
                yield return Ready("Schedule");
                Assert.That(gm.CurrentDay, Is.EqualTo(day + 1), "Leaving the shop advances one day exactly");
            }
            else yield return new WaitForSecondsRealtime(.2f);
        }

        [Serializable]
        private sealed class ColdCheckpointEvidence
        {
            public int writerProcessId, readerProcessId, day, gpaHundredths, jsonLength;
            public string sha256;
        }
        private static string SaveHash(string value)
        {
            using(var hash=System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();
        }

        [UnityTest, Category("ReleaseAcceptance"), Timeout(240000)]
        public IEnumerator ColdStartFromSerializedDayTwoSaveResumesThroughRealUI()
        {
            const string saveKey="FinalDefense.Campaign.v1";
            string resumePath=Environment.GetEnvironmentVariable("FINAL_DEFENSE_RESUME_SAVE_PATH");
            int processId=System.Diagnostics.Process.GetCurrentProcess().Id;
            var gm=GameManager.Instance;
            if(string.IsNullOrEmpty(resumePath))
            {
                // Writer process: all campaign state comes from actual UI play.
                yield return NewGameThroughPersonality(false);yield return UseSchedule(1,false);
                yield return WinAuthoredBattle(false);
                Click("ShopButton");yield return Ready("Shop");
                int before=gm.CurrentGPAHundredths;
                foreach(var item in BattleItemDefs.ShopItems)
                {
                    Click(item.name);yield return new WaitForEndOfFrame();
                    Assert.That(gm.BattleItemCount(item.key),Is.EqualTo(1));
                }
                Assert.That(gm.CurrentGPAHundredths,Is.EqualTo(before-140),"Four distinct waters cost 1.40 GPA exactly");
                yield return Capture("17-four-water-purchase");
                Click("LeaveBtn");yield return Ready("Schedule");
                Assert.That(gm.CurrentDay,Is.EqualTo(2));Assert.That(gm.CurrentBattleConfiguration,Is.Null);
                Click("ScheduleMenu");yield return Ready("MainMenu");
                Assert.That(gm.Phase,Is.EqualTo(CampaignPhase.Schedule));
                string raw=PlayerPrefs.GetString(saveKey);
                string exportPath=Environment.GetEnvironmentVariable("FINAL_DEFENSE_CHECKPOINT_OUTPUT");
                if(string.IsNullOrEmpty(exportPath)) exportPath=IOPath.Combine(output,"day-two-save.json");
                Directory.CreateDirectory(IOPath.GetDirectoryName(exportPath));File.WriteAllText(exportPath,raw);
                var checkpoint=new ColdCheckpointEvidence{writerProcessId=processId,day=gm.CurrentDay,gpaHundredths=gm.CurrentGPAHundredths,jsonLength=raw.Length,sha256=SaveHash(raw)};
                File.WriteAllText(exportPath+".evidence.json",JsonUtility.ToJson(checkpoint,true));
                File.WriteAllText(IOPath.Combine(output,"checkpoint-writer.json"),JsonUtility.ToJson(checkpoint,true));
                yield return Capture("18-saved-day-two-main-menu");
                Record("PASS writer process="+processId+" day=2 phase=Schedule four waters=1 each SHA256="+checkpoint.sha256);
            }
            else
            {
                // Reader process: the assembly fixture seeds only the on-disk
                // PlayerPrefs value before MainMenu is loaded. Production UI
                // performs LoadProgress itself in this completely fresh process.
                var checkpoint=JsonUtility.FromJson<ColdCheckpointEvidence>(File.ReadAllText(resumePath+".evidence.json"));
                Assert.That(processId,Is.Not.EqualTo(checkpoint.writerProcessId),"Persistence must be tested in a genuinely different Unity process");
                Assert.That(SaveHash(initialPersistedSave),Is.EqualTo(checkpoint.sha256),"Exact exported save bytes reached the new process");
                Assert.That(gm.PersonalitySelected,Is.True);Assert.That(gm.CurrentDay,Is.EqualTo(2));
                Assert.That(gm.Phase,Is.EqualTo(CampaignPhase.Schedule));
                Assert.That(gm.CurrentGPAHundredths,Is.EqualTo(checkpoint.gpaHundredths));
                Assert.That(gm.CurrentBattleConfiguration,Is.Null,"JsonUtility's null/empty object representation must not become a usable battle cache");
                foreach(var item in BattleItemDefs.ShopItems) Assert.That(gm.BattleItemCount(item.key),Is.EqualTo(1));
                Click("StartButton");yield return Ready("Schedule");yield return UseSchedule(2,false);
                var sim=Object.FindAnyObjectByType<BattleView>().Simulation;
                Assert.That(sim.Config.path.Length,Is.EqualTo(33));Assert.That(sim.TotalEnemies,Is.EqualTo(41));
                Assert.That(sim.Config.towers.Length,Is.EqualTo(8));Assert.That(sim.Config.enemies.Length,Is.EqualTo(8));
                foreach(var item in BattleItemDefs.ShopItems)
                {Assert.That(sim.Config.battleItems,Does.Contain(item.key));Assert.That(gm.BattleItemCount(item.key),Is.EqualTo(0));}
                yield return Capture("19-cold-restored-battle");
                yield return WinAuthoredBattle(false);
                Assert.That(gm.BattlesWon,Is.EqualTo(2));
                yield return Capture("20-cold-restored-victory");
                checkpoint.readerProcessId=processId;
                File.WriteAllText(IOPath.Combine(output,"checkpoint-reader.json"),JsonUtility.ToJson(checkpoint,true));
                Record("PASS reader process="+processId+" writer="+checkpoint.writerProcessId+" restored day2, five supplies, 41 kills/0 leaks");
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Category("ReleaseAcceptance"), Timeout(1200000)]
        public IEnumerator MainMenuToFinalDayVictoryFailureShoppingAndNewGameViaRealUI()
        {
            var gm = GameManager.Instance;
            Assert.That(gm, Is.Not.Null);
            yield return Capture("01-main-menu");
            yield return NewGameThroughPersonality();
            yield return UseSchedule(1);

            // First demonstrate a genuine loss with no towers, then reach the
            // normal result and retry UI. This must not reset the campaign day.
            double gpaBeforeLoss = gm.CurrentGPA;
            Click("Start");
            yield return AdvanceBattleTo(1000);
            var lost = Object.FindAnyObjectByType<BattleView>().Simulation;
            Assert.That(lost.Finished && !lost.Report.won, Is.True);
            Assert.That(lost.Protection, Is.EqualTo(0));
            Assert.That(gm.BattlesLost, Is.EqualTo(1));
            Assert.That(gm.Gold, Is.EqualTo(0));
            Assert.That(lost.Report.protectionLost, Is.EqualTo(10));
            Assert.That(gm.CurrentGPA, Is.EqualTo(gpaBeforeLoss - 10), "Losing ten protection removes ten actual campaign GPA");
            yield return Capture("05-battle-defeat");
            Click("Next"); yield return Ready("Result");
            Assert.That(gm.CurrentDay, Is.EqualTo(1));
            yield return Capture("06-defeat-result");
            yield return ResumeResultThroughMainMenu();
            double gpaBeforeRetry = gm.CurrentGPA;
            Click("RetryButton"); yield return Ready("Battle");
            Assert.That(gm.RetryCount, Is.EqualTo(1));
            Assert.That(gm.CurrentGPA, Is.EqualTo(gpaBeforeRetry), "The first retry is free");

            for (int day = 1; day <= GameManager.TotalDays; day++)
            {
                if (day > 1) yield return UseSchedule(day);
                yield return WinAuthoredBattle(day == 1);
                Assert.That(gm.CurrentDay, Is.EqualTo(day));
                Assert.That(gm.BattlesWon, Is.EqualTo(day));
                Assert.That(gm.BattlesLost, Is.EqualTo(1));
                if (day == 1) yield return Capture("09-victory-result");
                if (day == 1) yield return ResumeResultThroughMainMenu();
                yield return ShopThenNextDay(day);
            }

            Assert.That(gm.IsGameComplete, Is.True, "The real final-day flow reaches a finite ending");
            Assert.That(gm.BattlesWon, Is.EqualTo(GameManager.TotalDays));
            FindButton("EndingRestart");
            yield return Capture("11-campaign-ending");
            Click("EndingRestart");
            yield return Ready("PersonalityTest");
            Assert.That(GameManager.Instance, Is.SameAs(gm), "Restart resets the existing campaign rather than layering another manager");
            Assert.That(gm.CurrentDay, Is.EqualTo(1));
            Assert.That(gm.Gold, Is.EqualTo(0));
            Assert.That(gm.BattlesWon, Is.EqualTo(0));
            Assert.That(gm.BattlesLost, Is.EqualTo(0));
            Assert.That(gm.RetryCount, Is.EqualTo(0));
            Assert.That(gm.PersonalitySelected, Is.False);
            Assert.That(gm.ActionPoints, Is.EqualTo(gm.MaxActionPoints));
            Assert.That(gm.IsGameComplete, Is.False);
            Assert.That(BattleSceneBootstrap.NextBattle, Is.Null, "A prior battle override cannot leak into the new campaign");
            yield return Capture("12-restarted-personality");
            File.WriteAllLines(IOPath.Combine(output, "screenshots.txt"), screenshotNames);
            Record("PASS full campaign, genuine defeat and retry, " + GameManager.TotalDays + " legitimate victories, purchases, ending and new game");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Category("ReleaseAcceptance"), Timeout(180000)]
        public IEnumerator StartedBattleRestartAndExitUseRealForfeitAndRetryCosts()
        {
            yield return NewGameThroughPersonality(false); yield return UseSchedule(1, false);
            var gm = GameManager.Instance; int initialGpa = gm.CurrentGPAHundredths;
            for (int attempt = 1; attempt <= 2; attempt++)
            {
                Click("Start"); yield return AdvanceBattleTo(2);
                Click("Menu"); yield return new WaitForEndOfFrame();
                Assert.That(Object.FindAnyObjectByType<BattleView>().Simulation.Paused, Is.True);
                if (attempt == 2) yield return Capture("15-forfeit-menu");
                Click("Restart"); yield return Ready("Battle");
                Assert.That(gm.BattlesLost, Is.EqualTo(attempt), "Restarting a started battle counts its forfeit exactly once");
                Assert.That(gm.RetryCount, Is.EqualTo(attempt));
                Assert.That(gm.CurrentGPAHundredths, Is.EqualTo(initialGpa - (attempt == 1 ? 0 : 500)), "First retry is free; the second costs five GPA");
                var sim = Object.FindAnyObjectByType<BattleView>().Simulation;
                Assert.That(sim.Finished, Is.False); Assert.That(sim.Paused, Is.True); Assert.That(sim.Towers, Is.Empty);
                Assert.That(sim.Report.killed, Is.EqualTo(0)); Assert.That(sim.Config.towers.Length, Is.EqualTo(8));
            }
            Click("Start"); yield return AdvanceBattleTo(2); Click("Menu"); yield return new WaitForEndOfFrame(); Click("Exit"); yield return Ready("MainMenu");
            Assert.That(gm.BattlesLost, Is.EqualTo(3)); Assert.That(gm.CurrentGPAHundredths, Is.EqualTo(initialGpa - 500));
            Click("StartButton"); yield return Ready("Result");
            Assert.That(gm.BattlesLost, Is.EqualTo(3), "Returning to the forfeited report does not count it again");
            FindButton("RetryButton"); Record("PASS started restart/exit forfeits and free/paid retry costs via real UI");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Category("ReleaseAcceptance"), Timeout(600000)]
        public IEnumerator RepeatedRealDefeatsEndTheCampaignWhenGpaReachesZero()
        {
            // Keep this journey's records separate from the full successful run.
            yield return NewGameThroughPersonality(false);
            var gm = GameManager.Instance;
            double initialGpa = gm.CurrentGPA;
            int daysToLose = (int)Math.Ceiling(initialGpa / 10);
            for (int day = 1; day <= daysToLose; day++)
            {
                yield return UseSchedule(day, false);
                Click("Start"); yield return AdvanceBattleTo(1000);
                Assert.That(gm.CurrentGPA, Is.EqualTo(Math.Max(0, initialGpa - day * 10)));
                Assert.That(gm.BattlesLost, Is.EqualTo(day)); Assert.That(gm.BattlesWon, Is.EqualTo(0));
                Assert.That(gm.Gold, Is.EqualTo(0), "No kills produce no spendable reward");
                Click("Next"); yield return Ready("Result");
                if (day == daysToLose) break;
                Assert.That(gm.IsGameComplete, Is.False);
                Click("ShopButton"); yield return Ready("Shop");
                Click("LeaveBtn"); yield return Ready("Schedule");
                Assert.That(gm.CurrentDay, Is.EqualTo(day + 1));
            }
            Assert.That(gm.IsGameComplete, Is.True, "Zero GPA ends the campaign before the final scheduled day");
            Assert.That(gm.CurrentDay, Is.LessThan(GameManager.TotalDays));
            FindButton("EndingRestart");
            Assert.That(Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Any(button => button.name == "ShopButton" && button.IsInteractable()), Is.False, "A completed campaign cannot continue shopping into another day");
            yield return Capture("13-zero-gpa-ending");
            Record("PASS early campaign termination after " + daysToLose + " genuine defeats");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
