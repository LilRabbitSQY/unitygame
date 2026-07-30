using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using UnityEditor.SceneManagement;
using FinalDefense.Path;
using FinalDefense.Enemy;
using FinalDefense.Tower;
using FinalDefense.Battle;
using FinalDefense.Data;
using FinalDefense.Core;
using FinalDefense.UI;
using FinalDefense.Schedule;
using FinalDefense.Personality;
using FinalDefense.Shop;

namespace FinalDefense.Setup
{
    public static class DemoSetup
    {
        [MenuItem("FinalDefense/Setup Demo Scenes")]
        public static void SetupAll()
        {
            EnsureDirectories();
            EnsureLayers();
            CreateScriptableObjects();
            CreatePrefabs();
            SetupPersonalityTestScene();
            SetupMainMenuScene();
            SetupScheduleScene();
            SetupDialogueScene();
            SetupBattleScene();
            SetupResultScene();
            SetupShopScene();
            SetupBuildSettings();
            Debug.Log("期末保卫战 Demo 全部设置完成！（含人格测试/日程/对话/战斗/商店）");
        }

        private static void EnsureDirectories()
        {
            string[] dirs = {
                "Assets/_Game/ScriptableObjects",
                "Assets/_Game/ScriptableObjects/Towers",
                "Assets/_Game/ScriptableObjects/Enemies",
                "Assets/_Game/ScriptableObjects/Waves",
                "Assets/_Game/ScriptableObjects/Activities",
                "Assets/_Game/ScriptableObjects/DDLs",
                "Assets/_Game/ScriptableObjects/ShopItems",
                "Assets/_Game/Prefabs/Enemies",
                "Assets/_Game/Prefabs/Projectiles",
                "Assets/_Game/Scenes",
            };
            foreach (var dir in dirs)
            {
                if (!AssetDatabase.IsValidFolder(dir))
                {
                    var parts = dir.Split('/');
                    string current = parts[0];
                    for (int i = 1; i < parts.Length; i++)
                    {
                        string next = current + "/" + parts[i];
                        if (!AssetDatabase.IsValidFolder(next))
                            AssetDatabase.CreateFolder(current, parts[i]);
                        current = next;
                    }
                }
            }
        }

        private static void EnsureLayers()
        {
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            AddLayerIfMissing(layers, "Enemy");
            AddLayerIfMissing(layers, "Tower");
            tagManager.ApplyModifiedProperties();
        }

