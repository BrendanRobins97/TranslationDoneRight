#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using System.Reflection;

namespace PSS
{
    public class SceneTextExtractor : ITextExtractor
    {
        public TextSourceType SourceType => TextSourceType.Scene;
        public int Priority => 100; // Highest priority since scenes are most common
        public bool EnabledByDefault => true;
        public string Description => "Extracts text from all scenes in the build settings, including TextMeshPro, UI Text components, and fields marked with [Translated] attribute.";

        public HashSet<string> ExtractText(TranslationMetadata metadata)
        {
            var extractedText = new HashSet<string>();


            for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
            {
                string scenePath = EditorBuildSettings.scenes[i].path;
                Scene scene = SceneManager.GetSceneByPath(scenePath);

                if (!scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                }

                // Extract TextMeshPro texts
                ExtractTMProTexts(scene, extractedText, metadata, scenePath);

                // Extract UI Text texts
                ExtractUITexts(scene, extractedText, metadata, scenePath);

                // Extract fields marked with TranslatedAttribute
                foreach (GameObject rootObj in scene.GetRootGameObjects())
                {
                    ExtractFromGameObject(rootObj, extractedText, metadata, scenePath);
                }
            }

            return extractedText;
        }

        private void ExtractTMProTexts(Scene scene, HashSet<string> extractedText, TranslationMetadata metadata, string scenePath)
        {
            TextMeshProUGUI[] textMeshProObjects = GameObject.FindObjectsOfType<TextMeshProUGUI>(true);
            foreach (TextMeshProUGUI textObject in textMeshProObjects)
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
                        sourceType = TextSourceType.Scene,
                        sourcePath = scenePath,
                        objectPath = GetGameObjectPath(textObject.gameObject),
                        componentName = textObject.GetType().Name,
                        fieldName = "text",
                        wasInactive = !textObject.gameObject.activeInHierarchy
                    };
                    metadata.AddSource(textObject.text, sourceInfo);
                }
            }
        }

        private void ExtractUITexts(Scene scene, HashSet<string> extractedText, TranslationMetadata metadata, string scenePath)
        {
            Text[] uiTextObjects = GameObject.FindObjectsOfType<Text>(true);
            foreach (Text uiText in uiTextObjects)
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
                        sourceType = TextSourceType.Scene,
                        sourcePath = scenePath,
                        objectPath = GetGameObjectPath(uiText.gameObject),
                        componentName = uiText.GetType().Name,
                        fieldName = "text",
                        wasInactive = !uiText.gameObject.activeInHierarchy
                    };
                    metadata.AddSource(uiText.text, sourceInfo);
                }
            }
        }

        private void ExtractFromGameObject(GameObject obj, HashSet<string> extractedText, TranslationMetadata metadata, string scenePath)
        {
            Component[] components = obj.GetComponents<Component>();
            foreach (Component component in components)
            {
                if (component == null) continue;
                ExtractFieldsRecursive(component, extractedText, metadata, scenePath, GetGameObjectPath(obj), !obj.activeInHierarchy);
            }

            foreach (Transform child in obj.transform)
            {
                ExtractFromGameObject(child.gameObject, extractedText, metadata, scenePath);
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
                                sourceType = TextSourceType.Scene,
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