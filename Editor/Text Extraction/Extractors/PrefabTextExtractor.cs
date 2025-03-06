#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;
using System.IO;

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
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                
                if (prefab != null)
                {
                    Component[] allComponents = prefab.GetComponentsInChildren<Component>(true);
                    foreach (Component component in allComponents)
                    {
                        if (component == null)
                        {
                            Debug.LogWarning("Null component found in prefab: " + path);
                            continue;
                        }
                        
                        ExtractFieldsRecursive(
                            component, 
                            extractedText, 
                            metadata,
                            path, 
                            GetGameObjectPath(component.gameObject), 
                            !component.gameObject.activeInHierarchy
                        );
                    }
                }
            }
        }

        private void ExtractFieldsRecursive(object obj, HashSet<string> extractedText, TranslationMetadata metadata, string sourcePath, string objectPath, bool wasInactive)
        {
            if (obj == null) return;

            FieldInfo[] fields = obj.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (FieldInfo field in fields)
            {
                if (field.IsDefined(typeof(TranslatedAttribute), false))
                {
                    if (field.FieldType == typeof(string))
                    {
                        string fieldValue = field.GetValue(obj) as string;
                        if (!string.IsNullOrEmpty(fieldValue))
                        {
                            extractedText.Add(fieldValue);
                            
                            var sourceInfo = new TextSourceInfo
                            {
                                sourceType = TextSourceType.Prefab,
                                sourcePath = sourcePath,
                                objectPath = objectPath,
                                componentName = obj.GetType().Name,
                                fieldName = field.Name,
                                wasInactive = wasInactive
                            };
                            metadata.AddSource(fieldValue, sourceInfo);
                        }
                    }
                    else if (!field.FieldType.IsPrimitive && !field.FieldType.IsEnum && field.FieldType.IsClass)
                    {
                        object nestedObj = field.GetValue(obj);
                        ExtractFieldsRecursive(nestedObj, extractedText, metadata, sourcePath, objectPath, wasInactive);
                    }
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