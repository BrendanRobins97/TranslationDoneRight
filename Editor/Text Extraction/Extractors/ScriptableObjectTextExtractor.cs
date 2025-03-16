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
            var allGuids = new List<string>();

            // Find all ScriptableObjects without reporting progress
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
            }

            // Process all found ScriptableObjects
            ProcessScriptableObjects(allGuids.ToArray(), extractedText, metadata);
        }
        
        private void ProcessScriptableObjects(string[] guids, HashSet<string> extractedText, TranslationMetadata metadata, float progressOffset = 0f)
        {
            if (guids.Length == 0)
            {
                ITextExtractor.ReportProgress(this, 1f);
                return;
            }

            float progressIncrement = 1f / guids.Length;
            float currentProgress = 0f;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ScriptableObject scriptableObject = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                
                if (scriptableObject != null && TextExtractor.ShouldProcessPath(path, metadata, GetType()))
                {
                    try
                    {
                        TranslationExtractionHelper.ExtractTranslationsFromObject(
                            scriptableObject,
                            extractedText,
                            metadata,
                            path,
                            "",
                            TextSourceType.ScriptableObject
                        );
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"Failed to process ScriptableObject {path}: {e.Message}");
                    }
                }

                currentProgress += progressIncrement;
                ITextExtractor.ReportProgress(this, currentProgress);
            }
        }
    }
}
#endif 