        private static void AddLayerIfMissing(SerializedProperty layers, string layerName)
        {
            for (int i = 0; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).stringValue == layerName) return;
            }
            for (int i = 8; i < layers.arraySize; i++)
            {
                if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                {
                    layers.GetArrayElementAtIndex(i).stringValue = layerName;
                    return;
                }
            }
        }

        [MenuItem("FinalDefense/Create ScriptableObjects Only")]
        public static void CreateScriptableObjects()
        {
            EnsureDirectories();

            // Player Stats
            var stats = ScriptableObject.CreateInstance<PlayerStats>();
            stats.initialGPA = 100;
            stats.initialGrade = 1;
            stats.initialEmotion = 10;
            stats.initialStrength = 10;
            stats.initialEduPower = 10;
            stats.initialDetermination = 5;
            CreateOrReplace(stats, "Assets/_Game/ScriptableObjects/DefaultPlayerStats.asset");

            // Personality Config
            var pConfig = ScriptableObject.CreateInstance<PersonalityConfig>();
            CreateOrReplace(pConfig, "Assets/_Game/ScriptableObjects/PersonalityConfig.asset");

            // Tower Data
            CreateTower("Notebook", "笔记本", 5, 8, 0.8f, 2.5f, 150, new Color(0.2f, 0.8f, 0.2f));
            CreateTower("Calculator", "计算器", 8, 15, 1.2f, 3.0f, 250, new Color(0.2f, 0.4f, 0.9f));
            CreateTower("Coffee", "咖啡杯", 12, 12, 1.5f, 2.0f, 400, new Color(0.9f, 0.6f, 0.1f));

            // Enemy Data
            CreateEnemy("MultipleChoice", "选择题小怪", 30, 2.5f, 2, 2, 5, 1.5f, 1.2f, new Color(0.9f, 0.2f, 0.2f));
            CreateEnemy("FillBlank", "填空题中怪", 80, 1.8f, 5, 3, 10, 1.5f, 1.5f, new Color(0.7f, 0.1f, 0.7f));
            CreateEnemy("Essay", "论述题大怪", 200, 1.2f, 10, 5, 20, 2.0f, 1.8f, new Color(0.1f, 0.1f, 0.1f));

            // Wave Data
            var wave = ScriptableObject.CreateInstance<WaveData>();
            var mc = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/_Game/ScriptableObjects/Enemies/MultipleChoice.asset");
            var fb = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/_Game/ScriptableObjects/Enemies/FillBlank.asset");
            var es = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/_Game/ScriptableObjects/Enemies/Essay.asset");
            wave.waves = new WaveEntry[]
            {
                new WaveEntry { enemyType = mc, count = 5, spawnInterval = 1.2f, delayBeforeWave = 3f },
                new WaveEntry { enemyType = mc, count = 8, spawnInterval = 1.0f, delayBeforeWave = 5f },
                new WaveEntry { enemyType = fb, count = 4, spawnInterval = 1.5f, delayBeforeWave = 5f },
                new WaveEntry { enemyType = mc, count = 10, spawnInterval = 0.8f, delayBeforeWave = 5f },
                new WaveEntry { enemyType = fb, count = 5, spawnInterval = 1.2f, delayBeforeWave = 5f },
                new WaveEntry { enemyType = es, count = 2, spawnInterval = 3f, delayBeforeWave = 5f },
            };
            CreateOrReplace(wave, "Assets/_Game/ScriptableObjects/Waves/DemoWave.asset");

            // Activities
            CreateActivity("Study", "自习复习", "去图书馆刷题", 1, 0, -1, 3, 1);
            CreateActivity("Gym", "去健身", "增强体力恢复精力", 1, 1, 3, 0, 0);
            CreateActivity("Club", "社团活动", "参加社团放松心情", 1, 3, 0, 0, -1);
            CreateActivity("Hangout", "朋友开黑", "和室友打游戏", 1, 2, 1, -1, 0);
            CreateActivity("Intern", "实习打工", "提前积累经验", 1, -1, -1, 2, 2);
            CreateActivity("Lecture", "听讲座", "拓宽视野", 1, 0, 0, 2, 1);
            CreateActivity("Rest", "睡觉休息", "恢复体力心情", 1, 2, 2, -1, -1);

            // DDLs
            CreateDDL("DDL_Homework", "高数作业", "导数积分一大堆", 3, 1.3f, 5);
            CreateDDL("DDL_Paper", "课程论文", "三千字起步", 4, 1.5f, 10);
            CreateDDL("DDL_Lab", "实验报告", "数据处理要仔细", 3, 1.2f, 5);
            CreateDDL("DDL_Presentation", "小组汇报", "PPT还没做", 2, 1.8f, 8);

            // Shop Items
            CreateShopItem("IceTea", "冰红茶", "续命神器", 10, 3, 1, 0, 0, 0);
            CreateShopItem("Coffee", "美式咖啡", "提神醒脑", 15, -1, 0, 3, 2, 0);
            CreateShopItem("Snack", "零食大礼包", "快乐源泉", 20, 5, 2, 0, 0, 0);
            CreateShopItem("Book", "参考书", "学习效率提升", 25, 0, 0, 5, 1, 0);
            CreateShopItem("Vitamin", "维生素片", "恢复GPA", 30, 1, 1, 1, 0, 5);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void CreateTower(string fileName, string displayName, int deployCost, int damage, float interval, float range, int maxHP, Color color)
        {
            var data = ScriptableObject.CreateInstance<TowerData>();
            data.towerName = displayName;
            data.cost = deployCost;
            data.deployCost = deployCost;
            data.damage = damage;
            data.attackInterval = interval;
            data.attackRange = range;
            data.maxHP = maxHP;
            data.towerColor = color;
            CreateOrReplace(data, $"Assets/_Game/ScriptableObjects/Towers/{fileName}.asset");
        }

        private static void CreateEnemy(string fileName, string displayName, int hp, float speed, int gpaDmg, int costReward, int atkDmg, float atkSpd, float atkRange, Color color)
        {
            var data = ScriptableObject.CreateInstance<EnemyData>();
            data.enemyName = displayName;
            data.maxHP = hp;
            data.moveSpeed = speed;
            data.gpaDamage = gpaDmg;
            data.goldReward = costReward;
            data.costReward = costReward;
            data.attackDamage = atkDmg;
            data.attackSpeed = atkSpd;
            data.attackRange = atkRange;
            data.enemyColor = color;
            CreateOrReplace(data, $"Assets/_Game/ScriptableObjects/Enemies/{fileName}.asset");
        }

        private static void CreateActivity(string fileName, string name, string desc, int cost, int emo, int str, int edu, int det)
        {
            var data = ScriptableObject.CreateInstance<ActivityData>();
            data.activityName = name;
            data.description = desc;
            data.actPointCost = cost;
            data.emotionDelta = emo;
            data.strengthDelta = str;
            data.eduPowerDelta = edu;
            data.determinationDelta = det;
            CreateOrReplace(data, $"Assets/_Game/ScriptableObjects/Activities/{fileName}.asset");
        }

        private static void CreateDDL(string fileName, string name, string desc, int deadline, float mult, int penalty)
        {
            var data = ScriptableObject.CreateInstance<DDLData>();
            data.ddlName = name;
            data.description = desc;
            data.deadlineDays = deadline;
            data.waveCountMultiplier = mult;
            data.gpaPenalty = penalty;
            CreateOrReplace(data, $"Assets/_Game/ScriptableObjects/DDLs/{fileName}.asset");
        }

        private static void CreateShopItem(string fileName, string name, string desc, int price, int emo, int str, int edu, int det, int gpa)
        {
            var data = ScriptableObject.CreateInstance<ShopItemData>();
            data.itemName = name;
            data.description = desc;
            data.price = price;
            data.emotionDelta = emo;
            data.strengthDelta = str;
            data.eduPowerDelta = edu;
            data.determinationDelta = det;
            data.gpaDelta = gpa;
            CreateOrReplace(data, $"Assets/_Game/ScriptableObjects/ShopItems/{fileName}.asset");
        }

        [MenuItem("FinalDefense/Create Prefabs Only")]
        public static void CreatePrefabs()
        {
            EnsureDirectories();
            CreateEnemyPrefab();
            CreateProjectilePrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void CreateEnemyPrefab()
        {
            var go = new GameObject("Enemy");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreateCircleSprite();
            sr.color = Color.red;
            sr.sortingOrder = 10;
            go.transform.localScale = Vector3.one * 1.0f;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.4f;

            go.AddComponent<Enemy.Enemy>();
            go.AddComponent<EnemyHealth>();
            go.AddComponent<EnemyAttack>();
            go.AddComponent<EnemySkill>();

            var barBg = new GameObject("HealthBarBG");
            barBg.transform.SetParent(go.transform);
            barBg.transform.localPosition = new Vector3(0, 0.6f, 0);
            var bgSr = barBg.AddComponent<SpriteRenderer>();
            bgSr.sprite = CreateSquareSprite();
            bgSr.color = Color.gray;
            bgSr.sortingOrder = 11;
            barBg.transform.localScale = new Vector3(1f, 0.15f, 1f);

            var barFill = new GameObject("HealthBarFill");
            barFill.transform.SetParent(go.transform);
            barFill.transform.localPosition = new Vector3(0, 0.6f, 0);
            var fillSr = barFill.AddComponent<SpriteRenderer>();
            fillSr.sprite = CreateSquareSprite();
            fillSr.color = Color.green;
            fillSr.sortingOrder = 12;
            barFill.transform.localScale = new Vector3(1f, 0.15f, 1f);

            var hb = go.AddComponent<EnemyHealthBar>();
            SetField(hb, "barFill", barFill.transform);

            PrefabUtility.SaveAsPrefabAsset(go, "Assets/_Game/Prefabs/Enemies/Enemy.prefab");
            Object.DestroyImmediate(go);
        }

        private static void CreateProjectilePrefab()
        {
            var go = new GameObject("Projectile");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreateCircleSprite();
            sr.color = Color.yellow;
            sr.sortingOrder = 6;
            go.transform.localScale = Vector3.one * 0.2f;
            go.AddComponent<Projectile>();
            PrefabUtility.SaveAsPrefabAsset(go, "Assets/_Game/Prefabs/Projectiles/Projectile.prefab");
            Object.DestroyImmediate(go);
        }

        private static void SetupPersonalityTestScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = CreateCamera(new Color(0.12f, 0.1f, 0.2f));

            // GameManager
            CreateGameManager();

            // Canvas
            var canvasGo = CreateCanvas();

            // PersonalityTestManager
            var testMgrGo = new GameObject("PersonalityTestManager");
            testMgrGo.AddComponent<PersonalityTestManager>();

            // Question text
            var questionText = CreateUIText(canvasGo.transform, "QuestionText", "问题", 28, new Vector2(0, 200));
            var progressText = CreateUIText(canvasGo.transform, "ProgressText", "1/10", 18, new Vector2(0, 270));

            // Option buttons
            var optionButtons = new UnityEngine.UI.Button[4];
            var optionTexts = new TMPro.TextMeshProUGUI[4];
            for (int i = 0; i < 4; i++)
            {
                var btnGo = CreateUIButton(canvasGo.transform, $"Option_{i}", $"选项{i + 1}", new Vector2(0, 80 - i * 70));
                btnGo.GetComponent<RectTransform>().sizeDelta = new Vector2(500, 50);
                optionButtons[i] = btnGo.GetComponent<UnityEngine.UI.Button>();
                optionTexts[i] = btnGo.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            }

            // Result panel
            var resultPanel = new GameObject("ResultPanel");
            resultPanel.transform.SetParent(canvasGo.transform, false);
            resultPanel.AddComponent<RectTransform>();
            var resultTypeText = CreateUIText(resultPanel.transform, "ResultType", "", 36, new Vector2(0, 50));
            var resultDescText = CreateUIText(resultPanel.transform, "ResultDesc", "", 20, new Vector2(0, -20));
            var contBtn = CreateUIButton(resultPanel.transform, "ContinueBtn", "开始游戏", new Vector2(0, -100));

            // PersonalityTestUI
            var testUI = canvasGo.AddComponent<PersonalityTestUI>();
            SetField(testUI, "questionText", questionText);
            SetField(testUI, "progressText", progressText);
            SetField(testUI, "optionButtons", optionButtons);
            SetField(testUI, "optionTexts", optionTexts);
            SetField(testUI, "resultPanel", resultPanel);
            SetField(testUI, "resultTypeText", resultTypeText);
            SetField(testUI, "resultDescText", resultDescText);
            SetField(testUI, "continueButton", contBtn.GetComponent<UnityEngine.UI.Button>());

            CreateEventSystem(scene);
            EditorSceneManager.SaveScene(scene, "Assets/_Game/Scenes/PersonalityTest.unity");
        }

        private static void SetupMainMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera(new Color(0.15f, 0.15f, 0.25f));
            CreateGameManager();

            var canvasGo = CreateCanvas();
            CreateUIText(canvasGo.transform, "TitleText", "期末保卫战", 48, new Vector2(0, 150));
            var gpaText = CreateUIText(canvasGo.transform, "GPAText", "GPA: 100", 24, new Vector2(0, 50));
            var gradeText = CreateUIText(canvasGo.transform, "GradeText", "年级: 1", 24, new Vector2(0, 10));
            var personalityText = CreateUIText(canvasGo.transform, "PersonalityText", "", 20, new Vector2(0, -30));
            var btnGo = CreateUIButton(canvasGo.transform, "StartButton", "开始新的一天", new Vector2(0, -100));

            var menuUI = canvasGo.AddComponent<MainMenuUI>();
            SetField(menuUI, "startButton", btnGo.GetComponent<UnityEngine.UI.Button>());
            SetField(menuUI, "gpaText", gpaText);
            SetField(menuUI, "gradeText", gradeText);
            SetField(menuUI, "personalityText", personalityText);

            CreateEventSystem(scene);
            EditorSceneManager.SaveScene(scene, "Assets/_Game/Scenes/MainMenu.unity");
        }

        private static void SetupScheduleScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera(new Color(0.1f, 0.15f, 0.2f));

            var canvasGo = CreateCanvas();
            CreateUIText(canvasGo.transform, "Title", "日程安排", 36, new Vector2(0, 400));
            var apText = CreateUIText(canvasGo.transform, "ActionPoints", "行动点: 5/5", 22, new Vector2(0, 350));
            var statsText = CreateUIText(canvasGo.transform, "StatsText", "", 18, new Vector2(0, 310));

            // Activity list container
            var listGo = new GameObject("ActivityList");
            listGo.transform.SetParent(canvasGo.transform, false);
            var listRt = listGo.AddComponent<RectTransform>();
            listRt.anchoredPosition = new Vector2(0, 50);
            listRt.sizeDelta = new Vector2(500, 400);
            var vlg = listGo.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            vlg.spacing = 10;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var startBattleBtn = CreateUIButton(canvasGo.transform, "StartBattle", "出发战斗！", new Vector2(0, -350));

            // Event Panel
            var eventTitleText = CreateUIText(canvasGo.transform, "EventTitle", "当前事件", 18, new Vector2(350, 350));
            var eventListGo = new GameObject("EventList");
            eventListGo.transform.SetParent(canvasGo.transform, false);
            var eventRt = eventListGo.AddComponent<RectTransform>();
            eventRt.anchoredPosition = new Vector2(350, 200);
            eventRt.sizeDelta = new Vector2(300, 300);
            var eventVlg = eventListGo.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            eventVlg.spacing = 5;

            // ScheduleManager
            var schMgrGo = new GameObject("ScheduleManager");
            var schMgr = schMgrGo.AddComponent<ScheduleManager>();
            var activities = LoadAllAssets<ActivityData>("Assets/_Game/ScriptableObjects/Activities");
            SetField(schMgr, "availableActivities", activities);

            // Event System
            var eventSysGo = new GameObject("EventSystem_Game");
            eventSysGo.AddComponent<FinalDefense.Event.EventSystem>();

            // Dialogue Button
            var dialogueBtn = CreateUIButton(canvasGo.transform, "DialogueBtn", "进入对话", new Vector2(350, -300));
            dialogueBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 50);

            // ScheduleUI
            var schUI = canvasGo.AddComponent<ScheduleUI>();
            SetField(schUI, "activityContainer", listGo.transform);
            SetField(schUI, "actionPointsText", apText);
            SetField(schUI, "statsText", statsText);
            SetField(schUI, "startBattleButton", startBattleBtn.GetComponent<UnityEngine.UI.Button>());

            CreateEventSystem(scene);
            EditorSceneManager.SaveScene(scene, "Assets/_Game/Scenes/Schedule.unity");
        }

        private static void SetupBattleScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6;
            cam.backgroundColor = new Color(0.1f, 0.12f, 0.2f);
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.AddComponent<AudioListener>();
            camGo.tag = "MainCamera";

            // Grid + Tilemaps
            var gridGo = new GameObject("Grid");
            gridGo.AddComponent<Grid>();

            var placeGo = new GameObject("Tilemap_Placeable");
            placeGo.transform.SetParent(gridGo.transform);
            var placeableTilemap = placeGo.AddComponent<Tilemap>();
            var placeRenderer = placeGo.AddComponent<TilemapRenderer>();
            placeRenderer.sortingOrder = 2;

            // Path Points (S-shape)
            var pathPoints = new GameObject("PathPoints");
            var pm = pathPoints.AddComponent<PathManager>();
            Vector3[] positions = {
                new Vector3(-7, 3, 0), new Vector3(-3, 3, 0),
                new Vector3(-3, 0, 0), new Vector3(3, 0, 0),
                new Vector3(3, -3, 0), new Vector3(7, -3, 0),
            };
            var waypointsList = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < positions.Length; i++)
            {
                var wp = new GameObject($"WP_{i}");
                wp.transform.SetParent(pathPoints.transform);
                wp.transform.position = positions[i];
                waypointsList.Add(wp.transform);
            }
            SetField(pm, "waypoints", waypointsList);

            // Path visual
            var pathVisual = new GameObject("PathVisual");
            var lr = pathVisual.AddComponent<LineRenderer>();
            lr.positionCount = positions.Length;
            lr.SetPositions(positions);
            lr.startWidth = 0.5f;
            lr.endWidth = 0.5f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = new Color(0.4f, 0.4f, 0.4f);
            lr.endColor = new Color(0.4f, 0.4f, 0.4f);
            lr.sortingOrder = 1;

            // Battle systems
            new GameObject("BattleManager").AddComponent<BattleManager>();
            var deployCostGo = new GameObject("DeployCostSystem");
            deployCostGo.AddComponent<DeployCostSystem>();

            var aiAgentGo = new GameObject("AIAgentSystem");
            aiAgentGo.AddComponent<AIAgentSystem>();

            new GameObject("BattleAnalytics").AddComponent<BattleAnalytics>();

            var bootstrapGo = new GameObject("BattleSceneBootstrap");
            bootstrapGo.AddComponent<BattleSceneBootstrap>();

            var retryGo = new GameObject("RetrySystem");
            retryGo.AddComponent<RetrySystem>();

            var spawnerGo = new GameObject("EnemySpawner");
            var spawner = spawnerGo.AddComponent<EnemySpawner>();
            var waveAsset = AssetDatabase.LoadAssetAtPath<WaveData>("Assets/_Game/ScriptableObjects/Waves/DemoWave.asset");
            SetField(spawner, "waveData", waveAsset);
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Enemies/Enemy.prefab");
            SetField(spawner, "defaultEnemyPrefab", enemyPrefab);

            var placementGo = new GameObject("TowerPlacement");
            var tp = placementGo.AddComponent<TowerPlacement>();
            SetField(tp, "groundTilemap", placeableTilemap);
            SetField(tp, "mainCamera", cam);

            var tilemapInitGo = new GameObject("TilemapInitializer");
            var tmInit = tilemapInitGo.AddComponent<TilemapInitializer>();
            SetField(tmInit, "placeableTilemap", placeableTilemap);

            // Canvas
            var canvasGo = CreateCanvas();

            var costText = CreateUIText(canvasGo.transform, "CostText", "费用: 10/100", 22, new Vector2(-300, 480));
            var gpaText = CreateUIText(canvasGo.transform, "GPAText", "GPA: 100", 22, new Vector2(0, 480));
            var waveText = CreateUIText(canvasGo.transform, "WaveText", "波次: 1/6", 22, new Vector2(300, 480));

            var hud = canvasGo.AddComponent<BattleHUD>();
            SetField(hud, "costText", costText);
            SetField(hud, "gpaText", gpaText);
            SetField(hud, "waveText", waveText);

            // AI Agent button
            var aiBtn = CreateUIButton(canvasGo.transform, "AIAgentBtn", "AI代写\n(扣GPA)", new Vector2(400, -300));
            aiBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(120, 60);
            var aiBtnComp = aiBtn.GetComponent<UnityEngine.UI.Button>();
            // Wire up AI agent on click via MonoBehaviour at runtime

            // Tower buttons
            var towerPanel = new GameObject("TowerPanel");
            towerPanel.transform.SetParent(canvasGo.transform, false);
            var tpRt = towerPanel.AddComponent<RectTransform>();
            tpRt.anchoredPosition = new Vector2(0, -450);
            tpRt.sizeDelta = new Vector2(600, 100);
            var hlg = towerPanel.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            hlg.spacing = 20;
            hlg.childAlignment = TextAnchor.MiddleCenter;

            var towerNames = new string[] { "笔记本\n5费", "计算器\n8费", "咖啡杯\n12费" };
            var towerColors = new Color[] {
                new Color(0.2f, 0.8f, 0.2f),
                new Color(0.2f, 0.4f, 0.9f),
                new Color(0.9f, 0.6f, 0.1f)
            };
            var towerAssetNames = new string[] { "Notebook", "Calculator", "Coffee" };

            for (int i = 0; i < towerNames.Length; i++)
            {
                var btnGo = new GameObject($"TowerBtn_{i}");
                btnGo.transform.SetParent(towerPanel.transform, false);
                var btnRt = btnGo.AddComponent<RectTransform>();
                btnRt.sizeDelta = new Vector2(150, 80);
                var btnImg = btnGo.AddComponent<UnityEngine.UI.Image>();
                btnImg.color = towerColors[i];
                var btn = btnGo.AddComponent<UnityEngine.UI.Button>();
                btn.targetGraphic = btnImg;

                var txtGo = new GameObject("Text");
                txtGo.transform.SetParent(btnGo.transform, false);
                var txtRt = txtGo.AddComponent<RectTransform>();
                txtRt.anchorMin = Vector2.zero;
                txtRt.anchorMax = Vector2.one;
                txtRt.offsetMin = Vector2.zero;
                txtRt.offsetMax = Vector2.zero;
                var tmp = txtGo.AddComponent<TMPro.TextMeshProUGUI>();
                tmp.text = towerNames[i];
                tmp.fontSize = 18;
                tmp.alignment = TMPro.TextAlignmentOptions.Center;
                tmp.color = Color.white;

                var towerBtn = btnGo.AddComponent<TowerButton>();
                var towerAsset = AssetDatabase.LoadAssetAtPath<TowerData>($"Assets/_Game/ScriptableObjects/Towers/{towerAssetNames[i]}.asset");
                SetField(towerBtn, "towerData", towerAsset);
                SetField(towerBtn, "towerPlacement", tp);
            }

            // Halftime Panel
            var halftimePanelGo = new GameObject("HalftimePanel");
            halftimePanelGo.transform.SetParent(canvasGo.transform, false);
            halftimePanelGo.AddComponent<RectTransform>();
            halftimePanelGo.AddComponent<HalftimePanel>();
            halftimePanelGo.SetActive(false);

            CreateEventSystem(scene);
            EditorSceneManager.SaveScene(scene, "Assets/_Game/Scenes/Battle.unity");
        }

        private static void SetupResultScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera(new Color(0.1f, 0.1f, 0.15f));
            var canvasGo = CreateCanvas();

            var resultText = CreateUIText(canvasGo.transform, "ResultText", "结果", 48, new Vector2(0, 150));
            var gpaText = CreateUIText(canvasGo.transform, "GPAText", "GPA: 100", 28, new Vector2(0, 70));
            var goldText = CreateUIText(canvasGo.transform, "GoldText", "获得金币: 0", 22, new Vector2(0, 30));
            var returnBtn = CreateUIButton(canvasGo.transform, "ReturnButton", "返回主菜单", new Vector2(0, -60));
            var shopBtn = CreateUIButton(canvasGo.transform, "ShopButton", "去小卖部", new Vector2(0, -130));
            shopBtn.GetComponent<UnityEngine.UI.Image>().color = new Color(0.3f, 0.7f, 0.3f);
            var retryBtn = CreateUIButton(canvasGo.transform, "RetryButton", "再试一次", new Vector2(0, -130));
            retryBtn.GetComponent<UnityEngine.UI.Image>().color = new Color(0.8f, 0.3f, 0.3f);

            var retrySystemGo = new GameObject("RetrySystem");
            retrySystemGo.AddComponent<RetrySystem>();

            var resultUI = canvasGo.AddComponent<ResultScreenUI>();
            SetField(resultUI, "resultText", resultText);
            SetField(resultUI, "gpaText", gpaText);
            SetField(resultUI, "goldText", goldText);
            SetField(resultUI, "returnButton", returnBtn.GetComponent<UnityEngine.UI.Button>());
            SetField(resultUI, "shopButton", shopBtn.GetComponent<UnityEngine.UI.Button>());
            SetField(resultUI, "retryButton", retryBtn.GetComponent<UnityEngine.UI.Button>());

            CreateEventSystem(scene);
            EditorSceneManager.SaveScene(scene, "Assets/_Game/Scenes/Result.unity");
        }

        private static void SetupDialogueScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera(new Color(0.12f, 0.12f, 0.18f));
            var canvasGo = CreateCanvas();

            CreateUIText(canvasGo.transform, "Title", "对话", 36, new Vector2(0, 420));

            // NPC selection panel
            var npcPanel = new GameObject("NPCPanel");
            npcPanel.transform.SetParent(canvasGo.transform, false);
            var npcRt = npcPanel.AddComponent<RectTransform>();
            npcRt.anchoredPosition = new Vector2(-250, 100);
            npcRt.sizeDelta = new Vector2(200, 500);
            var npcVlg = npcPanel.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            npcVlg.spacing = 8;
            npcVlg.childAlignment = TextAnchor.UpperCenter;
            npcVlg.childForceExpandWidth = true;
            npcVlg.childForceExpandHeight = false;

            string[] npcNames = { "室友", "闺蜜", "Crush", "竹马", "社团学长", "同系学弟", "专业课老师", "院长" };
            for (int i = 0; i < npcNames.Length; i++)
            {
                var npcBtn = CreateUIButton(npcPanel.transform, $"NPC_{i}", npcNames[i], Vector2.zero);
                npcBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(180, 40);
            }

            // Decision point slider
            CreateUIText(canvasGo.transform, "PointsLabel", "决策点: 50", 18, new Vector2(100, 350));
            var sliderGo = new GameObject("DecisionSlider");
            sliderGo.transform.SetParent(canvasGo.transform, false);
            var sliderRt = sliderGo.AddComponent<RectTransform>();
            sliderRt.anchoredPosition = new Vector2(100, 310);
            sliderRt.sizeDelta = new Vector2(400, 30);
            sliderGo.AddComponent<UnityEngine.UI.Image>().color = new Color(0.2f, 0.2f, 0.3f);
            var slider = sliderGo.AddComponent<UnityEngine.UI.Slider>();
            slider.minValue = 0;
            slider.maxValue = 100;
            slider.value = 50;

            // Chat area
            var chatArea = new GameObject("ChatArea");
            chatArea.transform.SetParent(canvasGo.transform, false);
            var chatRt = chatArea.AddComponent<RectTransform>();
            chatRt.anchoredPosition = new Vector2(100, 0);
            chatRt.sizeDelta = new Vector2(450, 400);
            chatArea.AddComponent<UnityEngine.UI.Image>().color = new Color(0.15f, 0.15f, 0.2f, 0.8f);

            // Input field
            var inputGo = new GameObject("InputField");
            inputGo.transform.SetParent(canvasGo.transform, false);
            var inputRt = inputGo.AddComponent<RectTransform>();
            inputRt.anchoredPosition = new Vector2(50, -280);
            inputRt.sizeDelta = new Vector2(350, 45);
            inputGo.AddComponent<UnityEngine.UI.Image>().color = new Color(0.2f, 0.2f, 0.25f);
            var inputField = inputGo.AddComponent<TMPro.TMP_InputField>();
            var inputText = new GameObject("Text");
            inputText.transform.SetParent(inputGo.transform, false);
            var inputTextRt = inputText.AddComponent<RectTransform>();
            inputTextRt.anchorMin = Vector2.zero;
            inputTextRt.anchorMax = Vector2.one;
            inputTextRt.offsetMin = new Vector2(10, 5);
            inputTextRt.offsetMax = new Vector2(-10, -5);
            var inputTmp = inputText.AddComponent<TMPro.TextMeshProUGUI>();
            inputTmp.fontSize = 16;
            inputTmp.color = Color.white;
            inputField.textComponent = inputTmp;

            // Send button
            var sendBtn = CreateUIButton(canvasGo.transform, "SendBtn", "发送", new Vector2(270, -280));
            sendBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(80, 45);

            // End dialogue button
            var endBtn = CreateUIButton(canvasGo.transform, "EndBtn", "结束对话", new Vector2(200, -350));
            endBtn.GetComponent<UnityEngine.UI.Image>().color = new Color(0.7f, 0.3f, 0.3f);

            // NPC stats display
            CreateUIText(canvasGo.transform, "NPCStats", "好感度: -- | 耐心: --", 14, new Vector2(100, -370));

            // DialogueManager
            var dlgMgrGo = new GameObject("DialogueManager");
            dlgMgrGo.AddComponent<FinalDefense.Dialogue.DialogueManager>();

            // DialogueUI
            canvasGo.AddComponent<DialogueUI>();

            CreateEventSystem(scene);
            EditorSceneManager.SaveScene(scene, "Assets/_Game/Scenes/Dialogue.unity");
        }

        private static void SetupShopScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera(new Color(0.12f, 0.15f, 0.1f));
            var canvasGo = CreateCanvas();

            CreateUIText(canvasGo.transform, "Title", "小卖部", 36, new Vector2(0, 400));
            var goldText = CreateUIText(canvasGo.transform, "GoldText", "金币: 0", 22, new Vector2(0, 350));

            var listGo = new GameObject("ItemList");
            listGo.transform.SetParent(canvasGo.transform, false);
            var listRt = listGo.AddComponent<RectTransform>();
            listRt.anchoredPosition = new Vector2(0, 50);
            listRt.sizeDelta = new Vector2(500, 500);
            var vlg = listGo.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            vlg.spacing = 10;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var leaveBtn = CreateUIButton(canvasGo.transform, "LeaveBtn", "离开 (进入下一天)", new Vector2(0, -380));
            leaveBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 50);

            // ShopManager
            var shopMgrGo = new GameObject("ShopManager");
            var shopMgr = shopMgrGo.AddComponent<ShopManager>();
            var items = LoadAllAssets<ShopItemData>("Assets/_Game/ScriptableObjects/ShopItems");
            SetField(shopMgr, "availableItems", items);

            // ShopUI
            var shopUI = canvasGo.AddComponent<ShopUI>();
            SetField(shopUI, "itemContainer", listGo.transform);
            SetField(shopUI, "goldText", goldText);
            SetField(shopUI, "leaveButton", leaveBtn.GetComponent<UnityEngine.UI.Button>());

            CreateEventSystem(scene);
            EditorSceneManager.SaveScene(scene, "Assets/_Game/Scenes/Shop.unity");
        }

        private static void SetupBuildSettings()
        {
            var scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene("Assets/_Game/Scenes/PersonalityTest.unity", true),
                new EditorBuildSettingsScene("Assets/_Game/Scenes/MainMenu.unity", true),
                new EditorBuildSettingsScene("Assets/_Game/Scenes/Schedule.unity", true),
                new EditorBuildSettingsScene("Assets/_Game/Scenes/Dialogue.unity", true),
                new EditorBuildSettingsScene("Assets/_Game/Scenes/Battle.unity", true),
                new EditorBuildSettingsScene("Assets/_Game/Scenes/Result.unity", true),
                new EditorBuildSettingsScene("Assets/_Game/Scenes/Shop.unity", true),
            };
            EditorBuildSettings.scenes = scenes;
        }

        // --- Helpers ---

        private static GameObject CreateCamera(Color bgColor)
        {
            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5;
            cam.backgroundColor = bgColor;
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.AddComponent<AudioListener>();
            camGo.tag = "MainCamera";
            return camGo;
        }

        private static void CreateGameManager()
        {
            var gmGo = new GameObject("GameManager");
            var gm = gmGo.AddComponent<GameManager>();
            var statsAsset = AssetDatabase.LoadAssetAtPath<PlayerStats>("Assets/_Game/ScriptableObjects/DefaultPlayerStats.asset");
            SetField(gm, "playerStats", statsAsset);
            var pConfigAsset = AssetDatabase.LoadAssetAtPath<PersonalityConfig>("Assets/_Game/ScriptableObjects/PersonalityConfig.asset");
            SetField(gm, "personalityConfig", pConfigAsset);
        }

        private static GameObject CreateCanvas()
        {
            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            return canvasGo;
        }

        private static void CreateEventSystem(UnityEngine.SceneManagement.Scene scene)
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        private static TMPro.TextMeshProUGUI CreateUIText(Transform parent, string name, string text, int fontSize, Vector2 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(500, 60);
            var tmp = go.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = Color.white;
            return tmp;
        }

        private static GameObject CreateUIButton(Transform parent, string name, string label, Vector2 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(200, 50);
            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.color = new Color(0.3f, 0.5f, 0.8f);
            var btn = go.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = img;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var tmp = textGo.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 20;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = Color.white;
            return go;
        }

        private static Sprite CreateCircleSprite()
        {
            int size = 64;
            Texture2D tex = new Texture2D(size, size);
            Color[] colors = new Color[size * size];
            float center = size / 2f;
            float radius = size / 2f - 1;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    colors[y * size + x] = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) <= radius ? Color.white : Color.clear;
            tex.SetPixels(colors);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
        }

        private static Sprite CreateSquareSprite()
        {
            Texture2D tex = new Texture2D(32, 32);
            Color[] colors = new Color[32 * 32];
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
            tex.SetPixels(colors);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 32, 32), Vector2.one * 0.5f, 32);
        }

        private static void CreateOrReplace(Object asset, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(asset, path);
        }

        private static T[] LoadAllAssets<T>(string folder) where T : Object
        {
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder });
            var result = new T[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                result[i] = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[i]));
            }
            return result;
        }

        private static void SetField(object obj, string fieldName, object value)
        {
            var type = obj.GetType();
            while (type != null)
            {
                var field = type.GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                if (field != null)
                {
                    field.SetValue(obj, value);
                    return;
                }
                type = type.BaseType;
            }
        }
    }
}
