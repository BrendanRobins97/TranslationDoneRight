#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Translations
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
            
            return ITextExtractor.ProcessSourcesOrAll<string[]>(
                this,
                metadata,
                () => {
                    // Process all ScriptableObjects
                    string[] guids = AssetDatabase.FindAssets("t:ScriptableObject");
                    ProcessScriptableObjects(guids, extractedText, metadata);
                    return extractedText;
                },
                (sources) => {
                    // Process only ScriptableObjects within specified sources
                    ProcessSourceList(sources, extractedText, metadata);
                    return extractedText;
                }
            );
        }
        
        private void ProcessSourceList(ExtractionSourcesList sources, HashSet<string> extractedText, TranslationMetadata metadata)
        {
            float sourceProgress = 0f;
            float sourceIncrement = 1f / sources.Items.Count;
            var allGuids = new List<string>();

            foreach (var source in sources.Items)
            {
                string searchFolder = source.type == ExtractionSourceType.Folder ? source.folderPath : System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(source.asset));
                
                if (string.IsNullOrEmpty(searchFolder)) continue;
                
                // Normalize path
                searchFolder = searchFolder.Replace('\\', '/').TrimStart('/');
                if (!searchFolder.StartsWith("Assets/"))
                    searchFolder = "Assets/" + searchFolder;
                
                string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { searchFolder });
                allGuids.AddRange(guids);
                
                sourceProgress += sourceIncrement;
                ITextExtractor.ReportProgress(this, sourceProgress * 0.1f); // First 10% for finding scriptable objects
            }

            ProcessScriptableObjects(allGuids.ToArray(), extractedText, metadata, 0.1f); // Remaining 90% for processing
        }
        
        private void ProcessScriptableObjects(string[] guids, HashSet<string> extractedText, TranslationMetadata metadata, float progressOffset = 0f)
        {
            float progressIncrement = (1f - progressOffset) / (guids.Length > 0 ? guids.Length : 1);
            float currentProgress = progressOffset;

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

                currentProgress += progressIncrement;
                ITextExtractor.ReportProgress(this, currentProgress);
            }
        }
    }
}
#endif 