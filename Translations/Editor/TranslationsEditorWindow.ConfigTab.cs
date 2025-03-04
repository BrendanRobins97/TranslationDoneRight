using UnityEngine;
using UnityEditor;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;

namespace PSS
{
    public partial class TranslationsEditorWindow
    {
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
            EditorGUILayout.LabelField("DeepL Translation Settings", headerStyle);
            EditorGUILayout.Space(8);
            
            // API Settings
            EditorGUILayout.LabelField("API Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);
            
            deeplApiKey = EditorGUILayout.PasswordField("DeepL API Key:", deeplApiKey);
            useDeepLPro = EditorGUILayout.Toggle("Use DeepL Pro", useDeepLPro);
            
            if (string.IsNullOrEmpty(deeplApiKey))
            {
                GUIStyle warningStyle = new GUIStyle(EditorStyles.wordWrappedLabel);
                warningStyle.normal.textColor = new Color(0.9f, 0.6f, 0.1f);
                EditorGUILayout.LabelField("Please enter your DeepL API key to enable automatic translation.", 
                    warningStyle);
            }
            
            EditorGUILayout.Space(12);

            // Language Mapping Section
            EditorGUILayout.LabelField("Language Mappings", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);
            
            if (translationData?.Metadata != null)
            {
                EditorGUILayout.LabelField(
                    "Map your language names to DeepL language codes. These will take precedence over default mappings.", 
                    EditorStyles.wordWrappedLabel);
                EditorGUILayout.Space(5);

                EditorGUI.BeginChangeCheck();
                
                // Show existing mappings
                List<string> keysToRemove = new List<string>();
                foreach (var mapping in translationData.Metadata.CustomLanguageMappings.ToList())
                {
                    EditorGUILayout.BeginHorizontal();
                    
                    // Language dropdown
                    int langIndex = translationData.supportedLanguages.IndexOf(mapping.Key);
                    int newLangIndex = EditorGUILayout.Popup(
                        "Language:", 
                        langIndex, 
                        translationData.supportedLanguages.ToArray()
                    );

                    // DeepL code dropdown
                    string[] deeplCodes = new string[] {
                        "BG", "CS", "DA", "DE", "EL", "EN", "ES", "ET", "FI", "FR", 
                        "HU", "ID", "IT", "JA", "KO", "LT", "LV", "NB", "NL", "PL", 
                        "PT", "PT-BR", "RO", "RU", "SK", "SL", "SV", "TR", "UK", "ZH"
                    };
                    
                    int codeIndex = System.Array.IndexOf(deeplCodes, mapping.Value);
                    int newCodeIndex = EditorGUILayout.Popup(
                        "DeepL Code:", 
                        codeIndex, 
                        deeplCodes
                    );

                    if (GUILayout.Button("Remove", GUILayout.Width(60)))
                    {
                        keysToRemove.Add(mapping.Key);
                    }

                    EditorGUILayout.EndHorizontal();

                    // Update mapping if changed
                    if (newLangIndex != langIndex || newCodeIndex != codeIndex)
                    {
                        if (newLangIndex != langIndex)
                        {
                            keysToRemove.Add(mapping.Key);
                            string newLang = translationData.supportedLanguages[newLangIndex];
                            translationData.Metadata.CustomLanguageMappings[newLang] = mapping.Value;
                        }
                        if (newCodeIndex != codeIndex)
                        {
                            translationData.Metadata.CustomLanguageMappings[mapping.Key] = deeplCodes[newCodeIndex];
                        }
                        EditorUtility.SetDirty(translationData);
                    }
                }

                // Remove any mappings marked for removal
                foreach (var key in keysToRemove)
                {
                    translationData.Metadata.CustomLanguageMappings.Remove(key);
                    EditorUtility.SetDirty(translationData);
                }

                // Add new mapping button
                EditorGUILayout.Space(5);
                if (GUILayout.Button("Add New Mapping"))
                {
                    // Find first unmapped language
                    string newLang = translationData.supportedLanguages
                        .FirstOrDefault(l => !translationData.Metadata.CustomLanguageMappings.ContainsKey(l));
                    
                    if (!string.IsNullOrEmpty(newLang))
                    {
                        translationData.Metadata.CustomLanguageMappings[newLang] = "EN"; // Default to EN
                        EditorUtility.SetDirty(translationData);
                    }
                }

                if (EditorGUI.EndChangeCheck())
                {
                    AssetDatabase.SaveAssets();
                }
            }
            else
            {
                GUIStyle errorStyle = new GUIStyle(EditorStyles.wordWrappedLabel);
                errorStyle.normal.textColor = new Color(0.9f, 0.3f, 0.3f);
                EditorGUILayout.LabelField("Translation data not loaded.", errorStyle);
            }
            
            EditorGUILayout.Space(12);

            // Translation Settings
            EditorGUILayout.LabelField("Translation Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);
            
            includeContextInTranslation = EditorGUILayout.Toggle("Include Context", includeContextInTranslation);
            if (includeContextInTranslation)
            {
                EditorGUI.indentLevel++;
                GUIStyle infoStyle = new GUIStyle(EditorStyles.wordWrappedLabel);
                infoStyle.normal.textColor = new Color(0.3f, 0.65f, 0.9f);
                EditorGUILayout.LabelField("Context will be sent as additional information to DeepL to improve translation quality.", 
                    infoStyle);
                EditorGUI.indentLevel--;
            }

            preserveFormatting = EditorGUILayout.Toggle("Preserve Formatting", preserveFormatting);
            formalityPreference = EditorGUILayout.Toggle("Formal Language", formalityPreference);
            
            EditorGUILayout.Space(12);

            // Test Connection
            if (GUILayout.Button("Test DeepL Connection", GUILayout.Height(24)))
            {
                TestDeepLConnection();
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
            if (translationData?.Metadata?.CustomLanguageMappings != null &&
                translationData.Metadata.CustomLanguageMappings.TryGetValue(language, out string customCode))
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

        private async Task<List<string>> TranslateBatch(List<string> texts, string targetLanguage, List<string> contexts = null)
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

        #endregion

        #region Log Display

        private void DrawLogSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Configuration Logs", EditorStyles.boldLabel);
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

            int keyIndex = translationData.allKeys.IndexOf(key);
            if (keyIndex == -1) return;

            AddConfigLog($"Starting translations for '{key}'");
            
            try
            {
                var languages = translationData.supportedLanguages.Skip(1).ToList(); // Skip default language
                var tasks = new List<Task>();
                int totalLanguages = languages.Count;

                // Pre-load all language data on the main thread
                var languageDataDict = new Dictionary<string, LanguageData>();
                foreach (var language in languages)
                {
                    int langIndex = translationData.supportedLanguages.IndexOf(language) - 1;
                    if (langIndex >= 0 && langIndex < translationData.languageDataDictionary.Length)
                    {
                        var assetRef = translationData.languageDataDictionary[langIndex];
                        string assetPath = AssetDatabase.GUIDToAssetPath(assetRef.AssetGUID);
                        var languageData = AssetDatabase.LoadAssetAtPath<LanguageData>(assetPath);
                        if (languageData != null)
                        {
                            languageDataDict[language] = languageData;
                        }
                    }
                }

                // Group languages by their DeepL codes to optimize batch translations
                var languageGroups = languages
                    .GroupBy(l => GetDeepLLanguageCode(l))
                    .ToDictionary(g => g.Key, g => g.ToList());

                string context = includeContextInTranslation ? translationData.Metadata.GetTranslationContext(key) : "";

                foreach (var group in languageGroups)
                {
                    var task = Task.Run(async () =>
                    {
                        string targetLangCode = group.Key;
                        var targetLanguages = group.Value;
                        
                        var translations = await TranslateBatch(
                            new List<string> { key }, 
                            targetLanguages[0], // Use first language as they share the same DeepL code
                            new List<string> { context }
                        );

                        if (translations != null && translations.Count > 0)
                        {
                            string translation = translations[0];
                            // Apply the same translation to all languages in this group
                            foreach (var language in targetLanguages)
                            {
                                if (languageDataDict.TryGetValue(language, out var languageData))
                                {
                                    // Queue the UI updates for the main thread
                                    EditorApplication.delayCall += () =>
                                    {
                                        Undo.RecordObject(languageData, "Auto Translate");
                                        languageData.allText[keyIndex] = translation;
                                        EditorUtility.SetDirty(languageData);
                                    };
                                }
                            }
                        }
                    });
                    tasks.Add(task);
                }

                await Task.WhenAll(tasks);
                EditorApplication.delayCall += () => AssetDatabase.SaveAssets();
                AddConfigLog($"✓ All translations completed for '{key}'");
            }
            catch (System.Exception e)
            {
                AddConfigLog($"Error during translation: {e.Message}", LogType.Error);
            }
        }

        public async Task TranslateMissingLanguagesForKey(string key)
        {
            if (string.IsNullOrEmpty(deeplApiKey) || translationData == null) return;

            int keyIndex = translationData.allKeys.IndexOf(key);
            if (keyIndex == -1) return;

            AddConfigLog($"Checking missing translations for '{key}'");
            
            try
            {
                var languages = translationData.supportedLanguages.Skip(1).ToList(); // Skip default language
                var languagesToTranslate = new List<(string language, LanguageData data)>();

                // Pre-load all language data and check for missing translations on the main thread
                foreach (var language in languages)
                {
                    int langIndex = translationData.supportedLanguages.IndexOf(language) - 1;
                    if (langIndex >= 0 && langIndex < translationData.languageDataDictionary.Length)
                    {
                        var assetRef = translationData.languageDataDictionary[langIndex];
                        string assetPath = AssetDatabase.GUIDToAssetPath(assetRef.AssetGUID);
                        LanguageData languageData = AssetDatabase.LoadAssetAtPath<LanguageData>(assetPath);

                        if (languageData != null && 
                            string.IsNullOrWhiteSpace(languageData.allText[keyIndex]))
                        {
                            languagesToTranslate.Add((language, languageData));
                        }
                    }
                }

                if (languagesToTranslate.Count == 0)
                {
                    AddConfigLog("✓ No missing translations found");
                    return;
                }

                AddConfigLog($"Found {languagesToTranslate.Count} missing translations");

                // Group languages by their DeepL codes to optimize batch translations
                var languageGroups = languagesToTranslate
                    .GroupBy(l => GetDeepLLanguageCode(l.language))
                    .ToDictionary(g => g.Key, g => g.ToList());

                var tasks = new List<Task>();
                int totalLanguages = languagesToTranslate.Count;
                string context = includeContextInTranslation ? translationData.Metadata.GetTranslationContext(key) : "";

                foreach (var group in languageGroups)
                {
                    var task = Task.Run(async () =>
                    {
                        string targetLangCode = group.Key;
                        var targetLanguages = group.Value;
                        
                        var translations = await TranslateBatch(
                            new List<string> { key }, 
                            targetLanguages[0].language, // Use first language as they share the same DeepL code
                            new List<string> { context }
                        );

                        if (translations != null && translations.Count > 0)
                        {
                            string translation = translations[0];
                            // Apply the same translation to all languages in this group
                            foreach (var (language, languageData) in targetLanguages)
                            {
                                // Queue the UI updates for the main thread
                                EditorApplication.delayCall += () =>
                                {
                                    Undo.RecordObject(languageData, "Auto Translate");
                                    languageData.allText[keyIndex] = translation;
                                    EditorUtility.SetDirty(languageData);
                                };
                            }
                        }
                    });
                    tasks.Add(task);
                }

                await Task.WhenAll(tasks);
                EditorApplication.delayCall += () => AssetDatabase.SaveAssets();
                AddConfigLog($"✓ All missing translations completed for '{key}'");
            }
            catch (System.Exception e)
            {
                AddConfigLog($"Error during translation: {e.Message}", LogType.Error);
            }
        }

        public async void TranslateSingleField(string key, string targetLanguage, int keyIndex, LanguageData languageData)
        {
            if (string.IsNullOrEmpty(deeplApiKey)) return;

            AddConfigLog($"Translating '{key}' to {targetLanguage}...");
            string context = includeContextInTranslation ? translationData.Metadata.GetTranslationContext(key) : "";

            var result = await TranslateBatch(
                new List<string> { key },
                targetLanguage,
                new List<string> { context }
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
    }
} 