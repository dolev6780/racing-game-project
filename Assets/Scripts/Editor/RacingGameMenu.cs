using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RacingGame.Race;

namespace RacingGame.EditorTools
{
    public static class RacingGameMenu
    {
        const string ScenePath = "Assets/Scenes/Race.unity";

        [MenuItem("Racing Game/Create Race Scene")]
        static void CreateRaceScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("RaceManager").AddComponent<RaceManager>();
            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            Debug.Log("Race scene created at " + ScenePath + " and added to Build Settings. Press Play!");
        }

        [MenuItem("Racing Game/Reset Saved Money")]
        static void ResetMoney()
        {
            PlayerPrefs.DeleteKey("money");
            Debug.Log("Saved money reset.");
        }
    }
}
