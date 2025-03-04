using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PSS
{
    public static class TranslationManager
    {
        private static TranslationData translationData;
        private static Dictionary<string, string> translations = new Dictionary<string, string>();
        private static string currentLanguage = TranslationData.defaultLanguage;
        private static AssetReference currentLanguageAssetRef;

        public static event Action OnLanguageChanged;

        public static TranslationData TranslationData
        {
            get
            {
                if (translationData == null)
                {
                    translationData = TranslationDataProvider.Data;
                }
                return translationData;
            }
        }

        public static string CurrentLanguage
        {
            get
            {
                return currentLanguage;
            }
        }

        static TranslationManager()
        {
            LoadLanguage();
            SetLanguage();
        }

        public static void ChangeLanguage(string language)
        {
            if (!TranslationData.supportedLanguages.Contains(language))
            {
                Debug.LogError($"Unsupported language: {language}");
                return;
            }

            if (currentLanguage != language)
            {
                UnloadCurrentLanguage();
                currentLanguage = language;
                PlayerPrefs.SetString("Language", currentLanguage);
                PlayerPrefs.Save();
                SetLanguage();
            }
        }

        private static void LoadLanguage()
        {
            currentLanguage = PlayerPrefs.GetString("Language", TranslationData.defaultLanguage);
        }

        private static void SetLanguage()
        {
            LoadLanguage(currentLanguage, () =>
            {
                OnLanguageChanged?.Invoke();
            });
        }


        public static string Translate(string originalText)
        {
            // First check if this text has a canonical version
            string textToTranslate = TranslationData.GetCanonicalText(originalText);

            if (currentLanguage == TranslationData.defaultLanguage)
                return textToTranslate;

            if (translations.TryGetValue(textToTranslate, out var translatedText))
            {
                return translatedText;
            }

            // If no translation found, use the canonical text
            return textToTranslate;
        }
        private static string ProcessTranslationKeyReferences(string text, HashSet<string> processedKeys, int depth = 0)
        {
            if (depth > 10)
            {
                Debug.LogError("Translation key reference depth exceeded (possible circular reference)");
                return text;
            }

            var keyRefRegex = new Regex(@"\{@([^}]+)\}");
            var matches = keyRefRegex.Matches(text);
            
            if (matches.Count == 0)
                return text;

            string result = text;
            foreach (Match match in matches)
            {
                string key = match.Groups[1].Value;
                
                // Prevent circular references
                if (processedKeys.Contains(key))
                {
                    Debug.LogError($"Circular reference detected in translation key: {key}");
                    continue;
                }

                processedKeys.Add(key);
                
                // Get the translation for the referenced key
                string translatedValue = Translate(key);
                
                // Process any nested key references in the translated value
                translatedValue = ProcessTranslationKeyReferences(translatedValue, processedKeys, depth + 1);
                
                result = result.Replace(match.Value, translatedValue);
                
                processedKeys.Remove(key);
            }

            return result;
        }

        public static TMP_FontAsset GetFontForText(TMP_FontAsset defaultFont)
        {
            if (TranslationData.fonts.TryGetValue(defaultFont, out Dictionary<string, TMP_FontAsset> fontDictionary)
                && fontDictionary.TryGetValue(currentLanguage, out TMP_FontAsset font))
            {
                return font;
            }
            return defaultFont;
        }

        private static void UnloadCurrentLanguage()
        {
            if (currentLanguageAssetRef != null)
            {
                currentLanguageAssetRef.ReleaseAsset();
                currentLanguageAssetRef = null;
                translations.Clear();
            }
        }

        private static void LoadLanguage(string language, Action onComplete)
        {
            if (language == TranslationData.defaultLanguage)
            {
                onComplete?.Invoke();
                return;
            }

            int languageIndex = TranslationData.supportedLanguages.IndexOf(language) - 1;
            if (languageIndex < 0)
            {
                Debug.LogError($"Invalid language index for {language}");
                return;
            }

            var languageData = TranslationData.languageDataDictionary[languageIndex];
            LoadLanguageData(languageData, data =>
            {
                if (data != null)
                {
                    onComplete?.Invoke();
                }
            });
        }

        private static void LoadLanguageData(AssetReference assetRef, Action<LanguageData> onComplete)
        {
            translations.Clear();
            assetRef.LoadAssetAsync<LanguageData>().Completed += operation =>
            {
                if (operation.Status == AsyncOperationStatus.Succeeded)
                {
                    var languageData = operation.Result;
                    
                    for (var i = 0; i < languageData.allText.Count && i < TranslationData.allKeys.Count; i++)
                    {
                        var text = languageData.allText[i];
                        var key = TranslationData.allKeys[i];
                        translations[key] = text;
                    }

                    currentLanguageAssetRef = assetRef;
                    onComplete?.Invoke(languageData);
                }
                else
                {
                    Debug.LogError($"Failed to load language data for {currentLanguage}");
                    onComplete?.Invoke(null);
                }
            };
        }
    }
}
