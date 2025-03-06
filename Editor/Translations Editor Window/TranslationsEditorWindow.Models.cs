using UnityEngine;

namespace Translations
{
    public partial class TranslationsEditorWindow
    {
        private enum Tab
        {
            TextExtraction,
            AllText,
            Languages,
            Config
        }
        
        private enum TextViewMode
        {
            Detailed,
            Grid
        }

        [System.Serializable]
        private class DeepLResponse
        {
            public DeepLTranslation[] translations;
        }

        [System.Serializable]
        private class DeepLTranslation
        {
            public string text;
            public string detected_source_language;
        }

        [System.Serializable]
        private class BatchTranslationRequest
        {
            public string[] text;
            public string target_lang;
            public string context;
            public string formality;
            public bool preserve_formatting;
        }

        [System.Serializable]
        private class BatchTranslationResponse
        {
            public DeepLTranslation[] translations;
        }
    }
} 