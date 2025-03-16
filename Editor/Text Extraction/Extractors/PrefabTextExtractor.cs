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
            foreach (string guid in prefabGuids)
            {
                try
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    
                    if (prefab != null)
                    {
                        // Extract TextMeshPro texts
                        try
                        {
                            var tmpTexts = prefab.GetComponentsInChildren<TextMeshProUGUI>(true);
                            foreach (TextMeshProUGUI textObject in tmpTexts)
                            {
                                if (textObject == null) continue;
                                
                                try
                                {
                                    if (textObject.GetComponent<DynamicTMP>())
                                    {
                                        continue;
                                    }
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
                                    Debug.LogWarning($"Failed to process TMP text in prefab {path}: {e.Message}");
                                }
                            }
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogWarning($"Failed to process TMP components in prefab {path}: {e.Message}");
                        }

                        // Extract UI Text texts
                        try
                        {
                            var uiTexts = prefab.GetComponentsInChildren<Text>(true);
                            foreach (Text uiText in uiTexts)
                            {
                                if (uiText == null) continue;
                                
                                try
                                {
                                    if (uiText.GetComponent<DynamicTMP>())
                                    {
                                        continue;
                                    }
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
                                    Debug.LogWarning($"Failed to process UI text in prefab {path}: {e.Message}");
                                }
                            }
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogWarning($"Failed to process UI Text components in prefab {path}: {e.Message}");
                        }

                        // Extract fields marked with [Translated] attribute from all components
                        try
                        {
                            Component[] allComponents = prefab.GetComponentsInChildren<Component>(true);
                            foreach (Component component in allComponents)
                            {
                                if (component == null)
                                {
                                    Debug.LogWarning("Null component found in prefab: " + path);
                                    continue;
                                }

                                try
                                {
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
                                    Debug.LogWarning($"Failed to extract translations from component {component.GetType().Name} in prefab {path}: {e.Message}");
                                }
                            }
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogWarning($"Failed to process components in prefab {path}: {e.Message}");
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to process prefab with GUID {guid}: {e.Message}");
                }
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