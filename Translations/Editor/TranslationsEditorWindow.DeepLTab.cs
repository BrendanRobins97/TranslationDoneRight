using UnityEngine;
using UnityEditor;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace PSS
{
    public partial class TranslationsEditorWindow
    {
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