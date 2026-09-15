using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;
using FinalDefense.Combat;
using FinalDefense.Core;
using UnityEngine.UI;

namespace FinalDefense.Battle
{
    public sealed class BattleSceneBootstrap : MonoBehaviour
    {
        public static BattleConfiguration NextBattle;
        [SerializeField] private TMP_FontAsset chineseFont;
        private void Awake()
        {
            Time.timeScale = 1;
            if (Camera.main == null)
            {
                var cameraObject = new GameObject("Main Camera", typeof(Camera));
                cameraObject.tag = "MainCamera";
                cameraObject.GetComponent<Camera>().backgroundColor = new Color(.95f,.96f,.98f);
                cameraObject.GetComponent<Camera>().orthographic = true;
                cameraObject.GetComponent<Camera>().orthographicSize = 6;
                cameraObject.transform.position = new Vector3(0,0,-10);
                cameraObject.AddComponent<AudioListener>();
            }
            if (GameManager.Instance == null) new GameObject("GameManager").AddComponent<GameManager>();
            var eventSystem = FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem));
                eventSystem = events.GetComponent<EventSystem>();
            }
            foreach (var module in eventSystem.GetComponents<BaseInputModule>())
                if (!(module is InputSystemUIInputModule)) module.enabled = false;
            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            var level = Resources.Load<TextAsset>("Battle/Level1");
            var towers = Resources.Load<TextAsset>("Battle/Towers");
            if (level == null || towers == null)
            {
                Debug.LogError("Battle resources are missing: Battle/Level1 and Battle/Towers are required.");
                return;
            }
            var config = JsonUtility.FromJson<BattleConfiguration>(level.text);
            config.towers = JsonUtility.FromJson<UnitCatalog>(towers.text).units;
            var manager = GameManager.Instance;
            if (manager.Phase == CampaignPhase.Battle && GameManager.IsUsableBattleConfiguration(manager.CurrentBattleConfiguration))
                config = manager.CurrentBattleConfiguration;
            if (NextBattle != null)
            {
                if (GameManager.IsUsableBattleConfiguration(NextBattle)) config = NextBattle;
                NextBattle = null;
            }
            if (manager.PersonalitySelected)
                config.protection = Mathf.Min(config.protection, Mathf.Max(1, Mathf.CeilToInt(manager.CurrentGPA)));
            if (!manager.BeginBattle(config))
            {
                if (manager.Phase == CampaignPhase.Shop) SceneLoader.LoadShop();
                else SceneLoader.LoadResult();
                return;
            }
            manager.PrepareBattleItems(config);
            var canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("Battle requires its authored Canvas and HUD. Reopen Assets/_Game/Scenes/Battle.unity.");
                return;
            }
            var view = canvas.gameObject.AddComponent<BattleView>();
            view.Font = chineseFont;
            view.Initialize(config);
        }
    }
}
