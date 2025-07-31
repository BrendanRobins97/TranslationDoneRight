using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace Translations.UI
{
    /// <summary>
    /// Component that populates a dropdown with available languages and handles language switching.
    /// Can be attached to a TMP_Dropdown or legacy Dropdown component.
    /// For native language font support in dropdown items, add LanguageDropdownItemFont component to the dropdown template.
    /// </summary>
    [AddComponentMenu("Translations/Language Dropdown")]
    public class LanguageDropdown : MonoBehaviour
    {
        [Tooltip("Optional text format for dropdown items. Use {0} as placeholder for language name.")]
        [SerializeField] private string languageFormat = "{0}";
        
        [Tooltip("Should dropdown display language names in their native form")]
        [SerializeField] private bool useNativeLanguageNames = true;
        
        // References to potential dropdown components
        private TMP_Dropdown tmpDropdown;
        private Dropdown legacyDropdown;
        
        // Flag to prevent triggering language change when we're just updating the UI
        private bool updatingDropdown = false;
        
        // Dictionary mapping dropdown indices to language codes
        private Dictionary<int, string> indexToLanguage = new Dictionary<int, string>();
        
        // Font handling for the main label
        private TMP_FontAsset originalCaptionFont;
        
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

        private void Awake()
        {
            // Get dropdown components
            tmpDropdown = GetComponent<TMP_Dropdown>();
            legacyDropdown = GetComponent<Dropdown>();

            if (tmpDropdown == null && legacyDropdown == null)
            {
                Debug.LogError("LanguageDropdown requires either a TMP_Dropdown or legacy Dropdown component");
                enabled = false;
                return;
            }

            foreach (var tmp in GetComponentsInChildren<TMP_Text>())
            {
                tmp.gameObject.AddComponent<NotTranslatedTMP>();
            }

            foreach (var text in GetComponentsInChildren<Text>())
            {
                text.gameObject.AddComponent<NotTranslatedTMP>();
            }
            
            // Store original font for the caption
            if (tmpDropdown != null && tmpDropdown.captionText != null)
            {
                originalCaptionFont = tmpDropdown.captionText.font;
            }

            // Subscribe to language changed event
            TranslationManager.OnLanguageChanged += UpdateDropdownSelection;
            TranslationManager.OnLanguageChanged += UpdateCaptionFont;
        }

        private void OnEnable()
        {
            PopulateLanguageDropdown();
            UpdateDropdownSelection();
            UpdateCaptionFont();
        }

        private void OnDestroy()
        {
            // Unsubscribe to prevent memory leaks
            TranslationManager.OnLanguageChanged -= UpdateDropdownSelection;
            TranslationManager.OnLanguageChanged -= UpdateCaptionFont;
            
            // Remove listeners
            if (tmpDropdown != null)
            {
                tmpDropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);
            }
            else if (legacyDropdown != null)
            {
                legacyDropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);
            }
        }

        /// <summary>
        /// Updates the caption font based on current language (like TranslatedTMP)
        /// </summary>
        private void UpdateCaptionFont()
        {
            if (tmpDropdown == null || tmpDropdown.captionText == null || originalCaptionFont == null)
                return;
                
            string currentLanguage = TranslationManager.CurrentLanguage;
            
            // Look up font for current language
            TMP_FontAsset fontToUse = originalCaptionFont; // Default
            
            if (TranslationManager.TranslationData.fonts.TryGetValue(originalCaptionFont, out var fontDictionary)
                && fontDictionary.TryGetValue(currentLanguage, out var newFont) && newFont != null)
            {
                fontToUse = newFont;
            }
            
            // Set the font
            if (tmpDropdown.captionText.font != fontToUse)
            {
                tmpDropdown.captionText.font = fontToUse;
            }
        }

        /// <summary>
        /// Populates the dropdown with the supported languages
        /// </summary>
        private void PopulateLanguageDropdown()
        {
            if (TranslationManager.TranslationData == null)
            {
                Debug.LogError("TranslationData is null. Cannot populate language dropdown.");
                return;
            }

            var supportedLanguages = TranslationManager.TranslationData.supportedLanguages;
            
            // Clear existing options and map
            indexToLanguage.Clear();
            
            List<TMP_Dropdown.OptionData> tmpOptions = new List<TMP_Dropdown.OptionData>();
            List<Dropdown.OptionData> legacyOptions = new List<Dropdown.OptionData>();
            
            // Remove listeners temporarily to prevent triggering while populating
            if (tmpDropdown != null)
            {
                tmpDropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);
            }
            else if (legacyDropdown != null)
            {
                legacyDropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);
            }

            // Set updating flag to prevent language change during population
            updatingDropdown = true;
            
            // Add each supported language to the dropdown
            for (int i = 0; i < supportedLanguages.Count; i++)
            {
                string languageName = supportedLanguages[i];
                indexToLanguage[i] = languageName;
                
                // Get display name for language (native or original)
                string displayName = useNativeLanguageNames && NativeLanguageNames.ContainsKey(languageName) 
                    ? NativeLanguageNames[languageName] 
                    : languageName;
                
                displayName = string.Format(languageFormat, displayName);
                
                if (tmpDropdown != null)
                {
                    tmpOptions.Add(new TMP_Dropdown.OptionData(displayName));
                }
                else if (legacyDropdown != null)
                {
                    legacyOptions.Add(new Dropdown.OptionData(displayName));
                }
            }
            
            // Add options to dropdown
            if (tmpDropdown != null)
            {
                tmpDropdown.ClearOptions();
                tmpDropdown.AddOptions(tmpOptions);
            }
            else if (legacyDropdown != null)
            {
                legacyDropdown.ClearOptions();
                legacyDropdown.AddOptions(legacyOptions);
            }

            // Re-add listener
            if (tmpDropdown != null)
            {
                tmpDropdown.onValueChanged.AddListener(OnDropdownValueChanged);
            }
            else if (legacyDropdown != null)
            {
                legacyDropdown.onValueChanged.AddListener(OnDropdownValueChanged);
            }
            
            updatingDropdown = false;
        }

        /// <summary>
        /// Updates the dropdown selection to match the current language
        /// </summary>
        private void UpdateDropdownSelection()
        {
            if (tmpDropdown == null && legacyDropdown == null)
                return;
                
            string currentLanguage = TranslationManager.CurrentLanguage;
            
            // Find index for current language
            int selectedIndex = -1;
            foreach (var kvp in indexToLanguage)
            {
                if (kvp.Value == currentLanguage)
                {
                    selectedIndex = kvp.Key;
                    break;
                }
            }
            
            if (selectedIndex >= 0)
            {
                updatingDropdown = true;
                
                if (tmpDropdown != null)
                {
                    tmpDropdown.SetValueWithoutNotify(selectedIndex);
                }
                else if (legacyDropdown != null)
                {
                    legacyDropdown.SetValueWithoutNotify(selectedIndex);
                }
                
                updatingDropdown = false;
            }
        }

        /// <summary>
        /// Called when the dropdown value changes
        /// </summary>
        private void OnDropdownValueChanged(int index)
        {
            // Ignore if we're just updating the UI
            if (updatingDropdown)
                return;
                
            // Change language if index is valid
            if (indexToLanguage.TryGetValue(index, out string language))
            {
                TranslationManager.ChangeLanguage(language);
            }
        }
    }
} 