using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace Translations
{
    /// <summary>
    /// Service responsible for handling runtime translations, parameter substitution,
    /// and language switching functionality.
    /// </summary>
    public static class TranslationService
    {
        private static Dictionary<TextMeshProUGUI, string> originalTexts = new Dictionary<TextMeshProUGUI, string>();
        private static Dictionary<TextMeshProUGUI, TMP_FontAsset> originalFonts = new Dictionary<TextMeshProUGUI, TMP_FontAsset>();
        private static bool isInitialized = false;

        /// <summary>
        /// Initialize the translation service. Call this at application startup.
        /// </summary>
        public static void Initialize()
        {
            if (isInitialized) return;
            
            TranslationManager.OnLanguageChanged += Refresh;
            Refresh();
            isInitialized = true;
        }

        public static void Refresh()
        {
            StoreOriginalTexts();
            UpdateAllTexts();
        }

        public static void ChangeLanguage(string language)
        {
            TranslationManager.ChangeLanguage(language);
        }

        private static void StoreOriginalTexts()
        {
            TextMeshProUGUI[] textObjects = Object.FindObjectsOfType<TextMeshProUGUI>();

            foreach (TextMeshProUGUI textObject in textObjects)
            {
                // Skip over TMPs that are dynamic
                if (textObject.GetComponent<DynamicTMP>())
                {
                    continue;
                }
                if (!originalTexts.ContainsKey(textObject))
                {
                    originalTexts[textObject] = textObject.text;
                }

                if (!originalFonts.ContainsKey(textObject))
                {
                    originalFonts[textObject] = textObject.font;
                }
            }
        }

        private static void UpdateAllTexts()
        {
            foreach (var entry in originalTexts)
            {
                TextMeshProUGUI textObject = entry.Key;

                string originalText = entry.Value;
                textObject.text = TranslationManager.Translate(originalText);

                var tmpFontAsset = originalFonts.TryGetValue(textObject, out var textFont) ? textFont : textObject.font;
                if (TranslationManager.TranslationData.fonts.TryGetValue(tmpFontAsset, out var fontDictionary)
                    && fontDictionary.TryGetValue(TranslationManager.CurrentLanguage, out var font))
                {
                    // Change font
                    textObject.font = font;
                }
                else
                {
                    // Reset font
                    textObject.font = tmpFontAsset;
                }
            }
        }
    }
} 