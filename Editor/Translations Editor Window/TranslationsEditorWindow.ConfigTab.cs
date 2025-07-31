using UnityEngine;
using UnityEditor;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;

namespace Translations
{
    public partial class TranslationsEditorWindow
    {
        // AI Translation System selection
        public enum AITranslationSystem
        {
            DeepL,
            OpenAI
        }
        
        // AI System settings
        private AITranslationSystem selectedAISystem = AITranslationSystem.DeepL;
        private string openAIApiKey = "";
        private string openAIModel = "gpt-4o";
        private string openAICustomPrompt = "Translate the following text from English to {targetLanguage}, preserving all formatting, placeholders, and special syntax. This is for a video game, so use appropriate gaming terminology:\n\n\"{text}\"";
        
        // DeepL specific settings
        private string formality = null; // Can be "more", "less", or null (default)
        
        // Config settings
        private string translationDataPath = "";
        private bool showPathChangeWarning = false;
        
        // Log settings
        private const int MAX_LOG_ENTRIES = 1000; // Maximum number of log entries to keep
        
        private List<(string message, LogType type, System.DateTime timestamp)> configLogs = new List<(string, LogType, System.DateTime)>();
        private Vector2 configLogScrollPosition;
        private bool showConfigLogs = true;
        private GUIStyle logStyle;
        private GUIStyle errorLogStyle;
        private GUIStyle warningLogStyle;
        private bool logStylesInitialized;
        private bool shouldScrollToBottom;

        // DeepL specific constants
        private const int MAX_BATCH_SIZE = 50; // DeepL's maximum batch size
        private const int MAX_RETRIES = 3;  // Maximum number of retry attempts
        private const int INITIAL_RETRY_DELAY_MS = 1000; // Start with 1 second delay

        // Map of language-specific placeholder transformations
        private Dictionary<string, Func<string, string>> placeholderTransformStrategies = new Dictionary<string, Func<string, string>>();

        // Cache of placeholder templates by language 
        private Dictionary<string, Dictionary<string, string>> placeholderTemplatesByLanguage = new Dictionary<string, Dictionary<string, string>>();

        private void InitializeLogStyles()
        {
            if (logStylesInitialized) return;
            
            logStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                wordWrap = true,
                richText = true
            };

            errorLogStyle = new GUIStyle(logStyle);
            errorLogStyle.normal.textColor = new Color(0.9f, 0.3f, 0.3f);

            warningLogStyle = new GUIStyle(logStyle);
            warningLogStyle.normal.textColor = new Color(0.9f, 0.8f, 0.3f);

            logStylesInitialized = true;
        }

        private void AddConfigLog(string message, LogType type = LogType.Log)
        {
            if (UnityThread.isMainThread)
            {
                configLogs.Add((message, type, System.DateTime.Now));
                if (configLogs.Count > MAX_LOG_ENTRIES)
                {
                    configLogs.RemoveAt(0);
                }
                shouldScrollToBottom = true;
                Repaint();
            }
            else
            {
                EditorApplication.delayCall += () =>
                {
                    configLogs.Add((message, type, System.DateTime.Now));
                    if (configLogs.Count > MAX_LOG_ENTRIES)
                    {
                        configLogs.RemoveAt(0);
                    }
                    shouldScrollToBottom = true;
                    Repaint();
                };
            }
        }

        private void DrawConfigTab()
        {
            InitializeLogStyles();
            
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // General Settings Section
            DrawGeneralConfigSection();
            
            EditorGUILayout.Space(15);

            // DeepL Translation Section
            DrawDeepLConfigSection();

            EditorGUILayout.Space(15);

            // Log Display Area
            EditorGUILayout.LabelField("Configuration Logs", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);
            DrawLogSection();
        }

        #region General Configuration

        private void DrawGeneralConfigSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Section Header
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
            headerStyle.fontSize += 1;
            EditorGUILayout.LabelField("General Settings", headerStyle);
            EditorGUILayout.Space(8);
            
            // Get current path from TranslationDataProvider if not set
            if (string.IsNullOrEmpty(translationDataPath))
            {
                translationDataPath = TranslationDataProvider.BaseFolder;
            }
            
