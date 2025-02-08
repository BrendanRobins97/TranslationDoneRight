using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEngine.UIElements;
using System.Linq;

namespace PSS
{
    public class TranslationsEditorWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private TranslationData translationData;
        private string newLanguageName = "";
        private string searchFilter = "";
        private bool showMissingOnly = false;
        private bool showUnusedOnly = false;
        private Vector2 textScrollPosition;
        private string selectedKey = null;
        private bool autoTranslateEnabled = false;
        private string apiKey = "";
        
        // Extraction settings
        private bool extractFromScenes = true;
        private bool extractFromPrefabs = true;
        private bool extractFromScripts = true;
        private bool extractFromScriptableObjects = true;
        private bool includeInactive = false;
        
        private enum Tab
        {
            Settings,
            TextExtraction,
            AllText,
            Languages
        }
        
        private Tab currentTab = Tab.Languages;

        // Add coverage tracking
        private Dictionary<string, float> languageCoverage = new Dictionary<string, float>();
        private bool needsCoverageUpdate = true;

        private KeyUpdateMode updateMode = KeyUpdateMode.Replace;

        private bool isDirty = false;
        private float saveDelay = 1f;
        private double lastEditTime;

        [MenuItem("Window/Translations")]
        public static void ShowWindow()
        {
            GetWindow<TranslationsEditorWindow>("Translations Manager");
        }

        private void OnEnable()
        {
            translationData = Resources.Load<TranslationData>("TranslationData");
            LoadEditorPrefs();
            needsCoverageUpdate = true;
        }

        private void OnDisable()
        {
            SaveEditorPrefs();
        }

        private void LoadEditorPrefs()
        {
            autoTranslateEnabled = EditorPrefs.GetBool("TranslationManager_AutoTranslate", false);
            apiKey = EditorPrefs.GetString("TranslationManager_APIKey", "");
        }

        private void SaveEditorPrefs()
        {
            EditorPrefs.SetBool("TranslationManager_AutoTranslate", autoTranslateEnabled);
            EditorPrefs.SetString("TranslationManager_APIKey", apiKey);
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

        private void OnGUI()
        {
            EditorGUILayout.Space(10);

            if (translationData == null)
            {
                DrawNoTranslationDataUI();
                return;
            }

            DrawHeaderSection();
            EditorGUILayout.Space(5);

            // Draw tabs with icons using reliable built-in Unity icons
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Toggle(currentTab == Tab.Settings, new GUIContent(" Settings", EditorGUIUtility.IconContent("d_Settings").image), EditorStyles.toolbarButton))
                currentTab = Tab.Settings;
            if (GUILayout.Toggle(currentTab == Tab.TextExtraction, new GUIContent(" Text Extraction", EditorGUIUtility.IconContent("d_Prefab Icon").image), EditorStyles.toolbarButton))
                currentTab = Tab.TextExtraction;
            if (GUILayout.Toggle(currentTab == Tab.AllText, new GUIContent(" All Text", EditorGUIUtility.IconContent("d_TextAsset Icon").image), EditorStyles.toolbarButton))
                currentTab = Tab.AllText;
            if (GUILayout.Toggle(currentTab == Tab.Languages, new GUIContent(" Languages", EditorGUIUtility.IconContent("d_BuildSettings.Standalone").image), EditorStyles.toolbarButton))
                currentTab = Tab.Languages;
            EditorGUILayout.EndHorizontal();

            // Global search bar
            if (currentTab != Tab.Settings)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                searchFilter = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField);
                if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
                    searchFilter = "";
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(5);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            switch (currentTab)
            {
                case Tab.Languages:
                    DrawLanguagesTab();
                    break;
                case Tab.AllText:
                    DrawAllTextTab();
                    break;
                case Tab.TextExtraction:
                    DrawTextExtractionTab();
                    break;
                case Tab.Settings:
                    DrawSettingsTab();
                    break;
            }

