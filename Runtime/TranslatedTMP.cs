using TMPro;
using UnityEngine;
using System.Text.RegularExpressions;
using System;

namespace Translations
{
    public static class TranslatedTMPExtensions
    {
        public static void SetTextTranslated(this TMP_Text tmpText, string key)
        {
            if (tmpText.GetComponent<TranslatedTMP>() == null)
            {
                tmpText.gameObject.AddComponent<TranslatedTMP>();
            }
            tmpText.GetComponent<TranslatedTMP>().SetText(key);
        }

        public static void SetTextTranslated(this TMP_Text tmpText, string format, params object[] args)
        {
            if (tmpText.GetComponent<TranslatedTMP>() == null)
            {
                tmpText.gameObject.AddComponent<TranslatedTMP>();   
            }
            tmpText.GetComponent<TranslatedTMP>().SetText(format, args);
        }
    }

    [RequireComponent(typeof(TextMeshProUGUI))]
    public class TranslatedTMP : MonoBehaviour
    {
        private TextMeshProUGUI tmpText;
        private string currentText;
        private string formatPattern;
        private object[] formatArgs;
        private bool isInitialized = false;

        private void Awake()
        {
            tmpText = GetComponent<TextMeshProUGUI>();
            Initialize();
        }

        private void OnEnable()
        {
            TranslationManager.OnLanguageChanged += UpdateTranslation;
        }

        private void OnDisable()
        {
            TranslationManager.OnLanguageChanged -= UpdateTranslation;
        }

        private void Initialize()
        {
            if (isInitialized) return;
            
            // Store the initial text
            currentText = tmpText.text;
            formatPattern = "{0}";
            formatArgs = new object[0];
            
            isInitialized = true;
            UpdateTranslation();
        }

        private void UpdateTranslation()
        {
            if (!isInitialized) Initialize();

            if (formatArgs.Length == 0)
            {
                // Simple single word translation
                tmpText.text = TranslationManager.Translate(currentText);
                return;
            }

            // Translate the format string if it's a key
            string translatedFormat = TranslationManager.Translate(formatPattern);
            
            // Translate any args that are strings (potential translation keys)
            object[] translatedArgs = new object[formatArgs.Length];
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
            
            tmpText.text = string.Format(translatedFormat, translatedArgs);
        }

        // Simple key only - just shows translated text
        public void SetText(string key)
        {
            currentText = key;
            formatPattern = "{0}";
            formatArgs = new object[0];
            UpdateTranslation();
        }

        // Exactly like string.Format - format string can be a key, and any string args can be keys
        public void SetText(string format, params object[] args)
        {
            formatPattern = format;
            formatArgs = args ?? new object[0];
            UpdateTranslation();
        }
    }
} 