            // Translation Data Path
            EditorGUILayout.LabelField("Translation Base Location", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Current Path:", GUILayout.Width(100));
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField(translationDataPath);
            EditorGUI.EndDisabledGroup();
            
            if (GUILayout.Button("Browse...", GUILayout.Width(80)))
            {
                string newPath = EditorUtility.OpenFolderPanel("Select Translation Base Folder", translationDataPath, "");
                if (!string.IsNullOrEmpty(newPath))
                {
                    // Convert to a relative path if it's inside the Assets folder
                    if (newPath.StartsWith(Application.dataPath))
                    {
                        newPath = "Assets" + newPath.Substring(Application.dataPath.Length);
                    }
                    else
                    {
                        // Show error if the path is outside of Assets folder
                        EditorUtility.DisplayDialog("Invalid Path", 
                            "The selected folder must be inside the Assets folder.", "OK");
                        return;
                    }
                    
                    // If the path has changed, check if we need to move files
                    if (newPath != translationDataPath)
                    {
                        showPathChangeWarning = true;
                        translationDataPath = newPath;
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            
            // Warning about moving files
            if (showPathChangeWarning)
            {
                EditorGUILayout.Space(5);
                GUIStyle warningStyle = new GUIStyle(EditorStyles.wordWrappedLabel);
                warningStyle.normal.textColor = new Color(0.9f, 0.6f, 0.1f);
                EditorGUILayout.LabelField("Changing the base path requires moving your translation files. " +
                    "Apply the changes to move files to the new location?", warningStyle);
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Apply Changes", GUILayout.Height(24)))
                {
                    if (UpdateTranslationDataPath(translationDataPath))
                    {
                        showPathChangeWarning = false;
                        EditorPrefs.SetString("TranslationManager_BasePath", translationDataPath);
                        // Ensure the path is also updated in the TranslationDataProvider
                        TranslationDataProvider.SetDataFolder(translationDataPath);
                        AddConfigLog($"Translation base path updated to: {translationDataPath}");
                    }
                    else
                    {
                        // Reset the path if update failed
                        translationDataPath = EditorPrefs.GetString("TranslationManager_BasePath", TranslationDataProvider.DefaultBaseFolder);
                    }
                }
                
                if (GUILayout.Button("Cancel", GUILayout.Height(24)))
                {
                    showPathChangeWarning = false;
                    translationDataPath = EditorPrefs.GetString("TranslationManager_BasePath", TranslationDataProvider.DefaultBaseFolder);
                }
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(
                "This is the base location where all translation files will be stored.\n\n" +
                "Folder structure:\n" +
                "• " + Path.Combine(translationDataPath, "Data") + " - TranslationData and metadata\n" +
                "• " + Path.Combine(translationDataPath, "Languages") + " - Language assets\n\n" +
                "Default location: " + TranslationDataProvider.DefaultBaseFolder, 
                MessageType.Info);
            
            EditorGUILayout.Space(15);
            
            // Missing Text Behavior Setting
            EditorGUILayout.LabelField("Missing Text Behavior", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);
            
            var newMissingTextBehavior = (MissingTextBehavior)EditorGUILayout.EnumPopup(
                new GUIContent("When text is missing:", "What to return when a translation is not found or is blank"),
                translationData.missingTextBehavior
            );
            
            if (newMissingTextBehavior != translationData.missingTextBehavior)
            {
                Undo.RecordObject(translationData, "Change Missing Text Behavior");
                translationData.missingTextBehavior = newMissingTextBehavior;
                EditorUtility.SetDirty(translationData);
                AddConfigLog($"Missing text behavior changed to: {newMissingTextBehavior}");
            }
            
            // Help text for the missing text behavior setting
            string helpText = translationData.missingTextBehavior switch
            {
                MissingTextBehavior.ReturnNativeLanguage => "Returns the original text in the native language",
                MissingTextBehavior.ReturnBlank => "Returns an empty string",
                MissingTextBehavior.ReturnMissingMessage => "Returns 'TRANSLATION MISSING'",
                _ => ""
            };
            
            if (!string.IsNullOrEmpty(helpText))
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.HelpBox(helpText, MessageType.Info);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private bool UpdateTranslationDataPath(string newPath)
        {
            try
            {
                // Check if the source exists
                string currentAssetPath = AssetDatabase.GetAssetPath(translationData);
                if (string.IsNullOrEmpty(currentAssetPath))
                {
                    // Current asset not found, potentially creating a new one
                    AddConfigLog("No existing TranslationData asset found. A new one will be created at the specified location.", LogType.Warning);
                    
                    // Update the provider path and let it create a new asset
                    TranslationDataProvider.SetDataFolder(newPath);
                    
                    // Force reload the data
                    translationData = TranslationDataProvider.Data;
                    
                    return true;
                }
                
                // Define old and new paths for data and languages
                string oldDataFolder = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(currentAssetPath)), "Data");
                string oldLanguagesFolder = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(currentAssetPath)), "Languages");
                
                string newDataFolder = Path.Combine(newPath, "Data");
                string newLanguagesFolder = Path.Combine(newPath, "Languages");
                
                // Ensure the target directories exist
                EnsureDirectoryExists(newDataFolder);
                EnsureDirectoryExists(newLanguagesFolder);
                
                // Target path for the TranslationData asset
                string targetAssetPath = Path.Combine(newDataFolder, "TranslationData.asset");
                
                // If the target path is the same as the current path, nothing to do for the main asset
                if (currentAssetPath == targetAssetPath)
                {
                    AddConfigLog("The TranslationData asset is already at the specified location.");
                }
                else
                {
                    // Check if the target already exists (rare case)
                    if (AssetDatabase.LoadAssetAtPath<TranslationData>(targetAssetPath) != null)
                    {
                        bool overwrite = EditorUtility.DisplayDialog("File Already Exists", 
                            "The target location already contains a TranslationData asset. Do you want to overwrite it?", 
                            "Overwrite", "Cancel");
                        
                        if (!overwrite)
                        {
                            return false;
                        }
                        
                        // Delete the existing asset
                        AssetDatabase.DeleteAsset(targetAssetPath);
                    }
                    
                    // Move the TranslationData asset to the new location
                    string error = AssetDatabase.MoveAsset(currentAssetPath, targetAssetPath);
                    if (!string.IsNullOrEmpty(error))
                    {
                        EditorUtility.DisplayDialog("Error Moving Asset", 
                            $"Failed to move TranslationData asset: {error}", "OK");
                        AddConfigLog($"Failed to move TranslationData asset: {error}", LogType.Error);
                        return false;
                    }
                    
                    AddConfigLog($"Moved TranslationData to: {targetAssetPath}");
                }
                
                // Now move any language assets if they exist
                if (AssetDatabase.IsValidFolder(oldLanguagesFolder))
                {
                    // Get all assets in the languages folder
                    string[] languageAssets = AssetDatabase.FindAssets("t:LanguageData", new[] { oldLanguagesFolder });
                    
                    if (languageAssets.Length > 0)
                    {
                        AddConfigLog($"Moving {languageAssets.Length} language assets to the new location...");
                        
                        foreach (string assetGuid in languageAssets)
                        {
                            string assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                            string fileName = Path.GetFileName(assetPath);
                            string newAssetPath = Path.Combine(newLanguagesFolder, fileName);
                            
                            // Check if target already exists
                            if (AssetDatabase.LoadAssetAtPath<LanguageData>(newAssetPath) != null)
                            {
                                // If asset already exists at destination, delete it
                                AssetDatabase.DeleteAsset(newAssetPath);
                            }
                            
                            // Move the asset
                            string moveError = AssetDatabase.MoveAsset(assetPath, newAssetPath);
                            if (!string.IsNullOrEmpty(moveError))
                            {
                                AddConfigLog($"Warning: Failed to move language asset {fileName}: {moveError}", LogType.Warning);
                            }
                            else
                            {
                                // Re-add to addressables with the correct key (language code)
                                string languageCode = Path.GetFileNameWithoutExtension(fileName);
                                TranslationDataProvider.AddLanguageDataToAddressables(languageCode, newAssetPath);
                                AddConfigLog($"Moved language asset: {fileName}");
                            }
                        }
                    }
                }
                
                // Update the path in the TranslationDataProvider
                TranslationDataProvider.SetDataFolder(newPath);
                
                // Refresh to make sure we have the updated assets
                AssetDatabase.Refresh();
                
                // Load the moved asset
                translationData = AssetDatabase.LoadAssetAtPath<TranslationData>(targetAssetPath);
                
                if (translationData == null)
                {
                    AddConfigLog("Failed to load TranslationData after moving it.", LogType.Error);
                    return false;
                }
                
                AddConfigLog($"Successfully updated translation base path to: {newPath}");
                return true;
            }
            catch (System.Exception ex)
            {
                AddConfigLog($"Error updating translation base path: {ex.Message}", LogType.Error);
                EditorUtility.DisplayDialog("Error", 
                    $"Failed to update translation base path: {ex.Message}", "OK");
                return false;
            }
        }
        
        private void EnsureDirectoryExists(string directory)
        {
            if (string.IsNullOrEmpty(directory) || directory == "Assets")
                return;
                
            if (!AssetDatabase.IsValidFolder(directory))
            {
                string parentFolder = Path.GetDirectoryName(directory);
                string folderName = Path.GetFileName(directory);
                
                // Make sure the parent directory exists
                EnsureDirectoryExists(parentFolder);
                
                // Create the directory
                AssetDatabase.CreateFolder(parentFolder, folderName);
            }
        }

        #endregion

        #region DeepL Configuration

        private void DrawDeepLConfigSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Section Header
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
            headerStyle.fontSize += 1;
            EditorGUILayout.LabelField("AI Translation Settings", headerStyle);
            EditorGUILayout.Space(8);
            
            // AI System Selection
            EditorGUILayout.LabelField("Translation Service", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);
            
            AITranslationSystem newAISystem = (AITranslationSystem)EditorGUILayout.EnumPopup("AI System:", selectedAISystem);
            if (newAISystem != selectedAISystem)
            {
                selectedAISystem = newAISystem;
                SaveEditorPrefs(); // Save immediately when AI system changes
            }
            EditorGUILayout.Space(8);
            
            // API Settings based on selected AI system
            EditorGUILayout.LabelField("API Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);
            
            if (selectedAISystem == AITranslationSystem.DeepL)
            {
                // DeepL specific settings
            deeplApiKey = EditorGUILayout.PasswordField("DeepL API Key:", deeplApiKey);
            useDeepLPro = EditorGUILayout.Toggle("Use DeepL Pro", useDeepLPro);
            
            if (string.IsNullOrEmpty(deeplApiKey))
            {
                GUIStyle warningStyle = new GUIStyle(EditorStyles.wordWrappedLabel);
                warningStyle.normal.textColor = new Color(0.9f, 0.6f, 0.1f);
                EditorGUILayout.LabelField("Please enter your DeepL API key to enable automatic translation.", 
                    warningStyle);
            }
            }
            else if (selectedAISystem == AITranslationSystem.OpenAI)
            {
                // OpenAI specific settings
                string newOpenAIApiKey = EditorGUILayout.PasswordField("OpenAI API Key:", openAIApiKey);
                if (newOpenAIApiKey != openAIApiKey)
                {
                    openAIApiKey = newOpenAIApiKey;
                    SaveEditorPrefs(); // Save immediately when API key changes
                }
                
                string newOpenAIModel = EditorGUILayout.TextField("Model:", openAIModel);
                if (newOpenAIModel != openAIModel)
                {
                    openAIModel = newOpenAIModel;
                    SaveEditorPrefs(); // Save immediately when model changes
                }
                
                EditorGUILayout.Space(8);
                
                // Custom Prompt Section
                EditorGUILayout.LabelField("Custom Prompt Template", EditorStyles.boldLabel);
                EditorGUILayout.Space(3);
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Use {targetLanguage} for target language and {text} for the text to translate", EditorStyles.miniLabel);
                if (GUILayout.Button("Reset to Default", GUILayout.Width(120)))
                {
                    openAICustomPrompt = "Translate the following text from English to {targetLanguage}, preserving all formatting, placeholders, and special syntax. This is for a video game, so use appropriate gaming terminology:\n\n\"{text}\"";
                    SaveEditorPrefs();
                    GUI.FocusControl(null); // Clear focus to update the text area immediately
                }
                EditorGUILayout.EndHorizontal();
                
                string newOpenAICustomPrompt = EditorGUILayout.TextArea(openAICustomPrompt, GUILayout.Height(80));
                if (newOpenAICustomPrompt != openAICustomPrompt)
                {
                    openAICustomPrompt = newOpenAICustomPrompt;
                    SaveEditorPrefs(); // Save immediately when prompt changes
                }
                
                if (string.IsNullOrEmpty(openAIApiKey))
                {
                    GUIStyle warningStyle = new GUIStyle(EditorStyles.wordWrappedLabel);
                    warningStyle.normal.textColor = new Color(0.9f, 0.6f, 0.1f);
                    EditorGUILayout.LabelField("Please enter your OpenAI API key to enable automatic translation.", 
                        warningStyle);
                }
                
                EditorGUILayout.HelpBox("OpenAI models like GPT-4o can provide more natural translations but may be slower and more expensive than DeepL.", MessageType.Info);
            }
            
            EditorGUILayout.Space(12);
            
            // Formality settings (DeepL specific)
            if (selectedAISystem == AITranslationSystem.DeepL)
            {
                EditorGUILayout.LabelField("Translation Settings", EditorStyles.boldLabel);
                EditorGUILayout.Space(3);

                // Formality setting (DeepL only)
                string[] formalityOptions = new string[] { "Default", "More Formal", "More Informal" };
                int currentFormality = 0;
                
                if (formality == "more")
                    currentFormality = 1;
                else if (formality == "less")
                    currentFormality = 2;
                
                int newFormality = EditorGUILayout.Popup("Formality:", currentFormality, formalityOptions);
                
                if (newFormality != currentFormality)
                {
                    if (newFormality == 0)
                        formality = null;
                    else if (newFormality == 1)
                        formality = "more";
                    else
                        formality = "less";
                }
                
                EditorGUILayout.HelpBox(
                    "Formality setting affects languages that have formal/informal distinctions. " +
                    "Not all languages support formality settings.",
                    MessageType.Info);
                    
                EditorGUILayout.Space(12);
            }

            // Test Connection
            if (GUILayout.Button("Test Connection", GUILayout.Height(24)))
            {
                if (selectedAISystem == AITranslationSystem.DeepL)
            {
                TestDeepLConnection();
                }
                else if (selectedAISystem == AITranslationSystem.OpenAI)
                {
                    TestOpenAIConnection();
                }
            }

            EditorGUILayout.Space(3);
            EditorGUILayout.EndVertical();
        }

        private async void TestDeepLConnection()
        {
            if (string.IsNullOrEmpty(deeplApiKey))
            {
                AddConfigLog("Please enter an API key first.", LogType.Warning);
                EditorUtility.DisplayDialog("DeepL Test", "Please enter an API key first.", "OK");
                return;
            }

            try
            {
                AddConfigLog("Testing DeepL API connection...");
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"DeepL-Auth-Key {deeplApiKey}");
                    var baseUrl = useDeepLPro ? "https://api.deepl.com/v2" : "https://api-free.deepl.com/v2";
                    var response = await client.GetAsync($"{baseUrl}/usage");
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadAsStringAsync();
                        AddConfigLog("Connection successful! API key is valid.");
                        AddConfigLog($"Usage info: {result}");
                        EditorUtility.DisplayDialog("DeepL Test", "Connection successful! API key is valid.", "OK");
                    }
                    else
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        AddConfigLog($"Connection failed: {error}", LogType.Error);
                        EditorUtility.DisplayDialog("DeepL Test", "Connection failed. Please check your API key and settings.", "OK");
                    }
                }
            }
            catch (System.Exception e)
            {
                AddConfigLog($"Error testing connection: {e.Message}", LogType.Error);
                EditorUtility.DisplayDialog("DeepL Test", $"Error testing connection: {e.Message}", "OK");
            }
        }

        private async void TestOpenAIConnection()
        {
            if (string.IsNullOrEmpty(openAIApiKey))
            {
                AddConfigLog("Please enter an OpenAI API key first.", LogType.Warning);
                EditorUtility.DisplayDialog("OpenAI Test", "Please enter an API key first.", "OK");
                return;
            }

            try
            {
                AddConfigLog("Testing OpenAI API connection...");
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {openAIApiKey}");
                    
                    // Simple models list request to check if API key is valid
                    var response = await client.GetAsync("https://api.openai.com/v1/models");
                    
                    if (response.IsSuccessStatusCode)
                    {
                        AddConfigLog("Connection successful! API key is valid.");
                        EditorUtility.DisplayDialog("OpenAI Test", "Connection successful! API key is valid.", "OK");
                    }
                    else
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        AddConfigLog($"Connection failed: {error}", LogType.Error);
                        EditorUtility.DisplayDialog("OpenAI Test", "Connection failed. Please check your API key and settings.", "OK");
                    }
                }
            }
            catch (System.Exception e)
            {
                AddConfigLog($"Error testing connection: {e.Message}", LogType.Error);
                EditorUtility.DisplayDialog("OpenAI Test", $"Error testing connection: {e.Message}", "OK");
            }
        }

        private bool IsFormalitySupported(string languageCode)
        {
            // List of languages that support formality according to DeepL API documentation
            HashSet<string> formalitySupported = new HashSet<string>
            {
                "DE", // German
                "FR", // French
                "IT", // Italian
                "ES", // Spanish
                "NL", // Dutch
                "PL", // Polish
                "PT", // Portuguese
                "PT-BR", // Portuguese (Brazil)
                "RU", // Russian
                "JA", // Japanese
            };

            return formalitySupported.Contains(languageCode);
        }

        private string GetDeepLLanguageCode(string language)
        {
            // First check custom mappings
            if (TranslationMetaDataProvider.Metadata.CustomLanguageMappings != null &&
                TranslationMetaDataProvider.Metadata.CustomLanguageMappings.TryGetValue(language, out string customCode))
            {
                return customCode;
            }

            // Default mappings as fallback
            Dictionary<string, string> defaultLanguageCodes = new Dictionary<string, string>
            {
                { "Bulgarian", "BG" },
                { "Czech", "CS" },
                { "Danish", "DA" },
                { "German", "DE" },
                { "Greek", "EL" },
                { "English", "EN" },
                { "Spanish", "ES" },
                { "Estonian", "ET" },
                { "Finnish", "FI" },
                { "French", "FR" },
                { "Hungarian", "HU" },
                { "Indonesian", "ID" },
                { "Italian", "IT" },
                { "Japanese", "JA" },
                { "Korean", "KO" },
                { "Lithuanian", "LT" },
                { "Latvian", "LV" },
                { "Norwegian", "NB" },
                { "Dutch", "NL" },
                { "Polish", "PL" },
                { "Portuguese", "PT" },
                { "Portuguese (Brazil)", "PT-BR" },
                { "Romanian", "RO" },
                { "Russian", "RU" },
                { "Slovak", "SK" },
                { "Slovenian", "SL" },
                { "Swedish", "SV" },
                { "Turkish", "TR" },
                { "Ukrainian", "UK" },
                { "Chinese", "ZH" },
                { "Chinese (Simplified)", "ZH" },
                { "Chinese (Traditional)", "ZH" }
            };

            // Try default mappings
            if (defaultLanguageCodes.TryGetValue(language, out string code))
                return code;

            // Try to parse regional variants
            if (language.Contains("("))
            {
                string baseLang = language.Split('(')[0].Trim();
                if (defaultLanguageCodes.TryGetValue(baseLang, out string baseCode))
                    return baseCode;
            }

            AddConfigLog($"Language '{language}' not found in DeepL mappings. Please set up a custom mapping in the Configuration tab.", LogType.Error);
            return null; // Return null instead of a fallback to prevent API errors
        }

        private class PlaceholderMapping
        {
            public string OriginalText;
            public Dictionary<string, string> Tokens; // token -> original placeholder
            public string TokenizedText;
            public Dictionary<string, List<string>> InnerTextsToTranslate; // token -> list of inner texts that need translation
            public Dictionary<string, List<string>> TranslatedInnerTexts; // token -> list of translated inner texts
        }

        private PlaceholderMapping PreprocessTextForTranslation(string text)
        {
            AddConfigLog($"Preprocessing text: \"{text}\"", LogType.Log);
            
            var result = new PlaceholderMapping
            {
                OriginalText = text,
                Tokens = new Dictionary<string, string>(),
                InnerTextsToTranslate = new Dictionary<string, List<string>>(),
                TranslatedInnerTexts = new Dictionary<string, List<string>>()
            };

            // This regex will match placeholders of the format {name}, {name:type}, and more complex ones
            var placeholderRegex = new System.Text.RegularExpressions.Regex(@"{[^{}]*(?:{[^{}]*})*[^{}]*}");
            var matches = placeholderRegex.Matches(text);
            
            AddConfigLog($"Found {matches.Count} placeholder(s)", LogType.Log);
            
            string processedText = text;
            int counter = 0;

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var placeholder = match.Value;
                var token = $"{{PLACEHOLDER_{counter}}}";
                
                AddConfigLog($"  Placeholder {counter}: \"{placeholder}\" -> \"{token}\"", LogType.Log);
                
                result.Tokens[token] = placeholder;
                
                // Check if this is a plural placeholder with inner text to translate
                if (placeholder.Contains(":plural:"))
                {
                    var innerTexts = ExtractInnerTextsFromPlaceholder(placeholder);
                    if (innerTexts.Count > 0)
                    {
                        AddConfigLog($"  Inner texts to translate for {token}: [{string.Join(", ", innerTexts.Select(t => "\"" + t + "\""))}]", LogType.Log);
                        result.InnerTextsToTranslate[token] = innerTexts;
                    }
                    else
                    {
                        AddConfigLog($"  No inner texts found to translate in {token}", LogType.Warning);
                    }
                }
                
                processedText = processedText.Replace(placeholder, token);
                counter++;
            }

            result.TokenizedText = processedText;
            AddConfigLog($"Tokenized text: \"{processedText}\"", LogType.Log);
            return result;
        }

        private List<string> ExtractInnerTextsFromPlaceholder(string placeholder)
        {
            var result = new List<string>();
            
            AddConfigLog($"Extracting inner texts from: \"{placeholder}\"", LogType.Log);
            
            // For plural placeholders like {count:plural:1 {item}|{} {items}}
            // Extract the inner texts that need translation: "item" and "items"
            
            try
            {
                // Find the part after ":plural:"
                int pluralIndex = placeholder.IndexOf(":plural:");
                if (pluralIndex < 0) 
                {
                    AddConfigLog($"  No plural marker found in placeholder", LogType.Warning);
                    return result;
                }
                
                string pluralPart = placeholder.Substring(pluralIndex + 8); // Skip ":plural:"
                AddConfigLog($"  Plural part: \"{pluralPart}\"", LogType.Log);
                
                // Extract texts inside inner braces using regex
                var innerTextRegex = new System.Text.RegularExpressions.Regex(@"{([^{}]*)}");
                var matches = innerTextRegex.Matches(pluralPart);
                
                AddConfigLog($"  Found {matches.Count} inner text(s)", LogType.Log);
                
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    if (match.Groups.Count > 1 && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
                    {
                        string innerText = match.Groups[1].Value.Trim();
                        result.Add(innerText);
                        AddConfigLog($"  Extracted inner text: \"{innerText}\"", LogType.Log);
                    }
                }
            }
            catch (System.Exception ex)
            {
                AddConfigLog($"Error extracting inner texts from placeholder: {ex.Message}", LogType.Error);
                Debug.LogError($"Error extracting inner texts from placeholder: {ex.Message}");
            }
            
            return result;
        }

        private async Task<PlaceholderMapping> TranslateInnerTexts(PlaceholderMapping mapping, string targetLanguage, string context)
        {
            // Collect all inner texts that need translation
            var allInnerTexts = new List<string>();
            var innerTextTokens = new Dictionary<string, List<int>>();
            
            AddConfigLog($"Translating inner texts for {mapping.InnerTextsToTranslate.Count} placeholder(s)", LogType.Log);
            
            foreach (var kvp in mapping.InnerTextsToTranslate)
            {
                string token = kvp.Key;
                innerTextTokens[token] = new List<int>();
                
                foreach (string innerText in kvp.Value)
                {
                    innerTextTokens[token].Add(allInnerTexts.Count);
                    allInnerTexts.Add(innerText);
                    AddConfigLog($"  Queueing inner text from {token}: \"{innerText}\"", LogType.Log);
                }
            }
            
            if (allInnerTexts.Count == 0)
            {
                AddConfigLog("No inner texts to translate", LogType.Warning);
                return mapping;
            }
            
            AddConfigLog($"Sending {allInnerTexts.Count} inner text(s) to DeepL: [{string.Join(", ", allInnerTexts.Select(t => "\"" + t + "\""))}]", LogType.Log);
                
            // Translate all inner texts in one batch
            var translatedTexts = await TranslateBatchRaw(allInnerTexts, targetLanguage, new List<string> { context });
            
            if (translatedTexts != null)
            {
                AddConfigLog($"Received {translatedTexts.Count} translated inner text(s): [{string.Join(", ", translatedTexts.Select(t => "\"" + t + "\""))}]", LogType.Log);
                
                // Map translated texts back to their tokens
                foreach (var kvp in innerTextTokens)
                {
                    string token = kvp.Key;
                    List<int> indices = kvp.Value;
                    
                    mapping.TranslatedInnerTexts[token] = new List<string>();
                    
                    foreach (int index in indices)
                    {
                        if (index < translatedTexts.Count)
                        {
                            mapping.TranslatedInnerTexts[token].Add(translatedTexts[index]);
                            AddConfigLog($"  Mapped translated inner text for {token}: \"{translatedTexts[index]}\"", LogType.Log);
                        }
                    }
                }
            }
            else
            {
                AddConfigLog("Failed to translate inner texts", LogType.Error);
            }
            
            return mapping;
        }

        private string ReconstructPlaceholderWithTranslatedInnerTexts(string placeholder, List<string> translatedInnerTexts)
        {
            AddConfigLog($"Reconstructing placeholder: \"{placeholder}\" with {translatedInnerTexts?.Count ?? 0} translated inner text(s)", LogType.Log);
            
            if (translatedInnerTexts == null || translatedInnerTexts.Count == 0)
            {
                AddConfigLog("  No translated inner texts available, returning original placeholder", LogType.Warning);
                return placeholder;
            }
                
            try
            {
                // For plural placeholders like {count:plural:1 {item}|{} {items}}
                // Replace "item" and "items" with their translations
                
                int pluralIndex = placeholder.IndexOf(":plural:");
                if (pluralIndex < 0)
                {
                    AddConfigLog("  No plural marker found, returning original placeholder", LogType.Warning);
                    return placeholder;
                }
                
                string prefix = placeholder.Substring(0, pluralIndex + 8); // Include ":plural:"
                string pluralPart = placeholder.Substring(pluralIndex + 8); // Skip ":plural:"
                
                AddConfigLog($"  Prefix: \"{prefix}\", Plural part: \"{pluralPart}\"", LogType.Log);
                
                // Replace inner texts with their translations
                var innerTextRegex = new System.Text.RegularExpressions.Regex(@"{([^{}]*)}");
                string result = pluralPart;
                int translatedIndex = 0;
                
                result = innerTextRegex.Replace(pluralPart, match => {
                    if (match.Groups.Count > 1 && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
                    {
                        if (translatedIndex < translatedInnerTexts.Count)
                        {
                            string originalText = match.Groups[1].Value;
                            string translatedText = translatedInnerTexts[translatedIndex++];
                            AddConfigLog($"  Replacing inner text: \"{originalText}\" -> \"{translatedText}\"", LogType.Log);
                            return "{" + translatedText + "}";
                        }
                    }
                    return match.Value;
                });
                
                string reconstructed = prefix + result;
                AddConfigLog($"  Reconstructed placeholder: \"{reconstructed}\"", LogType.Log);
                return reconstructed;
            }
            catch (System.Exception ex)
            {
                AddConfigLog($"Error reconstructing placeholder: {ex.Message}", LogType.Error);
                Debug.LogError($"Error reconstructing placeholder: {ex.Message}");
                return placeholder;
            }
        }

        private string RestoreTextPlaceholders(string translatedText, PlaceholderMapping mapping)
        {
            AddConfigLog($"Restoring placeholders in: \"{translatedText}\"", LogType.Log);
            
            string result = translatedText;
            foreach (var kvp in mapping.Tokens)
            {
                string token = kvp.Key;
                string placeholder = kvp.Value;
                
                // If this placeholder has translated inner texts, reconstruct it
                if (mapping.TranslatedInnerTexts.ContainsKey(token) && 
                    mapping.TranslatedInnerTexts[token].Count > 0)
                {
                    string originalPlaceholder = placeholder;
                    placeholder = ReconstructPlaceholderWithTranslatedInnerTexts(
                        placeholder, 
                        mapping.TranslatedInnerTexts[token]
                    );
                    AddConfigLog($"  Reconstructed {token}: \"{originalPlaceholder}\" -> \"{placeholder}\"", LogType.Log);
                }
                
                result = result.Replace(token, placeholder);
                AddConfigLog($"  Replaced {token} with placeholder", LogType.Log);
            }
            
            AddConfigLog($"Final text with placeholders restored: \"{result}\"", LogType.Log);
            return result;
        }

        // Original TranslateBatch method without placeholder handling
        private async Task<List<string>> TranslateBatchRaw(List<string> texts, string targetLanguage, List<string> contexts = null)
        {
            if (string.IsNullOrEmpty(deeplApiKey) || texts == null || texts.Count == 0) return null;

            int retryCount = 0;
            int delayMs = INITIAL_RETRY_DELAY_MS;
            string contextToUse = contexts?.FirstOrDefault() ?? "";

            while (retryCount <= MAX_RETRIES)
            {
                try
                {
                    using (var client = new System.Net.Http.HttpClient())
                    {
                        client.DefaultRequestHeaders.Add("Authorization", $"DeepL-Auth-Key {deeplApiKey}");
                        var baseUrl = useDeepLPro ? "https://api.deepl.com/v2" : "https://api-free.deepl.com/v2";

                        string targetLangCode = GetDeepLLanguageCode(targetLanguage);
                        var request = new BatchTranslationRequest
                        {
                            text = texts.ToArray(),
                            target_lang = targetLangCode,
                            preserve_formatting = preserveFormatting,
                            context = contextToUse
                        };

                        // Only add formality if the language supports it
                        if (IsFormalitySupported(targetLangCode))
                        {
                            request.formality = formalityPreference ? "more" : "less";
                        }

                        var json = JsonUtility.ToJson(request);
                        var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
                        var response = await client.PostAsync($"{baseUrl}/translate", content);
                        
                        if (response.IsSuccessStatusCode)
                        {
                            var result = await response.Content.ReadAsStringAsync();
                            var jsonResponse = JsonUtility.FromJson<BatchTranslationResponse>(result);
                            return jsonResponse?.translations?.Select(t => t.text).ToList();
                        }
                        else
                        {
                            var error = await response.Content.ReadAsStringAsync();
                            
                            // Check if it's a rate limit error
                            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                            {
                                if (retryCount < MAX_RETRIES)
                                {
                                    AddConfigLog($"Rate limit hit. Retrying in {delayMs/1000f}s (Attempt {retryCount + 1}/{MAX_RETRIES})", LogType.Warning);
                                    await Task.Delay(delayMs);
                                    delayMs *= 2; // Exponential backoff
                                    retryCount++;
                                    continue;
                                }
                            }
                            
                            AddConfigLog($"Translation failed ({response.StatusCode}): {error}", LogType.Error);
                            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                            {
                                AddConfigLog("Consider upgrading to DeepL Pro for higher rate limits.", LogType.Warning);
                            }
                            return null;
                        }
                    }
                }
                catch (System.Exception e)
                {
                    AddConfigLog($"Translation error: {e.Message}", LogType.Error);
                    return null;
                }
            }

            return null;
        }

        // Initialize placeholder transformation strategies
        private void InitializePlaceholderStrategies()
        {
            AddConfigLog("Initializing placeholder transformation strategies", LogType.Log);
            
            // Default strategy for European languages (keeps plural structure)
            Func<string, string> defaultStrategy = (original) => original;
            
            // Create a map from full language names to ISO codes
            var languageNameToCode = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
            {
                { "Japanese", "ja" },
                { "Chinese", "zh" },
                { "Chinese (Simplified)", "zh" },
                { "Chinese (Traditional)", "zh" },
                { "Korean", "ko" },
                // Add more mappings as needed
            };
            
            // Special strategies for languages that handle plurals differently
            placeholderTransformStrategies["ja"] = (original) => {
                // Japanese doesn't use grammatical plurals, so we transform plural placeholders
                // Example: {count:plural:X|Y} -> {}個の[noun]
                if (original.Contains(":plural:"))
                {
                    // Use a counter pattern for Japanese (個 is a general counter)
                    return "{}個の[noun]";
                }
                return original;
            };
            
            placeholderTransformStrategies["zh"] = (original) => {
                // Chinese also doesn't use grammatical plurals
                if (original.Contains(":plural:"))
                {
                    return "{}个[noun]";
                }
                return original;
            };
            
            placeholderTransformStrategies["ko"] = (original) => {
                // Korean also doesn't use grammatical plurals
                if (original.Contains(":plural:"))
                {
                    // Note: We're removing the 가/은 particle since it will be added by the sentence
                    return "{}개의 [noun]";
                }
                return original;
            };
            
            // Initialize template cache for each language
            foreach (string langCode in translationData.supportedLanguages)
            {
                placeholderTemplatesByLanguage[langCode] = new Dictionary<string, string>();
            }
        }
        
        // Convert a language name to its ISO code
        private string GetLanguageIsoCode(string languageName)
        {
            // Map of language names to ISO codes
            var languageNameToCode = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
            {
                { "Japanese", "ja" },
                { "Chinese", "zh" },
                { "Chinese (Simplified)", "zh" },
                { "Chinese (Traditional)", "zh" },
                { "Korean", "ko" },
                { "French", "fr" },
                { "Spanish", "es" },
                { "German", "de" },
                { "Italian", "it" },
                { "Portuguese", "pt" },
                { "Portuguese (Brazil)", "pt-br" },
                { "Russian", "ru" },
                { "Dutch", "nl" },
                { "Danish", "da" },
                { "Swedish", "sv" },
                { "Ukrainian", "uk" },
                // Add more mappings as needed
            };
            
            if (languageNameToCode.TryGetValue(languageName, out string code))
            {
                return code;
            }
            
            // If no mapping found, return the input (it might already be a code)
            return languageName.ToLowerInvariant();
        }
        
        // Get the appropriate template for a placeholder in a specific language
        private string GetPlaceholderTemplate(string placeholder, string languageName)
        {
            string languageCode = GetLanguageIsoCode(languageName);
            AddConfigLog($"Getting template for language: {languageName} (code: {languageCode})", LogType.Log);
            
            // Use or create language-specific cache
            if (!placeholderTemplatesByLanguage.ContainsKey(languageCode))
            {
                placeholderTemplatesByLanguage[languageCode] = new Dictionary<string, string>();
            }
            
            var templates = placeholderTemplatesByLanguage[languageCode];
            
            // Return existing template if we have one
            if (templates.ContainsKey(placeholder))
            {
                return templates[placeholder];
            }
            
            // Get the appropriate transformation strategy
            Func<string, string> strategy = null;
            
            // Try to get a language-specific strategy, fall back to default
            if (placeholderTransformStrategies.ContainsKey(languageCode))
            {
                AddConfigLog($"  Found language-specific strategy for {languageCode}", LogType.Log);
                strategy = placeholderTransformStrategies[languageCode];
            }
            else 
            {
                AddConfigLog($"  Using default strategy for {languageCode}", LogType.Log);
                strategy = (original) => original;
            }
            
            // Transform the placeholder using the strategy
            string template = strategy(placeholder);
            
            // Cache the template
            templates[placeholder] = template;
            
            return template;
        }

        /// <summary>
        /// Completely new approach using templates for translations
        /// </summary>
        private async Task<List<string>> TranslateWithTemplates(List<string> texts, string targetLanguage, List<string> contexts = null)
        {
            if (string.IsNullOrEmpty(deeplApiKey) || texts == null || texts.Count == 0) return null;
            
            // Make sure strategies are initialized
            if (placeholderTransformStrategies.Count == 0)
            {
                InitializePlaceholderStrategies();
            }
            
            List<string> results = new List<string>();
            string contextToUse = contexts?.FirstOrDefault() ?? "";
            
            foreach (string text in texts)
            {
                AddConfigLog($"Template-based translation for: \"{text}\" to {targetLanguage}", LogType.Log);
                
                // 1. Extract all placeholders and their values
                Dictionary<string, string> extractedPlaceholders = ExtractPlaceholdersWithValues(text);
                
                if (extractedPlaceholders.Count == 0)
                {
                    // No placeholders, just translate normally
                    var simpleResult = await TranslateBatchRaw(new List<string> { text }, targetLanguage, contexts);
                    results.Add(simpleResult?.FirstOrDefault() ?? text);
                    continue;
                }
                
                // 2. Create a template string with simple numbered placeholders
                string templateWithNumberedPlaceholders;
                List<string> placeholderKeys;
                (templateWithNumberedPlaceholders, placeholderKeys) = CreateNumberedTemplate(text, extractedPlaceholders);
                
                AddConfigLog($"Template with numbered placeholders: \"{templateWithNumberedPlaceholders}\"", LogType.Log);
                
                // 3. Translate the template string with preservation of variable names
                var translatedTemplates = await TranslateBatchWithPreservation(
                    new List<string> { templateWithNumberedPlaceholders }, 
                    targetLanguage, 
                    contexts
                );
                
                if (translatedTemplates == null || translatedTemplates.Count == 0)
                {
                    AddConfigLog("Failed to translate template", LogType.Error);
                    results.Add(text); // Fallback to original
                    continue;
                }
                
                string translatedTemplate = translatedTemplates[0];
                AddConfigLog($"Translated template: \"{translatedTemplate}\"", LogType.Log);
                
                // 4. For any placeholder content that needs translation, translate it
                Dictionary<string, string> translatedValues = await TranslatePlaceholderValues(
                    extractedPlaceholders, 
                    targetLanguage, 
                    contextToUse,
                    text  // Pass the full sentence as context
                );
                
                // 5. For language-specific placeholder transformations, apply them
                Dictionary<string, string> transformedPlaceholders = TransformPlaceholdersForLanguage(
                    extractedPlaceholders, 
                    translatedValues, 
                    targetLanguage
                );
                
                // 6. Reconstruct the final string with original placeholder structure
                string result = ReconstructTranslatedString(
                    translatedTemplate, 
                    placeholderKeys, 
                    transformedPlaceholders
                );
                
                AddConfigLog($"Final reconstructed translation: \"{result}\"", LogType.Log);
                results.Add(result);
            }
            
            return results;
        }
        
        private Dictionary<string, string> ExtractPlaceholdersWithValues(string text)
        {
            var result = new Dictionary<string, string>();
            string simplifiedText = text;
            int placeholderCount = 0;

            // Order matters! Process from most complex to simplest
            
            // 1. Complex gender+plural combinations
            var genderPluralRegex = new System.Text.RegularExpressions.Regex(
                @"{[^{}]+:gender\([^)]+\):[^{}]*(?:{[^{}]+}[^{}|]*\|?)*}"
            );
            foreach (System.Text.RegularExpressions.Match match in genderPluralRegex.Matches(text))
            {
                var placeholder = match.Value;
                result[placeholder] = placeholder;
                var texts = ExtractGenderPluralTexts(placeholder);
                if (texts.Count > 0)
                {
                    result[$"__GENDER_TEXTS_{placeholderCount}__"] = string.Join("|", texts);
                    AddConfigLog($"  Found {texts.Count} translatable gender texts: [{string.Join(", ", texts.Select(t => $"\"{t}\""))}]", LogType.Log);
                }
                simplifiedText = simplifiedText.Replace(placeholder, $"%VAR_{placeholderCount++}%");
            }

            // 2. Choose placeholders
            var chooseRegex = new System.Text.RegularExpressions.Regex(
                @"{[^{}]+:choose\([^)]+\):[^{}]*(?:{[^{}]*}[^{}|]*\|?)*}"
            );
            foreach (System.Text.RegularExpressions.Match match in chooseRegex.Matches(simplifiedText))
            {
                var placeholder = match.Value;
                result[placeholder] = placeholder;
                var (options, texts) = ExtractChooseOptionsAndTexts(placeholder);
                if (texts.Count > 0)
                {
                    result[$"__CHOOSE_TEXTS_{placeholderCount}__"] = string.Join("|", texts);
                    AddConfigLog($"  Found {texts.Count} translatable choose texts: [{string.Join(", ", texts.Select(t => $"\"{t}\""))}]", LogType.Log);
                }
                simplifiedText = simplifiedText.Replace(placeholder, $"%VAR_{placeholderCount++}%");
            }

            // 3. Plural placeholders
            var pluralRegex = new System.Text.RegularExpressions.Regex(
                @"{[^{}]+:plural:[^{}]*(?:{[^{}]+}[^{}|]*\|?)*}"
            );
            foreach (System.Text.RegularExpressions.Match match in pluralRegex.Matches(simplifiedText))
            {
                var placeholder = match.Value;
                result[placeholder] = placeholder;
                var texts = ExtractPluralTexts(placeholder);
                if (texts.Count > 0)
                {
                    result[$"__PLURAL_TEXTS_{placeholderCount}__"] = string.Join("|", texts);
                    AddConfigLog($"  Found {texts.Count} translatable plural texts: [{string.Join(", ", texts.Select(t => $"\"{t}\""))}]", LogType.Log);
                }
                simplifiedText = simplifiedText.Replace(placeholder, $"%VAR_{placeholderCount++}%");
            }

            // 4. List placeholders
            var listRegex = new System.Text.RegularExpressions.Regex(
                @"{[^{}]+:list:[^{}]*}"
            );
            foreach (System.Text.RegularExpressions.Match match in listRegex.Matches(simplifiedText))
            {
                var placeholder = match.Value;
                result[placeholder] = placeholder;
                simplifiedText = simplifiedText.Replace(placeholder, $"%VAR_{placeholderCount++}%");
            }

            // 5. Date/time placeholders
            var dateTimeRegex = new System.Text.RegularExpressions.Regex(
                @"{[^{}]+:(?:date|time):[^{}]*}"
            );
            foreach (System.Text.RegularExpressions.Match match in dateTimeRegex.Matches(simplifiedText))
            {
                var placeholder = match.Value;
                result[placeholder] = placeholder;
                simplifiedText = simplifiedText.Replace(placeholder, $"%VAR_{placeholderCount++}%");
            }

            // 6. Nested scopes
            var nestedRegex = new System.Text.RegularExpressions.Regex(
                @"{[^{}]+:{[^{}]+}[^{}]*}"
            );
            foreach (System.Text.RegularExpressions.Match match in nestedRegex.Matches(simplifiedText))
            {
                var placeholder = match.Value;
                if (!result.ContainsKey(placeholder)) // Avoid duplicates from previous patterns
                {
                    result[placeholder] = placeholder;
                    simplifiedText = simplifiedText.Replace(placeholder, $"%VAR_{placeholderCount++}%");
                }
            }

            // 7. Simple variable placeholders (must be last)
            var simpleRegex = new System.Text.RegularExpressions.Regex(@"{[^{}]+}");
            foreach (System.Text.RegularExpressions.Match match in simpleRegex.Matches(simplifiedText))
            {
                var placeholder = match.Value;
                if (!result.ContainsKey(placeholder)) // Avoid duplicates from previous patterns
                {
                    result[placeholder] = placeholder;
                    simplifiedText = simplifiedText.Replace(placeholder, $"%VAR_{placeholderCount++}%");
                }
            }

            return result;
        }
        
        private (List<string>, List<string>) ExtractChooseOptionsAndTexts(string placeholder)
        {
            var options = new List<string>();
            var texts = new List<string>();
            
            try
            {
                // Extract options from choose(option1,option2,...)
                var optionsMatch = System.Text.RegularExpressions.Regex.Match(
                    placeholder,
                    @":choose\(([^)]+)\):"
                );
                
                if (optionsMatch.Success && optionsMatch.Groups.Count > 1)
                {
                    options.AddRange(optionsMatch.Groups[1].Value.Split(','));
                    
                    // Extract the text part after the options
                    var textPart = placeholder.Substring(placeholder.IndexOf("):") + 2);
                    textPart = textPart.TrimEnd('}'); // Remove closing brace
                    
                    // Split by | to get individual texts
                    var textOptions = textPart.Split('|');
                    texts.AddRange(textOptions.Select(t => t.Trim()));
                    
                    AddConfigLog($"  Extracted from choose: options=[{string.Join(",", options)}], texts=[{string.Join("|", texts)}]", LogType.Log);
                }
            }
            catch (System.Exception ex)
            {
                AddConfigLog($"Error extracting choose options: {ex.Message}", LogType.Error);
            }
            
            return (options, texts);
        }
        
        private (string, List<string>) CreateNumberedTemplate(string text, Dictionary<string, string> placeholders)
        {
            string result = text;
            List<string> keys = new List<string>();
            
            // Sort placeholders by length (descending) to handle nested placeholders correctly
            var sortedPlaceholders = placeholders.Keys.OrderByDescending(k => k.Length).ToList();
            
            int i = 0;
            foreach (var original in sortedPlaceholders)
            {
                // Use a format that's less likely to be translated
                // Using %VAR_X% format which is more technical and less likely to be translated
                string replacement = $"%VAR_{i}%";
                result = result.Replace(original, replacement);
                keys.Add(original);
                AddConfigLog($"  Replaced \"{original}\" with \"{replacement}\"", LogType.Log);
                i++;
            }
            
            // Process the text to preserve variable names
            result = PreserveVariableNames(result);
            
            return (result, keys);
        }
        
        private string PreserveVariableNames(string text)
        {
            // Find placeholder variable names and mark them for preservation
            var varNameRegex = new System.Text.RegularExpressions.Regex(@"%VAR_\d+%");
            string processedText = text;
            
            // Also look for patterns that might be variable names inside the placeholders
            var potentialVarRegex = new System.Text.RegularExpressions.Regex(@"(\w+)(:plural:|:gender:|:choose:)");
            var matches = potentialVarRegex.Matches(processedText);
            
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (match.Groups.Count > 2)
                {
                    string varName = match.Groups[1].Value;
                    AddConfigLog($"  Found variable name to preserve: \"{varName}\"", LogType.Log);
                    
                    // Mark the variable name to be preserved in translation
                    string replacement = $"<var>{varName}</var>{match.Groups[2].Value}";
                    processedText = processedText.Replace(match.Value, replacement);
                }
            }
            
            return processedText;
        }
        
        private async Task<List<string>> TranslateBatchWithPreservation(List<string> texts, string targetLanguage, List<string> contexts = null)
        {
            if (texts == null || texts.Count == 0) return null;
            
            // Mark variable names and other parts that should not be translated
            List<string> processedTexts = new List<string>();
            foreach (string text in texts)
            {
                // Wrap variable names in <var> tags which DeepL will preserve
                string processed = text;
                processedTexts.Add(processed);
            }
            
            var translatedTexts = await TranslateBatchRaw(processedTexts, targetLanguage, contexts);
            
            if (translatedTexts == null) return null;
            
            // Restore the <var> tags back to normal text
            List<string> restoredTexts = new List<string>();
            foreach (var translatedText in translatedTexts)
            {
                // Fix any double underscores or trailing underscores that might appear from DeepL
                string cleaned = translatedText.Replace("___", "__").Replace("__.", ".__");
                
                // Handle specific case for Chinese trailing underscore
                if (targetLanguage.Contains("Chinese"))
                {
                    cleaned = cleaned.Replace("___。", ".__。");
                    cleaned = cleaned.Replace("__。", ".__。");
                    cleaned = cleaned.Replace("_。", "。");
                }
                
                // Handle specific issue with Russian/Ukrainian dot after placeholder
                if (targetLanguage.Contains("Russian") || targetLanguage.Contains("Ukrainian"))
                {
                    cleaned = cleaned.Replace("__PLACEHOLDER_0.__", "__PLACEHOLDER_0__.");
                    cleaned = cleaned.Replace("__PLACEHOLDER_0._", "__PLACEHOLDER_0__");
                }
                
                string restored = System.Text.RegularExpressions.Regex.Replace(
                    cleaned, 
                    @"<var>([^<>]+)</var>", 
                    "$1"
                );
                restoredTexts.Add(restored);
            }
            
            return restoredTexts;
        }
        
        private async Task<Dictionary<string, string>> TranslatePlaceholderValues(
            Dictionary<string, string> placeholders, 
            string targetLanguage, 
            string context,
            string fullSentence = null)
        {
            var result = new Dictionary<string, string>();
            var textsToTranslate = new List<string>();
            var keys = new List<string>();
            
            AddConfigLog($"Translating placeholder values for {placeholders.Count} placeholder(s)", LogType.Log);
            
            // First handle choose texts
            var chooseTexts = placeholders
                .Where(kvp => kvp.Key.StartsWith("__CHOOSE_TEXTS_"))
                .SelectMany(kvp => kvp.Value.Split('|'))
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();
            
            if (chooseTexts.Count > 0)
            {
                AddConfigLog($"Found {chooseTexts.Count} choose texts to translate", LogType.Log);
                foreach (var text in chooseTexts)
                {
                    textsToTranslate.Add(text);
                    keys.Add(text);
                    AddConfigLog($"  Queueing choose text: \"{text}\"", LogType.Log);
                }
            }
            
            // Then handle plural placeholders as before
            foreach (var kvp in placeholders.Where(p => !p.Key.StartsWith("__CHOOSE_TEXTS_")))
            {
                string placeholder = kvp.Key;
                
                if (placeholder.Contains(":plural:"))
                {
                    AddConfigLog($"  Extracting inner texts from plural placeholder: {placeholder}", LogType.Log);
                    
                    var innerTextRegex = new System.Text.RegularExpressions.Regex(@"{([^{}]+)}");
                    var matches = innerTextRegex.Matches(placeholder);
                    
                    foreach (System.Text.RegularExpressions.Match match in matches)
                    {
                        if (match.Groups.Count > 1)
                        {
                            string innerText = match.Groups[1].Value.Trim();
                            if (!string.IsNullOrWhiteSpace(innerText) && innerText != "}")
                            {
                                textsToTranslate.Add(innerText);
                                keys.Add(innerText);
                                AddConfigLog($"    Found inner text to translate: \"{innerText}\"", LogType.Log);
                            }
                        }
                    }
                }
            }
            
            if (textsToTranslate.Count == 0)
            {
                AddConfigLog("No texts to translate", LogType.Warning);
                return result;
            }
            
            // Create an enhanced context for translation
            string enhancedContext = context;
            if (!string.IsNullOrEmpty(fullSentence))
            {
                enhancedContext = $"These phrases appear in the sentence: \"{fullSentence}\". {context}";
                AddConfigLog($"Using enhanced context: \"{enhancedContext}\"", LogType.Log);
            }
            
            // Translate all texts in one batch
            var translatedTexts = await TranslateBatchRaw(textsToTranslate, targetLanguage, new List<string> { enhancedContext });
            
            if (translatedTexts != null)
            {
                for (int i = 0; i < keys.Count && i < translatedTexts.Count; i++)
                {
                    result[keys[i]] = translatedTexts[i];
                    AddConfigLog($"  Translated \"{keys[i]}\" to \"{translatedTexts[i]}\"", LogType.Log);
                }
            }
            
            return result;
        }
        
        private Dictionary<string, string> TransformPlaceholdersForLanguage(
            Dictionary<string, string> originalPlaceholders, 
            Dictionary<string, string> translatedValues, 
            string targetLanguage)
        {
            var result = new Dictionary<string, string>();
            string languageCode = GetLanguageIsoCode(targetLanguage);
            
            AddConfigLog($"Transforming placeholders for language: {targetLanguage} (code: {languageCode})", LogType.Log);
            
            foreach (var kvp in originalPlaceholders)
            {
                string original = kvp.Key;
                string template = GetPlaceholderTemplate(original, targetLanguage);
                
                // If template is different from original, we're using a language-specific template
                if (template != original)
                {
                    // For special language-specific transformations
                    string transformed = template;
                    
                    // For a template like {}個の[noun], replace [noun] with a translated noun
                    if (original.Contains(":plural:"))
                    {
                        // Extract the variable name from original (e.g., "count" from "{count:plural:...")
                        string varName = "";
                        var varMatch = System.Text.RegularExpressions.Regex.Match(original, @"{([^:{}]+)");
                        if (varMatch.Success && varMatch.Groups.Count > 1)
                        {
                            varName = varMatch.Groups[1].Value;
                        }
                        
                        // Extract inner texts to get the nouns
                        var innerTexts = new List<string>();
                        var innerTextRegex = new System.Text.RegularExpressions.Regex(@"{([^{}]+)}");
                        var matches = innerTextRegex.Matches(original);
                        
                        foreach (System.Text.RegularExpressions.Match match in matches)
                        {
                            if (match.Groups.Count > 1)
                            {
                                string innerText = match.Groups[1].Value.Trim();
                                if (!string.IsNullOrWhiteSpace(innerText))
                                {
                                    innerTexts.Add(innerText);
                                }
                            }
                        }
                        
                        // Use translated value if available
                        if (innerTexts.Count > 0)
                        {
                            string firstInnerText = innerTexts[0];
                            string translatedNoun = translatedValues.ContainsKey(firstInnerText) 
                                ? translatedValues[firstInnerText]
                                : firstInnerText;
                                
                            transformed = transformed.Replace("[noun]", translatedNoun);
                        }
                        
                        // Replace {} with the original variable name
                        if (!string.IsNullOrEmpty(varName))
                        {
                            transformed = transformed.Replace("{}", "{" + varName + "}");
                        }
                    }
                    
                    result[original] = transformed;
                    AddConfigLog($"  Transformed for language: \"{original}\" -> \"{transformed}\"", LogType.Log);
                }
                else
                {
                    // If no special transformation, keep original structure but use translated values
                    string transformed = original;
                    
                    // Replace any inner texts with their translations while preserving variables like "count"
                    if (original.Contains(":plural:"))
                    {
                        // Preserve the variable name part
                        string varNamePart = "";
                        int pluralIndex = original.IndexOf(":plural:");
                        if (pluralIndex > 0)
                        {
                            varNamePart = original.Substring(0, pluralIndex + 8); // include ":plural:"
                            string pluralPart = original.Substring(pluralIndex + 8);
                            
                            // Now translate inner values in the plural part
                            foreach (var tvKvp in translatedValues)
                            {
                                string pattern = "{" + tvKvp.Key + "}";
                                string replacement = "{" + tvKvp.Value + "}";
                                pluralPart = pluralPart.Replace(pattern, replacement);
                            }
                            
                            transformed = varNamePart + pluralPart;
                        }
                    }
                    
                    result[original] = transformed;
                    AddConfigLog($"  Kept original structure with translated inner texts: \"{original}\" -> \"{transformed}\"", LogType.Log);
                }
            }
            
            return result;
        }
        
        private string ReconstructTranslatedString(
            string translatedTemplate, 
            List<string> placeholderKeys, 
            Dictionary<string, string> transformedPlaceholders)
        {
            string initialResult = System.Text.RegularExpressions.Regex.Replace(
                translatedTemplate, 
                @"<var>([^<>]+)</var>", 
                "$1"
            );
            
            AddConfigLog($"Reconstructing with preserved var names: \"{initialResult}\"", LogType.Log);
            
            string result = initialResult;
            for (int i = 0; i < placeholderKeys.Count; i++)
            {
                string key = placeholderKeys[i];
                string replacement = transformedPlaceholders.ContainsKey(key) 
                    ? transformedPlaceholders[key] 
                    : key;
                
                // Handle different placeholder types
                if (key.Contains(":choose("))
                {
                    string textsKey = $"__CHOOSE_TEXTS_{i}__";
                    if (transformedPlaceholders.ContainsKey(textsKey))
                    {
                        replacement = ReconstructChoosePlaceholder(key, transformedPlaceholders[textsKey]);
                    }
                }
                else if (key.Contains(":gender("))
                {
                    string textsKey = $"__GENDER_TEXTS_{i}__";
                    if (transformedPlaceholders.ContainsKey(textsKey))
                    {
                        replacement = ReconstructGenderPlaceholder(key, transformedPlaceholders[textsKey]);
                    }
                }
                else if (key.Contains(":plural:"))
                {
                    string textsKey = $"__PLURAL_TEXTS_{i}__";
                    if (transformedPlaceholders.ContainsKey(textsKey))
                    {
                        replacement = ReconstructPluralPlaceholder(key, transformedPlaceholders[textsKey]);
                    }
                }
                
                // Replace the placeholder marker
                result = result.Replace($"%VAR_{i}%", replacement);
                
                // Also handle any potential translations of our markers
                var varPattern = new System.Text.RegularExpressions.Regex(
                    $@"[_\-%](?:VAR|PLACEHOLDER|LOCUTOR|VARIABLE|var)[-_]?{i}[_\-%]",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );
                result = varPattern.Replace(result, replacement);
                
                AddConfigLog($"  Restored placeholder %VAR_{i}% -> \"{replacement}\"", LogType.Log);
            }
            
            return result;
        }

        private string ReconstructChoosePlaceholder(string originalPlaceholder, string translatedTexts)
        {
            try
            {
                // Extract the variable name and options part
                var match = System.Text.RegularExpressions.Regex.Match(
                    originalPlaceholder,
                    @"{([^:]+):choose\(([^)]+)\):"
                );
                
                if (!match.Success || match.Groups.Count < 3)
                {
                    AddConfigLog("Failed to extract choose components", LogType.Error);
                    return originalPlaceholder;
                }
                
                string varName = match.Groups[1].Value;
                string options = match.Groups[2].Value;
                
                // Split the translated texts
                var texts = translatedTexts.Split('|');
                
                // Reconstruct the choose placeholder
                string reconstructed = $"{{{varName}:choose({options}):";
                reconstructed += string.Join("|", texts);
                reconstructed += "}";
                
                AddConfigLog($"Reconstructed choose: \"{reconstructed}\"", LogType.Log);
                return reconstructed;
            }
            catch (System.Exception ex)
            {
                AddConfigLog($"Error reconstructing choose placeholder: {ex.Message}", LogType.Error);
                return originalPlaceholder;
            }
        }

        private string ReconstructGenderPlaceholder(string originalPlaceholder, string translatedTexts)
        {
            try
            {
                // Extract components: variable name, gender options, and text parts
                var match = System.Text.RegularExpressions.Regex.Match(
                    originalPlaceholder,
                    @"{([^:]+):gender\(([^)]+)\):(.+)}"
                );

                if (!match.Success || match.Groups.Count < 4)
                {
                    AddConfigLog("Failed to extract gender components", LogType.Error);
                    return originalPlaceholder;
                }

                string varName = match.Groups[1].Value;
                string genderOptions = match.Groups[2].Value;
                
                // Split translated texts and reconstruct the gender variants
                var texts = translatedTexts.Split('|');
                
                string reconstructed = $"{{{varName}:gender({genderOptions}):";
                reconstructed += string.Join("|", texts);
                reconstructed += "}";

                AddConfigLog($"Reconstructed gender: \"{reconstructed}\"", LogType.Log);
                return reconstructed;
            }
            catch (System.Exception ex)
            {
                AddConfigLog($"Error reconstructing gender placeholder: {ex.Message}", LogType.Error);
                return originalPlaceholder;
            }
        }

        private string ReconstructPluralPlaceholder(string originalPlaceholder, string translatedTexts)
        {
            try
            {
                // Extract the variable name and plural format
                var match = System.Text.RegularExpressions.Regex.Match(
                    originalPlaceholder,
                    @"{([^:]+):plural:(.+)}"
                );

                if (!match.Success || match.Groups.Count < 3)
                {
                    AddConfigLog("Failed to extract plural components", LogType.Error);
                    return originalPlaceholder;
                }

                string varName = match.Groups[1].Value;
                
                // Split translated texts and reconstruct the plural variants
                var texts = translatedTexts.Split('|');
                
                string reconstructed = $"{{{varName}:plural:";
                reconstructed += string.Join("|", texts);
                reconstructed += "}";

                AddConfigLog($"Reconstructed plural: \"{reconstructed}\"", LogType.Log);
                return reconstructed;
            }
            catch (System.Exception ex)
            {
                AddConfigLog($"Error reconstructing plural placeholder: {ex.Message}", LogType.Error);
                return originalPlaceholder;
            }
        }

        // Update main TranslateBatch method to use the selected AI system
        private async Task<List<string>> TranslateBatch(List<string> texts, string targetLanguage, List<string> contexts = null)
        {
            // If OpenAI is selected, use that implementation
            if (selectedAISystem == AITranslationSystem.OpenAI)
            {
                return await TranslateWithOpenAI(texts, targetLanguage, contexts);
            }
            
            // Otherwise, continue with the existing DeepL implementation
            if (string.IsNullOrEmpty(deeplApiKey) || texts == null || texts.Count == 0) return null;

            // Check if we should use the newer template-based approach or the older placeholder approach
            bool useTemplateApproach = true; // Set to true to use the new approach
            
            if (useTemplateApproach)
            {
                return await TranslateWithTemplates(texts, targetLanguage, contexts);
            }
            else
            {
                // Original phased approach from the previous implementation
                // Only kept as fallback
                
                string contextToUse = contexts?.FirstOrDefault() ?? "";

                // Phase 1: Preprocess texts to handle placeholders
                var placeholderMappings = texts.Select(PreprocessTextForTranslation).ToList();
                
                // Phase 2: Translate the tokenized texts
                List<string> tokenizedTexts = placeholderMappings.Select(m => m.TokenizedText).ToList();
                List<string> translatedTokenizedTexts = await TranslateBatchRaw(tokenizedTexts, targetLanguage, contexts);
                
                if (translatedTokenizedTexts == null || translatedTokenizedTexts.Count == 0)
                    return null;
                
                // Phase 3: Translate inner texts of placeholders
                for (int i = 0; i < placeholderMappings.Count; i++)
                {
                    placeholderMappings[i] = await TranslateInnerTexts(placeholderMappings[i], targetLanguage, contextToUse);
                }
                
                // Phase 4: Restore placeholders with translated inner texts
                List<string> results = new List<string>();
                for (int i = 0; i < Math.Min(translatedTokenizedTexts.Count, placeholderMappings.Count); i++)
                {
                    string translatedText = RestoreTextPlaceholders(translatedTokenizedTexts[i], placeholderMappings[i]);
                    results.Add(translatedText);
                }
                
                return results;
            }
        }

        #endregion

        #region Log Display

        private void DrawLogSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.BeginHorizontal();
            showConfigLogs = EditorGUILayout.Foldout(showConfigLogs, "Show Logs", true);
            EditorGUILayout.EndHorizontal();
            
            if (showConfigLogs)
            {
                // Show translation data path information with the new folder structure
                string baseFolder = TranslationDataProvider.BaseFolder;
                string dataFolder = TranslationDataProvider.DataFolder;
                string languagesFolder = TranslationDataProvider.LanguagesFolder;
                
                // Log controls
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Clear Logs", GUILayout.Width(100)))
                {
                    configLogs.Clear();
                    Repaint();
                }
                if (GUILayout.Button("Copy All", GUILayout.Width(100)))
                {
                    var logText = string.Join("\n", configLogs.Select(log => $"[{log.timestamp:HH:mm:ss}] {log.message}"));
                    EditorGUIUtility.systemCopyBuffer = logText;
                }
                EditorGUILayout.EndHorizontal();
                
                // Display logs
                configLogScrollPosition = EditorGUILayout.BeginScrollView(configLogScrollPosition, GUILayout.Height(200));
                
                // Display logs in chronological order (oldest to newest)
                foreach (var (message, type, timestamp) in configLogs)
                {
                    var style = type switch
                    {
                        LogType.Error => errorLogStyle,
                        LogType.Warning => warningLogStyle,
                        _ => logStyle
                    };

                    EditorGUILayout.LabelField($"[{timestamp:HH:mm:ss}] {message}", style);
                }
                
                if (shouldScrollToBottom && Event.current.type == EventType.Repaint)
                {
                    configLogScrollPosition.y = float.MaxValue;
                    shouldScrollToBottom = false;
                }
                
                EditorGUILayout.EndScrollView();
            }
            
            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Translation Methods

        public async Task TranslateAllLanguagesForKey(string key)
        {
            if (string.IsNullOrEmpty(deeplApiKey) || translationData == null) return;

            // Extract base text for translation but keep full key for storage
            string textForTranslation = key;
            string disambiguationContext = null;
            
            AddConfigLog($"Translating '{key}' to all languages...");

            // Get the key index
            int keyIndex = translationData.allKeys.IndexOf(key);
            if (keyIndex < 0) 
            {
                AddConfigLog($"Key '{key}' not found in translation data", LogType.Error);
                return;
            }

            // Get translation context, combining both explicit context and disambiguation context
            string translationContext = includeContextInTranslation ? TranslationMetaDataProvider.Metadata.GetTranslationContext(key) : "";
            
            // If we have disambiguation context, add it to the translation context
            if (!string.IsNullOrEmpty(disambiguationContext))
            {
                translationContext = string.IsNullOrEmpty(translationContext) 
                    ? $"Context: {disambiguationContext}" 
                    : $"{translationContext} (Disambiguation: {disambiguationContext})";
            }

            // Prepare all target translations with their language data
            var targetTranslations = new List<(string language, LanguageData data)>();
            
            for (int i = 0; i < translationData.languageDataDictionary.Length; i++)
            {
                if (translationData.supportedLanguages[i + 1] == translationData.defaultLanguage)
                    continue;
                    
                var assetRef = translationData.languageDataDictionary[i];
                string assetPath = AssetDatabase.GUIDToAssetPath(assetRef.AssetGUID);
                LanguageData languageData = AssetDatabase.LoadAssetAtPath<LanguageData>(assetPath);
                
                if (languageData != null && keyIndex < languageData.allText.Count)
                {
                    targetTranslations.Add((translationData.supportedLanguages[i + 1], languageData));
                }
            }

            if (targetTranslations.Count == 0)
            {
                AddConfigLog($"No languages to translate for '{key}'");
                return;
            }

            // Group languages by their DeepL codes to optimize batch translations
            var languageGroups = targetTranslations
                .GroupBy(l => GetDeepLLanguageCode(l.language))
                .ToList();

            int totalTranslations = 0;
            int successfulTranslations = 0;

            // For each unique language code
            foreach (var langGroup in languageGroups)
            {
                string langCode = langGroup.Key;
                if (string.IsNullOrEmpty(langCode)) continue;

                var result = await TranslateBatch(
                    new List<string> { textForTranslation }, // Send only the base text 
                    langGroup.First().language, // Use first language as they share the same DeepL code
                    new List<string> { translationContext }
                );

                if (result != null && result.Count > 0)
                {
                    // Apply the translation to all languages in this group
                    foreach (var (language, languageData) in langGroup)
                    {
                        if (keyIndex < languageData.allText.Count)
                        {
                            Undo.RecordObject(languageData, "Update Translations");
                            languageData.allText[keyIndex] = result[0];
                            EditorUtility.SetDirty(languageData);
                            isDirty = true;
                            successfulTranslations++;
                        }
                        totalTranslations++;
                    }
                }
            }

            lastEditTime = EditorApplication.timeSinceStartup;
            AddConfigLog($"✓ Translation completed for '{key}' to {successfulTranslations}/{totalTranslations} languages");
        }

        public async Task TranslateMissingLanguagesForKey(string key)
        {
            if (string.IsNullOrEmpty(deeplApiKey) || translationData == null) return;

            // Extract base text for translation but keep full key for storage
            string textForTranslation = key;
            string disambiguationContext = null;
            
            AddConfigLog($"Translating missing languages for '{key}'...");

            // Get the key index
            int keyIndex = translationData.allKeys.IndexOf(key);
            if (keyIndex < 0)
            {
                AddConfigLog($"Key '{key}' not found in translation data", LogType.Error);
                return;
            }

            // Get translation context, combining both explicit context and disambiguation context
            string translationContext = includeContextInTranslation ? TranslationMetaDataProvider.Metadata.GetTranslationContext(key) : "";
            
            // If we have disambiguation context, add it to the translation context
            if (!string.IsNullOrEmpty(disambiguationContext))
            {
                translationContext = string.IsNullOrEmpty(translationContext) 
                    ? $"Context: {disambiguationContext}" 
                    : $"{translationContext} (Disambiguation: {disambiguationContext})";
            }

            // Find languages with missing translations
            var missingTranslations = new List<(string language, LanguageData data)>();
            
            for (int i = 0; i < translationData.languageDataDictionary.Length; i++)
            {
                var assetRef = translationData.languageDataDictionary[i];
                string assetPath = AssetDatabase.GUIDToAssetPath(assetRef.AssetGUID);
                LanguageData languageData = AssetDatabase.LoadAssetAtPath<LanguageData>(assetPath);
                
                if (languageData != null && translationData.supportedLanguages[i + 1] != translationData.defaultLanguage && 
                    keyIndex < languageData.allText.Count && string.IsNullOrEmpty(languageData.allText[keyIndex]))
                {
                    missingTranslations.Add((translationData.supportedLanguages[i + 1], languageData));
                }
            }

            if (missingTranslations.Count == 0)
            {
                AddConfigLog($"✓ No missing translations found for '{key}'");
                return;
            }

            // Group languages by their DeepL codes to optimize batch translations
            var languageGroups = missingTranslations
                .GroupBy(l => GetDeepLLanguageCode(l.language))
                .ToList();

            int totalTranslations = 0;
            int successfulTranslations = 0;

            // For each unique language code
            foreach (var langGroup in languageGroups)
            {
                string langCode = langGroup.Key;
                if (string.IsNullOrEmpty(langCode)) continue;

                var result = await TranslateBatch(
                    new List<string> { textForTranslation }, // Send only the base text
                    langGroup.First().language, // Use first language as they share the same DeepL code
                    new List<string> { translationContext }
                );

                if (result != null && result.Count > 0)
                {
                    // Apply the translation to all languages in this group
                    foreach (var (language, languageData) in langGroup)
                    {
                        if (keyIndex < languageData.allText.Count)
                        {
                            Undo.RecordObject(languageData, "Update Translations");
                            languageData.allText[keyIndex] = result[0];
                            EditorUtility.SetDirty(languageData);
                            isDirty = true;
                            successfulTranslations++;
                        }
                        totalTranslations++;
                    }
                }
            }

            lastEditTime = EditorApplication.timeSinceStartup;
            AddConfigLog($"✓ Successfully translated '{key}' for {successfulTranslations}/{totalTranslations} missing languages");
        }

        public async void TranslateSingleField(string key, string targetLanguage, LanguageData languageData)
        {
            if (string.IsNullOrEmpty(deeplApiKey)) return;

                        // Get the key index
            int keyIndex = translationData.allKeys.IndexOf(key);

            // Extract base text for translation but keep full key for storage
            string textForTranslation = key;
            string disambiguationContext = null;
            
            AddConfigLog($"Translating '{key}' to {targetLanguage}...");
            
            // Get translation context, combining both explicit context and disambiguation context
            string translationContext = includeContextInTranslation ? TranslationMetaDataProvider.Metadata.GetTranslationContext(key) : "";
            
            // If we have both types of context, combine them for better translation
            if (!string.IsNullOrEmpty(disambiguationContext))
            {
                translationContext = string.IsNullOrEmpty(translationContext) 
                    ? $"Context: {disambiguationContext}" 
                    : $"{translationContext} (Disambiguation: {disambiguationContext})";
            }

            var result = await TranslateBatch(
                new List<string> { textForTranslation }, // Send only the base text without disambiguation markers
                targetLanguage,
                new List<string> { translationContext } // Include all context information
            );

            if (result != null && result.Count > 0)
            {
                Undo.RecordObject(languageData, "Update Translation");
                languageData.allText[keyIndex] = result[0];
                EditorUtility.SetDirty(languageData);
                isDirty = true;
                lastEditTime = EditorApplication.timeSinceStartup;
                AddConfigLog($"✓ Translation completed for '{key}' to {targetLanguage}");
            }
        }

        #endregion

        private static class UnityThread
        {
            private static int mainThreadId;
            private static bool initialized;

            public static bool isMainThread
            {
                get
                {
                    if (!initialized)
                    {
                        mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                        initialized = true;
                    }
                    return System.Threading.Thread.CurrentThread.ManagedThreadId == mainThreadId;
                }
            }
        }

        // Add these classes for OpenAI API serialization
        [System.Serializable]
        private class OpenAIMessage
        {
            public string role;
            public string content;
        }

        [System.Serializable]
        private class OpenAIRequest
        {
            public string model;
            public List<OpenAIMessage> messages;
            public float temperature = 0.3f;
            public bool stream = false;
        }

        [System.Serializable]
        private class OpenAIChoice
        {
            public OpenAIMessage message;
            public string finish_reason;
            public int index;
        }

        [System.Serializable]
        private class OpenAIResponse
        {
            public string id;
            public string model;
            public List<OpenAIChoice> choices;
        }

        // Add this method for OpenAI translation
        private async Task<List<string>> TranslateWithOpenAI(List<string> texts, string targetLanguage, List<string> contexts = null)
        {
            if (string.IsNullOrEmpty(openAIApiKey) || texts == null || texts.Count == 0) return null;
            
            AddConfigLog($"Translating {texts.Count} text(s) with OpenAI to {targetLanguage}");
            
            string contextToUse = contexts?.FirstOrDefault() ?? "";
            List<string> results = new List<string>();
            
            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {openAIApiKey}");
                    
                    foreach (var text in texts)
                    {
                        // Use custom prompt template and replace placeholders
                        string prompt = openAICustomPrompt
                            .Replace("{targetLanguage}", targetLanguage)
                            .Replace("{text}", text);
                        
                        if (!string.IsNullOrEmpty(contextToUse))
                        {
                            prompt += $"\n\nContext: {contextToUse}";
                        }
                        
                        if (text.Contains("{") && text.Contains("}"))
                        {
                            prompt += "\n\nIMPORTANT TRANSLATION RULES:\n" +
                                      "1. Preserve ALL placeholders in curly braces exactly as they appear, including their structure.\n" +
                                      "2. Do NOT translate variable names, properties, or function names inside placeholders (e.g., 'count', 'player.name', 'gender', 'choose', 'plural', 'list', 'date').\n" +
                                      "3. DO translate human-readable words/phrases that appear inside nested braces within placeholders (e.g., 'item', 'items', 'potion', 'potions').\n" +
                                      "4. For placeholders with plural forms like {count:plural:1 {item}|{} {items}}:\n" +
                                      "   - Translate readable words to the correct singular/plural forms in your language\n" +
                                      "   - For languages with more complex plural rules, maintain the structure but use correct grammar\n" +
                                      "5. For complex placeholders like {character:gender(male,female):{} found {...}}:\n" +
                                      "   - Preserve all formatting, colons, pipes, and parameters\n" +
                                      "   - Only translate the human-readable phrases between nested braces\n" +
                                      "   - Adapt pronouns and gender-specific words appropriately for the target language\n" +
                                      "6. For nested placeholders like {player:{name} ({level})}, keep all structure intact\n" +
                                      "7. For conditional formatting like {doorState:choose(locked,unlocked,broken):...}, preserve all option values\n" +
                                      "8. Use natural phrasing and grammar for the target language, while keeping the placeholder structure intact.";
                        }
                        
                        // Add gaming-specific context for certain languages to help with terminology
                        if (targetLanguage == "Korean" || targetLanguage == "Japanese" || 
                            targetLanguage == "Chinese" || targetLanguage == "Portuguese" ||
                            targetLanguage == "Dutch")
                        {
                            prompt += "\n\nNote: Terms like 'item', 'potion', etc. should be translated to the appropriate gaming term in your language, not left in English. In gaming contexts, use native terminology that gamers would recognize.";
                        }
                        
                        AddConfigLog($"OpenAI prompt: {prompt}");
                        
                        var requestData = new OpenAIRequest
                        {
                            model = openAIModel,
                            messages = new List<OpenAIMessage>
                            {
                                new OpenAIMessage { role = "system", content = "You are a professional game localization expert with deep knowledge of gaming terminology across languages. Your task is to translate text accurately while preserving all formatting, placeholders, and complex conditional logic." },
                                new OpenAIMessage { role = "user", content = prompt }
                            },
                            temperature = 0.3f
                        };
                        
                        var json = JsonUtility.ToJson(requestData);
                        var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
                        
                        var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);
                        
                        if (response.IsSuccessStatusCode)
                        {
                            var responseContent = await response.Content.ReadAsStringAsync();
                            var apiResponse = JsonUtility.FromJson<OpenAIResponse>(responseContent);
                            
                            if (apiResponse != null && apiResponse.choices != null && apiResponse.choices.Count > 0)
                            {
                                string translatedText = apiResponse.choices[0].message.content.Trim();
                                
                                // Remove quotes if OpenAI added them
                                if (translatedText.StartsWith("\"") && translatedText.EndsWith("\""))
                                {
                                    translatedText = translatedText.Substring(1, translatedText.Length - 2);
                                }
                                
                                AddConfigLog($"OpenAI translated: \"{translatedText}\"");
                                results.Add(translatedText);
                            }
                            else
                            {
                                AddConfigLog("Failed to get translation from OpenAI response", LogType.Error);
                                results.Add(text); // Fallback to original
                            }
                        }
                        else
                        {
                            var error = await response.Content.ReadAsStringAsync();
                            AddConfigLog($"OpenAI translation failed: {error}", LogType.Error);
                            results.Add(text); // Fallback to original
                        }
                    }
                }
                
                return results;
            }
            catch (System.Exception e)
            {
                AddConfigLog($"Error translating with OpenAI: {e.Message}", LogType.Error);
                return texts; // Return original texts on error
            }
        }

        private List<string> ExtractGenderPluralTexts(string placeholder)
        {
            var texts = new List<string>();
            try
            {
                // Extract the text part after gender(...):
                var match = System.Text.RegularExpressions.Regex.Match(
                    placeholder,
                    @":gender\([^)]+\):(.+)}"
                );

                if (match.Success && match.Groups.Count > 1)
                {
                    string textPart = match.Groups[1].Value;
                    // Split by | to get gender variants
                    var variants = textPart.Split('|');
                    foreach (var variant in variants)
                    {
                        // Extract any nested plural texts
                        var pluralTexts = ExtractPluralTexts("{dummy:plural:" + variant + "}");
                        texts.AddRange(pluralTexts);
                        
                        // Also extract any non-plural translatable text
                        var innerTexts = ExtractInnerTexts(variant);
                        texts.AddRange(innerTexts);
                    }
                }
            }
            catch (System.Exception ex)
            {
                AddConfigLog($"Error extracting gender-plural texts: {ex.Message}", LogType.Error);
            }
            return texts.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().ToList();
        }

        private List<string> ExtractPluralTexts(string placeholder)
        {
            var texts = new List<string>();
            try
            {
                // Extract the text part after plural:
                var match = System.Text.RegularExpressions.Regex.Match(
                    placeholder,
                    @":plural:(.+)}"
                );

                if (match.Success && match.Groups.Count > 1)
                {
                    string textPart = match.Groups[1].Value;
                    // Split by | to get plural variants
                    var variants = textPart.Split('|');
                    foreach (var variant in variants)
                    {
                        var innerTexts = ExtractInnerTexts(variant);
                        texts.AddRange(innerTexts);
                    }
                }
            }
            catch (System.Exception ex)
            {
                AddConfigLog($"Error extracting plural texts: {ex.Message}", LogType.Error);
            }
            return texts.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().ToList();
        }

        private List<string> ExtractInnerTexts(string text)
        {
            var texts = new List<string>();
            try
            {
                var regex = new System.Text.RegularExpressions.Regex(@"{([^{}]+)}");
                var matches = regex.Matches(text);

                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        string innerText = match.Groups[1].Value.Trim();
                        // Skip empty braces and variable references
                        if (!string.IsNullOrWhiteSpace(innerText) && !innerText.Contains(".") && !innerText.Contains(":"))
                        {
                            texts.Add(innerText);
                        }
                    }
                }

                // Also check for text outside of braces if it's not just numbers or special characters
                string outsideText = regex.Replace(text, "").Trim();
                if (!string.IsNullOrWhiteSpace(outsideText) && 
                    !System.Text.RegularExpressions.Regex.IsMatch(outsideText, @"^[\d\s\{\}\|\(\)]+$"))
                {
                    texts.Add(outsideText);
                }
            }
            catch (System.Exception ex)
            {
                AddConfigLog($"Error extracting inner texts: {ex.Message}", LogType.Error);
            }
            return texts;
        }
    }
} 