            EditorGUILayout.EndScrollView();

            // Status bar
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField($"Total Keys: {translationData.allKeys.Count}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Languages: {translationData.supportedLanguages.Count}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawNoTranslationDataUI()
        {
            EditorGUILayout.HelpBox("No TranslationData asset found in Resources folder.", MessageType.Warning);
            
            if (GUILayout.Button("Create TranslationData Asset"))
            {
                CreateTranslationDataAsset();
            }
        }

        private void DrawHeaderSection()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Translation Manager", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Select TranslationData Asset", GUILayout.Width(180)))
            {
                Selection.activeObject = translationData;
            }
            EditorGUILayout.EndHorizontal();
        }

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

        private void DrawAllTextTab()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Filters
            EditorGUILayout.BeginHorizontal();
            showMissingOnly = EditorGUILayout.ToggleLeft("Show Missing Translations Only", showMissingOnly, GUILayout.Width(200));
            showUnusedOnly = EditorGUILayout.ToggleLeft("Show Unused Keys Only", showUnusedOnly, GUILayout.Width(200));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Split view
            EditorGUILayout.BeginHorizontal();

            // Left side - keys list
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(300));
            DrawKeysList();
            EditorGUILayout.EndVertical();

            // Right side - translations
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (selectedKey != null)
            {
                DrawTranslationsForKey();
            }
            else
            {
                EditorGUILayout.LabelField("Select a key to edit translations", EditorStyles.centeredGreyMiniLabel);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }

        private void DrawKeysList()
        {
            EditorGUILayout.LabelField("Translation Keys", EditorStyles.boldLabel);
            textScrollPosition = EditorGUILayout.BeginScrollView(textScrollPosition);

            var filteredKeys = translationData.allKeys
                .Where(k => string.IsNullOrEmpty(searchFilter) || k.ToLower().Contains(searchFilter.ToLower()));

            foreach (var key in filteredKeys)
            {
                bool isSelected = key == selectedKey;
                bool newSelected = EditorGUILayout.ToggleLeft(key, isSelected);
                if (newSelected != isSelected)
                {
                    selectedKey = newSelected ? key : null;
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawTranslationsForKey()
        {
            if (selectedKey == null) return;

            EditorGUILayout.LabelField($"Translations for: {selectedKey}", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Get the index of the selected key
            int keyIndex = translationData.allKeys.IndexOf(selectedKey);
            if (keyIndex == -1)
            {
                EditorGUILayout.HelpBox("Selected key not found in translation data.", MessageType.Error);
                return;
            }

            // Show English (original) text first
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("English", GUILayout.Width(150));
            GUI.enabled = false;
            EditorGUILayout.TextField(selectedKey);
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            // Show translations for other languages
            for (int i = 0; i < translationData.languageDataDictionary.Length; i++)
            {
                string language = translationData.supportedLanguages[i + 1]; // +1 to skip English
                var assetRef = translationData.languageDataDictionary[i];
                
                string assetPath = AssetDatabase.GUIDToAssetPath(assetRef.AssetGUID);
                LanguageData languageData = AssetDatabase.LoadAssetAtPath<LanguageData>(assetPath);

                if (languageData != null && keyIndex < languageData.allText.Count)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(language, GUILayout.Width(150));

                    string currentTranslation = languageData.allText[keyIndex];
                    EditorGUI.BeginChangeCheck();
                    string newTranslation = EditorGUILayout.TextField(currentTranslation);
                    if (EditorGUI.EndChangeCheck())
                    {
                        languageData.allText[keyIndex] = newTranslation;
                        EditorUtility.SetDirty(languageData);
                        isDirty = true;
                        lastEditTime = EditorApplication.timeSinceStartup;
                    }

                    if (GUILayout.Button("Auto", GUILayout.Width(60)))
                    {
                        // Auto-translate implementation
                    }

                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(language, GUILayout.Width(150));
                    EditorGUILayout.HelpBox($"No translation data available for {language}", MessageType.Warning);
                    EditorGUILayout.EndHorizontal();
                }
            }

            // Handle delayed saving
            if (isDirty && EditorApplication.timeSinceStartup > lastEditTime + saveDelay)
            {
                AssetDatabase.SaveAssets();
                isDirty = false;
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Copy Key to Clipboard"))
            {
                EditorGUIUtility.systemCopyBuffer = selectedKey;
            }
            
            if (GUILayout.Button("Remove Key"))
            {
                if (EditorUtility.DisplayDialog("Remove Key", 
                    "Are you sure you want to remove this key and all its translations?", 
                    "Remove", "Cancel"))
                {
                    RemoveTranslationKey(keyIndex);
                }
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private void RemoveTranslationKey(int keyIndex)
        {
            Undo.RecordObject(translationData, "Remove Translation Key");
            
            // Remove from all language data files
            for (int i = 0; i < translationData.languageDataDictionary.Length; i++)
            {
                var assetRef = translationData.languageDataDictionary[i];
                string assetPath = AssetDatabase.GUIDToAssetPath(assetRef.AssetGUID);
                LanguageData languageData = AssetDatabase.LoadAssetAtPath<LanguageData>(assetPath);
                
                if (languageData != null && keyIndex < languageData.allText.Count)
                {
                    Undo.RecordObject(languageData, "Remove Translation");
                    languageData.allText.RemoveAt(keyIndex);
                    EditorUtility.SetDirty(languageData);
                }
            }

            // Remove from translation data
            translationData.allKeys.RemoveAt(keyIndex);
            EditorUtility.SetDirty(translationData);
            
            // Clear selection
            selectedKey = null;
            
            needsCoverageUpdate = true;
            AssetDatabase.SaveAssets();
        }

        private void DrawTextExtractionTab()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Extraction Sources
            EditorGUILayout.LabelField("Extraction Sources", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            extractFromScenes = EditorGUILayout.ToggleLeft(
                new GUIContent("Extract from Scenes", "Extract text from all scenes in build settings"), 
                extractFromScenes);
            extractFromPrefabs = EditorGUILayout.ToggleLeft(
                new GUIContent("Extract from Prefabs", "Extract text from all prefabs in the project"), 
                extractFromPrefabs);
            extractFromScripts = EditorGUILayout.ToggleLeft(
                new GUIContent("Extract from Scripts", "Find Translate() function calls in scripts"), 
                extractFromScripts);
            extractFromScriptableObjects = EditorGUILayout.ToggleLeft(
                new GUIContent("Extract from ScriptableObjects", "Extract text from all ScriptableObjects"), 
                extractFromScriptableObjects);
            includeInactive = EditorGUILayout.ToggleLeft(
                new GUIContent("Include Inactive GameObjects", "Extract text from disabled objects"), 
                includeInactive);

            EditorGUILayout.Space(10);

            // Extraction Mode
            EditorGUILayout.LabelField("Extraction Mode", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField("Direct Update", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Updates translation keys directly in the TranslationData asset. " +
                "Existing translations will be preserved.", 
                MessageType.Info);
            
            if (GUILayout.Button("Extract and Update Keys"))
            {
                if (EditorUtility.DisplayDialog("Extract Text", 
                    $"This will {(updateMode == KeyUpdateMode.Replace ? "replace" : "merge")} translation keys. Existing translations will be preserved. Continue?", 
                    "Extract", "Cancel"))
                {
                    var extractedText = TextExtractor.ExtractAllText(
                        extractFromScenes,
                        extractFromPrefabs,
                        extractFromScripts,
                        extractFromScriptableObjects,
                        includeInactive
                    );
                    
                    TextExtractor.UpdateTranslationData(translationData, extractedText, updateMode);
                    needsCoverageUpdate = true;
                }
            }

            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("CSV Export", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Exports found text to a CSV file. You can modify the CSV and import it back later.", 
                MessageType.Info);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Extract to New CSV"))
            {
                ExtractToNewCSV();
            }
            if (GUILayout.Button("Update Existing CSV"))
            {
                ExtractToExistingCSV();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            
            // CSV Management
            EditorGUILayout.LabelField("CSV Management", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Import CSV"))
            {
                translationData.ImportCSV();
            }
            if (GUILayout.Button("Export Current Keys to CSV"))
            {
                ExportCurrentKeysToCSV();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            if (GUILayout.Button("Generate Translation Report"))
            {
                GenerateReport();
            }
            
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            
            // Update Mode
            EditorGUILayout.LabelField("Update Mode", EditorStyles.boldLabel);
            updateMode = (KeyUpdateMode)EditorGUILayout.EnumPopup("Key Update Mode", updateMode);
            
            switch (updateMode)
            {
                case KeyUpdateMode.Replace:
                    EditorGUILayout.HelpBox(
                        "Replace mode will clear existing keys and add new ones, preserving translations for any keys that still exist.", 
                        MessageType.Info);
                    break;
                case KeyUpdateMode.Merge:
                    EditorGUILayout.HelpBox(
                        "Merge mode will keep existing keys and add new ones, preserving all existing translations.", 
                        MessageType.Info);
                    break;
            }
        }

        private void ExtractToNewCSV()
        {
            string path = EditorUtility.SaveFilePanel(
                "Save CSV File",
                "",
                "translations.csv",
                "csv");
                
            if (!string.IsNullOrEmpty(path))
            {
                TextExtractor.ExtractToCSV(
                    path,
                    translationData,
                    extractFromScenes,
                    extractFromPrefabs,
                    extractFromScripts,
                    extractFromScriptableObjects,
                    includeInactive
                );
            }
        }

        private void ExtractToExistingCSV()
        {
            string path = EditorUtility.OpenFilePanel("Select Existing CSV", "", "csv");
            if (!string.IsNullOrEmpty(path))
            {
                TextExtractor.UpdateExistingCSV(
                    path,
                    translationData,
                    extractFromScenes,
                    extractFromPrefabs,
                    extractFromScripts,
                    extractFromScriptableObjects,
                    includeInactive
                );
            }
        }

        private void ExportCurrentKeysToCSV()
        {
            string path = EditorUtility.SaveFilePanel(
                "Export Current Keys",
                "",
                "current_translations.csv",
                "csv");
                
            if (!string.IsNullOrEmpty(path))
            {
                TextExtractor.ExportCurrentKeys(path, translationData);
            }
        }

        private void GenerateReport()
        {
            string path = EditorUtility.SaveFilePanel(
                "Save Translation Report",
                "",
                "translation_report.txt",
                "txt");
                
            if (!string.IsNullOrEmpty(path))
            {
                TextExtractor.GenerateReport(
                    path,
                    translationData,
                    extractFromScenes,
                    extractFromPrefabs,
                    extractFromScripts,
                    extractFromScriptableObjects,
                    includeInactive
                );
            }
        }

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

        private void DrawProgressBar(float value)
        {
            Rect rect = GUILayoutUtility.GetRect(50, 18);
            EditorGUI.ProgressBar(rect, value, "");
        }

        private void CreateTranslationDataAsset()
        {
            translationData = ScriptableObject.CreateInstance<TranslationData>();
            
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }
            
            AssetDatabase.CreateAsset(translationData, "Assets/Resources/TranslationData.asset");
            AssetDatabase.SaveAssets();
            Selection.activeObject = translationData;
            
            Debug.Log("Created new TranslationData asset in Resources folder");
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

        private void OnInspectorUpdate()
        {
            // Trigger repaint to check for delayed save
            if (isDirty)
            {
                Repaint();
            }
        }
    }
} 