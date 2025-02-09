#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;

namespace PSS
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
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");

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

            return extractedText;
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