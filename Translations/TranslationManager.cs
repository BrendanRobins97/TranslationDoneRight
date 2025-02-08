using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;
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
        private static string currentLanguage = "English";
        private static AssetReference currentLanguageAssetRef;

        public static event Action OnLanguageChanged;

        private static TranslationData TranslationData
        {
            get
            {
                if (translationData == null)
                {
                    translationData = Resources.Load<TranslationData>("TranslationData");

#if UNITY_EDITOR
                    if (translationData == null)
                    {
                        // Create the TranslationData asset if it doesn't exist
                        translationData = ScriptableObject.CreateInstance<TranslationData>();
                        
                        // Ensure the Resources folder exists
                        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                        {
                            AssetDatabase.CreateFolder("Assets", "Resources");
                        }
                        
                        AssetDatabase.CreateAsset(translationData, "Assets/Resources/TranslationData.asset");
                        AssetDatabase.SaveAssets();
                        Debug.Log("Created new TranslationData asset in Resources folder");
                    }
#endif
                }
                return translationData;
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
            currentLanguage = PlayerPrefs.GetString("Language", "English");
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
            if (currentLanguage == "English") return originalText;
            return translations.TryGetValue(originalText, out string translation) ? translation : originalText;
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
            if (language == "English")
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
                    for (var i = 0; i < operation.Result.allText.Count; i++)
                    {
                        var text = operation.Result.allText[i];
                        translations[TranslationData.allKeys[i]] = text;
                    }

                    currentLanguageAssetRef = assetRef;
                    onComplete?.Invoke(operation.Result);
                }
                else
                {
                    onComplete?.Invoke(null);
                }
            };
        }
    }
}
