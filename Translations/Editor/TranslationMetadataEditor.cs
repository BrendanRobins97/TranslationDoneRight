using UnityEngine;
using UnityEditor;
using PSS;
using System.Linq;
using System.Collections.Generic;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using Sirenix.Utilities;

[CustomEditor(typeof(TranslationMetadata))]
public class TranslationMetadataEditor : OdinEditor
{
    private TranslationMetadata metadata;
    private bool showTextSources = false;
    private bool showTextContexts = false;
    private bool showCategories = false;
    private bool showCategoryTemplates = false;
    private bool showGroupMetadata = false;
    private bool showCustomMappings = false;
    private bool showExtractionSources = false;
    private string searchText = "";
    private Vector2 scrollPosition;

    private void OnEnable()
    {
        metadata = (TranslationMetadata)target;
    }

    public override void OnInspectorGUI()
    {
        // Draw Odin's default property tree for serialized properties
        // This ensures compatibility with SerializableDictionary and other Odin serialization
        base.OnInspectorGUI();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Enhanced Translation Metadata View", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        // Search field
        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Search:", GUILayout.Width(60));
        searchText = EditorGUILayout.TextField(searchText);
        if (GUILayout.Button("Clear", GUILayout.Width(60)))
        {
            searchText = "";
        }
        GUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // Begin scrollview
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        // Text Sources section
        showTextSources = EditorGUILayout.Foldout(showTextSources, "Text Sources", true);
        if (showTextSources)
        {
            DrawTextSources();
        }

        // Text Contexts section
        showTextContexts = EditorGUILayout.Foldout(showTextContexts, "Text Contexts", true);
        if (showTextContexts)
        {
            DrawTextContexts();
        }

        // Categories section
        showCategories = EditorGUILayout.Foldout(showCategories, "Text Categories", true);
        if (showCategories)
        {
            DrawCategories();
        }

        // Category Templates section
        showCategoryTemplates = EditorGUILayout.Foldout(showCategoryTemplates, "Category Templates", true);
        if (showCategoryTemplates)
        {
            DrawCategoryTemplates();
        }

        // Group Metadata section
        showGroupMetadata = EditorGUILayout.Foldout(showGroupMetadata, "Group Metadata", true);
        if (showGroupMetadata)
        {
            DrawGroupMetadata();
        }

        // Custom Language Mappings section
        showCustomMappings = EditorGUILayout.Foldout(showCustomMappings, "Custom Language Mappings", true);
        if (showCustomMappings)
        {
            DrawCustomLanguageMappings();
        }

        // Extraction Sources section
        showExtractionSources = EditorGUILayout.Foldout(showExtractionSources, "Extraction Sources", true);
        if (showExtractionSources)
        {
            DrawExtractionSources();
        }

        EditorGUILayout.EndScrollView();

        if (GUI.changed)
        {
            EditorUtility.SetDirty(target);
        }
    }

    private void DrawTextSources()
    {
        EditorGUI.indentLevel++;
        
        var allSources = metadata.GetAllSources();
        var filteredSources = FilterDictionary(allSources, searchText);
        
        EditorGUILayout.LabelField($"Total Text Sources: {filteredSources.Count}");
        EditorGUILayout.Space(5);
        
        foreach (var kvp in filteredSources)
        {
            string text = kvp.Key;
            List<TextSourceInfo> sources = kvp.Value;
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Sources: {sources.Count}");
            
            EditorGUI.indentLevel++;
            
            foreach (var source in sources)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{source.sourceType}:", GUILayout.Width(100));
                EditorGUILayout.LabelField(source.sourcePath);
                EditorGUILayout.EndHorizontal();
                
                if (!string.IsNullOrEmpty(source.objectPath))
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField($"Object: {source.objectPath}");
                    EditorGUI.indentLevel--;
                }
                
                if (!string.IsNullOrEmpty(source.componentName))
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField($"Component: {source.componentName}");
                    EditorGUI.indentLevel--;
                }
                
