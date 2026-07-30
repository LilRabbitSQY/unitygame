using UnityEngine;
using UnityEngine.SceneManagement;

namespace FinalDefense.Core
{
    public static class SceneLoader
    {
        public const string MainMenuScene = "MainMenu";
        public const string BattleScene = "Battle";
        public const string ResultScene = "Result";
        public const string ScheduleScene = "Schedule";
        public const string PersonalityTestScene = "PersonalityTest";
        public const string ShopScene = "Shop";
        public const string DialogueScene = "Dialogue";

        public static void LoadScene(string sceneName)
        {
            SceneManager.LoadScene(sceneName);
        }

        public static void LoadMainMenu() => LoadScene(MainMenuScene);
        public static void LoadBattle() => LoadScene(BattleScene);
        public static void LoadResult() => LoadScene(ResultScene);
        public static void LoadSchedule() => LoadScene(ScheduleScene);
        public static void LoadPersonalityTest() => LoadScene(PersonalityTestScene);
        public static void LoadShop() => LoadScene(ShopScene);
        public static void LoadDialogue() => LoadScene(DialogueScene);
    }
}
