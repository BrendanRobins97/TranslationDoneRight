using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEngine.UIElements;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
        
        // Remove old extraction settings
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
            Languages,
            DeepL
        }
        
        private Tab currentTab = Tab.Languages;

        private enum TextViewMode
        {
            Detailed,
            Grid
        }
        
        private TextViewMode currentTextViewMode = TextViewMode.Detailed;
        private Vector2 gridScrollPosition;
        private float gridViewScale = 1f;  // Add scale field

        // Add coverage tracking
        private Dictionary<string, float> languageCoverage = new Dictionary<string, float>();
        private bool needsCoverageUpdate = true;

        private KeyUpdateMode updateMode = KeyUpdateMode.Replace;

        private bool isDirty = false;
        private float saveDelay = 1f;
        private double lastEditTime;

        private string deeplApiKey = "";
        private bool useDeepLPro = false;
        private bool includeContextInTranslation = true;
        private bool preserveFormatting = true;
        private bool formalityPreference = true;  // true = formal, false = informal

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
            gridViewScale = EditorPrefs.GetFloat("TranslationManager_GridScale", 1f);
            deeplApiKey = EditorPrefs.GetString("TranslationManager_DeepLApiKey", "");
            useDeepLPro = EditorPrefs.GetBool("TranslationManager_UseDeepLPro", false);
            includeContextInTranslation = EditorPrefs.GetBool("TranslationManager_IncludeContext", true);
            preserveFormatting = EditorPrefs.GetBool("TranslationManager_PreserveFormatting", true);
            formalityPreference = EditorPrefs.GetBool("TranslationManager_Formality", true);
        }

        private void SaveEditorPrefs()
        {
            EditorPrefs.SetBool("TranslationManager_AutoTranslate", autoTranslateEnabled);
            EditorPrefs.SetString("TranslationManager_APIKey", apiKey);
            EditorPrefs.SetFloat("TranslationManager_GridScale", gridViewScale);
            EditorPrefs.SetString("TranslationManager_DeepLApiKey", deeplApiKey);
            EditorPrefs.SetBool("TranslationManager_UseDeepLPro", useDeepLPro);
            EditorPrefs.SetBool("TranslationManager_IncludeContext", includeContextInTranslation);
            EditorPrefs.SetBool("TranslationManager_PreserveFormatting", preserveFormatting);
            EditorPrefs.SetBool("TranslationManager_Formality", formalityPreference);
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
            if (GUILayout.Toggle(currentTab == Tab.DeepL, new GUIContent(" DeepL", EditorGUIUtility.IconContent("d_BuildSettings.Web.Small").image), EditorStyles.toolbarButton))
                currentTab = Tab.DeepL;
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
                case Tab.DeepL:
                    DrawDeepLTab();
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
            
            // View mode and scale controls
            EditorGUILayout.BeginHorizontal();
            
            // View mode toggle
            EditorGUILayout.LabelField("View Mode:", GUILayout.Width(70));
            TextViewMode newMode = (TextViewMode)EditorGUILayout.EnumPopup(currentTextViewMode, GUILayout.Width(100));
            if (newMode != currentTextViewMode)
            {
                currentTextViewMode = newMode;
                selectedKey = null; // Reset selection when switching views
            }

            if (currentTextViewMode == TextViewMode.Grid)
            {
                EditorGUILayout.Space(20);
                EditorGUILayout.LabelField("Scale:", GUILayout.Width(45));
                float newScale = EditorGUILayout.Slider(gridViewScale, 0.5f, 1.5f, GUILayout.Width(150));
                if (newScale != gridViewScale)
                {
                    gridViewScale = newScale;
                    SaveEditorPrefs();
                }
            }
            
            EditorGUILayout.EndHorizontal();
            
            // Filters
            EditorGUILayout.BeginHorizontal();
            showMissingOnly = EditorGUILayout.ToggleLeft("Show Missing Translations Only", showMissingOnly, GUILayout.Width(200));
            showUnusedOnly = EditorGUILayout.ToggleLeft("Show Unused Keys Only", showUnusedOnly, GUILayout.Width(200));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            switch (currentTextViewMode)
            {
                case TextViewMode.Detailed:
                    DrawDetailedView();
                    break;
                case TextViewMode.Grid:
                    DrawGridView();
                    break;
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawDetailedView()
        {
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
        }

        private void DrawGridView()
        {
            if (translationData == null || translationData.allKeys == null) return;

            // Base sizes that will be scaled
            float baseColumnWidth = 150f;
            float baseMinRowHeight = 22f;
            float baseFontSize = 9f;
            
            // Apply scale to measurements
            float columnWidth = Mathf.Round(baseColumnWidth * gridViewScale);
            float minRowHeight = Mathf.Round(baseMinRowHeight * gridViewScale);
            int fontSize = Mathf.RoundToInt(baseFontSize * gridViewScale);
            
            // Begin the scrollable area
            gridScrollPosition = EditorGUILayout.BeginScrollView(gridScrollPosition);

            // Header row
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            var headerStyle = new GUIStyle(EditorStyles.toolbarButton)
            {
                fontSize = fontSize
            };
            foreach (var language in translationData.supportedLanguages)
            {
                EditorGUILayout.LabelField(language, headerStyle, GUILayout.Width(columnWidth));
            }
            EditorGUILayout.EndHorizontal();

            // Filter keys based on search
            var filteredKeys = translationData.allKeys
                .Where(k => string.IsNullOrEmpty(searchFilter) || k.ToLower().Contains(searchFilter.ToLower()))
                .ToList();

            // Create base text style with scaled font
            var baseTextStyle = new GUIStyle(EditorStyles.textArea)
            {
                fontSize = fontSize,
                wordWrap = true,
                padding = new RectOffset(
                    Mathf.RoundToInt(2 * gridViewScale),
                    Mathf.RoundToInt(2 * gridViewScale),
                    Mathf.RoundToInt(1 * gridViewScale),
                    Mathf.RoundToInt(1 * gridViewScale)
                )
            };

            // Content rows
            foreach (var key in filteredKeys)
            {
                // First pass: Calculate the maximum height needed for this row
                float maxHeight = minRowHeight;
                var translations = new List<string> { key }; // Start with English key
                
                // Get all translations for this row
                for (int i = 0; i < translationData.languageDataDictionary.Length; i++)
                {
                    var assetRef = translationData.languageDataDictionary[i];
                    string assetPath = AssetDatabase.GUIDToAssetPath(assetRef.AssetGUID);
                    LanguageData languageData = AssetDatabase.LoadAssetAtPath<LanguageData>(assetPath);

                    int keyIndex = translationData.allKeys.IndexOf(key);
                    string translation = "";
                    
                    if (languageData != null && keyIndex >= 0 && keyIndex < languageData.allText.Count)
                    {
                        translation = languageData.allText[keyIndex];
                    }
                    translations.Add(translation);
                }

                // Calculate height needed for each translation
                foreach (var text in translations)
                {
                    var content = new GUIContent(text);
                    float height = baseTextStyle.CalcHeight(content, columnWidth);
                    maxHeight = Mathf.Max(maxHeight, height);
                }

                // Second pass: Draw the row with consistent height
                EditorGUILayout.BeginHorizontal();
                
                // English column (key) - read-only with word wrap
                var readOnlyStyle = new GUIStyle(baseTextStyle)
                {
                    richText = true
                };
                readOnlyStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f, 1f);
                readOnlyStyle.normal.background = baseTextStyle.normal.background;
                readOnlyStyle.focused.background = baseTextStyle.normal.background;
                readOnlyStyle.hover.background = baseTextStyle.normal.background;
                EditorGUILayout.SelectableLabel(key, readOnlyStyle, GUILayout.Width(columnWidth), GUILayout.Height(maxHeight));

                // Other languages
                for (int i = 0; i < translationData.languageDataDictionary.Length; i++)
                {
                    var assetRef = translationData.languageDataDictionary[i];
                    string assetPath = AssetDatabase.GUIDToAssetPath(assetRef.AssetGUID);
                    LanguageData languageData = AssetDatabase.LoadAssetAtPath<LanguageData>(assetPath);

                    int keyIndex = translationData.allKeys.IndexOf(key);
                    string translation = "";
                    
                    if (languageData != null && keyIndex >= 0 && keyIndex < languageData.allText.Count)
                    {
                        translation = languageData.allText[keyIndex];
                    }

                    EditorGUILayout.BeginVertical(GUILayout.Width(columnWidth));
                    
                    EditorGUI.BeginChangeCheck();
                    string newTranslation = EditorGUILayout.TextArea(
                        translation,
                        baseTextStyle,
                        GUILayout.Width(columnWidth),
                        GUILayout.Height(maxHeight)  // Remove the height reduction since we no longer need space for the button
                    );
                    
                    if (EditorGUI.EndChangeCheck() && languageData != null)
                    {
                        Undo.RecordObject(languageData, "Update Translation");
                        languageData.allText[keyIndex] = newTranslation;
                        EditorUtility.SetDirty(languageData);
                        isDirty = true;
                        lastEditTime = EditorApplication.timeSinceStartup;
                    }

                    EditorGUILayout.EndVertical();
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
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

            // Parameters section
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Parameters", EditorStyles.boldLabel);
            
            var parameters = translationData.GetKeyParameters(selectedKey);
            EditorGUI.BeginChangeCheck();
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Show current parameters
            for (int i = 0; i < parameters.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                parameters[i] = EditorGUILayout.TextField(parameters[i]);
                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    parameters.RemoveAt(i);
                    i--;
                }
                EditorGUILayout.EndHorizontal();
            }

            // Add new parameter button
            if (GUILayout.Button("Add Parameter"))
            {
                parameters.Add($"param{parameters.Count}");
            }

            EditorGUILayout.EndVertical();

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(translationData, "Update Translation Parameters");
                translationData.keyParameters[selectedKey] = parameters;
                EditorUtility.SetDirty(translationData);
            }

            // Metadata section
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Source Information", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var sources = translationData.Metadata.GetSources(selectedKey);
            if (sources.Count > 0)
            {
                foreach (var source in sources)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    
                    // Source type with icon
                    EditorGUILayout.BeginHorizontal();
                    string iconName = source.sourceType switch
                    {
                        TextSourceType.Scene => "SceneAsset Icon",
                        TextSourceType.Prefab => "Prefab Icon",
                        TextSourceType.Script => "cs Script Icon",
                        TextSourceType.ScriptableObject => "ScriptableObject Icon",
                        _ => "TextAsset Icon"
                    };
                    GUILayout.Label(EditorGUIUtility.IconContent(iconName), GUILayout.Width(20));
                    EditorGUILayout.LabelField($"Type: {source.sourceType}", EditorStyles.boldLabel);
                    EditorGUILayout.EndHorizontal();

                    // Source path (clickable if asset exists)
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Source:", GUILayout.Width(60));
                    if (GUILayout.Button(source.sourcePath, EditorStyles.linkLabel))
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(source.sourcePath);
                        if (asset != null)
                        {
                            Selection.activeObject = asset;
                            EditorGUIUtility.PingObject(asset);
                        }
                    }
                    EditorGUILayout.EndHorizontal();

                    // Object path if it exists
                    if (!string.IsNullOrEmpty(source.objectPath))
                    {
                        EditorGUILayout.LabelField("Object Path:", source.objectPath);
                    }

                    // Component and field info
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Component:", source.componentName, GUILayout.Width(200));
                    EditorGUILayout.LabelField("Field:", source.fieldName);
                    EditorGUILayout.EndHorizontal();

                    // Inactive state if relevant
                    if (source.wasInactive)
                    {
                        EditorGUILayout.LabelField("State: Inactive", EditorGUIStyleUtility.WarningLabelStyle);
                    }

                    // Enhanced context fields
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Context Information", EditorStyles.boldLabel);

                    EditorGUI.BeginChangeCheck();
                    
                    // Replace enum popup with our new dropdown
                    var categoryDropdown = new TextCategoryDropdown(translationData.Metadata, source);
                    categoryDropdown.Draw();

                    // // Category-specific fields
                    // switch (source.textCategory)
                    // {
                    //     case "TextCategory.Dialog":
                    //         source.speakerInfo = EditorGUILayout.TextField("Speaker Info:", source.speakerInfo);
                    //         break;
                    //     case TextCategory.Tutorial:
                    //         source.mechanicContext = EditorGUILayout.TextField("Game Mechanic:", source.mechanicContext);
                    //         source.targetAudience = EditorGUILayout.TextField("Target Audience:", source.targetAudience);
                    //         break;
                    //     case TextCategory.Item:
                    //         source.mechanicContext = EditorGUILayout.TextField("Game Mechanic:", source.mechanicContext);
                    //         source.culturalNotes = EditorGUILayout.TextField("Cultural/Lore Notes:", source.culturalNotes);
                    //         break;
                    //     case TextCategory.UI:
                    //         source.visualContext = EditorGUILayout.TextField("Visual Context:", source.visualContext);
                    //         break;
                    // }

                    // Common fields for all categories
                    source.locationContext = EditorGUILayout.TextField("Location Context:", source.locationContext);
                    source.targetAudience = EditorGUILayout.TextField("Target Audience:", source.targetAudience);

                    // Main context field
                    EditorGUILayout.LabelField("Additional Context:");
                    source.manualContext = EditorGUILayout.TextArea(source.manualContext, GUILayout.Height(60));

                    if (EditorGUI.EndChangeCheck())
                    {
                        EditorUtility.SetDirty(translationData);
                    }

                    // Show auto-generated context
                    if (!string.IsNullOrEmpty(source.Context))
                    {
                        EditorGUILayout.Space(5);
                        EditorGUILayout.LabelField("Auto-Generated Context:", EditorStyles.boldLabel);
                        EditorGUI.BeginDisabledGroup(true);
                        EditorGUILayout.TextArea(source.Context, GUILayout.Height(40));
                        EditorGUI.EndDisabledGroup();
                    }

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(5);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No source information available for this key.", MessageType.Info);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Add context field
            EditorGUILayout.LabelField("Context", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            string currentContext = translationData.GetKeyContext(selectedKey);
            
            string newContext = EditorGUIStyleUtility.DrawExpandingTextArea(
                currentContext, 
                EditorGUIUtility.currentViewWidth
            );

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(translationData, "Update Translation Context");
                translationData.SetKeyContext(selectedKey, newContext);
                EditorUtility.SetDirty(translationData);
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Translations", EditorStyles.boldLabel);

            // Parameter usage hint
            if (parameters.Count > 0)
            {
                EditorGUILayout.HelpBox($"Available parameters: {string.Join(", ", parameters.Select(p => $"{{{p}}}"))}", MessageType.Info);
            }

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

                    EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                    
                    var translationEntry = languageData.allText[keyIndex];
                    EditorGUI.BeginChangeCheck();

                    string newTranslation = EditorGUIStyleUtility.DrawExpandingTextArea(
                        translationEntry,
                        EditorGUIUtility.currentViewWidth
                    );
                    
                    // Check parameters inline
                    var usedParameters = ExtractParametersFromText(translationEntry);
                    var missingParameters = parameters.Except(usedParameters).ToList();
                    var extraParameters = usedParameters.Except(parameters).ToList();

                    if (missingParameters.Any() || extraParameters.Any())
                    {
                        if (missingParameters.Any())
                        {
                            EditorGUILayout.LabelField(
                                $"Missing: {string.Join(", ", missingParameters)}", 
                                EditorGUIStyleUtility.WarningLabelStyle
                            );
                        }
                        if (extraParameters.Any())
                        {
                            EditorGUILayout.LabelField(
                                $"Extra: {string.Join(", ", extraParameters)}", 
                                EditorGUIStyleUtility.WarningLabelStyle
                            );
                        }
                    }

                    EditorGUILayout.EndVertical();

                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(languageData, "Update Translation");
                        languageData.allText[keyIndex] = newTranslation;
                        EditorUtility.SetDirty(languageData);
                    }

                    if (GUILayout.Button("Auto", GUILayout.Width(60)))
                    {
                        if (!string.IsNullOrEmpty(deeplApiKey))
                        {
                            TranslateSingleField(selectedKey, language, keyIndex, languageData);
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("DeepL Translation", 
                                "Please configure your DeepL API key in the DeepL tab first.", "OK");
                        }
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

            EditorGUILayout.Space(10);

            // Add translate all button
            if (!string.IsNullOrEmpty(deeplApiKey))
            {
                if (GUILayout.Button("Translate All Languages"))
                {
                    _ = TranslateAllLanguagesForKey(selectedKey);
                }
            }
        }

        private List<string> ExtractParametersFromText(string text)
        {
            var parameters = new List<string>();
            
            // Return empty list if text is null or empty
            if (string.IsNullOrEmpty(text))
            {
                return parameters;
            }
            
            var regex = new Regex(@"\{([^}]+)\}");
            var matches = regex.Matches(text);
            
            foreach (Match match in matches)
            {
                parameters.Add(match.Groups[1].Value);
            }
            
            return parameters;
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

            var extractors = TextExtractor.GetExtractors();
            foreach (var extractor in extractors)
            {
                var extractorType = extractor.GetType();
                bool isEnabled = TextExtractor.IsExtractorEnabled(extractorType);
                
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                // Header with priority
                EditorGUILayout.BeginHorizontal();
                var newEnabled = EditorGUILayout.ToggleLeft(
                    new GUIContent(
                        $"{extractor.SourceType} Extractor (Priority: {extractor.Priority})", 
                        extractor.Description
                    ),
                    isEnabled
                );
                
                if (newEnabled != isEnabled)
                {
                    TextExtractor.SetExtractorEnabled(extractorType, newEnabled);
                }
                EditorGUILayout.EndHorizontal();

                // Show description in a help box if enabled
                if (isEnabled)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.HelpBox(extractor.Description, MessageType.Info);
                    EditorGUI.indentLevel--;
                }
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            // Include inactive toggle
            EditorGUILayout.Space(10);
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
                    var extractedText = TextExtractor.ExtractAllText();
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
                    translationData
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
                    translationData
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
                    translationData
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

        private void DrawDeepLTab()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField("DeepL Translation Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            // API Settings
            EditorGUILayout.LabelField("API Configuration", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            deeplApiKey = EditorGUILayout.PasswordField("DeepL API Key:", deeplApiKey);
            useDeepLPro = EditorGUILayout.Toggle("Use DeepL Pro", useDeepLPro);
            
            if (string.IsNullOrEmpty(deeplApiKey))
            {
                EditorGUILayout.HelpBox("Please enter your DeepL API key to enable automatic translation.", MessageType.Warning);
            }
            
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Translation Settings
            EditorGUILayout.LabelField("Translation Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            includeContextInTranslation = EditorGUILayout.Toggle("Include Context", includeContextInTranslation);
            if (includeContextInTranslation)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("Context will be sent as additional information to DeepL to improve translation quality.", MessageType.Info);
                EditorGUI.indentLevel--;
            }

            preserveFormatting = EditorGUILayout.Toggle("Preserve Formatting", preserveFormatting);
            formalityPreference = EditorGUILayout.Toggle("Formal Language", formalityPreference);
            
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Test Connection
            if (GUILayout.Button("Test DeepL Connection"))
            {
                TestDeepLConnection();
            }

            EditorGUILayout.EndVertical();
        }

        private async void TestDeepLConnection()
        {
            if (string.IsNullOrEmpty(deeplApiKey))
            {
                EditorUtility.DisplayDialog("DeepL Test", "Please enter an API key first.", "OK");
                return;
            }

            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"DeepL-Auth-Key {deeplApiKey}");
                    var baseUrl = useDeepLPro ? "https://api.deepl.com/v2" : "https://api-free.deepl.com/v2";
                    var response = await client.GetAsync($"{baseUrl}/usage");
                    
                    if (response.IsSuccessStatusCode)
                    {
                        EditorUtility.DisplayDialog("DeepL Test", "Connection successful! API key is valid.", "OK");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("DeepL Test", "Connection failed. Please check your API key and settings.", "OK");
                    }
                }
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("DeepL Test", $"Error testing connection: {e.Message}", "OK");
            }
        }

        private async Task<string> TranslateViaDeepL(string sourceText, string targetLanguage, string context = null)
        {
            if (string.IsNullOrEmpty(deeplApiKey)) return null;

            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"DeepL-Auth-Key {deeplApiKey}");
                    var baseUrl = useDeepLPro ? "https://api.deepl.com/v2" : "https://api-free.deepl.com/v2";

                    // Get enhanced context if available
                    string enhancedContext = null;
                    if (includeContextInTranslation)
                    {
                        if (translationData?.Metadata != null)
                        {
                            enhancedContext = translationData.Metadata.GetTranslationContext(sourceText);
                        }
                        if (string.IsNullOrEmpty(enhancedContext))
                        {
                            enhancedContext = context;
                        }
                    }

                    var content = new System.Net.Http.FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        { "text", sourceText },
                        { "target_lang", GetDeepLLanguageCode(targetLanguage) },
                        { "preserve_formatting", preserveFormatting ? "1" : "0" },
                        { "formality", formalityPreference ? "more" : "less" },
                        { "context", enhancedContext ?? "" }
                    });

                    var response = await client.PostAsync($"{baseUrl}/translate", content);
                    if (response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadAsStringAsync();
                        var jsonResponse = JsonUtility.FromJson<DeepLResponse>(result);
                        return jsonResponse?.translations?[0]?.text;
                    }
                    else
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        Debug.LogError($"DeepL translation failed: {error}");
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Translation error: {e.Message}");
            }
            return null;
        }

        [System.Serializable]
        private class DeepLResponse
        {
            public DeepLTranslation[] translations;
        }

        [System.Serializable]
        private class DeepLTranslation
        {
            public string text;
            public string detected_source_language;
        }

        private async Task TranslateAllLanguagesForKey(string key)
        {
            if (string.IsNullOrEmpty(deeplApiKey) || translationData == null) return;

            var context = translationData.GetKeyContext(key);
            int keyIndex = translationData.allKeys.IndexOf(key);
            
            if (keyIndex == -1) return;

            EditorUtility.DisplayProgressBar("Translating", "Initializing translations...", 0f);
            int currentLanguage = 0;
            int totalLanguages = translationData.supportedLanguages.Count - 1; // Minus English

            try
            {
                foreach (var language in translationData.supportedLanguages.Skip(1)) // Skip English
                {
                    EditorUtility.DisplayProgressBar("Translating", $"Translating to {language}...", (float)currentLanguage / totalLanguages);
                    currentLanguage++;

                    int langIndex = translationData.supportedLanguages.IndexOf(language) - 1;
                    if (langIndex >= 0 && langIndex < translationData.languageDataDictionary.Length)
                    {
                        var assetRef = translationData.languageDataDictionary[langIndex];
                        string assetPath = AssetDatabase.GUIDToAssetPath(assetRef.AssetGUID);
                        LanguageData languageData = AssetDatabase.LoadAssetAtPath<LanguageData>(assetPath);

                        if (languageData != null)
                        {
                            string translation = await TranslateViaDeepL(key, language, context);
                            if (!string.IsNullOrEmpty(translation))
                            {
                                Undo.RecordObject(languageData, "Auto Translate");
                                languageData.allText[keyIndex] = translation;
                                EditorUtility.SetDirty(languageData);
                            }
                        }
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
            }
        }

        private string GetDeepLLanguageCode(string language)
        {
            // Map your language names to DeepL codes
            Dictionary<string, string> languageCodes = new Dictionary<string, string>
            {
                { "English", "EN" },
                { "German", "DE" },
                { "French", "FR" },
                { "Spanish", "ES" },
                { "Italian", "IT" },
                { "Dutch", "NL" },
                { "Polish", "PL" },
                { "Russian", "RU" },
                { "Portuguese", "PT" },
                { "Chinese", "ZH" },
                { "Japanese", "JA" },
                // Add more mappings as needed
            };

            if (languageCodes.TryGetValue(language, out string code))
                return code;
            
            return language.ToUpper(); // Fallback to uppercase language name
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

        private async void TranslateSingleField(string key, string targetLanguage, int keyIndex, LanguageData languageData)
        {
            if (languageData == null) return;

            EditorUtility.DisplayProgressBar("Translating", $"Translating to {targetLanguage}...", 0.5f);
            try
            {
                string translation = await TranslateViaDeepL(
                    key, 
                    targetLanguage, 
                    translationData?.Metadata?.GetTranslationContext(key)
                );

                if (!string.IsNullOrEmpty(translation))
                {
                    Undo.RecordObject(languageData, "Auto Translate");
                    languageData.allText[keyIndex] = translation;
                    EditorUtility.SetDirty(languageData);
                    AssetDatabase.SaveAssets();
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
} 