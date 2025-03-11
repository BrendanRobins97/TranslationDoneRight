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

namespace Translations
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
            if (string.IsNullOrEmpty(originalText))
                return originalText;
                
            // First check if this text has a canonical version
            string canonicalText = TranslationData.GetCanonicalText(originalText);
            
            // Use the full text (with disambiguation) for lookup in the translation dictionary
            string lookupKey = canonicalText;
            
            // If we're already in the default language, strip the disambiguation part
            if (currentLanguage == TranslationData.defaultLanguage)
            {
                // Parse and remove the disambiguation suffix if present
                int pipeIndex = canonicalText.IndexOf('|');
                if (pipeIndex > 0 && pipeIndex < canonicalText.Length - 1)
                {
                    // Return only the base word without the disambiguation
                    return canonicalText.Substring(0, pipeIndex);
                }
                
                // No disambiguation, return as is
                return canonicalText;
            }

            // Look up the translation using the full key (with disambiguation)
            if (translations.TryGetValue(lookupKey, out var translatedText))
            {
                return translatedText;
            }

            // If no translation found, use the canonical text but with disambiguation removed
            int disambiguationIndex = canonicalText.IndexOf('|');
            if (disambiguationIndex > 0 && disambiguationIndex < canonicalText.Length - 1)
            {
                return canonicalText.Substring(0, disambiguationIndex);
            }
            
            // No disambiguation, return as is
            return canonicalText;
        }

        /// <summary>
        /// Translates a smart string with placeholders and replaces the placeholders with provided values
        /// </summary>
        /// <param name="smartText">The smart string with placeholders like {var:format:options}</param>
        /// <param name="args">Dictionary of arguments to replace placeholders</param>
        /// <returns>Translated and formatted text</returns>
        public static string TranslateSmart(string smartText, IDictionary<string, object> args = null)
        {
            if (string.IsNullOrEmpty(smartText))
                return smartText;

            // Extract placeholders to ensure they don't get translated
            var (tokenizedText, placeholders) = SmartString.ExtractPlaceholders(smartText);
            
            // Translate the tokenized text
            string translatedTokenized = Translate(tokenizedText);
            
            // Restore placeholders in the translated text
            string translatedWithPlaceholders = SmartString.RestorePlaceholders(translatedTokenized, placeholders);
            
            // Format the translated text with the provided arguments
            if (args != null && args.Count > 0)
            {
                return SmartString.Format(translatedWithPlaceholders, args);
            }
            
            return translatedWithPlaceholders;
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
            if (TranslationData.fonts.TryGetValue(defaultFont, out SerializableDictionary<string, TMP_FontAsset> fontDictionary)
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

        /// <summary>
        /// Extension method to easily format a string for a specific context
        /// </summary>
        /// <param name="text">The base text</param>
        /// <param name="context">The disambiguation context (e.g., "verb", "noun", or a numeric identifier like "1", "2", etc.)</param>
        /// <returns>A formatted text with disambiguation markers</returns>
        public static string ForContext(this string text, string context)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(context))
                return text;
                
            return $"{text}|{context}";
        }
        
        /// <summary>
        /// Formats a string for disambiguation with a specific meaning context
        /// </summary>
        /// <param name="text">The base text</param>
        /// <param name="context">The disambiguation context (e.g., "verb", "noun", or a numeric identifier like "1", "2", etc.)</param>
        /// <returns>A formatted text with disambiguation markers</returns>
        public static string WithContext(string text, string context)
        {
            return ForContext(text, context);
        }
        
        /// <summary>
        /// Gets the base text from a potentially disambiguated term
        /// </summary>
        /// <param name="text">The possibly disambiguated text</param>
        /// <returns>The base text without disambiguation markers</returns>
        public static string GetBaseText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
                
            int pipeIndex = text.IndexOf('|');
            if (pipeIndex > 0)
                return text.Substring(0, pipeIndex);
                
            return text;
        }
        
        /// <summary>
        /// Gets the context from a disambiguated term
        /// </summary>
        /// <param name="text">The possibly disambiguated text</param>
        /// <returns>The context, or null if no context is present</returns>
        public static string GetContext(string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;
                
            int pipeIndex = text.IndexOf('|');
            if (pipeIndex > 0 && pipeIndex < text.Length - 1)
                return text.Substring(pipeIndex + 1);
                
            return null;
        }
    }
}