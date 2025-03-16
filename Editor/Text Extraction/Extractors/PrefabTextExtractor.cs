#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using TMPro;
using UnityEngine.UI;

namespace Translations
{
    public class PrefabTextExtractor : ITextExtractor
    {
        public TextSourceType SourceType => TextSourceType.Prefab;
        public int Priority => 90; // High priority, but lower than scenes
        public bool EnabledByDefault => true;
        public string Description => "Extracts text from all prefabs in the project, including fields marked with [Translated] attribute.";

        public HashSet<string> ExtractText(TranslationMetadata metadata)
        {
            var extractedText = new HashSet<string>();
            
            return ITextExtractor.ProcessSourcesOrAll<HashSet<string>>(
                this,
                metadata,
                () => {
                    // Process all prefabs
                    string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
                    ProcessPrefabs(prefabGuids, extractedText, metadata);
                    return extractedText;
                },
                (sources) => {
                    // Process only prefabs within specified sources
                    ProcessSourceList(sources, extractedText, metadata);
                    return extractedText;
                }
            );
        }
        
        private void ProcessSourceList(List<ExtractionSource> sources, HashSet<string> extractedText, TranslationMetadata metadata)
        {
            var allPrefabGuids = new List<string>();

            // Find all prefabs without reporting progress
            foreach (var source in sources)
            {
                string searchFolder = source.type == ExtractionSourceType.Folder ? source.folderPath : Path.GetDirectoryName(AssetDatabase.GetAssetPath(source.asset));
                
                if (string.IsNullOrEmpty(searchFolder)) continue;
                
                // Normalize path
                searchFolder = searchFolder.Replace('\\', '/').TrimStart('/');
                if (!searchFolder.StartsWith("Assets/"))
                    searchFolder = "Assets/" + searchFolder;
                
                string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { searchFolder });
                allPrefabGuids.AddRange(prefabGuids);
            }

            // Process all found prefabs
            ProcessPrefabs(allPrefabGuids.ToArray(), extractedText, metadata);
        }
        
        private void ProcessPrefabs(string[] prefabGuids, HashSet<string> extractedText, TranslationMetadata metadata, float progressOffset = 0f)
        {
            if (prefabGuids.Length == 0)
            {
                ITextExtractor.ReportProgress(this, 1f);
                return;
            }

            float progressIncrement = 1f / prefabGuids.Length;
            float currentProgress = 0f;

            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string guid = prefabGuids[i];
                try
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null)
                    {
                        Debug.LogWarning($"[PrefabTextExtractor] Failed to load prefab at path: {path}");
                        continue;
                    }

                    // Extract TextMeshPro texts
                    try
                    {
                        var tmpTexts = prefab.GetComponentsInChildren<TextMeshProUGUI>(true);
                        foreach (TextMeshProUGUI textObject in tmpTexts)
                        {
                            if (textObject == null)
                            {
                                Debug.LogWarning($"[PrefabTextExtractor] Null TMP object found in prefab: {path}");
                                continue;
                            }
                            
                            try
                            {
                                if (textObject.GetComponent<DynamicTMP>())
                                    continue;

                                if (!string.IsNullOrWhiteSpace(textObject.text))
                                {
                                    extractedText.Add(textObject.text);
                                    
                                    var sourceInfo = new TextSourceInfo
                                    {
                                        sourceType = TextSourceType.Prefab,
                                        sourcePath = path,
                                        objectPath = GetGameObjectPath(textObject.gameObject),
                                        componentName = textObject.GetType().Name,
                                        fieldName = "text",
                                    };
                                    metadata.AddSource(textObject.text, sourceInfo);
                                }
                            }
                            catch (System.Exception e)
                            {
                                Debug.LogWarning($"[PrefabTextExtractor] Failed to process TMP text in prefab {path} at {GetGameObjectPath(textObject.gameObject)}: {e.Message}\nStack trace: {e.StackTrace}");
                            }
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[PrefabTextExtractor] Failed to process TMP components in prefab {path}: {e.Message}\nStack trace: {e.StackTrace}");
                    }

                    // Extract UI Text texts
                    try
                    {
                        var uiTexts = prefab.GetComponentsInChildren<Text>(true);
                        foreach (Text uiText in uiTexts)
                        {
                            if (uiText == null)
                            {
                                Debug.LogWarning($"[PrefabTextExtractor] Null UI Text object found in prefab: {path}");
                                continue;
                            }
                            
                            try
                            {
                                if (uiText.GetComponent<DynamicTMP>())
                                    continue;

                                if (!string.IsNullOrWhiteSpace(uiText.text))
                                {
                                    extractedText.Add(uiText.text);
                                    
                                    var sourceInfo = new TextSourceInfo
                                    {
                                        sourceType = TextSourceType.Prefab,
                                        sourcePath = path,
                                        objectPath = GetGameObjectPath(uiText.gameObject),
                                        componentName = uiText.GetType().Name,
                                        fieldName = "text",
                                    };
                                    metadata.AddSource(uiText.text, sourceInfo);
                                }
                            }
                            catch (System.Exception e)
                            {
                                Debug.LogWarning($"[PrefabTextExtractor] Failed to process UI text in prefab {path} at {GetGameObjectPath(uiText.gameObject)}: {e.Message}\nStack trace: {e.StackTrace}");
                            }
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[PrefabTextExtractor] Failed to process UI Text components in prefab {path}: {e.Message}\nStack trace: {e.StackTrace}");
                    }

                    // Extract fields marked with [Translated] attribute from all components
                    try
                    {
                        Component[] allComponents = prefab.GetComponentsInChildren<Component>(true);
                        foreach (Component component in allComponents)
                        {
                            if (component == null)
                            {
                                Debug.LogWarning($"[PrefabTextExtractor] Null component found in prefab: {path}");
                                continue;
                            }

                            try
                            {
                                // Skip Unity built-in components unless they're MonoBehaviours
                                var componentType = component.GetType();
                                if (componentType.FullName.StartsWith("UnityEngine") && 
                                    !typeof(MonoBehaviour).IsAssignableFrom(componentType))
                                {
                                    continue;
                                }

                                TranslationExtractionHelper.ExtractTranslationsFromObject(
                                    component,
                                    extractedText,
                                    metadata,
                                    path,
                                    GetGameObjectPath(component.gameObject),
                                    TextSourceType.Prefab
                                );
                            }
                            catch (System.Exception e)
                            {
                                Debug.LogWarning($"[PrefabTextExtractor] Failed to extract translations from component {component.GetType().Name} in prefab {path} at {GetGameObjectPath(component.gameObject)}: {e.Message}\nStack trace: {e.StackTrace}");
                            }
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[PrefabTextExtractor] Failed to process components in prefab {path}: {e.Message}\nStack trace: {e.StackTrace}");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[PrefabTextExtractor] Failed to process prefab with GUID {guid}: {e.Message}\nStack trace: {e.StackTrace}");
                }

                currentProgress += progressIncrement;
                ITextExtractor.ReportProgress(this, currentProgress);
            }
        }

        private string GetGameObjectPath(GameObject obj)
        {
            string path = obj.name;
            Transform parent = obj.transform.parent;
            
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            
            return path;
        }
    }
}
#endif 