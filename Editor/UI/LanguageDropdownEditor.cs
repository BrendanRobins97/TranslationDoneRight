using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

namespace Translations.UI.Editor
{
    [CustomEditor(typeof(LanguageDropdown))]
    public class LanguageDropdownEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var dropdown = target as LanguageDropdown;
            var gameObject = dropdown.gameObject;
            
            bool hasTMPDropdown = gameObject.GetComponent<TMP_Dropdown>() != null;
            bool hasLegacyDropdown = gameObject.GetComponent<Dropdown>() != null;
            
            if (!hasTMPDropdown && !hasLegacyDropdown)
            {
                EditorGUILayout.HelpBox(
                    "This component requires either a TMP_Dropdown or legacy Dropdown component.",
                    MessageType.Warning
                );
                
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("Add TMP Dropdown"))
                {
                    Undo.RecordObject(gameObject, "Add TMP Dropdown");
                    gameObject.AddComponent<TMP_Dropdown>();
                }
                
                if (GUILayout.Button("Add Legacy Dropdown"))
                {
                    Undo.RecordObject(gameObject, "Add Legacy Dropdown");
                    gameObject.AddComponent<Dropdown>();
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            DrawDefaultInspector();
            
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "This component automatically populates the dropdown with available languages and handles language switching.",
                MessageType.Info
            );
        }
    }
} 