                if (!string.IsNullOrEmpty(source.fieldName))
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField($"Field: {source.fieldName}");
                    EditorGUI.indentLevel--;
                }
                
                EditorGUILayout.Space(5);
            }
            
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }
        
        EditorGUI.indentLevel--;
    }

    private void DrawTextContexts()
    {
        EditorGUI.indentLevel++;
        
        // Need to check if this dictionary exists in the TranslationMetadata class
        // Assuming there's a method to get contexts for all texts
        var contexts = new Dictionary<string, SerializableDictionary<string, string>>();
        var texts = metadata.GetAllSources().Keys.ToList();
        
        foreach (var text in texts)
        {
            var context = metadata.GetContext(text);
            if (context != null && context.Count > 0)
            {
                contexts[text] = context;
            }
        }
        
        var filteredContexts = FilterDictionary(contexts, searchText);
        
        EditorGUILayout.LabelField($"Total Texts with Context: {filteredContexts.Count}");
        EditorGUILayout.Space(5);
        
        foreach (var kvp in filteredContexts)
        {
            string text = kvp.Key;
            var contextDict = kvp.Value;
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Context Entries: {contextDict.Count}");
            
            EditorGUI.indentLevel++;
            
            foreach (var contextKvp in contextDict)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(contextKvp.Key, GUILayout.Width(120));
                EditorGUILayout.LabelField(contextKvp.Value);
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }
        
        EditorGUI.indentLevel--;
    }

    private void DrawCategories()
    {
        EditorGUI.indentLevel++;
        
        var categories = metadata.TextCategories;
        var filteredCategories = FilterDictionary(categories, searchText);
        
        EditorGUILayout.LabelField($"Total Categories: {filteredCategories.Count}");
        EditorGUILayout.Space(5);
        
        foreach (var kvp in filteredCategories)
        {
            string category = kvp.Key;
            StringList texts = kvp.Value;
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField(category, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Texts: {texts.Items.Count}");
            
            EditorGUI.indentLevel++;
            
            foreach (var text in texts.Items)
            {
                EditorGUILayout.LabelField(text);
            }
            
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }
        
        EditorGUI.indentLevel--;
    }

    private void DrawCategoryTemplates()
    {
        EditorGUI.indentLevel++;
        
        var templates = metadata.CategoryTemplates;
        var filteredTemplates = FilterDictionary(templates, searchText);
        
        EditorGUILayout.LabelField($"Total Templates: {filteredTemplates.Count}");
        EditorGUILayout.Space(5);
        
        foreach (var kvp in filteredTemplates)
        {
            string category = kvp.Key;
            CategoryTemplate template = kvp.Value;
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField(category, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Format: {template.format}");
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }
        
        EditorGUI.indentLevel--;
    }

    private void DrawGroupMetadata()
    {
        EditorGUI.indentLevel++;
        
        // This is a placeholder since we don't have direct access to all group metadata
        // You would need to modify TranslationMetadata to expose this data
        EditorGUILayout.LabelField("Group metadata display requires implementation of a method in TranslationMetadata to get all group metadata.");
        
        EditorGUI.indentLevel--;
    }

    private void DrawCustomLanguageMappings()
    {
        EditorGUI.indentLevel++;
        
        var mappings = metadata.CustomLanguageMappings;
        var filteredMappings = FilterDictionary(mappings, searchText);
        
        EditorGUILayout.LabelField($"Total Mappings: {filteredMappings.Count}");
        EditorGUILayout.Space(5);
        
        foreach (var kvp in filteredMappings)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(kvp.Key, GUILayout.Width(150));
            EditorGUILayout.LabelField("→", GUILayout.Width(20));
            EditorGUILayout.LabelField(kvp.Value);
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUI.indentLevel--;
    }

    private void DrawExtractionSources()
    {
        EditorGUI.indentLevel++;
        
        var sources = metadata.extractionSources;
        var filteredSources = sources;
        
        if (!string.IsNullOrEmpty(searchText))
        {
            filteredSources = sources.Where(s => s.ToString().Contains(searchText)).ToList();
        }
        
        EditorGUILayout.LabelField($"Total Extraction Sources: {filteredSources.Count}");
        EditorGUILayout.Space(5);
        
        foreach (var source in filteredSources)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(source.ToString());
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }
        
        EditorGUI.indentLevel--;
    }

    // Helper methods for filtering dictionaries based on search text
    private Dictionary<TKey, TValue> FilterDictionary<TKey, TValue>(Dictionary<TKey, TValue> dict, string searchText)
    {
        if (string.IsNullOrEmpty(searchText))
            return dict;
            
        return dict.Where(kvp => 
            kvp.Key.ToString().Contains(searchText) || 
            kvp.Value.ToString().Contains(searchText)
        ).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }
    
    private Dictionary<string, TValue> FilterDictionary<TValue>(SerializableDictionary<string, TValue> dict, string searchText)
    {
        if (string.IsNullOrEmpty(searchText))
            return dict.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            
        return dict.Where(kvp => 
            kvp.Key.Contains(searchText) || 
            kvp.Value.ToString().Contains(searchText)
        ).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }
} 