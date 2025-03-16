#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Debug = UnityEngine.Debug;

namespace Translations
{
    [Serializable]
    public class VersionInfo
    {
        public string version;
        public string releaseDate;
        public string[] changes;
        public string minUnityVersion;
        public string downloadUrl = "https://github.com/BrendanRobins97/TranslationDoneRight";
        public string commitHash;
    }

    public class VersionManager
    {
        private const string HIDE_UPDATE_NOTIFICATION_KEY = "TranslationsDoneRight_HideUpdateNotification";
        private const string LAST_CHECK_DATE_KEY = "TranslationsDoneRight_LastVersionCheck";
        private const string CURRENT_VERSION_KEY = "TranslationsDoneRight_CurrentVersion";
        
        public static bool ShouldShowUpdateNotification
        {
            get => !EditorPrefs.GetBool(HIDE_UPDATE_NOTIFICATION_KEY, false);
            set => EditorPrefs.SetBool(HIDE_UPDATE_NOTIFICATION_KEY, !value);
        }

        public static string CurrentVersion
        {
            get
            {
                string packageJsonPath = Path.Combine(Application.dataPath, "Translations Done Right/package.json");
                if (File.Exists(packageJsonPath))
                {
                    try
                    {
                        string jsonContent = File.ReadAllText(packageJsonPath);
                        var packageData = JsonUtility.FromJson<PackageInfo>(jsonContent);
                        return packageData.version;
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Error reading package version: {e.Message}");
                    }
                }
                return "1.0.0"; // Fallback version
            }
        }

        [Serializable]
        private class PackageInfo
        {
            public string version;
        }

        private static string GetPackagePath()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "Translations Done Right"));
        }

        private static async Task<string> ExecuteGitCommand(string command, string workingDir)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = command,
                    WorkingDirectory = workingDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process == null) throw new Exception("Failed to start git process");
                    
                    // Create tasks for reading output and error
                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errorTask = process.StandardError.ReadToEndAsync();

                    // Wait for the process to exit
                    process.WaitForExit();

                    // Get the output and error
                    string output = await outputTask;
                    string error = await errorTask;

                    if (process.ExitCode != 0)
                    {
                        throw new Exception($"Git command failed: {error}");
                    }

                    return output.Trim();
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Error executing git command: {e.Message}");
            }
        }

        private static async Task<(string hash, string message)> GetLatestCommitInfo(string workingDir)
        {
            string output = await ExecuteGitCommand("log -1 --format=%H%n%s", workingDir);
            string[] parts = output.Split('\n');
            if (parts.Length >= 2)
            {
                return (parts[0].Trim(), parts[1].Trim());
            }
            throw new Exception("Failed to get commit info");
        }

        private static async Task FetchLatestChanges(string workingDir)
        {
            await ExecuteGitCommand("fetch origin", workingDir);
        }

        private static async Task<bool> HasRemoteChanges(string workingDir)
        {
            try
            {
                string behindCount = await ExecuteGitCommand("rev-list HEAD..origin/main --count", workingDir);
                return int.Parse(behindCount) > 0;
            }
            catch
            {
                // Try master branch if main doesn't exist
                try
                {
                    string behindCount = await ExecuteGitCommand("rev-list HEAD..origin/master --count", workingDir);
                    return int.Parse(behindCount) > 0;
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error checking for remote changes: {e.Message}");
                    return false;
                }
            }
        }

        private static async Task<string> GetRemoteChangelogContent(string workingDir)
        {
            try
            {
                return await ExecuteGitCommand("show origin/main:CHANGELOG.md", workingDir);
            }
            catch
            {
                try
                {
                    return await ExecuteGitCommand("show origin/master:CHANGELOG.md", workingDir);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error getting remote changelog: {e.Message}");
                    return null;
                }
            }
        }

        public static async Task<VersionInfo> CheckForUpdates()
        {
            string packagePath = GetPackagePath();
            string changelogPath = Path.Combine(packagePath, "CHANGELOG.md");
            
            try
            {
                // Fetch latest changes from remote
                await FetchLatestChanges(packagePath);

                // Check if we have any updates
                if (!await HasRemoteChanges(packagePath))
                {
                    return null; // No updates available
                }

                // Get remote changelog content
                string remoteChangelogContent = await GetRemoteChangelogContent(packagePath);
                if (string.IsNullOrEmpty(remoteChangelogContent))
                {
                    return null;
                }

                // Write remote changelog to a temporary file
                string tempChangelogPath = Path.Combine(Path.GetTempPath(), "CHANGELOG.md");
                File.WriteAllText(tempChangelogPath, remoteChangelogContent);

                // Parse the changelog
                var unreleasedChanges = ChangelogParser.GetLatestUnreleasedChanges(tempChangelogPath);
                if (unreleasedChanges == null)
                {
                    return null;
                }

                // Get current commit info
                var (currentHash, _) = await GetLatestCommitInfo(packagePath);

                // Create version info from changelog
                var versionInfo = new VersionInfo
                {
                    version = unreleasedChanges.Version,
                    releaseDate = unreleasedChanges.Date,
                    changes = unreleasedChanges.GetAllChanges(),
                    minUnityVersion = "2020.3", // This should ideally be read from the remote package.json
                    downloadUrl = "https://github.com/BrendanRobins97/TranslationDoneRight",
                    commitHash = currentHash
                };

                // Clean up temporary file
                File.Delete(tempChangelogPath);

                return versionInfo;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error checking for updates: {e.Message}");
                return null;
            }
        }

        public static void ShowUpdateNotificationIfNeeded()
        {
            if (!ShouldShowUpdateNotification) return;

            // Check if we've already checked recently (e.g., in the last 24 hours)
            string lastCheckStr = EditorPrefs.GetString(LAST_CHECK_DATE_KEY, "");
            if (!string.IsNullOrEmpty(lastCheckStr))
            {
                if (DateTime.TryParse(lastCheckStr, out DateTime lastCheck))
                {
                    if ((DateTime.Now - lastCheck).TotalHours < 24)
                    {
                        return;
                    }
                }
            }

            // Update last check date
            EditorPrefs.SetString(LAST_CHECK_DATE_KEY, DateTime.Now.ToString("o"));

            // Start async version check
            CheckForUpdatesAndShowWindow();
        }

        private static async void CheckForUpdatesAndShowWindow()
        {
            try
            {
                var updateInfo = await CheckForUpdates();
                if (updateInfo != null)
                {
                    UpdateNotificationWindow.ShowWindow(updateInfo);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error checking for updates: {e.Message}");
            }
        }

        public static async Task<bool> UpdatePackage(string targetCommit)
        {
            string packagePath = GetPackagePath();
            
            try
            {
                // First stash any local changes
                await ExecuteGitCommand("stash", packagePath);

                // Fetch latest changes
                await ExecuteGitCommand("fetch origin", packagePath);

                // Checkout the specific commit
                string result = await ExecuteGitCommand($"checkout {targetCommit}", packagePath);

                // Pop stashed changes if any
                try
                {
                    await ExecuteGitCommand("stash pop", packagePath);
                }
                catch
                {
                    // Ignore stash pop errors as there might not be any stashed changes
                }

                // Force Unity to refresh assets
                AssetDatabase.Refresh();

                Debug.Log($"Successfully updated package to version {targetCommit}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to update package: {e.Message}");
                return false;
            }
        }
    }
}
#endif 