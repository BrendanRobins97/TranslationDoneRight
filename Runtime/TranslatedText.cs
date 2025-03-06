using UnityEngine;
using UnityEngine.UI;
using System;

namespace Translations
{
    public static class TranslatedTextExtensions
    {
        public static void SetTextTranslated(this Text text, string key)
        {
            if (text.GetComponent<TranslatedText>() == null)
            {
                text.gameObject.AddComponent<TranslatedText>();
            }
            text.GetComponent<TranslatedText>().SetText(key);
        }

        public static void SetTextTranslated(this Text text, string format, params object[] args)
        {
            if (text.GetComponent<TranslatedText>() == null)
            {
                text.gameObject.AddComponent<TranslatedText>();   
            }
            text.GetComponent<TranslatedText>().SetText(format, args);
        }
    }

    [RequireComponent(typeof(Text))]
    public class TranslatedText : MonoBehaviour
    {
        private Text text;
        private string currentText;
        private string formatPattern;
        private object[] formatArgs;
        private bool isInitialized = false;

        private void Awake()
        {
            text = GetComponent<Text>();
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
            currentText = text.text;
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
                text.text = TranslationManager.Translate(currentText);
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
            
            text.text = string.Format(translatedFormat, translatedArgs);
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