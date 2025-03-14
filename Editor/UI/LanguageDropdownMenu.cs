using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

namespace Translations.UI.Editor
{
    public static class LanguageDropdownMenu
    {
        [MenuItem("GameObject/UI/Translations/Language Dropdown - TMP", false, 10)]
        static void CreateTMPLanguageDropdown(MenuCommand menuCommand)
        {
            // Create a new GameObject with required components
            GameObject go = new GameObject("Language Dropdown");
            
            // Register creation for undo
            Undo.RegisterCreatedObjectUndo(go, "Create Language Dropdown");
            
            // Add RectTransform component
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(160, 30);
            
            // Add TMP_Dropdown component
            TMP_Dropdown dropdown = go.AddComponent<TMP_Dropdown>();
            
            // Add our LanguageDropdown component
            go.AddComponent<LanguageDropdown>();
            
            // Set up parent-child relationship
            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
            
            // Make the dropdown the focused object
            Selection.activeObject = go;
        }
        
        [MenuItem("GameObject/UI/Translations/Language Dropdown - Legacy", false, 11)]
        static void CreateLegacyLanguageDropdown(MenuCommand menuCommand)
        {
            // Create a new GameObject with required components
            GameObject go = new GameObject("Language Dropdown");
            
            // Register creation for undo
            Undo.RegisterCreatedObjectUndo(go, "Create Language Dropdown");
            
            // Add RectTransform component
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(160, 30);
            
            // Add Legacy Dropdown component
            Dropdown dropdown = go.AddComponent<Dropdown>();
            
            // Add our LanguageDropdown component
            go.AddComponent<LanguageDropdown>();
            
            // Set up parent-child relationship
            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
            
            // Make the dropdown the focused object
            Selection.activeObject = go;
        }
    }
} 