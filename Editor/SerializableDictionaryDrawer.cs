using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;

namespace Translations
{
    [CustomPropertyDrawer(typeof(SerializableDictionary<,>))]
    public class SerializableDictionaryDrawer : PropertyDrawer
    {
        private const float HeaderHeight = 20f;
        private const float ElementPadding = 2f;
        private const float ElementBaseHeight = 20f;
        private const int ItemsPerPage = 20;
        
        private Dictionary<string, bool> foldoutStates = new Dictionary<string, bool>();
        private Dictionary<string, Vector2> scrollPositions = new Dictionary<string, Vector2>();
        private Dictionary<string, int> currentPages = new Dictionary<string, int>();
        private Dictionary<string, Dictionary<int, float>> elementHeights = new Dictionary<string, Dictionary<int, float>>();
        
        private bool GetFoldoutState(string key)
        {
            if (!foldoutStates.ContainsKey(key))
                foldoutStates[key] = true;
            return foldoutStates[key];
        }

        private Vector2 GetScrollPosition(string key)
        {
            if (!scrollPositions.ContainsKey(key))
                scrollPositions[key] = Vector2.zero;
            return scrollPositions[key];
        }

        private int GetCurrentPage(string key)
        {
            if (!currentPages.ContainsKey(key))
                currentPages[key] = 0;
            return currentPages[key];
        }

        private float GetElementHeight(string key, int index, SerializedProperty keyProp, SerializedProperty valueProp)
        {
            if (!elementHeights.ContainsKey(key))
                elementHeights[key] = new Dictionary<int, float>();
            
            if (!elementHeights[key].ContainsKey(index))
            {
                float keyHeight = EditorGUI.GetPropertyHeight(keyProp, GUIContent.none, true);
                float valueHeight = EditorGUI.GetPropertyHeight(valueProp, GUIContent.none, true);
                elementHeights[key][index] = Mathf.Max(ElementBaseHeight, Mathf.Max(keyHeight, valueHeight) + ElementPadding * 2);
            }
            
            return elementHeights[key][index];
        }

