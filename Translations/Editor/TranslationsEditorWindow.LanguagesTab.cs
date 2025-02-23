using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.AddressableAssets;
using UnityEditorInternal;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

namespace PSS
{
    public partial class TranslationsEditorWindow
    {
        private static readonly string[] DEFAULT_LANGUAGES = new string[]
        {
            "English",
            "French", 
            "Italian", 
            "German", 
            "Danish", 
            "Dutch", 
            "Japanese",
            "Korean", 
            "Portuguese", 
            "Portuguese (Brazil)", 
            "Russian", 
            "Chinese (Simplified)",
            "Spanish", 
            "Swedish", 
            "Ukrainian",
            "Chinese (Traditional)", 
        };

        private ReorderableList languageList;
        private Vector2 languageScrollPosition;

        private void InitializeLanguageList()
        {
            if (languageList != null) return;
            
            languageList = new ReorderableList(
                translationData.supportedLanguages,
                typeof(string),
                true, // draggable
                false, // display header
                false, // display add button
                false  // display remove button
            );

            languageList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                if (index >= translationData.supportedLanguages.Count) return;
                string language = translationData.supportedLanguages[index];
                float coverage = languageCoverage.TryGetValue(language, out float value) ? value : 0f;

                // Star button
                var starContent = new GUIContent(
                    language == translationData.defaultLanguage ? "★" : "○",
                    language == translationData.defaultLanguage ? 
                        "Current Default Language" : 
                        "Click to set as Default Language"
                );
                
                var starStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0),
                    fixedWidth = 15,
                    normal = { textColor = language == translationData.defaultLanguage ? 
                        new Color(1f, 0.8f, 0f, 1f) : // Gold color for current default
                        new Color(0.7f, 0.7f, 0.7f, 1f) // Gray for non-default
                    }
                };

                Rect starRect = new Rect(rect.x, rect.y + 2, 15, rect.height - 4);
                if (GUI.Button(starRect, starContent, starStyle))
                {
                    if (language != translationData.defaultLanguage)
                    {
                        if (EditorUtility.DisplayDialog("Set Default Language",
                            $"Set {language} as the default language?", "Yes", "Cancel"))
                        {
                            Undo.RecordObject(translationData, "Change Default Language");
                            
                            // Store the old default language
                            string oldDefaultLanguage = translationData.defaultLanguage;
                            
                            // Set the new default language
                            translationData.defaultLanguage = language;
                            
                            // Reorder languages to put default first
                            ReorderLanguagesWithDefaultFirst();
                            
                            // Update the dictionary and verify assets
                            UpdateLanguageDataDictionary();
                            VerifyAddressableAssets();
                            
                            EditorUtility.SetDirty(translationData);
                            needsCoverageUpdate = true;
                            
                            // Force a full refresh of the addressable assets
                            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
                            if (settings != null)
                            {
                                settings.SetDirty(AddressableAssetSettings.ModificationEvent.GroupSchemaModified, null, true);
                                EditorUtility.SetDirty(settings);
                                AssetDatabase.SaveAssets();
                            }
                        }
                    }
                }

                // Language name
                Rect labelRect = new Rect(rect.x + 20, rect.y, 135, rect.height);
                EditorGUI.LabelField(labelRect, language);

                // Coverage percentage
                Rect percentRect = new Rect(rect.x + 160, rect.y, 50, rect.height);
                EditorGUI.LabelField(percentRect, $"{coverage:F1}%");

                // Progress bar
                Rect progressRect = new Rect(rect.x + 215, rect.y + 2, rect.width - 290, rect.height - 4);
                EditorGUI.ProgressBar(progressRect, coverage / 100f, "");

