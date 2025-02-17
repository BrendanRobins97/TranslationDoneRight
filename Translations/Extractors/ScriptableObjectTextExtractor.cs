#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace PSS
{
    public class ScriptableObjectTextExtractor : ITextExtractor
    {
        public TextSourceType SourceType => TextSourceType.ScriptableObject;
        public int Priority => 70;
        public bool EnabledByDefault => true;
        public string Description => "Extracts text from all ScriptableObjects in the project, finding fields marked with [Translated] attribute.";

        public HashSet<string> ExtractText(TranslationMetadata metadata)
        {
            var extractedText = new HashSet<string>();
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ScriptableObject scriptableObject = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);

                if (scriptableObject != null)
                {
                    TranslationExtractionHelper.ExtractTranslationsFromObject(
                        scriptableObject,
                        extractedText,
                        metadata,
                        path,
                        sourceType: TextSourceType.ScriptableObject);
                }
            }

            return extractedText;
        }
    }
}
#endif 