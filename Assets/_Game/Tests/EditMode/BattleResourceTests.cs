using System.Linq;
using FinalDefense.Battle;
using FinalDefense.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FinalDefense.Tests
{
    public sealed class BattleResourceTests
    {
        [Test]
        public void NativeUnityJsonLoadsCatalogsAndBuildsTheAuthoredBattle()
        {
            var level = Resources.Load<TextAsset>("Battle/Level1");
            var towers = Resources.Load<TextAsset>("Battle/Towers");
            var enemies = Resources.Load<TextAsset>("Battle/Enemies");
            Assert.That(level, Is.Not.Null);
            Assert.That(towers, Is.Not.Null);
            Assert.That(enemies, Is.Not.Null);

            var config = JsonUtility.FromJson<BattleConfiguration>(level.text);
            config.towers = JsonUtility.FromJson<UnitCatalog>(towers.text).units;
            var opponentCatalog = JsonUtility.FromJson<UnitCatalog>(enemies.text).units;
            Assert.That(config.towers.Length, Is.EqualTo(16));
            Assert.That(opponentCatalog.Length, Is.EqualTo(16));
            Assert.That(config.towers.All(unit => unit.skill != null && unit.hp.Length == 3), Is.True);
            var simulation = new BattleSimulation(config);
            Assert.That(simulation.TotalEnemies, Is.EqualTo(41));
            Assert.That(simulation.TotalWaves, Is.EqualTo(4));
            Assert.That(simulation.PathLength, Is.EqualTo(32).Within(.001));
        }

        [Test]
        public void RegisteredBattleSceneContainsOneBootstrapAndNoMissingScripts()
        {
            const string path = "Assets/_Game/Scenes/Battle.unity";
            Assert.That(EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path == path), Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null);
            var scene = SceneManager.GetSceneByPath(path);
            bool openedByTest = !scene.IsValid() || !scene.isLoaded;
            if (openedByTest) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var bootstraps = roots.SelectMany(root => root.GetComponentsInChildren<BattleSceneBootstrap>(true)).ToArray();
                Assert.That(bootstraps.Length, Is.EqualTo(1));
                foreach (var transform in roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true)))
                    Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject), Is.Zero,
                        "Missing script on " + transform.name);
            }
            finally
            {
                if (openedByTest) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
