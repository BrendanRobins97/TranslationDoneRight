using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEditorInternal;

namespace Translations
{
    /// <summary>
    /// Utility class for drawing extraction sources in the editor
    /// </summary>
    public static class ExtractionSourcesDrawer
    {
        /// <summary>
        /// Creates a ReorderableList for extraction sources and configures it
        /// </summary>
        /// <param name="sources">The list of extraction sources to edit</param>
        /// <param name="headerText">The header text to display (default: "Extraction Sources")</param>
        /// <param name="onSourceModified">Callback when a source is modified</param>
        /// <returns>A configured ReorderableList</returns>
        public static ReorderableList CreateExtractionSourcesList(
            List<ExtractionSource> sources,
            string headerText = "Extraction Sources (Empty = Full Project)",
            System.Action onSourceModified = null)
        {
            var sourcesList = new ReorderableList(
                sources ?? new List<ExtractionSource>(),
                typeof(ExtractionSource),
                true, true, true, true);

            sourcesList.drawHeaderCallback = (Rect rect) =>
            {
                EditorGUI.LabelField(rect, headerText);
            };

            sourcesList.onAddCallback = (ReorderableList list) =>
            {
                sources.Add(new ExtractionSource());
                onSourceModified?.Invoke();
            };

            sourcesList.onRemoveCallback = (ReorderableList list) =>
            {
                if (EditorUtility.DisplayDialog("Remove Source", 
                    "Are you sure you want to remove this extraction source?", "Yes", "No"))
                {
                    sources.RemoveAt(list.index);
                    onSourceModified?.Invoke();
                }
            };

            sourcesList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                if (sources == null || index >= sources.Count)
                    return;

                var source = sources[index];
                rect.y += 2;
                rect.height = EditorGUIUtility.singleLineHeight;

                // Calculate rects for the different elements
                var typeRect = new Rect(rect.x, rect.y, 100, rect.height);
                var pathRect = new Rect(rect.x + 105, rect.y, rect.width - 325, rect.height);
                var recursiveRect = new Rect(rect.x + rect.width - 215, rect.y, 80, rect.height);
                var browseRect = new Rect(rect.x + rect.width - 130, rect.y, 60, rect.height);
                var pingRect = new Rect(rect.x + rect.width - 65, rect.y, 60, rect.height);

                // Draw the type enum
                EditorGUI.BeginChangeCheck();
                source.type = (ExtractionSourceType)EditorGUI.EnumPopup(typeRect, source.type);
                bool changed = EditorGUI.EndChangeCheck();

                // Draw the path field
                if (source.type == ExtractionSourceType.Folder)
                {
                    EditorGUI.BeginChangeCheck();
                    source.folderPath = EditorGUI.TextField(pathRect, source.folderPath);
                    changed |= EditorGUI.EndChangeCheck();
                    
                    EditorGUI.BeginChangeCheck();
                    source.recursive = EditorGUI.ToggleLeft(recursiveRect, new GUIContent("Recursive", "Include subfolders"), source.recursive);
                    changed |= EditorGUI.EndChangeCheck();
                }
                else
                {
                    EditorGUI.BeginChangeCheck();
                    source.asset = EditorGUI.ObjectField(pathRect, source.asset, typeof(UnityEngine.Object), false) as UnityEngine.Object;
                    changed |= EditorGUI.EndChangeCheck();
                }

                // Browse button
                if (GUI.Button(browseRect, "Browse"))
                {
                    string title = source.type == ExtractionSourceType.Folder ? "Select Folder" : "Select Asset";
                    string path = EditorUtility.OpenFolderPanel(title, "Assets", "");
                    if (!string.IsNullOrEmpty(path))
                    {
                        if (path.StartsWith(Application.dataPath))
                        {
                            source.folderPath = "Assets" + path.Substring(Application.dataPath.Length);
                            changed = true;
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("Invalid Path", 
                                "Please select a folder within your Unity project's Assets folder.", "OK");
                        }
                    }
                }

                // Ping button
                if (GUI.Button(pingRect, "Select"))
                {
                    if (source.type == ExtractionSourceType.Folder)
                    {
                        var folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(source.folderPath);
                        if (folder != null)
                            EditorGUIUtility.PingObject(folder);
                    }
                    else if (source.asset != null)
                    {
                        EditorGUIUtility.PingObject(source.asset);
                    }
                }

                if (changed)
                {
                    onSourceModified?.Invoke();
                }
            };

            return sourcesList;
        }

        /// <summary>
        /// Creates and draws a drag and drop area for extraction sources
        /// </summary>
        /// <param name="sources">The list to add sources to</param>
        /// <param name="onSourceModified">Callback when a source is modified</param>
        public static void DrawDragAndDropArea(List<ExtractionSource> sources, System.Action onSourceModified = null)
        {
            // Drag and drop area
            Rect dropArea = GUILayoutUtility.GetRect(0.0f, 50.0f, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "Drag and drop folders or assets here", EditorStyles.helpBox);
            
            Event evt = Event.current;
            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!dropArea.Contains(evt.mousePosition))
                        break;
                        
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        
                        foreach (var path in DragAndDrop.paths)
                        {
                            var source = new ExtractionSource
                            {
                                type = System.IO.Directory.Exists(path) ? 
                                    ExtractionSourceType.Folder : ExtractionSourceType.Asset,
                                folderPath = path,
                            };

                            if (source.type == ExtractionSourceType.Asset)
                            {
                                source.asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                            }

                            sources.Add(source);
                        }

                        onSourceModified?.Invoke();
                        evt.Use();
                    }
                    break;
            }
        }
    }
} 