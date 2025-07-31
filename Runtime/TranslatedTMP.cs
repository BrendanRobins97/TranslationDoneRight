using TMPro;
using UnityEngine;
using System.Text.RegularExpressions;
using System;
using System.Text;
using System.Collections.Generic;

namespace Translations
{
    public static class TranslatedTMPExtensions
    {
        // Shared object pool between all TMPs for argument translation
        private static readonly Dictionary<int, object[]> argsPool = new Dictionary<int, object[]>();
        
        public static void SetTextTranslated(this TMP_Text tmpText, string key)
        {
            var translatedTMP = tmpText.GetComponent<TranslatedTMP>();
            if (translatedTMP == null)
            {
                translatedTMP = tmpText.gameObject.AddComponent<TranslatedTMP>();
            }
            translatedTMP.SetText(key);
        }

        public static void SetTextTranslated(this TMP_Text tmpText, string format, params object[] args)
        {
            var translatedTMP = tmpText.GetComponent<TranslatedTMP>();
            if (translatedTMP == null)
            {
                translatedTMP = tmpText.gameObject.AddComponent<TranslatedTMP>();   
            }
            
            // Get from pool or create new
            int length = args?.Length ?? 0;
            if (!argsPool.TryGetValue(length, out object[] pooledArgs))
            {
                pooledArgs = new object[length];
                argsPool[length] = pooledArgs;
            }
            
            // Copy args to pooled array
            if (length > 0)
            {
                Array.Copy(args, pooledArgs, length);
            }
            
            translatedTMP.SetText(format, pooledArgs);
        }
    }

    [RequireComponent(typeof(TextMeshProUGUI))]
    public class TranslatedTMP : MonoBehaviour
    {
        private TextMeshProUGUI tmpText;
        [SerializeField, HideInInspector] private string currentText;
        [SerializeField, HideInInspector] private string formatPattern;
        private object[] formatArgs;
        private TMP_FontAsset originalFont;
        private bool isInitialized = false;
        private readonly StringBuilder stringBuilder = new StringBuilder(256);
        private string lastTranslatedText;
        private string lastLanguage;
        private bool isSubscribed = false;
        
        // Static font cache shared between all TMPs
        private static readonly Dictionary<int, Dictionary<string, TMP_FontAsset>> fontCache = 
            new Dictionary<int, Dictionary<string, TMP_FontAsset>>();

        private void Awake()
        {
            tmpText = GetComponent<TextMeshProUGUI>();
            Initialize();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }
        
        private void SubscribeToEvents()
        {
            if (!isSubscribed)
            {
                TranslationManager.OnLanguageChanged += UpdateTranslation;
                isSubscribed = true;
            }
        }
        
        private void UnsubscribeFromEvents()
        {
            if (isSubscribed)
            {
                TranslationManager.OnLanguageChanged -= UpdateTranslation;
                isSubscribed = false;
            }
        }

        private void Initialize()
        {
            if (isInitialized) return;
            
            // Store the initial text and font
            currentText = tmpText.text;
            originalFont = tmpText.font;
            formatPattern = "{0}";
            formatArgs = Array.Empty<object>();
            
            isInitialized = true;
            SubscribeToEvents();

            if (TranslationManager.HasLanguageLoaded)
            {
                UpdateTranslation();
            } else {
                tmpText.text = "...";
            }
        }

        private void UpdateTranslation()
        {
            if (!isInitialized) Initialize();

            // Skip if language hasn't changed and we have a cached translation
            string currentLanguage = TranslationManager.CurrentLanguage;

            if (lastLanguage == currentLanguage && lastTranslatedText != null)
            {
                return;
            }

            // Update text translation
            if (formatArgs == null || formatArgs.Length == 0)
            {
                // Simple single word translation
                lastTranslatedText = TranslationManager.Translate(currentText);
                tmpText.text = lastTranslatedText;
            }
            else
            {
                // Translate the format string if it's a key
                string translatedFormat = TranslationManager.Translate(formatPattern);
                
                // Reuse the same array for translated args to reduce allocations
                object[] translatedArgs = new object[formatArgs.Length];
                
                // Translate any args that are strings (potential translation keys)
                for (int i = 0; i < formatArgs.Length; i++)
                {
                    if (formatArgs[i] is string strArg)
                    {
                        translatedArgs[i] = TranslationManager.Translate(strArg);
                    }
                    else
                    {
                        translatedArgs[i] = formatArgs[i];
                    }
                }
                
                // Use StringBuilder for better performance
                stringBuilder.Clear();
                stringBuilder.AppendFormat(translatedFormat, translatedArgs);
                lastTranslatedText = stringBuilder.ToString();
                tmpText.SetText(lastTranslatedText);
            }

            // Update font based on current language using cached lookups
            UpdateFont(currentLanguage);

            lastLanguage = currentLanguage;
        }
        
        private void UpdateFont(string language)
        {
            // Try to get from cache first
            int fontInstanceID = originalFont.GetInstanceID();
            
            // Check if we have this font in cache
            if (!fontCache.TryGetValue(fontInstanceID, out var languageCache))
            {
                languageCache = new Dictionary<string, TMP_FontAsset>();
                fontCache[fontInstanceID] = languageCache;
            }
            
            // Check if we have this language for this font in cache
            if (!languageCache.TryGetValue(language, out var font))
            {
                // Not in cache, look it up in TranslationData
                if (TranslationManager.TranslationData.fonts.TryGetValue(originalFont, out var fontDictionary)
                    && fontDictionary.TryGetValue(language, out var newFont))
                {
                    font = newFont;
                }
                else
                {
                    font = originalFont;
                }
                
                // Cache the result for future lookups
                languageCache[language] = font;
            }
            
            // Set the font
            if (tmpText.font != font)
            {
                tmpText.font = font;
            }
        }

        // Simple key only - just shows translated text
        public void SetText(string key)
        {
            if (key == currentText && formatArgs.Length == 0)
            {
                return; // No change needed
            }
            
            currentText = key;
            formatPattern = "{0}";
            formatArgs = Array.Empty<object>();
            lastTranslatedText = null; // Clear cache to force update
            UpdateTranslation();
        }

        // Exactly like string.Format - format string can be a key, and any string args can be keys
        public void SetText(string format, params object[] args)
        {
            if (format == formatPattern && ArgsEqual(formatArgs, args))
            {
                return; // No change needed
            }
            
            formatPattern = format;
            formatArgs = args ?? Array.Empty<object>();
            lastTranslatedText = null; // Clear cache to force update
            UpdateTranslation();
        }
        
        // Helper to compare argument arrays
        private bool ArgsEqual(object[] a, object[] b)
        {
            if (a == null || b == null)
            {
                return a == b;
            }
            
            if (a.Length != b.Length)
            {
                return false;
            }
            
            for (int i = 0; i < a.Length; i++)
            {
                if (!Equals(a[i], b[i]))
                {
                    return false;
                }
            }
            
            return true;
        }
    }
} 