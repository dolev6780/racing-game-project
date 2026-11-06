using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace RacingGame.EditorTools
{
    /// <summary>
    /// Keeps a cloned copy of the project up to date: pulls the latest commits from GitHub when the editor
    /// opens, and from Cloudborne > Update from GitHub. Fast-forward only, so local changes are never
    /// touched (git refuses and the Console says why). Needs git: Git for Windows, macOS's git, or the
    /// copy inside GitHub Desktop. A project downloaded as a zip has no git history and is left alone.
    /// </summary>
    [InitializeOnLoad]
    public static class GitUpdate
    {
        const string CheckedKey = "RacingGame.GitUpdate.Checked";
        static Task<string> pulling;

        static GitUpdate()
        {
            // Once per editor session: scripts reload after every pull, and this runs again on each reload.
            if (Application.isBatchMode || SessionState.GetBool(CheckedKey, false)) return;
            SessionState.SetBool(CheckedKey, true);
            EditorApplication.delayCall += () => Pull(false);
        }

        [MenuItem("Cloudborne/Update from GitHub")]
        static void Menu() => Pull(true);

        static void Pull(bool fromMenu)
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            if (!Directory.Exists(Path.Combine(root, ".git")))
            {
                if (fromMenu) Debug.LogWarning("Update: this folder is not a git clone (was it downloaded as a zip?). Clone it with GitHub Desktop instead.");
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Update: stop Play mode first.");
                return;
            }
            if (pulling != null) return;
            string git = FindGit();
            if (git == null)
            {
                Debug.LogWarning("Update: git was not found. Install GitHub Desktop (desktop.github.com) or Git, or pull from GitHub Desktop.");
                return;
            }
            Debug.Log("Update: checking GitHub for a newer version...");
            pulling = Task.Run(() => Run(git, "pull --ff-only", root));
            EditorApplication.update += Finish;
        }

        static void Finish()
        {
            if (!pulling.IsCompleted) return;
            EditorApplication.update -= Finish;
            string result = pulling.IsFaulted ? "error: " + pulling.Exception.GetBaseException().Message : pulling.Result;
            pulling = null;
            if (result.StartsWith("error:"))
                Debug.LogWarning("Update failed. If you changed files in this folder, discard the changes in GitHub Desktop and try again.\n" + result);
            else if (result.Contains("Already up"))
                Debug.Log("Update: already the latest version.");
            else
            {
                Debug.Log("Update: pulled the latest version from GitHub.\n" + result);
                AssetDatabase.Refresh();
            }
        }

        static string Run(string git, string args, string dir)
        {
            var info = new ProcessStartInfo(git, args)
            {
                WorkingDirectory = dir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            info.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";   // never wait for a password prompt
            using (var p = Process.Start(info))
            {
                var output = p.StandardOutput.ReadToEndAsync();
                var errors = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(120000))
                {
                    p.Kill();
                    return "error: git did not finish within two minutes";
                }
                return p.ExitCode == 0 ? output.Result : "error: " + errors.Result + output.Result;
            }
        }

        /// <summary>git on the PATH, Git for Windows, Homebrew or Xcode's git, or the one bundled with GitHub Desktop.</summary>
        static string FindGit()
        {
            bool windows = Application.platform == RuntimePlatform.WindowsEditor;
            string exe = windows ? "git.exe" : "git";
            foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                try
                {
                    if (dir.Length > 0 && File.Exists(Path.Combine(dir, exe))) return Path.Combine(dir, exe);
                }
                catch (ArgumentException) { }   // a malformed PATH entry
            }
            if (windows)
            {
                string programs = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string installed = Path.Combine(programs, "Git", "cmd", "git.exe");
                if (File.Exists(installed)) return installed;
                string desktop = Path.Combine(local, "GitHubDesktop");
                if (Directory.Exists(desktop))
                {
                    string[] apps = Directory.GetDirectories(desktop, "app-*");
                    Array.Sort(apps);
                    for (int i = apps.Length - 1; i >= 0; i--)
                    {
                        string bundled = Path.Combine(apps[i], "resources", "app", "git", "cmd", "git.exe");
                        if (File.Exists(bundled)) return bundled;
                    }
                }
                return null;
            }
            foreach (string path in new[] { "/usr/bin/git", "/opt/homebrew/bin/git", "/usr/local/bin/git",
                         "/Applications/GitHub Desktop.app/Contents/Resources/app/git/bin/git" })
                if (File.Exists(path)) return path;
            return null;
        }
    }
}
