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
using System.Linq;

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

        private static async Task<string> GetRemoteCommitHash(string workingDir)
        {
            try
            {
                return await ExecuteGitCommand("rev-parse origin/main", workingDir);
            }
            catch
            {
                try
                {
                    return await ExecuteGitCommand("rev-parse origin/master", workingDir);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error getting remote commit hash: {e.Message}");
                    return null;
                }
            }
        }

        private static async Task<string> GetCurrentCommitHash(string workingDir)
        {
            try
            {
                return await ExecuteGitCommand("rev-parse HEAD", workingDir);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error getting current commit hash: {e.Message}");
                return null;
            }
        }

        private static async Task<bool> HasRemoteChanges(string workingDir)
        {
            string currentHash = await GetCurrentCommitHash(workingDir);
            string remoteHash = await GetRemoteCommitHash(workingDir);
            
            if (currentHash == null || remoteHash == null)
                return false;

            return currentHash != remoteHash;
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

        private static async Task<string> GetRemotePackageVersion(string workingDir)
        {
            try
            {
                string packageJson = await ExecuteGitCommand("show origin/main:package.json", workingDir);
                var remotePackageInfo = JsonUtility.FromJson<PackageInfo>(packageJson);
                return remotePackageInfo.version;
            }
            catch
            {
                try
                {
                    string packageJson = await ExecuteGitCommand("show origin/master:package.json", workingDir);
                    var remotePackageInfo = JsonUtility.FromJson<PackageInfo>(packageJson);
                    return remotePackageInfo.version;
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error getting remote package version: {e.Message}");
                    return null;
                }
            }
        }

        public static async Task<VersionInfo> CheckForUpdates()
        {
            string packagePath = GetPackagePath();
            
            try
            {
                // Fetch latest changes from remote
                await FetchLatestChanges(packagePath);

                // Check if we have any updates
                if (!await HasRemoteChanges(packagePath))
                {
                    return null; // No updates available
                }

                // Get remote version from package.json
                string remoteVersion = await GetRemotePackageVersion(packagePath);
                if (string.IsNullOrEmpty(remoteVersion))
                {
                    return null;
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
                var entries = ChangelogParser.ParseChangelog(tempChangelogPath);
                
                // Find the entry that matches the remote version
                var matchingEntry = entries.FirstOrDefault(e => e.Version == remoteVersion) ?? 
                                  entries.FirstOrDefault(e => e.Version == "Unreleased");

                if (matchingEntry == null)
                {
                    File.Delete(tempChangelogPath);
                    return null;
                }

                // Get remote commit hash
                string remoteHash = await GetRemoteCommitHash(packagePath);

                // Create version info from changelog
                var versionInfo = new VersionInfo
                {
                    version = remoteVersion,
                    releaseDate = matchingEntry.Date,
                    changes = matchingEntry.GetAllChanges(),
                    minUnityVersion = "2020.3", // This should ideally be read from the remote package.json
                    downloadUrl = "https://github.com/BrendanRobins97/TranslationDoneRight",
                    commitHash = remoteHash
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
                await ExecuteGitCommand($"checkout {targetCommit}", packagePath);

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