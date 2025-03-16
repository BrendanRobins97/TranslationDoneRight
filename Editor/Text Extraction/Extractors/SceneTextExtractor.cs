#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEditor.AddressableAssets;
using System.Linq;

namespace Translations
{
    /// <summary>
    /// Defines where to get scenes from for text extraction
    /// </summary>
    public enum SceneSourceType
    {
        /// <summary>Use scenes from the Build Settings</summary>
        SceneSettings,
        /// <summary>Use scenes from Addressables</summary>
        Addressables,
        /// <summary>Use manually specified scenes</summary>
        Manual
    }
    
    public class SceneTextExtractor : ITextExtractor
    {
        private const string SCENE_SOURCE_PREF_KEY = "Translations.SceneTextExtractor.SceneSourceType";
        
        public TextSourceType SourceType => TextSourceType.Scene;
        public int Priority => 100; // Highest priority since scenes are most common
        public bool EnabledByDefault => true;
        public string Description => "Extracts text from all scenes in the build settings, including TextMeshPro, UI Text components, and fields marked with [Translated] attribute.";

        // Scene source configuration
        private SceneSourceType _sceneSourceType;
        public SceneSourceType SceneSource 
        { 
            get => _sceneSourceType;
            set
            {
                if (_sceneSourceType != value)
                {
                    _sceneSourceType = value;
                    // Save to EditorPrefs whenever the value changes
                    EditorPrefs.SetInt(SCENE_SOURCE_PREF_KEY, (int)value);
                }
            }
        }

        public SceneTextExtractor()
        {
            // Load from EditorPrefs in constructor
            _sceneSourceType = (SceneSourceType)EditorPrefs.GetInt(SCENE_SOURCE_PREF_KEY, (int)SceneSourceType.SceneSettings);
        }

        public HashSet<string> ExtractText(TranslationMetadata metadata)
        {
            var extractedText = new HashSet<string>();
            
            return ITextExtractor.ProcessSourcesOrAll<EditorBuildSettingsScene[]>(
                this,
                metadata,
                () => {
                    // Process scenes based on the selected source type
                    switch (SceneSource)
                    {
                        case SceneSourceType.SceneSettings:
                            // Process all scenes from build settings (default behavior)
                            ProcessScenes(EditorBuildSettings.scenes, extractedText, metadata);
                            break;
                        case SceneSourceType.Addressables:
                            // Process scenes from Addressables
                            ProcessAddressableScenes(extractedText, metadata);
                            break;
                        case SceneSourceType.Manual:
                            // Process manually specified scenes (stored in metadata)
                            ProcessManualScenes(extractedText, metadata);
                            break;
                    }
                    return extractedText;
                },
                (sources) => {
                    // Process only scenes within specified sources
                    ProcessScenesBySource(sources, extractedText, metadata);
                    return extractedText;
                }
            );
        }
        
        private void ProcessScenesBySource(ExtractionSourcesList sources, HashSet<string> extractedText, TranslationMetadata metadata)
        {
            foreach (var source in sources.Items)
            {
                if (source.type == ExtractionSourceType.Asset && source.asset != null)
                {
                    // If it's a direct scene asset
                    string scenePath = AssetDatabase.GetAssetPath(source.asset);
                    if (scenePath.EndsWith(".unity"))
                    {
                        ProcessScene(scenePath, extractedText, metadata);
                    }
                }
                else if (source.type == ExtractionSourceType.Folder)
                {
                    // Search for scenes in folder
                    string searchFolder = source.folderPath;
                    if (string.IsNullOrEmpty(searchFolder)) continue;
                    
                    // Normalize path
                    searchFolder = searchFolder.Replace('\\', '/').TrimStart('/');
                    if (!searchFolder.StartsWith("Assets/"))
                        searchFolder = "Assets/" + searchFolder;
                    
                    string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { searchFolder });
                    foreach (string guid in sceneGuids)
                    {
                        string scenePath = AssetDatabase.GUIDToAssetPath(guid);
                        ProcessScene(scenePath, extractedText, metadata);
                    }
                }
            }
        }
        
        private void ProcessScenes(EditorBuildSettingsScene[] scenes, HashSet<string> extractedText, TranslationMetadata metadata)
        {
            foreach (var sceneSettings in scenes)
            {
                string scenePath = sceneSettings.path;
                ProcessScene(scenePath, extractedText, metadata);
            }
        }
        
        private void ProcessScene(string scenePath, HashSet<string> extractedText, TranslationMetadata metadata)
        {
            // Store current scene setup
            var sceneSetup = EditorSceneManager.GetSceneManagerSetup();
            
            try
            {
                Scene scene = SceneManager.GetSceneByPath(scenePath);

                if (!scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                }

                // Extract TextMeshPro texts
                try 
                {
                    TextMeshProUGUI[] textMeshProObjects = GameObject.FindObjectsOfType<TextMeshProUGUI>(true);
                    foreach (TextMeshProUGUI textObject in textMeshProObjects)
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
                                    sourceType = TextSourceType.Scene,
                                    sourcePath = scenePath,
                                    objectPath = GetGameObjectPath(textObject.gameObject),
                                    componentName = textObject.GetType().Name,
                                    fieldName = "text",
                                };
                                metadata.AddSource(textObject.text, sourceInfo);
                            }
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogWarning($"Failed to process TMP text in scene {scenePath}: {e.Message}");
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Failed to process TMP components in scene {scenePath}: {e.Message}");
                }

