using UnityEngine;

namespace PSS
{
    public partial class TranslationsEditorWindow
    {
        private enum Tab
        {
            Settings,
            TextExtraction,
            AllText,
            Languages,
            DeepL
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
    }
} 