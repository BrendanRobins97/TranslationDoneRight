#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace Translations
{
    public class UpdateNotificationWindow : EditorWindow
    {
        private static UpdateNotificationWindow _window;
        private VersionInfo _updateInfo;
        private Vector2 _changelogScrollPosition;
        private GUIStyle _headerStyle;
        private GUIStyle _versionStyle;
        private GUIStyle _dateStyle;
        private GUIStyle _changelogStyle;
        private GUIStyle _changeItemStyle;
        private bool _stylesInitialized;
        private bool _isUpdating;
        private List<ChangelogEntry> _allVersions;
        private int _selectedVersionIndex = 0;
        private string[] _versionOptions;

        public static void ShowWindow(VersionInfo updateInfo)
        {
            if (_window != null)
            {
                _window.Close();
            }

            _window = GetWindow<UpdateNotificationWindow>(true, updateInfo.HasUpdate ? "Update Available" : "Latest Changes");
            _window._updateInfo = updateInfo;
            _window.minSize = new Vector2(600, 500);
            _window.maxSize = new Vector2(800, 700);
            _window.position = new Rect(
                (Screen.currentResolution.width - _window.minSize.x) / 2,
                (Screen.currentResolution.height - _window.minSize.y) / 2,
                _window.minSize.x,
                _window.minSize.y
            );
            _window.LoadAllVersions();
            _window.Show();
        }

        private void LoadAllVersions()
        {
            _allVersions = new List<ChangelogEntry>();
            string changelogPath = Path.Combine(Application.dataPath, "Translations Done Right/CHANGELOG.md");
            
            if (File.Exists(changelogPath))
            {
                string content = File.ReadAllText(changelogPath);
                var versionMatches = Regex.Matches(content, @"## \[([^\]]+)\] - (\d{4}-\d{2}-\d{2})(.*?)(?=## \[|$)", 
                    RegexOptions.Singleline);
                
                foreach (Match match in versionMatches)
                {
                    var entry = new ChangelogEntry
                    {
                        Version = match.Groups[1].Value,
                        Date = match.Groups[2].Value,
                        Content = match.Groups[3].Value.Trim()
                    };
                    _allVersions.Add(entry);
                }

                // Create version options array for the dropdown
                _versionOptions = new string[_allVersions.Count];
                for (int i = 0; i < _allVersions.Count; i++)
                {
                    _versionOptions[i] = $"Version {_allVersions[i].Version} ({_allVersions[i].Date})";
                }
            }
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                margin = new RectOffset(0, 0, 10, 10)
            };

            _versionStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                margin = new RectOffset(0, 0, 5, 5)
            };

            _dateStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                margin = new RectOffset(0, 0, 5, 5)
            };

            _changelogStyle = new GUIStyle(EditorStyles.helpBox)
            {
                margin = new RectOffset(5, 5, 5, 5),
                padding = new RectOffset(10, 10, 10, 10)
            };

            _changeItemStyle = new GUIStyle(EditorStyles.label)
            {
                richText = true,
                padding = new RectOffset(15, 0, 2, 2)
            };

            _stylesInitialized = true;
        }

        private void OnGUI()
        {
            if (_updateInfo == null || _allVersions == null || _allVersions.Count == 0) return;

            InitializeStyles();

            EditorGUILayout.Space(10);

            // Header
            EditorGUILayout.LabelField(_updateInfo.HasUpdate ? "New Version Available!" : "Latest Changes", _headerStyle);

            EditorGUILayout.Space(5);

            // Version Dropdown
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Select Version:", GUILayout.Width(100));
                int newSelectedIndex = EditorGUILayout.Popup(_selectedVersionIndex, _versionOptions);
                if (newSelectedIndex != _selectedVersionIndex)
                {
                    _selectedVersionIndex = newSelectedIndex;
                    _changelogScrollPosition = Vector2.zero; // Reset scroll position when changing versions
                }
            }

            EditorGUILayout.Space(10);

            // Unity version requirement
            EditorGUILayout.HelpBox($"Minimum Unity Version: {_updateInfo.minUnityVersion}", MessageType.Info);

            EditorGUILayout.Space(10);

            // Version Changes
            var selectedVersion = _allVersions[_selectedVersionIndex];
            bool isLatestVersion = _selectedVersionIndex == 0;

            using (new EditorGUILayout.VerticalScope(_changelogStyle))
            {
                _changelogScrollPosition = EditorGUILayout.BeginScrollView(_changelogScrollPosition, GUILayout.Height(250));
                
                // Parse and display the content with proper formatting
                var sections = ParseChangelogContent(selectedVersion.Content);
                foreach (var section in sections)
                {
                    if (!string.IsNullOrEmpty(section.Key))
                    {
                        EditorGUILayout.LabelField(section.Key, EditorStyles.boldLabel);
                        EditorGUILayout.Space(2);
                    }

                    foreach (var change in section.Value)
                    {
                        EditorGUILayout.LabelField($"• {change}", _changeItemStyle);
                    }

                    EditorGUILayout.Space(5);
                }
                
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.Space(15);

            // Update button
            using (new EditorGUILayout.HorizontalScope())
            {
                // Only enable the update button if viewing the latest version and it's newer than current
                GUI.enabled = !_isUpdating && isLatestVersion && _updateInfo.HasUpdate;
                
                if (GUILayout.Button(_isUpdating ? "Updating..." : "Update Now", GUILayout.Height(30)))
                {
                    UpdatePackage();
                }

                GUI.enabled = true;

                if (GUILayout.Button("Close", GUILayout.Height(30)))
                {
                    Close();
                }
            }

            EditorGUILayout.Space(5);

            // Don't show again checkbox
            using (new EditorGUILayout.HorizontalScope())
            {
                bool showNotifications = VersionManager.ShouldShowUpdateNotification;
                bool newValue = EditorGUILayout.ToggleLeft("Show update notifications", showNotifications);
                if (newValue != showNotifications)
                {
                    VersionManager.ShouldShowUpdateNotification = newValue;
                }
            }

            // Footer
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Current Version: {VersionManager.CurrentVersion}", EditorStyles.miniLabel);
                
                if (GUILayout.Button("View All Releases", EditorStyles.linkLabel))
                {
                    Application.OpenURL(_updateInfo.downloadUrl);
                }
            }
        }

        private Dictionary<string, List<string>> ParseChangelogContent(string content)
        {
            var sections = new Dictionary<string, List<string>>();
            var currentSection = "";
            var currentList = new List<string>();

            foreach (var line in content.Split('\n'))
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine)) continue;

                if (trimmedLine.StartsWith("###"))
                {
                    if (currentList.Count > 0)
                    {
                        sections[currentSection] = new List<string>(currentList);
                        currentList.Clear();
                    }
                    currentSection = trimmedLine.Replace("###", "").Trim();
                }
                else if (trimmedLine.StartsWith("-") || trimmedLine.StartsWith("*"))
                {
                    currentList.Add(trimmedLine.Substring(1).Trim());
                }
            }

            if (currentList.Count > 0)
            {
                sections[currentSection] = new List<string>(currentList);
            }

            return sections;
        }

        private async void UpdatePackage()
        {
            if (_isUpdating) return;
            
            _isUpdating = true;
            bool success = await VersionManager.UpdatePackage(_updateInfo.commitHash);
            _isUpdating = false;

            if (success)
            {
                Close();
                EditorUtility.DisplayDialog("Update Complete", 
                    $"Successfully updated to version {_updateInfo.version}.\n\nPlease restart Unity to ensure all changes take effect.", 
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Update Failed", 
                    "Failed to update the package. Please check the console for error details.", 
                    "OK");
            }
        }

        private class ChangelogEntry
        {
            public string Version;
            public string Date;
            public string Content;
        }
    }
}
#endif 