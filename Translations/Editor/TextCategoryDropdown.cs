#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace PSS
{
    public class TextCategoryDropdown
    {
        private bool isEditing;
        private string editingValue;
        private readonly TranslationMetadata metadata;
        private readonly TextSourceInfo sourceInfo;
        private static Dictionary<int, bool> isEditingStates = new Dictionary<int, bool>();
        private static Dictionary<int, string> editingValues = new Dictionary<int, string>();

        public TextCategoryDropdown(TranslationMetadata metadata, TextSourceInfo sourceInfo)
        {
            this.metadata = metadata;
            this.sourceInfo = sourceInfo;
            
            int sourceId = sourceInfo.GetHashCode();
            if (sourceInfo.textCategory == null || sourceInfo.textCategory == ""){
                sourceInfo.textCategory = "UI";
            }
            if (!isEditingStates.ContainsKey(sourceId))
            {
                isEditingStates[sourceId] = false;
                editingValues[sourceId] = sourceInfo.textCategory.ToString();
            }
        }

        public void Draw()
        {
            int sourceId = sourceInfo.GetHashCode();

            if (!isEditingStates.ContainsKey(sourceId))
            {
                isEditingStates[sourceId] = false;
                editingValues[sourceId] = sourceInfo.textCategory.ToString();
            }

            isEditing = isEditingStates[sourceId];
            editingValue = editingValues[sourceId];

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Category:", GUILayout.Width(150));

            if (isEditing)
            {
                DrawEditMode(sourceId);
            }
            else
            {
                DrawDropdownMode(sourceId);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawEditMode(int sourceId)
        {
            float buttonWidth = 50;
            float spacing = 5;

            // Text field for editing
            editingValue = EditorGUILayout.TextField(editingValue, 
                GUILayout.Width(EditorGUIUtility.currentViewWidth - 300));

            // Update the stored editing value
            editingValues[sourceId] = editingValue;

            // Add/Save button
            if (GUILayout.Button("Save", GUILayout.Width(buttonWidth)))
            {
                if (!string.IsNullOrEmpty(editingValue))
                {
                    string oldCategory = sourceInfo.textCategory.ToString();
                    
                    // Add to categories if it doesn't exist
                    metadata.AddTextCategory(editingValue);
                    
                    sourceInfo.textCategory = editingValue;
                    editingValues[sourceId] = editingValue.ToString();

                    // Update any other sources using the old category
                    metadata.UpdateTextCategory(oldCategory, editingValue);
                    
                    EditorUtility.SetDirty(metadata);
                }
                
                isEditingStates[sourceId] = false;
            }

            // Cancel button
            if (GUILayout.Button("Cancel", GUILayout.Width(buttonWidth)))
            {
                isEditingStates[sourceId] = false;
                editingValues[sourceId] = sourceInfo.textCategory.ToString();
            }
        }

        private void DrawDropdownMode(int sourceId)
        {
            var categories = new List<string>(metadata.TextCategories);
            int currentIndex = categories.IndexOf(sourceInfo.textCategory.ToString());
            
            // Add "New Category" option at the beginning
            categories.Insert(0, "(New Category)");
            
            // Draw the dropdown
            int selectedIndex = EditorGUILayout.Popup(
                currentIndex + 1, // +1 because we added "New Category" at the start
                categories.ToArray(),
                GUILayout.Width(EditorGUIUtility.currentViewWidth - 300)
            );

            // Handle selection
            if (selectedIndex == 0) // New Category selected
            {
                isEditingStates[sourceId] = true;
                editingValues[sourceId] = sourceInfo.textCategory.ToString();
            }
            else if (selectedIndex > 0) // Existing category selected
            {
                string selectedCategory = categories[selectedIndex];
                sourceInfo.textCategory = selectedCategory;
            }

            // Edit button
            if (GUILayout.Button("Edit", GUILayout.Width(40)))
            {
                isEditingStates[sourceId] = true;
                editingValues[sourceId] = sourceInfo.textCategory.ToString();
            }
        }
    }
}
#endif 