#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.IO;

namespace Translations
{
    public static class UpdateNotificationWindowTests
    {
        [MenuItem("Window/Translations/Test Update Window/Latest Changes (From Changelog)")]
        public static void TestLatestChanges()
        {
            string changelogPath = Path.Combine(Application.dataPath, "Translations Done Right/CHANGELOG.md");
            var unreleasedChanges = ChangelogParser.GetLatestUnreleasedChanges(changelogPath);
            
            if (unreleasedChanges != null)
            {
                var updateInfo = new VersionInfo
                {
                    version = unreleasedChanges.Version,
                    releaseDate = unreleasedChanges.Date,
                    changes = unreleasedChanges.GetAllChanges(),
                    minUnityVersion = "2020.3",
                    downloadUrl = "https://github.com/BrendanRobins97/TranslationDoneRight",
                    commitHash = "HEAD" // Using HEAD as this is just a test
                };

                UpdateNotificationWindow.ShowWindow(updateInfo);
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "No Changes Found", 
                    "No unreleased changes found in the CHANGELOG.md file. Make sure the file exists and has an [Unreleased] section.", 
                    "OK"
                );
            }
        }

        [MenuItem("Window/Translations/Test Update Window/Minor Update")]
        public static void TestMinorUpdate()
        {
            var currentVersion = VersionManager.CurrentVersion;
            var versionParts = currentVersion.Split('.');
            if (versionParts.Length >= 3 && int.TryParse(versionParts[2], out int patch))
            {
                var updateInfo = new VersionInfo
                {
                    version = $"{versionParts[0]}.{versionParts[1]}.{patch + 1}",
                    releaseDate = DateTime.Now.ToString("yyyy-MM-dd"),
                    changes = new string[]
                    {
                        "Fixed scene extraction progress reporting",
                        "Improved error handling in prefab processing",
                        "Added new test cases for text similarity detection"
                    },
                    minUnityVersion = "2020.3",
                    downloadUrl = "https://github.com/BrendanRobins97/TranslationDoneRight",
                    commitHash = "abc123" // Simulated commit hash
                };

                UpdateNotificationWindow.ShowWindow(updateInfo);
            }
        }

        [MenuItem("Window/Translations/Test Update Window/Major Update")]
        public static void TestMajorUpdate()
        {
            var currentVersion = VersionManager.CurrentVersion;
            var versionParts = currentVersion.Split('.');
            if (versionParts.Length >= 2 && int.TryParse(versionParts[0], out int major))
            {
                var updateInfo = new VersionInfo
                {
                    version = $"{major + 1}.0.0",
                    releaseDate = DateTime.Now.ToString("yyyy-MM-dd"),
                    changes = new string[]
                    {
                        "🎉 Major Update: Version 2.0",
                        "Complete rewrite of the extraction engine",
                        "Added support for Unity DOTS",
                        "New AI-powered similarity detection",
                        "Improved memory usage and performance",
                        "Added support for custom extraction rules"
                    },
                    minUnityVersion = "2021.3",
                    downloadUrl = "https://github.com/BrendanRobins97/TranslationDoneRight",
                    commitHash = "def456" // Simulated commit hash
                };

                UpdateNotificationWindow.ShowWindow(updateInfo);
            }
        }

        [MenuItem("Window/Translations/Test Update Window/Breaking Update")]
        public static void TestBreakingUpdate()
        {
            var updateInfo = new VersionInfo
            {
                version = "2.0.0-preview.1",
                releaseDate = DateTime.Now.ToString("yyyy-MM-dd"),
                changes = new string[]
                {
                    "⚠️ BREAKING CHANGES - Preview Release",
                    "Completely new API for better type safety",
                    "Migration required: Old translation files need conversion",
                    "New extraction pipeline with async support",
                    "Dropped support for Unity versions below 2021.3"
                },
                minUnityVersion = "2021.3",
                downloadUrl = "https://github.com/BrendanRobins97/TranslationDoneRight",
                commitHash = "ghi789" // Simulated commit hash
            };

            UpdateNotificationWindow.ShowWindow(updateInfo);
        }

        [MenuItem("Window/Translations/Test Update Window/Multiple Small Changes")]
        public static void TestMultipleChanges()
        {
            var currentVersion = VersionManager.CurrentVersion;
            var versionParts = currentVersion.Split('.');
            if (versionParts.Length >= 3 && int.TryParse(versionParts[2], out int patch))
            {
                var updateInfo = new VersionInfo
                {
                    version = $"{versionParts[0]}.{versionParts[1]}.{patch + 1}",
                    releaseDate = DateTime.Now.ToString("yyyy-MM-dd"),
                    changes = new string[]
                    {
                        "Added version update notifications",
                        "Fixed progress reporting in scene extraction",
                        "Updated TextMeshPro dependency to 3.0.7",
                        "Fixed memory leak in prefab processing",
                        "Added error handling for Git operations",
                        "Updated documentation with new features",
                        "Added test menu for update notifications",
                        "Improved error messages in extraction process",
                        "Added support for custom extraction paths"
                    },
                    minUnityVersion = "2020.3",
                    downloadUrl = "https://github.com/BrendanRobins97/TranslationDoneRight",
                    commitHash = "jkl012" // Simulated commit hash
                };

                UpdateNotificationWindow.ShowWindow(updateInfo);
            }
        }

        [MenuItem("Window/Translations/Test Update Window/Reset Notification Settings")]
        public static void ResetNotificationSettings()
        {
            VersionManager.ShouldShowUpdateNotification = true;
            EditorPrefs.DeleteKey("TranslationsDoneRight_LastVersionCheck");
            Debug.Log("Update notification settings have been reset. Notifications will be shown again.");
        }
    }
}
#endif 