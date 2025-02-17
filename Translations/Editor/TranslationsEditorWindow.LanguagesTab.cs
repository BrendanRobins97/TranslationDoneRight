using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace PSS
{
    public partial class TranslationsEditorWindow
    {
        private void DrawLanguagesTab()
        {
            if (needsCoverageUpdate)
            {
                UpdateCoverageData();
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Language statistics
            EditorGUILayout.LabelField("Language Coverage", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            foreach (var language in translationData.supportedLanguages)
            {
                float coverage = languageCoverage.TryGetValue(language, out float value) ? value : 0f;
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(language, GUILayout.Width(150));
                EditorGUILayout.LabelField($"{coverage:F1}%", GUILayout.Width(50));
                EditorGUILayout.Space(5);
                DrawProgressBar(coverage / 100f);
                
                if (language != "English")
                {
                    if (GUILayout.Button("Export", GUILayout.Width(60)))
                    {
                        // Export language data
                    }
                    if (GUILayout.Button("Import", GUILayout.Width(60)))
                    {
                        // Import language data
                    }
                    if (GUILayout.Button("Remove", GUILayout.Width(60)))
                    {
                        if (EditorUtility.DisplayDialog("Remove Language", 
                            $"Are you sure you want to remove {language}?", "Remove", "Cancel"))
                        {
                            RemoveLanguage(language);
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(10);

            // Add new language section
            EditorGUILayout.BeginHorizontal();
            newLanguageName = EditorGUILayout.TextField("New Language:", newLanguageName);
            GUI.enabled = !string.IsNullOrWhiteSpace(newLanguageName) && 
                         !translationData.supportedLanguages.Contains(newLanguageName);
            
            if (GUILayout.Button("Add", GUILayout.Width(60)))
            {
                AddNewLanguage(newLanguageName);
                newLanguageName = "";
                GUI.FocusControl(null);
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            
            if (GUILayout.Button("Setup Language Data Files"))
            {
                translationData.SetupLanguageDataAssets();
            }

            EditorGUILayout.Space(5);
            
            if (GUILayout.Button("Batch Auto-Translate Missing Texts"))
            {
                // Auto-translate implementation
            }

            if (GUILayout.Button("Refresh Coverage Data"))
            {
                needsCoverageUpdate = true;
                Repaint();
            }
            
            EditorGUILayout.EndVertical();
        }

        private void UpdateCoverageData()
        {
            if (translationData == null) return;
            
            languageCoverage.Clear();
            
            // Skip English as it's always 100%
            languageCoverage["English"] = 100f;
            
            int totalKeys = translationData.allKeys.Count;
            if (totalKeys == 0) return;

            // Calculate coverage for each non-English language
            for (int i = 0; i < translationData.languageDataDictionary.Length; i++)
            {
                string language = translationData.supportedLanguages[i + 1]; // +1 to skip English
                var assetRef = translationData.languageDataDictionary[i];
                string assetPath = AssetDatabase.GUIDToAssetPath(assetRef.AssetGUID);
                LanguageData languageData = AssetDatabase.LoadAssetAtPath<LanguageData>(assetPath);

                if (languageData != null)
                {
                    int nonEmptyTranslations = languageData.allText.Count(t => !string.IsNullOrWhiteSpace(t));
                    float coverage = totalKeys > 0 ? (nonEmptyTranslations * 100f) / totalKeys : 100f;
                    languageCoverage[language] = coverage;
                }
                else
                {
                    languageCoverage[language] = 0f;
                }
            }
            
            needsCoverageUpdate = false;
        }

        private void AddNewLanguage(string language)
        {
            Undo.RecordObject(translationData, "Add Language");
            translationData.supportedLanguages.Add(language);
            EditorUtility.SetDirty(translationData);
            AssetDatabase.SaveAssets();
        }

        private void RemoveLanguage(string language)
        {
            Undo.RecordObject(translationData, "Remove Language");
            int index = translationData.supportedLanguages.IndexOf(language);
            translationData.supportedLanguages.RemoveAt(index);
            
            // Remove the language data file if it exists
            string sanitizedName = language.Replace(" ", "_").Replace("(", "_").Replace(")", "_");
            string assetPath = $"Assets/Resources/LanguageData_{sanitizedName}.asset";
            if (AssetDatabase.LoadAssetAtPath<LanguageData>(assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
            
            EditorUtility.SetDirty(translationData);
            AssetDatabase.SaveAssets();
            
            needsCoverageUpdate = true;
        }
    }
} 