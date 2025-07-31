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

        public static bool IsDefaultLanguage
        {
            get
            {
                return currentLanguage == TranslationData.defaultLanguage;
            }
        }

        public static bool HasLanguageLoaded => IsDefaultLanguage || currentLanguageAssetRef != null;

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
            if (translations.TryGetValue(lookupKey, out var translatedText) && !string.IsNullOrEmpty(translatedText))
            {
                return translatedText;
            }

            // Handle missing or empty translation based on the configured behavior
            return HandleMissingTranslation(canonicalText);
        }

        /// <summary>
        /// Checks if a translation exists for the given text
        /// </summary>
        /// <param name="originalText">The text to check for translation</param>
        /// <returns>True if a translation exists, false otherwise</returns>
        public static bool HasTranslation(string originalText)
        {
            if (string.IsNullOrEmpty(originalText))
                return false;
                
            // First check if this text has a canonical version
            string canonicalText = TranslationData.GetCanonicalText(originalText);
            
            // Use the full text (with disambiguation) for lookup in the translation dictionary
            string lookupKey = canonicalText;
            
            // Check if translation exists using the full key (with disambiguation)
            return TranslationData.allKeys.Contains(lookupKey);
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

        /// <summary>
        /// Handles missing translations based on the configured behavior
        /// </summary>
        /// <param name="canonicalText">The canonical text that was requested</param>
        /// <returns>The appropriate fallback text based on the missing text behavior setting</returns>
        private static string HandleMissingTranslation(string canonicalText)
        {
            switch (TranslationData.missingTextBehavior)
            {
                case MissingTextBehavior.ReturnBlank:
                    return string.Empty;
                    
                case MissingTextBehavior.ReturnMissingMessage:
                    // Return the canonical text but with disambiguation removed for the display
                    int disambiguationIndex = canonicalText.IndexOf('|');
                    string displayText = disambiguationIndex > 0 && disambiguationIndex < canonicalText.Length - 1
                        ? canonicalText.Substring(0, disambiguationIndex)
                        : canonicalText;
                    return $"MISSING: {displayText}";
                    
                case MissingTextBehavior.ReturnNativeLanguage:
                default:
                    // Return the canonical text but with disambiguation removed
                    int nativeDisambiguationIndex = canonicalText.IndexOf('|');
                    if (nativeDisambiguationIndex > 0 && nativeDisambiguationIndex < canonicalText.Length - 1)
                    {
                        return canonicalText.Substring(0, nativeDisambiguationIndex);
                    }
                    // No disambiguation, return as is
                    return canonicalText;
            }
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