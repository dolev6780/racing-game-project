using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RacingGame.Race;

namespace RacingGame.EditorTools
{
    public static class RacingGameMenu
    {
        const string GameScenePath = "Assets/Scenes/Game.unity";
        const string QuickRaceScenePath = "Assets/Scenes/QuickRace.unity";

        /// <summary>Empty scene: the bootstrap opens the main menu. This is the scene to build.</summary>
        [MenuItem("Racing Game/Create Game Scene (Main Menu)")]
        static void CreateGameScene()
        {
            if (!CreateScene(GameScenePath, null)) return;
            AddToBuild(GameScenePath, first: true);
            Debug.Log("Game scene created at " + GameScenePath + " and set as the first scene in Build Settings. Press Play!");
        }

        /// <summary>Developer shortcut: skips the menu and starts a race immediately.</summary>
        [MenuItem("Racing Game/Create Quick Race Scene (dev)")]
        static void CreateQuickRaceScene()
        {
            if (!CreateScene(QuickRaceScenePath, () => new GameObject("RaceManager").AddComponent<RaceManager>())) return;
            Debug.Log("Quick race scene created at " + QuickRaceScenePath + ". Press Play to race without the menu.");
        }

        /// <summary>
        /// Makes sure the game scene exists and is the first scene in the build (no prompts, safe in batch mode).
        /// Returns its path.
        /// </summary>
        public static string EnsureGameScene()
        {
            if (!System.IO.File.Exists(GameScenePath))
            {
                System.IO.Directory.CreateDirectory("Assets/Scenes");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, GameScenePath);
            }
            AddToBuild(GameScenePath, first: true);
            return GameScenePath;
        }

        static bool CreateScene(string path, System.Action populate)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            populate?.Invoke();
            EditorSceneManager.SaveScene(scene, path);
            return true;
        }

        static void AddToBuild(string path, bool first)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != path).ToList();
            scenes.Insert(first ? 0 : scenes.Count, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        [MenuItem("Racing Game/Reset Saved Progress")]
        static void ResetProgress()
        {
            PlayerPrefs.DeleteAll();
            RacingGame.Progress.Stats.ClearCache();
            Debug.Log("Saved money and settings reset.");
        }
    }
}
