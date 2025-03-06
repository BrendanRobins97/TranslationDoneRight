using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using TMPro;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Sirenix.OdinInspector;

namespace Translations
{
    /// <summary>
    /// Service responsible for handling runtime translations, parameter substitution,
    /// and language switching functionality.
    /// </summary>
    public class TranslationService : MonoBehaviour
    {
        private Dictionary<TextMeshProUGUI, string> originalTexts = new Dictionary<TextMeshProUGUI, string>();
        private Dictionary<TextMeshProUGUI, TMP_FontAsset> originalFonts = new Dictionary<TextMeshProUGUI, TMP_FontAsset>();

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            TranslationManager.OnLanguageChanged += Refresh;

            Refresh();
        }

        public void Refresh()
        {
            StoreOriginalTexts();
            UpdateAllTexts();
        }

        [Button]
        public void ChangeLanguage(string language)
        {
            TranslationManager.ChangeLanguage(language);
        }

        private void StoreOriginalTexts()
        {
            TextMeshProUGUI[] textObjects = FindObjectsOfType<TextMeshProUGUI>();

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

        private void UpdateAllTexts()
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