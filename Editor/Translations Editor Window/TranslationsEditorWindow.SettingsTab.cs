using UnityEngine;
using UnityEditor;

namespace Translations
{
    public partial class TranslationsEditorWindow
    {
        private void DrawSettingsTab()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField("Translation Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Auto-translation settings
            EditorGUILayout.LabelField("Auto-Translation", EditorStyles.boldLabel);
            autoTranslateEnabled = EditorGUILayout.ToggleLeft("Enable Auto-Translation", autoTranslateEnabled);
            
            if (autoTranslateEnabled)
            {
                EditorGUI.indentLevel++;
                apiKey = EditorGUILayout.TextField("API Key:", apiKey);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // Font settings
            EditorGUILayout.LabelField("Font Management", EditorStyles.boldLabel);
            if (GUILayout.Button("Manage Fonts"))
            {
                // Open font management window
            }

            EditorGUILayout.Space(10);

            // Backup settings
            EditorGUILayout.LabelField("Backup Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create Backup"))
            {
                // Backup implementation
            }
            if (GUILayout.Button("Restore from Backup"))
            {
                // Restore implementation
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Development tools
            EditorGUILayout.LabelField("Development Tools", EditorStyles.boldLabel);
            if (GUILayout.Button("Validate All Translations"))
            {
                // Validation implementation
            }
            if (GUILayout.Button("Clean Unused Keys"))
            {
                // Cleanup implementation
            }
            
            EditorGUILayout.EndVertical();
        }
    }
} 