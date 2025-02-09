#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;

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
                    ExtractFieldsRecursive(scriptableObject, extractedText, metadata, path);
                }
            }

            return extractedText;
        }

        private void ExtractFieldsRecursive(object obj, HashSet<string> extractedText, TranslationMetadata metadata, string sourcePath)
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
                                sourceType = TextSourceType.ScriptableObject,
                                sourcePath = sourcePath,
                                componentName = obj.GetType().Name,
                                fieldName = field.Name
                            };
                            metadata.AddSource(fieldValue, sourceInfo);
                        }
                    }
                    else if (!field.FieldType.IsPrimitive && !field.FieldType.IsEnum && field.FieldType.IsClass)
                    {
                        object nestedObj = field.GetValue(obj);
                        ExtractFieldsRecursive(nestedObj, extractedText, metadata, sourcePath);
                    }
                }
            }
        }
    }
}
#endif 