                // Remove button (only for non-default languages or if it's the only language)
                if (language != translationData.defaultLanguage || translationData.supportedLanguages.Count == 1)
                {
                    Rect removeRect = new Rect(rect.x + rect.width - 70, rect.y + 3, 65, rect.height - 6);
                    if (GUI.Button(removeRect, "Remove"))
                    {
                        if (EditorUtility.DisplayDialog("Remove Language", 
                            $"Are you sure you want to remove {language}?", "Remove", "Cancel"))
                        {
                            RemoveLanguage(language);
                        }
                    }
                }
            };

            languageList.onReorderCallback = (ReorderableList list) =>
            {
                EditorUtility.SetDirty(translationData);
                UpdateLanguageDataDictionary();
            };

            // Set the height of each element
            languageList.elementHeight = 24;
        }

        private void DrawLanguagesTab()
        {
            EditorGUILayout.Space(10);
            GUILayout.Label("Languages", EditorGUIStyleUtility.HeaderLabel);
            EditorGUILayout.Space(5);
            
            EditorGUILayout.LabelField("Manage your project's supported languages and their translation assets. Each non-default language is automatically set up as an addressable asset for efficient runtime loading. The default language serves as the source text for translations.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(10);

            // Initialize if needed
            if (translationData.supportedLanguages == null)
            {
                translationData.supportedLanguages = new List<string>();
            }

            // Show welcome message only when no languages exist
            if (translationData.supportedLanguages.Count == 0)
            {
                EditorGUILayout.LabelField("Welcome to the Translation Manager! Start by setting up your supported languages.", EditorStyles.wordWrappedLabel);
                EditorGUILayout.Space(10);
            }

            if (needsCoverageUpdate)
            {
                UpdateCoverageData();
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // Language statistics
                if (translationData.supportedLanguages.Count > 0)
                {
                    EditorGUILayout.LabelField("Language Coverage", EditorStyles.boldLabel);
                    EditorGUILayout.Space(5);

                    InitializeLanguageList();
                    languageList.DoLayoutList();
                    EditorGUILayout.Space(5);
                }

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

                // Action buttons row
                var buttonStyle = new GUIStyle(EditorStyles.miniButton)
                {
                    fontSize = 12,
                    fixedHeight = 30,
                    padding = new RectOffset(15, 15, 8, 8),
                    alignment = TextAnchor.MiddleCenter
                };

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                
                if (GUILayout.Button("Add Common Languages", buttonStyle, GUILayout.Width(200), GUILayout.Height(32)))
                {
                    AddDefaultLanguages();
                }
                
                GUILayout.Space(10);
                
                if (GUILayout.Button("Refresh Coverage Data", buttonStyle, GUILayout.Width(200), GUILayout.Height(32)))
                {
                    needsCoverageUpdate = true;
                    Repaint();
                }
                
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
        }

        private void ReorderLanguagesWithDefaultFirst()
        {
            if (translationData.supportedLanguages == null || translationData.supportedLanguages.Count <= 1) return;

            // Remove default language from current position
            if (translationData.supportedLanguages.Contains(translationData.defaultLanguage))
            {
                translationData.supportedLanguages.Remove(translationData.defaultLanguage);
                // Add it back at the beginning
                translationData.supportedLanguages.Insert(0, translationData.defaultLanguage);
                
                // Update the asset references to match the new order
                UpdateLanguageDataDictionary();
                VerifyAddressableAssets();
            }
        }

        private void UpdateCoverageData()
        {
            if (translationData == null || translationData.supportedLanguages == null || translationData.supportedLanguages.Count == 0) 
            {
                needsCoverageUpdate = false;
                return;
            }
            
            languageCoverage.Clear();
            
            // Default language is always first and 100%
            languageCoverage[translationData.defaultLanguage] = 100f;
            
            int totalKeys = translationData.allKeys?.Count ?? 0;
            if (totalKeys == 0) 
            {
                // Set all languages to 100% if there are no keys yet
                foreach (var lang in translationData.supportedLanguages)
                {
                    languageCoverage[lang] = 100f;
                }
                needsCoverageUpdate = false;
                return;
            }

            // Skip if language data dictionary isn't initialized
            if (translationData.languageDataDictionary == null || translationData.languageDataDictionary.Length == 0)
            {
                foreach (var lang in translationData.supportedLanguages.Skip(1))
                {
                    languageCoverage[lang] = 0f;
                }
                needsCoverageUpdate = false;
                return;
            }

            // Calculate coverage for each non-default language
            for (int i = 0; i < translationData.languageDataDictionary.Length; i++)
            {
                if (i + 1 >= translationData.supportedLanguages.Count) continue;
                
                string language = translationData.supportedLanguages[i + 1]; // +1 to skip default language
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

        private void VerifyAddressableAssets()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null)
            {
                Debug.LogError("Addressable Asset Settings not found. Please ensure Addressables is properly set up in your project.");
                return;
            }

            // Get or create the Languages group
            AddressableAssetGroup languageGroup = settings.FindGroup("Languages");
            if (languageGroup == null)
            {
                languageGroup = settings.CreateGroup("Languages", false, false, true, null);
            }

            // Get all files in the Languages folder
            string languagesFolderPath = "Assets/Translations/Languages";
            if (!AssetDatabase.IsValidFolder(languagesFolderPath))
            {
                AssetDatabase.CreateFolder("Assets/Translations", "Languages");
            }
            
            var existingFiles = System.IO.Directory.GetFiles(languagesFolderPath, "*.asset")
                .Select(path => path.Replace("\\", "/"))
                .ToList();

            // Keep track of valid language assets
            var validAssets = new HashSet<string>();

            // Ensure all non-default languages have addressable assets
            foreach (var language in translationData.supportedLanguages.Skip(1)) // Skip default language
            {
                string sanitizedName = SanitizeFileName(language);
                string assetPath = GetLanguageAssetPath(sanitizedName);
                validAssets.Add(assetPath);

                // Create asset if it doesn't exist
                var languageData = AssetDatabase.LoadAssetAtPath<LanguageData>(assetPath);
                if (languageData == null)
                {
                    CreateLanguageAsset(language);
                }
                else
                {
                    // Ensure it's properly set up as addressable
                    string guid = AssetDatabase.AssetPathToGUID(assetPath);
                    var entry = settings.CreateOrMoveEntry(guid, languageGroup);
                    entry.address = $"Language_{sanitizedName}";
                    settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
                }
            }

            // Remove any language assets that aren't in the valid set
            foreach (var file in existingFiles)
            {
                if (!validAssets.Contains(file))
                {
                    // Remove from addressables first
                    string guid = AssetDatabase.AssetPathToGUID(file);
                    if (!string.IsNullOrEmpty(guid))
                    {
                        var entry = settings.FindAssetEntry(guid);
                        if (entry != null)
                        {
                            settings.RemoveAssetEntry(guid);
                        }
                    }
                    
                    // Then delete the file
                    AssetDatabase.DeleteAsset(file);
                }
            }

            // Save all changes
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        private void AddDefaultLanguages()
        {
            Undo.RecordObject(translationData, "Add Default Languages");
            
            // Ensure default language is added first
            if (!translationData.supportedLanguages.Contains(translationData.defaultLanguage))
            {
                translationData.supportedLanguages.Add(translationData.defaultLanguage);
            }
            
            foreach (var language in DEFAULT_LANGUAGES)
            {
                if (language != translationData.defaultLanguage && !translationData.supportedLanguages.Contains(language))
                {
                    translationData.supportedLanguages.Add(language);
                }
            }
            
            EditorUtility.SetDirty(translationData);
            AssetDatabase.SaveAssets();
            needsCoverageUpdate = true;
            UpdateLanguageDataDictionary();
            VerifyAddressableAssets();
        }

        private void AddNewLanguage(string language)
        {
            Undo.RecordObject(translationData, "Add Language");
            
            // If this is the first language, set it as default
            if (translationData.supportedLanguages.Count == 0)
            {
                translationData.defaultLanguage = language;
            }
            
            // Add the new language at the end (default language always stays at the top)
            translationData.supportedLanguages.Add(language);
            
            EditorUtility.SetDirty(translationData);
            AssetDatabase.SaveAssets();
            needsCoverageUpdate = true;
            UpdateLanguageDataDictionary();
            VerifyAddressableAssets();
        }

        private void RemoveLanguage(string language)
        {
            // Don't allow removing default language unless it's the only one
            if (language == translationData.defaultLanguage && translationData.supportedLanguages.Count > 1)
            {
                EditorUtility.DisplayDialog("Cannot Remove Language", 
                    "Cannot remove the default language. Please set a different default language first.", "OK");
                return;
            }

            Undo.RecordObject(translationData, "Remove Language");
            int index = translationData.supportedLanguages.IndexOf(language);
            translationData.supportedLanguages.RemoveAt(index);
            
            EditorUtility.SetDirty(translationData);
            AssetDatabase.SaveAssets();
            needsCoverageUpdate = true;
            UpdateLanguageDataDictionary();
            VerifyAddressableAssets();
        }

        private void CreateLanguageAsset(string language)
        {
            // Ensure the Translations directory exists
            if (!AssetDatabase.IsValidFolder("Assets/Translations"))
            {
                AssetDatabase.CreateFolder("Assets", "Translations");
            }
            if (!AssetDatabase.IsValidFolder("Assets/Translations/Languages"))
            {
                AssetDatabase.CreateFolder("Assets/Translations", "Languages");
            }

            string sanitizedName = SanitizeFileName(language);
            string assetPath = GetLanguageAssetPath(sanitizedName);

            // Check if asset already exists
            var existingAsset = AssetDatabase.LoadAssetAtPath<LanguageData>(assetPath);
            LanguageData languageData;

            if (existingAsset != null)
            {
                languageData = existingAsset;
            }
            else
            {
                // Create new language asset
                languageData = ScriptableObject.CreateInstance<LanguageData>();
                languageData.allText = new List<string>();

                // Copy existing keys if any
                if (translationData.allKeys != null)
                {
                    languageData.allText.AddRange(translationData.allKeys.Select(_ => string.Empty));
                }

                try
                {
                    // Create the asset
                    AssetDatabase.CreateAsset(languageData, assetPath);
                    AssetDatabase.SaveAssets();
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to create language asset at {assetPath}: {e.Message}");
                    EditorUtility.DisplayDialog("Error", 
                        $"Failed to create language asset for {language}. Check the console for details.", "OK");
                    return;
                }
            }

            // Set up Addressables
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null)
            {
                Debug.LogError("Addressable Asset Settings not found. Please ensure Addressables is properly set up in your project.");
                return;
            }

            // Get or create the Languages group
            AddressableAssetGroup languageGroup = settings.FindGroup("Languages");
            if (languageGroup == null)
            {
                languageGroup = settings.CreateGroup("Languages", false, false, true, null);
            }

            // Make the asset addressable
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            var entry = settings.CreateOrMoveEntry(guid, languageGroup);
            entry.address = $"Language_{sanitizedName}";

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        private void DeleteLanguageAsset(string language)
        {
            string sanitizedName = SanitizeFileName(language);
            string assetPath = GetLanguageAssetPath(sanitizedName);
            
            // Remove from Addressables if it exists
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings != null)
            {
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                if (!string.IsNullOrEmpty(guid))
                {
                    settings.RemoveAssetEntry(guid);
                    settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryRemoved, null, true);
                    EditorUtility.SetDirty(settings);
                }
            }

            // Delete the asset if it exists
            if (AssetDatabase.LoadAssetAtPath<LanguageData>(assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }

            // Also check and delete any old assets in Resources folder
            string oldAssetPath = $"Assets/Resources/LanguageData_{sanitizedName}.asset";
            if (AssetDatabase.LoadAssetAtPath<LanguageData>(oldAssetPath) != null)
            {
                AssetDatabase.DeleteAsset(oldAssetPath);
            }

            AssetDatabase.SaveAssets();
        }

        private void UpdateLanguageDataDictionary()
        {
            if (translationData.supportedLanguages == null || translationData.supportedLanguages.Count <= 1) return;

            var newDictionary = new List<AssetReference>();

            // Add entries for all languages except default language
            foreach (var language in translationData.supportedLanguages.Skip(1))
            {
                string sanitizedName = SanitizeFileName(language);
                string assetPath = GetLanguageAssetPath(sanitizedName);
                
                var languageAsset = AssetDatabase.LoadAssetAtPath<LanguageData>(assetPath);
                if (languageAsset != null)
                {
                    string guid = AssetDatabase.AssetPathToGUID(assetPath);
                    newDictionary.Add(new AssetReference(guid));
                }
            }

            translationData.languageDataDictionary = newDictionary.ToArray();
            EditorUtility.SetDirty(translationData);
            AssetDatabase.SaveAssets();
        }

        private string GetLanguageAssetPath(string sanitizedName)
        {
            return $"Assets/Translations/Languages/LanguageData_{sanitizedName}.asset";
        }

        private string SanitizeFileName(string fileName)
        {
            return fileName.Replace(" ", "_").Replace("(", "_").Replace(")", "_");
        }
    }
} 