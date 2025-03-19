using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Translations
{
    public partial class TranslationsEditorWindow
    {
        private void DrawSettingsTab()
        {
            EditorGUILayout.BeginVertical();
            // Translation Settings
            EditorGUILayout.Space(20);
            EditorGUILayout.LabelField("Translation Settings", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // DeepL Settings
                EditorGUILayout.LabelField("DeepL Translation Settings", EditorStyles.boldLabel);
                deeplApiKey = EditorGUILayout.TextField("DeepL API Key", deeplApiKey);
                useDeepLPro = EditorGUILayout.Toggle("Use DeepL Pro", useDeepLPro);
                includeContextInTranslation = EditorGUILayout.Toggle(
                    new GUIContent(
                        "Include Context",
                        "Include context information when translating text"
                    ),
                    includeContextInTranslation
                );
                preserveFormatting = EditorGUILayout.Toggle(
                    new GUIContent(
                        "Preserve Formatting",
                        "Preserve text formatting during translation"
                    ),
                    preserveFormatting
                );
                formalityPreference = EditorGUILayout.Toggle(
                    new GUIContent(
                        "Prefer Formal",
                        "Use formal language in translations when possible"
                    ),
                    formalityPreference
                );
            }

            // Development tools
            EditorGUILayout.Space(20);
            EditorGUILayout.LabelField("Development Tools", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (GUILayout.Button("Validate All Translations"))
                {
                    // Validation implementation
                }
                if (GUILayout.Button("Clean Unused Keys"))
                {
                    // Cleanup implementation
                }
            }
            
            EditorGUILayout.EndVertical();
        }
    }
} 