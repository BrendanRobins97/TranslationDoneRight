using UnityEngine;
using UnityEditor;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace PSS
{
    public partial class TranslationsEditorWindow
    {
        private const int MAX_BATCH_SIZE = 50; // DeepL's maximum batch size
        private const int MAX_RETRIES = 3;  // Maximum number of retry attempts
        private const int INITIAL_RETRY_DELAY_MS = 1000; // Start with 1 second delay
        private const int MAX_LOG_ENTRIES = 1000; // Maximum number of log entries to keep
        
        private List<(string message, LogType type, System.DateTime timestamp)> deeplLogs = new List<(string, LogType, System.DateTime)>();
        private Vector2 deeplLogScrollPosition;
        private bool showDeeplLogs = true;
        private GUIStyle logStyle;
        private GUIStyle errorLogStyle;
        private GUIStyle warningLogStyle;
        private bool logStylesInitialized;
        private bool shouldScrollToBottom;

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

        private void AddDeepLLog(string message, LogType type = LogType.Log)
        {
            if (UnityThread.isMainThread)
            {
                deeplLogs.Add((message, type, System.DateTime.Now));
                if (deeplLogs.Count > MAX_LOG_ENTRIES)
                {
                    deeplLogs.RemoveAt(0);
                }
                shouldScrollToBottom = true;
                Repaint();
            }
            else
            {
                EditorApplication.delayCall += () =>
                {
                    deeplLogs.Add((message, type, System.DateTime.Now));
                    if (deeplLogs.Count > MAX_LOG_ENTRIES)
                    {
                        deeplLogs.RemoveAt(0);
                    }
                    shouldScrollToBottom = true;
                    Repaint();
                };
            }
        }

        private void DrawDeepLTab()
        {
            InitializeLogStyles();
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

            // Language Mapping Section
            EditorGUILayout.LabelField("Language Mappings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            if (translationData?.Metadata != null)
            {
                EditorGUILayout.HelpBox(
                    "Map your language names to DeepL language codes. These will take precedence over default mappings.", 
                    MessageType.Info
                );

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
                EditorGUILayout.HelpBox("Translation data not loaded.", MessageType.Warning);
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

            EditorGUILayout.Space(10);

            // Log Display Area
            EditorGUILayout.LabelField("Translation Logs", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            showDeeplLogs = EditorGUILayout.Foldout(showDeeplLogs, "Show Logs", true);
            if (showDeeplLogs)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Clear Logs", GUILayout.Width(100)))
                {
                    deeplLogs.Clear();
                    Repaint();
                }
                if (GUILayout.Button("Copy All", GUILayout.Width(100)))
                {
                    var logText = string.Join("\n", deeplLogs.Select(log => $"[{log.timestamp:HH:mm:ss}] {log.message}"));
                    EditorGUIUtility.systemCopyBuffer = logText;
                }
                EditorGUILayout.EndHorizontal();

                deeplLogScrollPosition = EditorGUILayout.BeginScrollView(deeplLogScrollPosition, GUILayout.Height(200));
                
                // Display logs in chronological order (oldest to newest)
                foreach (var (message, type, timestamp) in deeplLogs)
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
                    deeplLogScrollPosition.y = float.MaxValue;
                    shouldScrollToBottom = false;
                }
                
                EditorGUILayout.EndScrollView();
            }
            
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndVertical();
        }

        private async void TestDeepLConnection()
        {
            if (string.IsNullOrEmpty(deeplApiKey))
            {
                AddDeepLLog("Please enter an API key first.", LogType.Warning);
                EditorUtility.DisplayDialog("DeepL Test", "Please enter an API key first.", "OK");
                return;
            }

            try
            {
                AddDeepLLog("Testing DeepL API connection...");
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"DeepL-Auth-Key {deeplApiKey}");
                    var baseUrl = useDeepLPro ? "https://api.deepl.com/v2" : "https://api-free.deepl.com/v2";
                    var response = await client.GetAsync($"{baseUrl}/usage");
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadAsStringAsync();
                        AddDeepLLog("Connection successful! API key is valid.");
                        AddDeepLLog($"Usage info: {result}");
                        EditorUtility.DisplayDialog("DeepL Test", "Connection successful! API key is valid.", "OK");
                    }
                    else
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        AddDeepLLog($"Connection failed: {error}", LogType.Error);
                        EditorUtility.DisplayDialog("DeepL Test", "Connection failed. Please check your API key and settings.", "OK");
                    }
                }
            }
            catch (System.Exception e)
            {
                AddDeepLLog($"Error testing connection: {e.Message}", LogType.Error);
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
                                    AddDeepLLog($"Rate limit hit. Retrying in {delayMs/1000f}s (Attempt {retryCount + 1}/{MAX_RETRIES})", LogType.Warning);
                                    await Task.Delay(delayMs);
                                    delayMs *= 2; // Exponential backoff
                                    retryCount++;
                                    continue;
                                }
                            }
                            
                            AddDeepLLog($"Translation failed ({response.StatusCode}): {error}", LogType.Error);
                            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                            {
                                AddDeepLLog("Consider upgrading to DeepL Pro for higher rate limits.", LogType.Warning);
                            }
                            return null;
                        }
                    }
                }
                catch (System.Exception e)
                {
                    AddDeepLLog($"Translation error: {e.Message}", LogType.Error);
                    return null;
                }
            }

            return null;
        }

        private async Task TranslateAllLanguagesForKey(string key)
        {
            if (string.IsNullOrEmpty(deeplApiKey) || translationData == null) return;

            int keyIndex = translationData.allKeys.IndexOf(key);
            if (keyIndex == -1) return;

            AddDeepLLog($"Starting translations for '{key}'");
            
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
                AddDeepLLog($"✓ All translations completed for '{key}'");
            }
            catch (System.Exception e)
            {
                AddDeepLLog($"Error during translation: {e.Message}", LogType.Error);
            }
        }

        private async Task TranslateMissingLanguagesForKey(string key)
        {
            if (string.IsNullOrEmpty(deeplApiKey) || translationData == null) return;

            int keyIndex = translationData.allKeys.IndexOf(key);
            if (keyIndex == -1) return;

            AddDeepLLog($"Checking missing translations for '{key}'");
            
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
                    AddDeepLLog("✓ No missing translations found");
                    return;
                }

                AddDeepLLog($"Found {languagesToTranslate.Count} missing translations");

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
                AddDeepLLog($"✓ All missing translations completed for '{key}'");
            }
            catch (System.Exception e)
            {
                AddDeepLLog($"Error during translation: {e.Message}", LogType.Error);
            }
        }

        private async void TranslateSingleField(string key, string targetLanguage, int keyIndex, LanguageData languageData)
        {
            if (string.IsNullOrEmpty(deeplApiKey)) return;

            AddDeepLLog($"Translating '{key}' to {targetLanguage}...");
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
                AddDeepLLog($"✓ Translation completed for '{key}' to {targetLanguage}");
            }
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

            AddDeepLLog($"Language '{language}' not found in DeepL mappings. Please set up a custom mapping in the DeepL tab of the Translations window.", LogType.Error);
            return null; // Return null instead of a fallback to prevent API errors
        }

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