        private void ClearElementHeight(string key, int index)
        {
            if (elementHeights.ContainsKey(key) && elementHeights[key].ContainsKey(index))
                elementHeights[key].Remove(index);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            string propertyKey = property.propertyPath;
            EditorGUI.BeginProperty(position, label, property);

            var keysProperty = property.FindPropertyRelative("keys");
            var valuesProperty = property.FindPropertyRelative("values");
            
            // Header
            var headerRect = new Rect(position.x, position.y, position.width, HeaderHeight);
            var foldout = GetFoldoutState(propertyKey);
            
            // Draw header background
            EditorGUI.DrawRect(headerRect, new Color(0.2f, 0.2f, 0.2f, 0.1f));
            
            // Draw foldout and count
            var countLabel = $" [{keysProperty.arraySize}]";
            var labelWithCount = new GUIContent(label.text + countLabel, label.tooltip);
            foldout = EditorGUI.Foldout(headerRect, foldout, labelWithCount, true, EditorStyles.foldoutHeader);
            foldoutStates[propertyKey] = foldout;

            if (foldout)
            {
                EditorGUI.indentLevel++;
                
                // Toolbar
                var toolbarRect = new Rect(
                    position.x,
                    headerRect.yMax + ElementPadding,
                    position.width,
                    EditorGUIUtility.singleLineHeight
                );
                
                DrawToolbar(toolbarRect, keysProperty, valuesProperty, propertyKey);

                // Content area
                var contentRect = new Rect(
                    position.x,
                    toolbarRect.yMax + ElementPadding,
                    position.width,
                    GetContentHeight(propertyKey, keysProperty, valuesProperty)
                );

                DrawContent(contentRect, keysProperty, valuesProperty, propertyKey);

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        private void DrawToolbar(Rect rect, SerializedProperty keys, SerializedProperty values, string propertyKey)
        {
            var style = new GUIStyle(EditorStyles.toolbar);
            var toolbarRect = EditorGUI.IndentedRect(rect);
            
            GUI.Box(toolbarRect, GUIContent.none, style);

            // Calculate button rects
            float buttonWidth = 50f;
            var addRect = new Rect(toolbarRect.xMax - buttonWidth, toolbarRect.y, buttonWidth, toolbarRect.height);
            var clearRect = new Rect(addRect.x - buttonWidth - 2, toolbarRect.y, buttonWidth, toolbarRect.height);
            
            // Page navigation for large dictionaries
            if (keys.arraySize > ItemsPerPage)
            {
                int totalPages = Mathf.CeilToInt(keys.arraySize / (float)ItemsPerPage);
                int currentPage = GetCurrentPage(propertyKey);
                
                var pageStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter
                };
                
                var prevRect = new Rect(toolbarRect.x + 2, toolbarRect.y, 20, toolbarRect.height);
                var nextRect = new Rect(prevRect.xMax + 44, toolbarRect.y, 20, toolbarRect.height);
                var pageRect = new Rect(prevRect.xMax, toolbarRect.y, 40, toolbarRect.height);

                if (GUI.Button(prevRect, "<", EditorStyles.toolbarButton))
                    currentPages[propertyKey] = Mathf.Max(0, currentPage - 1);
                
                GUI.Label(pageRect, $"{currentPage + 1}/{totalPages}", pageStyle);
                
                if (GUI.Button(nextRect, ">", EditorStyles.toolbarButton))
                    currentPages[propertyKey] = Mathf.Min(totalPages - 1, currentPage + 1);
            }

            // Clear and Add buttons
            if (GUI.Button(clearRect, "Clear", EditorStyles.toolbarButton))
            {
                if (EditorUtility.DisplayDialog("Clear Dictionary", 
                    "Are you sure you want to clear all entries?", "Yes", "No"))
                {
                    keys.ClearArray();
                    values.ClearArray();
                    elementHeights[propertyKey]?.Clear();
                }
            }

            if (GUI.Button(addRect, "Add", EditorStyles.toolbarButton))
            {
                keys.arraySize++;
                values.arraySize++;
            }
        }

        private void DrawContent(Rect rect, SerializedProperty keys, SerializedProperty values, string propertyKey)
        {
            var contentRect = EditorGUI.IndentedRect(rect);
            var viewRect = new Rect(0, 0, contentRect.width - 16, GetTotalContentHeight(propertyKey, keys, values));
            
            // Begin scrollview
            scrollPositions[propertyKey] = GUI.BeginScrollView(
                contentRect, GetScrollPosition(propertyKey), viewRect);

            int startIndex = GetCurrentPage(propertyKey) * ItemsPerPage;
            int endIndex = Mathf.Min(startIndex + ItemsPerPage, keys.arraySize);

            float yOffset = 0;
            
            // Draw elements
            for (int i = startIndex; i < endIndex; i++)
            {
                var keyProp = keys.GetArrayElementAtIndex(i);
                var valueProp = values.GetArrayElementAtIndex(i);
                float elementHeight = GetElementHeight(propertyKey, i, keyProp, valueProp);
                
                var elementRect = new Rect(0, yOffset, viewRect.width, elementHeight);
                
                // Background
                if (i % 2 == 0)
                    EditorGUI.DrawRect(elementRect, new Color(0.5f, 0.5f, 0.5f, 0.1f));
                
                float fieldWidth = (viewRect.width - 30) / 2; // -30 for delete button and spacing
                
                var keyRect = new Rect(elementRect.x, elementRect.y + ElementPadding, fieldWidth, elementHeight - ElementPadding * 2);
                var valueRect = new Rect(keyRect.xMax + 10, elementRect.y + ElementPadding, fieldWidth, elementHeight - ElementPadding * 2);
                var deleteRect = new Rect(valueRect.xMax + 5, elementRect.y + ElementPadding, 20, 18);

                // Draw key and value
                EditorGUI.PropertyField(keyRect, keyProp, GUIContent.none, true);
                EditorGUI.PropertyField(valueRect, valueProp, GUIContent.none, true);
                
                // Delete button
                if (GUI.Button(deleteRect, "×"))
                {
                    keys.DeleteArrayElementAtIndex(i);
                    values.DeleteArrayElementAtIndex(i);
                    ClearElementHeight(propertyKey, i);
                    break;
                }

                yOffset += elementHeight;
            }

            GUI.EndScrollView();
        }

        private float GetContentHeight(string propertyKey, SerializedProperty keys, SerializedProperty values)
        {
            float totalHeight = 0;
            int startIndex = GetCurrentPage(propertyKey) * ItemsPerPage;
            int endIndex = Mathf.Min(startIndex + ItemsPerPage, keys.arraySize);
            
            for (int i = startIndex; i < endIndex; i++)
            {
                var keyProp = keys.GetArrayElementAtIndex(i);
                var valueProp = values.GetArrayElementAtIndex(i);
                totalHeight += GetElementHeight(propertyKey, i, keyProp, valueProp);
            }
            
            return totalHeight + ElementPadding * 2;
        }

        private float GetTotalContentHeight(string propertyKey, SerializedProperty keys, SerializedProperty values)
        {
            float totalHeight = 0;
            for (int i = 0; i < keys.arraySize; i++)
            {
                var keyProp = keys.GetArrayElementAtIndex(i);
                var valueProp = values.GetArrayElementAtIndex(i);
                totalHeight += GetElementHeight(propertyKey, i, keyProp, valueProp);
            }
            return totalHeight;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            string propertyKey = property.propertyPath;
            if (!GetFoldoutState(propertyKey))
                return HeaderHeight;

            var keysProperty = property.FindPropertyRelative("keys");
            var valuesProperty = property.FindPropertyRelative("values");
            return HeaderHeight + EditorGUIUtility.singleLineHeight + ElementPadding * 3 + 
                   GetContentHeight(propertyKey, keysProperty, valuesProperty);
        }
    }
} 