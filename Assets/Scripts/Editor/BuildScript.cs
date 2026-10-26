using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RacingGame.EditorTools
{
    /// <summary>
    /// Command-line / CI builds (GameCI calls BuildAndroid through "buildMethod").
    /// Sets up URP and the game scene first, so a fresh clone builds without opening the editor.
    /// </summary>
    public static class BuildScript
    {
        [MenuItem("Racing Game/Build Android APK")]
        public static void BuildAndroid()
        {
            UrpSetup.Ensure(true);
            string scene = RacingGameMenu.EnsureGameScene();

            string path = Arg("-customBuildPath") ?? "build/Android/RacingGame.apk";
            if (!path.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)) path = Path.Combine(path, "RacingGame.apk");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));

            // 64-bit ARM is required by newer phones (and by Google Play).
            EditorUserBuildSettings.buildAppBundle = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.productName = "Riot Racers";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scene },
                locationPathName = path,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            });

            var summary = report.summary;
            Debug.Log($"Build {summary.result}: {path} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalErrors} errors)");
            if (Application.isBatchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
