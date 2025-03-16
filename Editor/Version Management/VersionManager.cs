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
        public bool HasUpdate;
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
                // First try Assets folder
                string packageJsonPath = Path.Combine(Application.dataPath, "Translations Done Right/package.json");
                
                // If not in Assets, check Package Cache
                if (!File.Exists(packageJsonPath))
                {
                    // Find the package in the packages-lock.json to get its version
                    string packagesLockPath = GetPackagesLockPath();
                    if (packagesLockPath != null)
                    {
                        Debug.Log($"[Version Check] Found packages-lock.json at: {packagesLockPath}");
                        try
                        {
                            string jsonContent = File.ReadAllText(packagesLockPath);
                            // Parse the packages-lock.json to get the hash
                            var packageLock = JsonUtility.FromJson<PackageLockInfo>(jsonContent);
                            
                            if (packageLock?.dependencies != null && 
                                packageLock.dependencies.ContainsKey("com.flamboozle.translations-done-right"))
                            {
                                var packageInfo = packageLock.dependencies["com.flamboozle.translations-done-right"];
                                string hash = packageInfo.hash;
                                
                                if (!string.IsNullOrEmpty(hash))
                                {
                                    // Get the tag for this commit hash using git
                                    try
                                    {
                                        string packagePath = GetPackagePath();
                                        if (packagePath != null)
                                        {
                                            // Try to get the tag that points to this commit
                                            var tagTask = ExecuteGitCommand($"describe --tags --exact-match {hash}", packagePath);
                                            tagTask.Wait();
                                            string tag = tagTask.Result;
                                            
                                            if (!string.IsNullOrEmpty(tag))
                                            {
                                                // Remove the 'v' prefix if present
                                                if (tag.StartsWith("v"))
                                                {
                                                    tag = tag.Substring(1);
                                                }
                                                return tag;
                                            }
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        Debug.LogWarning($"[Version Check] Could not get version from git tag: {e.Message}");
                                    }
                                    
                                    // If we couldn't get the tag, try to get version from package.json at this commit
                                    try
                                    {
                                        var packageJsonTask = ExecuteGitCommand($"show {hash}:package.json", packagePath);
                                        packageJsonTask.Wait();
                                        string packageJson = packageJsonTask.Result;
                                        var packageData = JsonUtility.FromJson<PackageInfo>(packageJson);
                                        if (!string.IsNullOrEmpty(packageData?.version))
                                        {
                                            return packageData.version;
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        Debug.LogWarning($"[Version Check] Could not get version from package.json at commit: {e.Message}");
                                    }
                                }
                            }
                            else
                            {
                                Debug.LogError("[Version Check] Package not found in packages-lock.json");
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[Version Check] Error reading package version from packages-lock.json: {e.Message}");
                        }
                    }
                }
                else
                {
                    try
                    {
                        string jsonContent = File.ReadAllText(packageJsonPath);
                        var packageData = JsonUtility.FromJson<PackageInfo>(jsonContent);
                        return packageData.version;
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[Version Check] Error reading package version from package.json: {e.Message}");
                    }
                }
                
                return "Unknown"; // Changed from "1.0.0" to "Unknown" to be more accurate
            }
        }

        [Serializable]
        private class PackageInfo
        {
            public string version;
        }

        [Serializable]
        private class PackageLockInfo
        {
            public Dictionary<string, PackageLockEntry> dependencies;
        }

        [Serializable]
        private class PackageLockEntry
        {
            public string version;
            public string source;
            public string hash;
        }

        private static string GetPackagePath()
        {
            // First check if the package is in the Assets folder
            string assetsPath = Path.GetFullPath(Path.Combine(Application.dataPath, "Translations Done Right"));
            if (Directory.Exists(assetsPath))
            {
                return assetsPath;
            }

            // If not in Assets, it must be in the package cache
            // We don't need the actual path for Git operations when using package cache
            // since we'll be using packages-lock.json instead
            return null;
        }

        private static string GetPackagesLockPath()
        {
            string projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            
            // Try the Unity 2021+ location first
            string packagesLockPath = Path.Combine(projectPath, "Packages", "packages-lock.json");
            if (File.Exists(packagesLockPath))
            {
                return packagesLockPath;
            }

            // Fallback to the root location for older Unity versions
            packagesLockPath = Path.Combine(projectPath, "packages-lock.json");
            if (File.Exists(packagesLockPath))
            {
                return packagesLockPath;
            }

            return null;
        }

        private static async Task<string> ExecuteGitCommand(string command, string workingDir)
        {
            try
            {
                Debug.Log($"[Version Check] Executing git command: {command}");
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

                    Debug.Log($"[Version Check] Git command completed successfully");
                    return output.Trim();
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Error executing git command '{command}': {e.Message}");
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

        private static async Task<(string currentHash, string remoteHash)> GetPackageHashes()
        {
            try
            {
                // Find the packages-lock.json file in the Unity project root
                string projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string packagesLockPath = Path.Combine(projectPath, "packages-lock.json");
                
                if (!File.Exists(packagesLockPath))
                {
                    Debug.LogError("[Version Check] packages-lock.json not found");
                    return (null, null);
                }

                // Read and parse the packages-lock.json file
                string jsonContent = File.ReadAllText(packagesLockPath);
                var packageLock = JsonUtility.FromJson<PackageLockInfo>(jsonContent);

                if (packageLock?.dependencies == null || 
                    !packageLock.dependencies.ContainsKey("com.flamboozle.translations-done-right"))
                {
                    Debug.LogError("[Version Check] Package not found in packages-lock.json");
                    return (null, null);
                }

                var packageInfo = packageLock.dependencies["com.flamboozle.translations-done-right"];
                string currentHash = packageInfo.hash;

                // Fetch latest changes to get remote hash
                string packagePath = GetPackagePath();
                await FetchLatestChanges(packagePath);
                string remoteHash = await GetRemoteCommitHash(packagePath);

                Debug.Log($"[Version Check] Current package hash: {currentHash}");
                Debug.Log($"[Version Check] Remote package hash: {remoteHash}");

                return (currentHash, remoteHash);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Version Check] Error getting package hashes: {e.Message}");
                return (null, null);
            }
        }

        private static async Task<bool> HasRemoteChanges(string workingDir)
        {
            try
            {
                // Get the current version
                string currentVersion = CurrentVersion;
                if (currentVersion == "Unknown")
                {
                    Debug.LogError("[Version Check] Could not determine current version");
                    return false;
                }

                // Get the latest version from GitHub
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("Unity/1.0");
                    
                    // Get the latest release version from GitHub API
                    var response = await client.GetAsync("https://api.github.com/repos/BrendanRobins97/TranslationDoneRight/releases/latest");
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var versionMatch = Regex.Match(content, "\"tag_name\":\\s*\"v([^\"]+)\"");
                        if (versionMatch.Success)
                        {
                            string latestVersion = versionMatch.Groups[1].Value;
                            Debug.Log($"[Version Check] Current version: {currentVersion}, Latest version: {latestVersion}");
                            
                            // Compare versions
                            var current = new Version(currentVersion);
                            var latest = new Version(latestVersion);
                            
                            return latest > current;
                        }
                    }
                    else
                    {
                        Debug.LogError($"[Version Check] Failed to get latest release: {response.StatusCode}");
                    }
                }

                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Version Check] Error checking for updates: {e.Message}");
                return false;
            }
        }

        private static async Task<string> GetRemoteChangelogContent(string workingDir)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "Unity");
                    var response = await client.GetAsync("https://raw.githubusercontent.com/BrendanRobins97/TranslationDoneRight/main/CHANGELOG.md");
                    if (response.IsSuccessStatusCode)
                    {
                        return await response.Content.ReadAsStringAsync();
                    }
                }
                return null;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error getting remote changelog: {e.Message}");
                return null;
            }
        }

        private static async Task<string> GetRemotePackageVersion(string workingDir)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "Unity");
                    var response = await client.GetAsync("https://raw.githubusercontent.com/BrendanRobins97/TranslationDoneRight/main/package.json");
                    if (response.IsSuccessStatusCode)
                    {
                        string packageJson = await response.Content.ReadAsStringAsync();
                        var remotePackageInfo = JsonUtility.FromJson<PackageInfo>(packageJson);
                        return remotePackageInfo.version;
                    }
                }
                return null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Version Check] Error getting remote package version: {e.Message}");
                return null;
            }
        }

        public static async Task<VersionInfo> CheckForUpdates()
        {
            string packagePath = GetPackagePath();
            Debug.Log($"[Version Check] Starting version check...");
            
            try
            {
                // Check if we have any updates using packages-lock.json and GitHub API
                Debug.Log("[Version Check] Checking for remote changes...");
                bool hasUpdates = await HasRemoteChanges(packagePath);
                Debug.Log(hasUpdates ? "[Version Check] Remote changes detected!" : "[Version Check] No remote changes found.");

                // Get remote version from package.json
                Debug.Log("[Version Check] Getting remote package version...");
                string remoteVersion = await GetRemotePackageVersion(packagePath);
                if (string.IsNullOrEmpty(remoteVersion))
                {
                    Debug.LogError("[Version Check] Failed to get remote package version.");
                    return null;
                }
                Debug.Log($"[Version Check] Remote version: {remoteVersion}, Current version: {CurrentVersion}");

                // Get remote changelog content
                Debug.Log("[Version Check] Getting remote changelog content...");
                string remoteChangelogContent = await GetRemoteChangelogContent(packagePath);
                if (string.IsNullOrEmpty(remoteChangelogContent))
                {
                    Debug.LogError("[Version Check] Failed to get remote changelog content.");
                    return null;
                }
                Debug.Log("[Version Check] Successfully retrieved remote changelog.");

                // Write remote changelog to a temporary file
                string tempChangelogPath = Path.Combine(Path.GetTempPath(), "CHANGELOG.md");
                File.WriteAllText(tempChangelogPath, remoteChangelogContent);

                // Parse the changelog
                var entries = ChangelogParser.ParseChangelog(tempChangelogPath);
                Debug.Log($"[Version Check] Found {entries.Count} changelog entries.");
                
                // Always get the latest entry (either Unreleased or the most recent version)
                var latestEntry = entries.FirstOrDefault(e => e.Version == "Unreleased") ?? 
                                 entries.OrderByDescending(e => e.Version).FirstOrDefault();

                if (latestEntry == null)
                {
                    Debug.LogError("[Version Check] No changelog entries found");
                    File.Delete(tempChangelogPath);
                    return null;
                }
                Debug.Log($"[Version Check] Found latest changelog entry for version {latestEntry.Version}");

                // Get the latest commit hash from GitHub API
                string remoteHash = null;
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "Unity");
                    var response = await client.GetAsync("https://api.github.com/repos/BrendanRobins97/TranslationDoneRight/commits/main");
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var match = Regex.Match(content, "\"sha\":\\s*\"([a-f0-9]+)\"");
                        if (match.Success)
                        {
                            remoteHash = match.Groups[1].Value;
                        }
                    }
                }

                if (string.IsNullOrEmpty(remoteHash))
                {
                    Debug.LogError("[Version Check] Failed to get remote commit hash");
                    return null;
                }

                // Create version info from changelog
                var versionInfo = new VersionInfo
                {
                    version = remoteVersion,
                    releaseDate = latestEntry.Date,
                    changes = latestEntry.GetAllChanges(),
                    minUnityVersion = "2020.3", // This should ideally be read from the remote package.json
                    downloadUrl = "https://github.com/BrendanRobins97/TranslationDoneRight",
                    commitHash = remoteHash,
                    HasUpdate = hasUpdates
                };

                // Clean up temporary file
                File.Delete(tempChangelogPath);
                Debug.Log("[Version Check] Successfully created version info.");
                Debug.Log($"[Version Check] Update available: {versionInfo.HasUpdate}");

                return versionInfo;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Version Check] Error checking for updates: {e.Message}\nStack trace: {e.StackTrace}");
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

        public static async void CheckForUpdatesAndShowWindow()
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

                // Update the package hash in packages-lock.json
                string packagesLockPath = GetPackagesLockPath();
                if (packagesLockPath != null)
                {
                    string jsonContent = File.ReadAllText(packagesLockPath);
                    var packageLock = JsonUtility.FromJson<PackageLockInfo>(jsonContent);
                    
                    if (packageLock?.dependencies != null && 
                        packageLock.dependencies.ContainsKey("com.flamboozle.translations-done-right"))
                    {
                        packageLock.dependencies["com.flamboozle.translations-done-right"].hash = targetCommit;
                        File.WriteAllText(packagesLockPath, JsonUtility.ToJson(packageLock, true));
                    }
                }

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