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
            
            return ITextExtractor.ProcessSourcesOrAll<string[]>(
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
        
        private void ProcessSourceList(ExtractionSourcesList sources, HashSet<string> extractedText, TranslationMetadata metadata)
        {
            foreach (var source in sources.Items)
            {
                string searchFolder = source.type == ExtractionSourceType.Folder ? source.folderPath : Path.GetDirectoryName(AssetDatabase.GetAssetPath(source.asset));
                
                if (string.IsNullOrEmpty(searchFolder)) continue;
                
                // Normalize path
                searchFolder = searchFolder.Replace('\\', '/').TrimStart('/');
                if (!searchFolder.StartsWith("Assets/"))
                    searchFolder = "Assets/" + searchFolder;
                
                string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { searchFolder });
                ProcessPrefabs(prefabGuids, extractedText, metadata);
            }
        }
        
        private void ProcessPrefabs(string[] prefabGuids, HashSet<string> extractedText, TranslationMetadata metadata)
        {
            Debug.Log($"[PrefabTextExtractor] Starting to process {prefabGuids.Length} prefabs");
            
            foreach (string guid in prefabGuids)
            {
                try
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    Debug.Log($"[PrefabTextExtractor] Processing prefab at path: {path}");
                    
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null)
                    {
                        Debug.LogWarning($"[PrefabTextExtractor] Failed to load prefab at path: {path}");
                        continue;
                    }
                    
                    Debug.Log($"[PrefabTextExtractor] Successfully loaded prefab: {prefab.name}");

                    // Extract TextMeshPro texts
                    try
                    {
                        Debug.Log($"[PrefabTextExtractor] Starting TMP extraction in prefab: {prefab.name}");
                        var tmpTexts = prefab.GetComponentsInChildren<TextMeshProUGUI>(true);
                        Debug.Log($"[PrefabTextExtractor] Found {tmpTexts.Length} TMP objects in {prefab.name}");
                        
                        foreach (TextMeshProUGUI textObject in tmpTexts)
                        {
                            if (textObject == null)
                            {
                                Debug.LogWarning($"[PrefabTextExtractor] Null TMP object found in prefab: {path}");
                                continue;
                            }
                            
                            try
                            {
                                Debug.Log($"[PrefabTextExtractor] Processing TMP object: {GetGameObjectPath(textObject.gameObject)}");
                                if (textObject.GetComponent<DynamicTMP>())
                                {
                                    Debug.Log($"[PrefabTextExtractor] Skipping DynamicTMP object: {GetGameObjectPath(textObject.gameObject)}");
                                    continue;
                                }
                                if (!string.IsNullOrWhiteSpace(textObject.text))
                                {
                                    Debug.Log($"[PrefabTextExtractor] Found text in TMP: '{textObject.text}' at {GetGameObjectPath(textObject.gameObject)}");
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
                        Debug.Log($"[PrefabTextExtractor] Starting UI Text extraction in prefab: {prefab.name}");
                        var uiTexts = prefab.GetComponentsInChildren<Text>(true);
                        Debug.Log($"[PrefabTextExtractor] Found {uiTexts.Length} UI Text objects in {prefab.name}");
                        
                        foreach (Text uiText in uiTexts)
                        {
                            if (uiText == null)
                            {
                                Debug.LogWarning($"[PrefabTextExtractor] Null UI Text object found in prefab: {path}");
                                continue;
                            }
                            
                            try
                            {
                                Debug.Log($"[PrefabTextExtractor] Processing UI Text object: {GetGameObjectPath(uiText.gameObject)}");
                                if (uiText.GetComponent<DynamicTMP>())
                                {
                                    Debug.Log($"[PrefabTextExtractor] Skipping DynamicTMP UI Text object: {GetGameObjectPath(uiText.gameObject)}");
                                    continue;
                                }
                                if (!string.IsNullOrWhiteSpace(uiText.text))
                                {
                                    Debug.Log($"[PrefabTextExtractor] Found text in UI Text: '{uiText.text}' at {GetGameObjectPath(uiText.gameObject)}");
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
                        Debug.Log($"[PrefabTextExtractor] Starting component extraction in prefab: {prefab.name}");
                        Component[] allComponents = prefab.GetComponentsInChildren<Component>(true);
                        Debug.Log($"[PrefabTextExtractor] Found {allComponents.Length} components in {prefab.name}");
                        
                        foreach (Component component in allComponents)
                        {
                            if (component == null)
                            {
                                Debug.LogWarning($"[PrefabTextExtractor] Null component found in prefab: {path}");
                                continue;
                            }

                            try
                            {
                                Debug.Log($"[PrefabTextExtractor] Processing component {component.GetType().Name} on {GetGameObjectPath(component.gameObject)}");
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
            }
            
            Debug.Log($"[PrefabTextExtractor] Finished processing all prefabs");
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