                // Extract UI Text texts
                try
                {
                    Text[] uiTextObjects = GameObject.FindObjectsOfType<Text>(true);
                    foreach (Text uiText in uiTextObjects)
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
                                    sourceType = TextSourceType.Scene,
                                    sourcePath = scenePath,
                                    objectPath = GetGameObjectPath(uiText.gameObject),
                                    componentName = uiText.GetType().Name,
                                    fieldName = "text",
                                };
                                metadata.AddSource(uiText.text, sourceInfo);
                            }
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogWarning($"Failed to process UI text in scene {scenePath}: {e.Message}");
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Failed to process UI Text components in scene {scenePath}: {e.Message}");
                }

                // Extract fields marked with TranslatedAttribute
                try
                {
                    foreach (GameObject rootObj in scene.GetRootGameObjects())
                    {
                        try
                        {
                            ExtractFromGameObject(rootObj, extractedText, metadata, scenePath);
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogWarning($"Failed to process root GameObject '{rootObj.name}' in scene {scenePath}: {e.Message}");
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Failed to process root GameObjects in scene {scenePath}: {e.Message}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to process scene {scenePath}: {e.Message}");
            }
            finally
            {
                // Restore original scene setup
                RestoreSceneSetup(sceneSetup);
            }
        }

        private void ExtractFromGameObject(GameObject obj, HashSet<string> extractedText, TranslationMetadata metadata, string scenePath)
        {
            try
            {
                Component[] components = obj.GetComponents<Component>();
                foreach (Component component in components)
                {
                    if (component == null)
                    {
                        Debug.LogWarning($"Null component found in GameObject '{obj.name}' in scene {scenePath}");
                        continue;
                    }

                    try
                    {
                        TranslationExtractionHelper.ExtractTranslationsFromObject(
                            component, 
                            extractedText, 
                            metadata, 
                            scenePath, 
                            GetGameObjectPath(obj), 
                            TextSourceType.Scene);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"Failed to extract translations from component {component.GetType().Name} in GameObject '{obj.name}' in scene {scenePath}: {e.Message}");
                    }
                }

                foreach (Transform child in obj.transform)
                {
                    try
                    {
                        ExtractFromGameObject(child.gameObject, extractedText, metadata, scenePath);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"Failed to process child GameObject '{child.name}' in scene {scenePath}: {e.Message}");
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to process GameObject '{obj.name}' in scene {scenePath}: {e.Message}");
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

        private void ProcessAddressableScenes(HashSet<string> extractedText, TranslationMetadata metadata)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogWarning("Addressable Asset Settings not found. Please set up Addressables first.");
                return;
            }

            var sceneEntries = settings.groups
                .Where(g => g != null)
                .SelectMany(g => g.entries)
                .Where(e => e != null && e.AssetPath.EndsWith(".unity"));

            foreach (var entry in sceneEntries)
            {
                ProcessScene(entry.AssetPath, extractedText, metadata);
            }
        }
        
        private void ProcessManualScenes(HashSet<string> extractedText, TranslationMetadata metadata)
        {
            // Process manually specified scenes from the metadata
            if (metadata.manualScenePaths == null || metadata.manualScenePaths.Count == 0)
            {
                Debug.LogWarning("No manual scenes specified. Please add scene paths in the inspector.");
                return;
            }
            
            foreach (var scenePath in metadata.manualScenePaths)
            {
                if (string.IsNullOrEmpty(scenePath)) continue;
                if (System.IO.File.Exists(scenePath))
                {
                    ProcessScene(scenePath, extractedText, metadata);
                }
                else
                {
                    Debug.LogWarning($"Scene at path '{scenePath}' does not exist.");
                }
            }
        }

        private void RestoreSceneSetup(SceneSetup[] originalSetup)
        {
            // If we have unsaved changes, ask the user what to do
            if (EditorSceneManager.GetActiveScene().isDirty)
            {
                if (EditorUtility.DisplayDialog("Unsaved Changes",
                    "The scene has unsaved changes. Do you want to save them?",
                    "Save", "Discard"))
                {
                    EditorSceneManager.SaveOpenScenes();
                }
            }

            try
            {
                // Restore the original scene setup
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to restore scene setup: {e.Message}");
                
                // Fallback: At least try to reopen the active scene
                if (originalSetup != null && originalSetup.Length > 0)
                {
                    var activeSetup = originalSetup.FirstOrDefault(s => s.isActive);
                    if (activeSetup != null)
                    {
                        EditorSceneManager.OpenScene(activeSetup.path, OpenSceneMode.Single);
                    }
                }
            }
        }

        /// <summary>
        /// Draws custom inspector GUI for the scene extractor
        /// </summary>
        /// <returns>True if any changes were made that require saving, false otherwise</returns>
        public bool DrawCustomInspectorGUI()
        {
            bool isDirty = false;
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Scene Source Settings", EditorStyles.boldLabel);
            
            // Scene source dropdown
            EditorGUI.BeginChangeCheck();
            var newSceneSource = (SceneSourceType)EditorGUILayout.EnumPopup(
                new GUIContent("Scene Source", "Where to get scenes from for text extraction"),
                SceneSource
            );
            if (EditorGUI.EndChangeCheck())
            {
                SceneSource = newSceneSource;
                isDirty = true;
            }
            
            // Show different options based on the selected source type
            switch (SceneSource)
            {
                case SceneSourceType.SceneSettings:
                    if (EditorBuildSettings.scenes.Length > 0)
                    {
                        EditorGUILayout.LabelField(
                            $"Using scenes from the Build Settings. Found {EditorBuildSettings.scenes.Length} scenes in build settings."
                        );
                    }
                    else
                    {
                        EditorGUILayout.HelpBox(
                            "No scenes found in the Build Settings.",
                            MessageType.Warning
                        );
                    }
                    break;
                    
                case SceneSourceType.Addressables:
                    var settings = AddressableAssetSettingsDefaultObject.Settings;
                    if (settings == null)
                    {
                        EditorGUILayout.HelpBox(
                            "Addressable Asset Settings not found.\n" +
                            "Please set up Addressables first.",
                            MessageType.Warning
                        );
                    }
                    else
                    {
                        var sceneCount = settings.groups
                            .Where(g => g != null)
                            .SelectMany(g => g.entries)
                            .Count(e => e != null && e.AssetPath.EndsWith(".unity"));
                        if (sceneCount > 0)
                        {
                            EditorGUILayout.LabelField(
                                $"Using scenes from Addressables. Found {sceneCount} addressable scenes."
                            );
                        }
                        else
                        {
                            EditorGUILayout.HelpBox(
                                "No addressable scenes found.",
                                MessageType.Warning
                            );
                        }
                    }
                    break;
                    
                case SceneSourceType.Manual:
                    EditorGUILayout.LabelField(
                        "Add scene paths manually below. Drag and drop scenes from the Project window or use the + button."
                    );
                    
                    // Get the metadata instance
                    var metadata = TranslationMetaDataProvider.Metadata;
                    
                    // Manual scene list
                    EditorGUI.BeginChangeCheck();
                    
                    // Show the list of manual scenes
                    for (int i = 0; i < metadata.manualScenePaths.Count; i++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        
                        // Allow dragging scenes - using SceneAsset type to restrict to only scenes
                        var sceneObj = AssetDatabase.LoadAssetAtPath<SceneAsset>(metadata.manualScenePaths[i]);
                        var newSceneObj = EditorGUILayout.ObjectField(
                            GUIContent.none, 
                            sceneObj, 
                            typeof(SceneAsset), 
                            false
                        ) as SceneAsset;
                        
                        if (newSceneObj != sceneObj)
                        {
                            string path = AssetDatabase.GetAssetPath(newSceneObj);
                            metadata.manualScenePaths[i] = path;
                            isDirty = true;
                        }
                        
                        // Remove button
                        if (GUILayout.Button("-", GUILayout.Width(25)))
                        {
                            metadata.manualScenePaths.RemoveAt(i);
                            isDirty = true;
                            i--;
                        }
                        
                        EditorGUILayout.EndHorizontal();
                    }
                    
                    EditorGUILayout.Space(5);
                    
                    // Add button
                    if (GUILayout.Button("Add Scene"))
                    {
                        metadata.manualScenePaths.Add("");
                        isDirty = true;
                    }
                    
                    // Drag and drop area
                    Rect dropArea = GUILayoutUtility.GetRect(0.0f, 50.0f, GUILayout.ExpandWidth(true));
                    GUI.Box(dropArea, "Drag & Drop Scenes Here", EditorStyles.helpBox);
                    
                    Event evt = Event.current;
                    switch (evt.type)
                    {
                        case EventType.DragUpdated:
                        case EventType.DragPerform:
                            if (!dropArea.Contains(evt.mousePosition))
                                break;
                            
                            // Only accept if all dragged items are scenes
                            bool allScenes = DragAndDrop.objectReferences.All(obj => obj is SceneAsset);
                            if (allScenes)
                            {
                                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                            }
                            else
                            {
                                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                                break;
                            }
                            
                            if (evt.type == EventType.DragPerform)
                            {
                                DragAndDrop.AcceptDrag();
                                
                                foreach (var draggedObject in DragAndDrop.objectReferences)
                                {
                                    string path = AssetDatabase.GetAssetPath(draggedObject);
                                    if (!metadata.manualScenePaths.Contains(path))
                                    {
                                        metadata.manualScenePaths.Add(path);
                                        isDirty = true;
                                    }
                                }
                            }
                            evt.Use();
                            break;
                    }
                    break;
            }
            
            EditorGUILayout.EndVertical();
            
            return isDirty;
        }
    }
}
#endif 