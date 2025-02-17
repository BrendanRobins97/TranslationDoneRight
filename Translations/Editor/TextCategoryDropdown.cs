#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;

namespace PSS
{
    public class TextCategoryDropdown
    {
        private bool isEditing;
        private string editingValue;

        private static Dictionary<int, Dictionary<int, bool>> isEditingStatesDictionary = new Dictionary<int, Dictionary<int, bool>>();
        private static Dictionary<int, Dictionary<int, string>> editingValuesDictionary = new Dictionary<int, Dictionary<int, string>>();
        
        private Dictionary<int, string> editingValues {
            get {
                if (!editingValuesDictionary.ContainsKey(guid)) {
                    editingValuesDictionary[guid] = new Dictionary<int, string>();
                }
                return editingValuesDictionary[guid];
            }
        }

        private Dictionary<int, bool> isEditingStates {
            get {
                if (!isEditingStatesDictionary.ContainsKey(guid)) {
                    isEditingStatesDictionary[guid] = new Dictionary<int, bool>();
                }
                return isEditingStatesDictionary[guid];
            }
        }

        private int guid;

        private Action<string> onEditted;
        public string Draw(string label, int sourceId, string textCategory, List<string> categories, UnityEngine.Object target, Action<string> onEditted)
        {
            this.guid = sourceId + label.GetHashCode();
            this.onEditted = onEditted;
            if (!isEditingStates.ContainsKey(sourceId))
            {
                isEditingStates[sourceId] = false;
                editingValues[sourceId] = textCategory;
            }

            isEditing = isEditingStates[sourceId];
            editingValue = editingValues[sourceId];

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{label}:", GUILayout.Width(150));

            if (isEditing)
            {
                textCategory = DrawEditMode(sourceId, textCategory, target, categories);
            }
            else
            {
                textCategory = DrawDropdownMode(sourceId, textCategory, target, categories);
            }

            EditorGUILayout.EndHorizontal();
            return textCategory;
        }

        private string DrawEditMode(int sourceId, string textCategory, UnityEngine.Object target, List<string> categories)
        {
            float buttonWidth = 50;
            float spacing = 5;

            // Text field for editing with a specific control name
            GUI.SetNextControlName("CategoryEditField");
            editingValue = EditorGUILayout.TextField(editingValue, 
                GUILayout.Width(300));

            // Update the stored editing value
            editingValues[sourceId] = editingValue;

            // Add/Save button
            if (GUILayout.Button("Save", GUILayout.Width(buttonWidth)))
            {
                if (string.IsNullOrEmpty(editingValue))
                {
                    textCategory = "";
                }
                else
                {
                    textCategory = editingValue;
                    editingValues[sourceId] = editingValue;

                    if (!categories.Contains(editingValue))
                    {
                        categories.Add(editingValue);
                        EditorUtility.SetDirty(target);
                    }

                    if (isEditingStates[sourceId])
                    {
                        onEditted?.Invoke(editingValue);
                    }
                }
                
                isEditingStates[sourceId] = false;
                GUI.FocusControl(null);
                EditorGUI.FocusTextInControl(null);
            }

            // Cancel button
            if (GUILayout.Button("Cancel", GUILayout.Width(buttonWidth)))
            {
                isEditingStates[sourceId] = false;
                editingValues[sourceId] = textCategory;
                GUI.FocusControl(null); // Clear focus explicitly
                EditorGUI.FocusTextInControl(null); // Also clear text focus
                EditorUtility.SetDirty(target);
            }
            return textCategory;
        }

        private string DrawDropdownMode(int sourceId, string textCategory, UnityEngine.Object target, List<string> categories)
        {
            var currentCategories = new List<string>(categories);
            
            // Handle empty/null textCategory
            bool isNoneSelected = string.IsNullOrEmpty(textCategory);
            int currentIndex = isNoneSelected ? -1 : currentCategories.IndexOf(textCategory);
            
            // Add "None" and "New Category" options at the beginning
            currentCategories.Insert(0, "(New Category)");
            currentCategories.Insert(0, "(None)");
            
            // Draw the dropdown (+2 because we added two items at the start)
            int selectedIndex = EditorGUILayout.Popup(
                isNoneSelected ? 0 : currentIndex + 2,
                currentCategories.ToArray(),
                GUILayout.Width(300)
            );

            // Handle selection
            if (selectedIndex == 0) // None selected
            {
                textCategory = "";
                EditorUtility.SetDirty(target);
            }
            else if (selectedIndex == 1) // New Category selected
            {
                isEditingStates[sourceId] = true;
                editingValues[sourceId] = textCategory;
                EditorUtility.SetDirty(target);
            }
            else if (selectedIndex > 1) // Existing category selected
            {
                string selectedCategory = currentCategories[selectedIndex];
                textCategory = selectedCategory;
                EditorUtility.SetDirty(target);
            }

            // Edit button (only show if a category is selected)
            if (!string.IsNullOrEmpty(textCategory) && GUILayout.Button("Edit", GUILayout.Width(40)))
            {
                isEditingStates[sourceId] = true;
                editingValues[sourceId] = textCategory;
            }
            return textCategory;
        }
    }
}
#endif 