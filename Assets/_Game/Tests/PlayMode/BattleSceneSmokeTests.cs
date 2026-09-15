using System.Collections;
using System.Linq;
using FinalDefense.Battle;
using FinalDefense.Combat;
using FinalDefense.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FinalDefense.Tests
{
    // These are real Unity scene/UI integration tests. The offline verifier only
    // compiles them; a licensed Unity Test Runner must execute these coroutines.
    public sealed class BattleSceneSmokeTests
    {
        private BattleView view;
        private GameManager previousManager;

        [UnitySetUp]
        public IEnumerator OpenBattleScene()
        {
            previousManager = Object.FindAnyObjectByType<GameManager>();
            BattleSceneBootstrap.NextBattle = null;
            yield return SceneManager.LoadSceneAsync("Battle", LoadSceneMode.Single);
            yield return null;
            Canvas.ForceUpdateCanvases();
            view = Object.FindAnyObjectByType<BattleView>();
            Assert.That(view, Is.Not.Null, "Battle scene bootstrap should create the production view");
        }

        [UnityTearDown]
        public IEnumerator CleanUp()
        {
            BattleSceneBootstrap.NextBattle = null;
            Time.timeScale = 1;
            if (view != null) view.Simulation.Paused = true;
            var currentManager = Object.FindAnyObjectByType<GameManager>();
            if (currentManager != null && currentManager != previousManager) Object.Destroy(currentManager.gameObject);
            var battleScene = SceneManager.GetSceneByName("Battle");
            if (battleScene.IsValid() && battleScene.isLoaded)
            {
                var empty = SceneManager.CreateScene("Battle test cleanup");
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(battleScene);
            }
            yield return null;
        }

        private Button Button(string name)
        {
            var matches = view.GetComponentsInChildren<Button>(true).Where(button => button.name == name).ToArray();
            Assert.That(matches.Length, Is.EqualTo(1), "Expected one production UI button: " + name);
            return matches[0];
        }

        private void ClickVisibleButton(string name)
        {
            var button = Button(name);
            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position),
                button = PointerEventData.InputButton.Left
            };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            Assert.That(pointer.position.x,Is.InRange(0f,(float)Screen.width),"Button is inside the real game window: "+name);
            Assert.That(pointer.position.y,Is.InRange(0f,(float)Screen.height),"Button is inside the real game window: "+name);
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, "The rendered button must receive pointer input: " + name);
            var topButton = hits[0].gameObject.GetComponentInParent<Button>();
            Assert.That(topButton, Is.EqualTo(button), "The intended button must be the topmost UI hit, without a covering panel: " + name);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        [UnityTest]
        public IEnumerator BootstrapCreatesPausedBattleCanvasInputAndSixteenCards()
        {
            Assert.That(view.Simulation, Is.Not.Null);
            Assert.That(view.Simulation.Paused, Is.True);
            Assert.That(view.Simulation.TotalEnemies, Is.EqualTo(41));
            Assert.That(view.GetComponentInChildren<Canvas>(), Is.Not.Null);
            Assert.That(view.GetComponentInChildren<GraphicRaycaster>(), Is.Not.Null);
            Assert.That(EventSystem.current, Is.Not.Null);
            Assert.That(EventSystem.current.GetComponent<InputSystemUIInputModule>(), Is.Not.Null);
            Assert.That(view.GetComponentsInChildren<Button>(true).Count(button => button.name.StartsWith("Card ")), Is.EqualTo(16));
            Assert.That(view.GetComponentInChildren<ScrollRect>(), Is.Not.Null);
            Assert.That(view.GetComponentInChildren<RectMask2D>(), Is.Not.Null);
            OriginalUIAssertions.AssertScene("Battle");
            foreach (var name in new[]{"CostText","GPAText","WaveText"})
            {
                var label=OriginalUIAssertions.Rect(name).GetComponent<TMPro.TextMeshProUGUI>();
                Assert.That(label,Is.Not.Null);Assert.That(label.text,Is.Not.Empty,"Original HUD has live model data: "+name);
                Assert.That(label.canvasRenderer.GetMesh().vertexCount,Is.GreaterThan(3),"Original HUD text has real geometry: "+name);
                Assert.That(label.canvasRenderer.cull,Is.False,"Original HUD is not culled: "+name);
            }
            AssertHudMatchesSimulation();
            yield return null;
            Assert.That(view.Simulation.Time, Is.EqualTo(0), "Initial preparation screen must not advance battle time");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator StartPauseAndMenuButtonsControlTheRealSimulation()
        {
            ClickVisibleButton("Start");
            Assert.That(view.Simulation.Paused, Is.False);
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(view.Simulation.Time, Is.GreaterThan(0));
            ClickVisibleButton("Menu");
            Assert.That(view.Simulation.Paused, Is.True);
            float pausedAt = view.Simulation.Time;
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(view.Simulation.Time, Is.EqualTo(pausedAt));
            var menu = view.GetComponentsInChildren<Image>(true).Single(image => image.name == "Pause Menu");
            Assert.That(menu.gameObject.activeInHierarchy && menu.raycastTarget, Is.True);
            ClickVisibleButton("Continue");
            Assert.That(view.Simulation.Paused, Is.False, "Closing the menu restores the prior running state");
            ClickVisibleButton("Start");
            Assert.That(view.Simulation.Paused, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DragEventThroughTheActualCardDeploysOnTheRenderedBoardCell()
        {
            var definition = view.Simulation.Config.towers.First(unit => !unit.highGround && unit.deployCost <= view.Simulation.Cost);
            var cell = view.Simulation.Config.ground.First();
            var card = Button("Card " + definition.id);
            var square = Button("Cell " + cell.x + "," + cell.y);
            Canvas.ForceUpdateCanvases();
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, square.transform.position);
            var pointer = new PointerEventData(EventSystem.current) { position = screen, pressPosition = screen };
            float before = view.Simulation.Cost;
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.beginDragHandler);
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.endDragHandler);
            yield return null;
            Assert.That(view.Simulation.Towers.Count, Is.EqualTo(1));
            var deployed = view.Simulation.Towers[0];
            Assert.That(deployed.definition.id, Is.EqualTo(definition.id));
            Assert.That(deployed.x, Is.EqualTo(cell.x));
            Assert.That(deployed.y, Is.EqualTo(cell.y));
            Assert.That(view.Simulation.Cost, Is.EqualTo(before - definition.deployCost).Within(.001));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RosterModalRejectsSevenThenReloadsWithEightExclusiveIdentities()
        {
            var catalog = view.Simulation.Config.towers;
            ClickVisibleButton("Roster Setup");
            yield return null; // The first opening creates the modal's graphics.
            var panel = view.GetComponentsInChildren<Image>(true).Single(image => image.name == "Choose Roster");
            Assert.That(panel.gameObject.activeInHierarchy && panel.raycastTarget, Is.True);
            Assert.That(view.GetComponentsInChildren<Button>(true).Count(button => button.name.StartsWith("Pick ")), Is.EqualTo(16));
            ClickVisibleButton("Pick tower_counter_shield");
            var original = view;
            ClickVisibleButton("Confirm Roster");
            yield return null;
            Assert.That(Object.FindAnyObjectByType<BattleView>(), Is.EqualTo(original), "Seven selected identities must not reload the scene");
            Assert.That(BattleSceneBootstrap.NextBattle, Is.Null);
            ClickVisibleButton("Pick tower_counter_shield");
            ClickVisibleButton("Confirm Roster");
            yield return null;
            yield return null;
            view = Object.FindAnyObjectByType<BattleView>();
            Assert.That(view, Is.Not.Null);
            Assert.That(view.Simulation.Config.towers.Length, Is.EqualTo(8));
            Assert.That(view.Simulation.Config.enemies.Length, Is.EqualTo(8));
            Assert.That(view.Simulation.Config.towers.Select(unit => unit.skill.id)
                .Intersect(view.Simulation.Config.enemies.Select(unit => unit.skill.id)), Is.Empty);
            Assert.That(BattleSceneBootstrap.NextBattle, Is.Null, "Bootstrap consumes the selected configuration once");
            Assert.That(view.Simulation.Paused, Is.True);
            ClickVisibleButton("Roster Setup");yield return new WaitForEndOfFrame();
            yield return AssertModalBlocksOriginalControls("Choose Roster");
            ClickVisibleButton("Cancel Roster");yield return new WaitForEndOfFrame();
            ClickVisibleButton("Supplies");yield return new WaitForEndOfFrame();
            yield return AssertModalBlocksOriginalControls("Battle Supplies");
            ClickVisibleButton("Close Supplies");yield return new WaitForEndOfFrame();
            Assert.That(view.Simulation.Paused,Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        private IEnumerator AssertModalBlocksOriginalControls(string name)
        {
            Assert.That(view.Simulation.Config.towers.Length,Is.EqualTo(8));
            var modal=view.GetComponentsInChildren<Image>().Single(image=>image.name==name);
            Assert.That(view.Simulation.Paused,Is.True);float before=view.Simulation.Time;
            foreach(string control in new[]{"Start","Menu"})
            {
                var button=Button(control);var pointer=new PointerEventData(EventSystem.current)
                {position=RectTransformUtility.WorldToScreenPoint(null,button.transform.position),button=PointerEventData.InputButton.Left};
                Assert.That(pointer.position.x,Is.InRange(0f,(float)Screen.width));Assert.That(pointer.position.y,Is.InRange(0f,(float)Screen.height));
                var hits=new System.Collections.Generic.List<RaycastResult>();float deadline=Time.realtimeSinceStartup+3;
                do
                {
                    Canvas.ForceUpdateCanvases();hits.Clear();EventSystem.current.RaycastAll(pointer,hits);
                    if(hits.Count>0&&hits[0].gameObject==modal.gameObject)break;
                    yield return new WaitForEndOfFrame();
                }while(Time.realtimeSinceStartup<deadline);
                Assert.That(hits,Is.Not.Empty);Assert.That(hits[0].gameObject,Is.EqualTo(modal.gameObject),"The full-canvas modal shield covers the original "+control+" point");
                Assert.That(hits[0].gameObject.GetComponentInParent<Button>(),Is.Null,"A shield hit must not trigger an underlying control");
                ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerDownHandler);
                ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerUpHandler);
                ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
                yield return new WaitForEndOfFrame();
                Assert.That(modal.gameObject.activeInHierarchy,Is.True);Assert.That(view.Simulation.Paused,Is.True);
                Assert.That(view.Simulation.Time,Is.EqualTo(before),"Blocked pointer input cannot resume the simulation");
            }
        }

        private void AssertHudMatchesSimulation()
        {
            var sim=view.Simulation;
            Assert.That(OriginalUIAssertions.Rect("CostText").GetComponent<TMPro.TextMeshProUGUI>().text,Does.Contain(Mathf.FloorToInt(sim.Cost)+"/"+sim.Config.maxCost));
            Assert.That(OriginalUIAssertions.Rect("GPAText").GetComponent<TMPro.TextMeshProUGUI>().text,Does.Contain(sim.Protection+"/"+sim.Config.protection));
            Assert.That(OriginalUIAssertions.Rect("WaveText").GetComponent<TMPro.TextMeshProUGUI>().text,Does.Contain(sim.CurrentWave+"/"+sim.TotalWaves));
            Assert.That(OriginalUIAssertions.Rect("GoldText").GetComponent<TMPro.TextMeshProUGUI>().text,Does.Contain(sim.Report.killed+"/"+sim.TotalEnemies));
        }

        private IEnumerator Capture(string fileName)
        {
            string folder = System.Environment.GetEnvironmentVariable("FINAL_DEFENSE_CAPTURE_DIR");
            if (string.IsNullOrEmpty(folder)) Assert.Ignore("Set FINAL_DEFENSE_CAPTURE_DIR and run Unity with graphics to capture rendered review states.");
            System.IO.Directory.CreateDirectory(folder);
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            Assert.That(texture, Is.Not.Null, "Unity should return the rendered game view");
            string path = System.IO.Path.Combine(folder, fileName + ".png");
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            OriginalUIAssertions.RecordTextLayout(System.IO.Path.Combine(folder,fileName+".layout.json"));
            Object.Destroy(texture);
            Assert.That(new System.IO.FileInfo(path).Length, Is.GreaterThan(1000));
            string captureLog = "BATTLE_CAPTURE " + path + " " + Screen.width + "x" + Screen.height;
            LogAssert.Expect(LogType.Log, captureLog);
            Debug.Log(captureLog);
        }

        private IEnumerator ShowCard(string id)
        {
            var card=Button("Card "+id);
            for(int attempt=0;attempt<=view.Simulation.Config.towers.Length;attempt++)
            {
                Canvas.ForceUpdateCanvases();
                if(OriginalUIAssertions.VisibleCardFraction((RectTransform)card.transform)>=.9f)yield break;
                ClickVisibleButton("Next Cards");yield return new WaitForSecondsRealtime(.05f);
            }
            Assert.Fail("Card must be at least 90% visible in the original scrolling panel: "+id);
        }

        private static void ExpectFirstMetalCaptureWarnings()
        {
            // Unity 6000.5.1's first Metal capture after loading a scene emits
            // these two engine warnings. All other logs retain normal checks.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Metal)
            {
                LogAssert.Expect(LogType.Warning, "Ignoring depth surface load action as it is memoryless");
                LogAssert.Expect(LogType.Warning, "Ignoring depth surface store action as it is memoryless");
            }
        }

        [UnityTest, Category("VisualCapture")]
        public IEnumerator CaptureSixRealRenderedBattleStates()
        {
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("FINAL_DEFENSE_CAPTURE_DIR")))
                Assert.Ignore("Visual capture is opt-in and requires a graphical Unity Game view.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExecuteMenuItem("Window/General/Game");
#endif
            yield return null;
            ExpectFirstMetalCaptureWarnings();
            yield return Capture("01-initial");

            var definition = view.Simulation.Config.towers.First(unit => !unit.highGround && unit.deployCost <= view.Simulation.Cost);
            var cell = view.Simulation.Config.ground.First(point => point.x == 5 && point.y == 7);
            ClickVisibleButton("Card " + definition.id);
            ClickVisibleButton("Cell " + cell.x + "," + cell.y);
            yield return null;
            yield return Capture("02-deployed");

            var longDetails = view.Simulation.Config.towers.OrderByDescending(unit => unit.skill.description.Length).First();
            yield return ShowCard(longDetails.id);
            ClickVisibleButton("Card " + longDetails.id);
            yield return null;
            var scroll = view.GetComponentsInChildren<ScrollRect>(true).Single(item => item.name == "Detail Scroll");
            scroll.verticalNormalizedPosition = .5f;
            yield return Capture("03-long-details");

            ClickVisibleButton("Roster Setup");
            yield return null;
            yield return Capture("04-roster");
            ClickVisibleButton("Cancel Roster");

            ClickVisibleButton("Start");
            view.Simulation.Tick(1000);
            Assert.That(view.Simulation.Finished, Is.True, "Production battle should naturally finish before result capture");
            yield return new WaitForSecondsRealtime(.15f);
            yield return Capture("05-result");

            // The actual retry control enters the second battle; the campaign
            // phase guard correctly rejects directly loading Battle from Result.
            var finishedView=view;
            ClickVisibleButton("Retry");
            float retryDeadline=Time.realtimeSinceStartup+10;
            do { yield return null; view=Object.FindAnyObjectByType<BattleView>(); }
            while((view==null||view==finishedView)&&Time.realtimeSinceStartup<retryDeadline);
            Assert.That(view,Is.Not.Null);Assert.That(view,Is.Not.SameAs(finishedView));
            yield return new WaitForEndOfFrame();
            var guard = view.Simulation.TryDeploy("tower_counter_shield", 8, 7, -1, 0);
            var striker = view.Simulation.TryDeploy("tower_pursuit_strike", 7, 6, 0, 1);
            Assert.That(guard, Is.Not.Null); Assert.That(striker, Is.Not.Null);
            ClickVisibleButton("Start");
            view.Simulation.Tick(10);
            Assert.That(view.Simulation.TryUpgrade(striker), Is.True, "The ten-second upgrade pays the authored price");
            view.Simulation.Tick(5);
            ClickVisibleButton("Start");
            Assert.That(view.Simulation.Time, Is.EqualTo(15).Within(.001));
            Assert.That(view.Simulation.Enemies.Any(unit => unit.alive), Is.True, "The active screenshot includes live opponents");
            // BattleView refreshes at 10 Hz. Wait for its normal rendered update,
            // then assert actual visuals instead of capturing a stale first frame.
            yield return new WaitForSecondsRealtime(.15f);
            foreach (var unit in view.Simulation.Towers.Concat(view.Simulation.Enemies).Where(unit => unit.alive))
                Assert.That(Button("Unit " + unit.id).gameObject.activeInHierarchy, Is.True);
            AssertHudMatchesSimulation();
            ExpectFirstMetalCaptureWarnings();
            yield return Capture("06-active-battle");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
