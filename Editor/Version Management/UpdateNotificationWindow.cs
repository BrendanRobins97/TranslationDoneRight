#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;

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
            _window.Show();
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
            if (_updateInfo == null) return;

            InitializeStyles();

            EditorGUILayout.Space(10);

            // Header
            EditorGUILayout.LabelField(_updateInfo.HasUpdate ? "New Version Available!" : "Latest Changes", _headerStyle);

            // Version info
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Version {_updateInfo.version}", _versionStyle);
                EditorGUILayout.LabelField(_updateInfo.releaseDate, _dateStyle);
            }

            EditorGUILayout.Space(5);

            // Unity version requirement
            EditorGUILayout.HelpBox($"Minimum Unity Version: {_updateInfo.minUnityVersion}", MessageType.Info);

            EditorGUILayout.Space(10);

            // Changelog
            EditorGUILayout.LabelField("What's New", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(_changelogStyle))
            {
                _changelogScrollPosition = EditorGUILayout.BeginScrollView(_changelogScrollPosition, GUILayout.Height(120));
                foreach (var change in _updateInfo.changes)
                {
                    EditorGUILayout.LabelField($"• {change}", _changeItemStyle);
                }
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.Space(15);

            // Update button
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = !_isUpdating && _updateInfo.HasUpdate;
                
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
    }
}
#endif 