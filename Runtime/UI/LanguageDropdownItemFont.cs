using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace Translations.UI
{
    /// <summary>
    /// Component that automatically sets the appropriate font for dropdown items based on their text content.
    /// Add this to the dropdown template's item to handle native language font switching.
    /// </summary>
    [AddComponentMenu("Translations/Language Dropdown Item Font")]
    public class LanguageDropdownItemFont : MonoBehaviour
    {
        // Dictionary of native language names (if available)
        private static readonly Dictionary<string, string> NativeLanguageNames = new Dictionary<string, string>
        {
            { "English", "English" },
            { "French", "Français" },
            { "Italian", "Italiano" },
            { "German", "Deutsch" },
            { "Danish", "Dansk" },
            { "Dutch", "Nederlands" },
            { "Japanese", "日本語" },
            { "Korean", "한국어" },
            { "Portuguese", "Português" },
            { "Portuguese (Brazil)", "Português (Brasil)" },
            { "Russian", "Русский" },
            { "Chinese (Simplified)", "中文(简体)" },
            { "Spanish", "Español" },
            { "Swedish", "Svenska" },
            { "Ukrainian", "Українська" },
            { "Chinese (Traditional)", "中文(繁體)" },
        };

        private void OnEnable()
        {
            UpdateFont();
        }

        /// <summary>
        /// Updates the font based on the text content
        /// </summary>
        private void UpdateFont()
        {
            // Try to find TMP_Text first
            TMP_Text tmpText = GetComponent<TMP_Text>();
            if (tmpText != null)
            {
                UpdateTextFont(tmpText);
                return;
            }

            // Try to find TMP_Text in children
            tmpText = GetComponentInChildren<TMP_Text>();
            if (tmpText != null)
            {
                UpdateTextFont(tmpText);
                return;
            }

            // Try to find legacy Text component
            Text legacyText = GetComponent<Text>();
            if (legacyText != null)
            {
                UpdateLegacyTextFont(legacyText);
                return;
            }

            // Try to find legacy Text in children
            legacyText = GetComponentInChildren<Text>();
            if (legacyText != null)
            {
                UpdateLegacyTextFont(legacyText);
            }
        }

        /// <summary>
        /// Updates font for TMP_Text component
        /// </summary>
        private void UpdateTextFont(TMP_Text textComponent)
        {
            if (textComponent == null || textComponent.font == null)
                return;

            // Find which language this text represents
            string textContent = textComponent.text;
            string matchedLanguage = FindLanguageForText(textContent);

            if (string.IsNullOrEmpty(matchedLanguage))
                return;

            TMP_FontAsset originalFont = textComponent.font;

            // Look up in TranslationData font dictionary
            if (TranslationManager.TranslationData != null &&
                TranslationManager.TranslationData.fonts.TryGetValue(originalFont, out var fontDictionary) &&
                fontDictionary.TryGetValue(matchedLanguage, out var newFont) && newFont != null)
            {
                textComponent.font = newFont;
            }
        }

        /// <summary>
        /// Updates font for legacy Text component (basic implementation)
        /// </summary>
        private void UpdateLegacyTextFont(Text textComponent)
        {
            // For legacy Text components, we can't easily change fonts like TMP
            // This is a placeholder for basic legacy support
            // In practice, TMP_Dropdown should be used for better font support
        }

        /// <summary>
        /// Finds which language a text content represents
        /// </summary>
        private string FindLanguageForText(string textContent)
        {
            foreach (var kvp in NativeLanguageNames)
            {
                if (textContent.Contains(kvp.Value))
                {
                    return kvp.Key;
                }
            }
            return null;
        }
    }
} 