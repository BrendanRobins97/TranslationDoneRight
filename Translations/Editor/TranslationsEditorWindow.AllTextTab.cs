using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PSS
{
    public partial class TranslationsEditorWindow
    {
        private TextViewMode currentTextViewMode = TextViewMode.Detailed;
        private const float KEY_ROW_HEIGHT = 20f; // Height of each key row
        private const int VISIBLE_ROWS = 30; // Increased from 15 to 30 visible rows
        private float cachedTotalHeight; // Cache for total content height
        private List<string> filteredKeysList = new List<string>(); // Cache for filtered keys

        private const float BASE_COLUMN_WIDTH = 150f;
        private const float BASE_ROW_HEIGHT = 22f;
        private const float BASE_FONT_SIZE = 9f;
        private const float ROW_SPACING = 1f;
        private const int BASE_VISIBLE_GRID_ROWS = 30;
        private const int MAX_VISIBLE_GRID_ROWS = 60;

        private Dictionary<string, float> rowHeights = new Dictionary<string, float>();
        private const float MIN_ROW_HEIGHT = 22f;
        private const float MAX_ROW_HEIGHT = 150f;

        private bool showSourceInformation = true;  // Add this field at the top with other state variables

        private void DrawAllTextTab()
        {
            // Header
            EditorGUILayout.Space(10);
            GUILayout.Label("Translation Management", EditorGUIStyleUtility.HeaderLabel);
            EditorGUILayout.Space(5);

            // Controls Section
            using (new EditorGUILayout.VerticalScope(EditorGUIStyleUtility.CardStyle))
            {
                // View Controls Row
                using (new EditorGUILayout.HorizontalScope())
                {
                    // Left side - View Mode
                    using (new EditorGUILayout.HorizontalScope(GUILayout.Width(position.width / 2 - 15)))
                    {
                        EditorGUILayout.LabelField("View Mode:", GUILayout.Width(70));
                        TextViewMode newMode = (TextViewMode)EditorGUILayout.EnumPopup(currentTextViewMode, GUILayout.Width(100));
                        if (newMode != currentTextViewMode)
                        {
                            currentTextViewMode = newMode;
                            selectedKey = null;
                        }
                    }

                    GUILayout.Space(10);

                    // Right side - Scale (only for grid view)
                    using (new EditorGUILayout.HorizontalScope(GUILayout.Width(position.width / 2 - 15)))
                    {
                        if (currentTextViewMode == TextViewMode.Grid)
                        {
                            EditorGUILayout.LabelField("Scale:", GUILayout.Width(45));
                            float newScale = EditorGUILayout.Slider(gridViewScale, 0.5f, 1.5f);
                            if (newScale != gridViewScale)
                            {
                                gridViewScale = newScale;
                                SaveEditorPrefs();
                            }
                        }
                    }
                }

                EditorGUILayout.Space(5);

                // Filters Row
                using (new EditorGUILayout.HorizontalScope())
                {
                    // Left side - Search
                    using (new EditorGUILayout.HorizontalScope(GUILayout.Width(position.width / 2 - 15)))
                    {
                        string newSearchFilter = EditorGUILayout.TextField(
                            new GUIContent("Search:", "Filter translation keys by text"),
                            searchFilter
                        );
                        if (newSearchFilter != searchFilter)
                        {
                            searchFilter = newSearchFilter;
                            UpdateFilteredKeys();
                        }
                    }

                    GUILayout.Space(10);

                    // Right side - Toggle Filters
                    using (new EditorGUILayout.HorizontalScope(GUILayout.Width(position.width / 2 - 15)))
                    {
                        showMissingOnly = EditorGUILayout.ToggleLeft(
                            new GUIContent("Missing Only", "Show only keys with missing translations"),
                            showMissingOnly,
                            GUILayout.Width(100)
                        );
                        showUnusedOnly = EditorGUILayout.ToggleLeft(
                            new GUIContent("Unused Only", "Show only unused translation keys"),
                            showUnusedOnly,
                            GUILayout.Width(100)
                        );
                    }
                }
            }

            EditorGUILayout.Space(10);

            // Main Content Area
            switch (currentTextViewMode)
            {
                case TextViewMode.Detailed:
                    DrawDetailedView();
                    break;
                case TextViewMode.Grid:
                    DrawGridView();
                    break;
            }
        }

        private void DrawDetailedView()
        {
            // Split view with keys list and translation details
            EditorGUILayout.BeginHorizontal();

            // Left Panel - Keys List
            using (new EditorGUILayout.VerticalScope(EditorGUIStyleUtility.CardStyle, GUILayout.Width(300)))
            {
                EditorGUILayout.LabelField("Available Keys", EditorStyles.boldLabel);
                DrawKeysList();
            }

            GUILayout.Space(10);

            // Right Panel - Translation Details
            using (new EditorGUILayout.VerticalScope(EditorGUIStyleUtility.CardStyle))
            {
                if (selectedKey != null)
                {
                    DrawTranslationsForKey();
                }
                else
                {
                    EditorGUILayout.LabelField("Select a key to edit translations", EditorStyles.centeredGreyMiniLabel);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawGridView()
        {
            if (translationData == null || translationData.allKeys == null) return;

            // Apply scale to measurements
            float columnWidth = Mathf.Round(BASE_COLUMN_WIDTH * gridViewScale);
            float baseRowHeight = Mathf.Round(BASE_ROW_HEIGHT * gridViewScale);
            float rowSpacing = Mathf.Round(ROW_SPACING * gridViewScale);
            int fontSize = Mathf.RoundToInt(BASE_FONT_SIZE * gridViewScale);

            // Calculate visible rows based on zoom
            int visibleGridRows = Mathf.RoundToInt(Mathf.Clamp(
                BASE_VISIBLE_GRID_ROWS / gridViewScale,
                BASE_VISIBLE_GRID_ROWS,
                MAX_VISIBLE_GRID_ROWS
            ));

            // Filter keys based on search
            var filteredKeys = translationData.allKeys
                .Where(k => string.IsNullOrEmpty(searchFilter) || k.ToLower().Contains(searchFilter.ToLower()))
                .ToList();

            using (new EditorGUILayout.VerticalScope(EditorGUIStyleUtility.CardStyle))
            {
                // Grid Header
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    DrawGridHeader(new Rect(0, 0, position.width - 20, baseRowHeight), columnWidth, fontSize);
                }

                // Grid Content
                float viewHeight = position.height * 0.7f - baseRowHeight;
                Rect contentRect = GUILayoutUtility.GetRect(0, viewHeight);
                
                // Calculate total content height and width
                float totalContentHeight = 0;
                foreach (var key in filteredKeys)
                {
                    float rowHeight = rowHeights.TryGetValue(key, out float height) ? height : baseRowHeight;
                    totalContentHeight += rowHeight + rowSpacing;
                }
                float totalContentWidth = columnWidth * (translationData.supportedLanguages.Count);

                // Begin scroll view with virtual space
                Rect virtualRect = new Rect(0, 0, totalContentWidth, totalContentHeight);
                gridScrollPosition = GUI.BeginScrollView(contentRect, gridScrollPosition, virtualRect);

                float currentY = 0;
                var baseTextStyle = CreateGridTextStyle(fontSize);

                // Draw only visible rows
                for (int i = 0; i < filteredKeys.Count; i++)
                {
                    string key = filteredKeys[i];
                    float rowHeight = rowHeights.TryGetValue(key, out float height) ? height : baseRowHeight;
                    Rect rowRect = new Rect(0, currentY, totalContentWidth, rowHeight);

                    // Check if row is visible in scroll view
                    if (rowRect.yMax >= gridScrollPosition.y && rowRect.y <= gridScrollPosition.y + contentRect.height)
                    {
                        float newHeight = DrawGridViewRow(key, i, rowRect, columnWidth, rowHeight, baseTextStyle);
                        if (Mathf.Abs(newHeight - rowHeight) > 1)
                        {
                            rowHeights[key] = Mathf.Clamp(newHeight, MIN_ROW_HEIGHT * gridViewScale, MAX_ROW_HEIGHT * gridViewScale);
                            Repaint();
                        }
                    }

                    currentY += rowHeight + rowSpacing;
                }

                GUI.EndScrollView();
            }
        }

        private void DrawGridHeader(Rect headerRect, float columnWidth, int fontSize)
        {
            // Scale the header height
            float scaledHeaderHeight = BASE_ROW_HEIGHT * gridViewScale;
            
            var headerStyle = new GUIStyle(EditorStyles.toolbarButton)
            {
                fontSize = fontSize,
                alignment = TextAnchor.MiddleLeft,
                fixedHeight = scaledHeaderHeight  // Set fixed height to match scale
            };

            float xOffset = headerRect.x;  // Start from the provided x position
            
            // English column
            GUI.Label(new Rect(xOffset, 0, columnWidth, scaledHeaderHeight), "English", headerStyle);
            xOffset += columnWidth;

            // Other language columns
            foreach (var language in translationData.supportedLanguages.Skip(1))
            {
                GUI.Label(new Rect(xOffset, 0, columnWidth, scaledHeaderHeight), language, headerStyle);
                xOffset += columnWidth;
            }
        }

        private GUIStyle CreateGridTextStyle(int fontSize)
        {
            return new GUIStyle(EditorStyles.textArea)
            {
                fontSize = fontSize,
                wordWrap = true,
                padding = new RectOffset(
                    Mathf.RoundToInt(2 * gridViewScale),
                    Mathf.RoundToInt(2 * gridViewScale),
                    Mathf.RoundToInt(1 * gridViewScale),
                    Mathf.RoundToInt(1 * gridViewScale)
                )
            };
        }

        private float DrawGridViewRow(string key, int rowIndex, Rect rowRect, float columnWidth, float rowHeight, GUIStyle baseTextStyle)
        {
            float xOffset = 0;
            float maxHeight = MIN_ROW_HEIGHT * gridViewScale;  // Start with minimum height

            // Draw row background
            if (rowIndex % 2 == 0)
            {
                EditorGUI.DrawRect(rowRect, new Color(0.5f, 0.5f, 0.5f, 0.1f));
            }

            // First pass: Calculate maximum height needed for all cells
            // English column height
            var readOnlyStyle = new GUIStyle(baseTextStyle)
            {
                richText = true,
                normal = { textColor = new Color(0.5f, 0.5f, 0.5f, 1f) }
            };
            var keyContent = new GUIContent(key);
            float keyHeight = readOnlyStyle.CalcHeight(keyContent, columnWidth - 5);
            maxHeight = Mathf.Max(maxHeight, keyHeight);

            // Other languages height calculation
            for (int i = 0; i < translationData.languageDataDictionary.Length; i++)
            {
                var assetRef = translationData.languageDataDictionary[i];
                string assetPath = AssetDatabase.GUIDToAssetPath(assetRef.AssetGUID);
                LanguageData languageData = AssetDatabase.LoadAssetAtPath<LanguageData>(assetPath);
                int keyIndex = translationData.allKeys.IndexOf(key);
                
                if (languageData != null && keyIndex >= 0 && keyIndex < languageData.allText.Count)
                {
                    string translation = languageData.allText[keyIndex];
                    var content = new GUIContent(translation);
                    float cellHeight = baseTextStyle.CalcHeight(content, columnWidth - 5);
                    maxHeight = Mathf.Max(maxHeight, cellHeight);
                }
            }

            // Clamp the maximum height
            maxHeight = Mathf.Clamp(maxHeight, MIN_ROW_HEIGHT * gridViewScale, MAX_ROW_HEIGHT * gridViewScale);

            // Second pass: Draw all cells with the same height
            // Draw English column
            EditorGUI.SelectableLabel(
                new Rect(xOffset, rowRect.y, columnWidth, maxHeight),
                key,
                readOnlyStyle
            );
            xOffset += columnWidth;

            // Draw other languages
            for (int i = 0; i < translationData.languageDataDictionary.Length; i++)
            {
                DrawGridViewCell(
                    new Rect(xOffset, rowRect.y, columnWidth, maxHeight),
                    i,
                    key,
                    baseTextStyle
                );
                xOffset += columnWidth;
            }

            return maxHeight;
        }

        private float DrawGridViewCell(Rect cellRect, int languageIndex, string key, GUIStyle baseTextStyle)
        {
            var assetRef = translationData.languageDataDictionary[languageIndex];
            string assetPath = AssetDatabase.GUIDToAssetPath(assetRef.AssetGUID);
            LanguageData languageData = AssetDatabase.LoadAssetAtPath<LanguageData>(assetPath);

            int keyIndex = translationData.allKeys.IndexOf(key);
            string translation = "";
            
            if (languageData != null && keyIndex >= 0 && keyIndex < languageData.allText.Count)
            {
                translation = languageData.allText[keyIndex];
            }

            EditorGUI.BeginChangeCheck();
            string newTranslation = EditorGUI.TextArea(
                cellRect,
                translation,
                baseTextStyle
            );
            
            if (EditorGUI.EndChangeCheck() && languageData != null)
            {
                Undo.RecordObject(languageData, "Update Translation");
                languageData.allText[keyIndex] = newTranslation;
                EditorUtility.SetDirty(languageData);
                isDirty = true;
                lastEditTime = EditorApplication.timeSinceStartup;

                // Force repaint to update the layout immediately
                Repaint();
            }

            return cellRect.height;
        }

        private void DrawKeysList()
        {
            // Get filtered keys if search filter changed or list not initialized
            if (filteredKeysList == null || filteredKeysList.Count == 0)
            {
                filteredKeysList = translationData.allKeys
                    .Where(k => string.IsNullOrEmpty(searchFilter) || 
                               k.ToLower().Contains(searchFilter.ToLower()))
                    .ToList();
            }

            // Calculate total height of all items
            cachedTotalHeight = filteredKeysList.Count * KEY_ROW_HEIGHT;

            // Begin scrollable area with total height
            Rect scrollViewRect = GUILayoutUtility.GetRect(0, position.height * 0.7f);
            Rect contentRect = new Rect(0, 0, scrollViewRect.width - 16, cachedTotalHeight); // 16 is scrollbar width

            textScrollPosition = GUI.BeginScrollView(scrollViewRect, textScrollPosition, contentRect);

            if (filteredKeysList.Count > 0)
            {
                // Calculate which items should be visible
                float scrollY = textScrollPosition.y;
                int startIndex = Mathf.Max(0, Mathf.FloorToInt(scrollY / KEY_ROW_HEIGHT));
                int endIndex = Mathf.Min(filteredKeysList.Count, startIndex + VISIBLE_ROWS);

                // Draw only visible items 
                for (int i = startIndex; i < endIndex; i++)
                {
                    Rect itemRect = new Rect(0, i * KEY_ROW_HEIGHT, contentRect.width, KEY_ROW_HEIGHT);

                    // Skip if item is not visible in scroll view
                    if (itemRect.yMax < scrollY || itemRect.y > scrollY + scrollViewRect.height)
                        continue;

                    string key = filteredKeysList[i];
                    bool isSelected = key == selectedKey;

                    // Draw selection background
                    if (isSelected)
                    {
                        EditorGUI.DrawRect(itemRect, new Color(0.3f, 0.5f, 0.7f, 0.3f));
                    }
                    else if (i % 2 == 0)
                    {
                        EditorGUI.DrawRect(itemRect, new Color(0.5f, 0.5f, 0.5f, 0.1f));
                    }

                    // Draw the toggle
                    bool newSelected = EditorGUI.ToggleLeft(itemRect, key, isSelected);
                    if (newSelected != isSelected)
                    {
                        selectedKey = newSelected ? key : null;
                        Repaint();
                    }

                    // Handle mouse events for selection
                    if (Event.current.type == EventType.MouseDown && itemRect.Contains(Event.current.mousePosition))
                    {
                        if (Event.current.button == 0) // Left click
                        {
                            selectedKey = key;
                            GUI.changed = true;
                            Event.current.Use();
                        }
                    }
                }
            }
            else
            {
                EditorGUI.LabelField(new Rect(0, 0, contentRect.width, KEY_ROW_HEIGHT), "No matching keys found");
            }

            GUI.EndScrollView();

            // Handle keyboard navigation
            if (Event.current.type == EventType.KeyDown)
            {
                bool handled = false;
                int currentIndex = selectedKey != null ? filteredKeysList.IndexOf(selectedKey) : -1;

                switch (Event.current.keyCode)
                {
                    case KeyCode.UpArrow:
                        if (currentIndex > 0)
                        {
                            selectedKey = filteredKeysList[currentIndex - 1];
                            handled = true;
                        }
                        break;

                    case KeyCode.DownArrow:
                        if (currentIndex < filteredKeysList.Count - 1)
                        {
                            selectedKey = filteredKeysList[currentIndex + 1];
                            handled = true;
                        }
                        break;

                    case KeyCode.Home:
                        if (filteredKeysList.Count > 0)
                        {
                            selectedKey = filteredKeysList[0];
                            handled = true;
                        }
                        break;

                    case KeyCode.End:
                        if (filteredKeysList.Count > 0)
                        {
                            selectedKey = filteredKeysList[filteredKeysList.Count - 1];
                            handled = true;
                        }
                        break;
                }

                if (handled)
                {
                    // Ensure selected item is visible
                    if (selectedKey != null)
                    {
                        int selectedIndex = filteredKeysList.IndexOf(selectedKey);
                        float targetY = selectedIndex * KEY_ROW_HEIGHT;
                        float viewHeight = position.height * 0.7f;
                        
                        if (targetY < textScrollPosition.y)
                        {
                            textScrollPosition.y = targetY;
                        }
                        else if (targetY > textScrollPosition.y + viewHeight - KEY_ROW_HEIGHT)
                        {
                            textScrollPosition.y = targetY - viewHeight + KEY_ROW_HEIGHT;
                        }
                    }

                    Event.current.Use();
                    Repaint();
                }
            }
        }

        // Update filtered keys when search filter changes
        private void UpdateFilteredKeys()
        {
            filteredKeysList = translationData.allKeys
                .Where(k => string.IsNullOrEmpty(searchFilter) || 
                           k.ToLower().Contains(searchFilter.ToLower()))
                .ToList();
        }

        private void DrawTranslationsForKey()
        {
            if (selectedKey == null) return;

            EditorGUILayout.LabelField($"Translations for: {selectedKey}", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Get the index of the selected key
            int keyIndex = translationData.allKeys.IndexOf(selectedKey);
            if (keyIndex == -1)
            {
                EditorGUILayout.HelpBox("Selected key not found in translation data.", MessageType.Error);
                return;
            }

            // Show English (original) text first
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("English", GUILayout.Width(150));
            GUI.enabled = false;
            EditorGUILayout.TextField(selectedKey);
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Details Section (Source Info and Context)
            showSourceInformation = EditorGUILayout.Foldout(showSourceInformation, "Details", true, EditorGUIStyleUtility.FoldoutHeader);
            if (showSourceInformation)
            {
                EditorGUI.indentLevel++;

                // Source Information
                EditorGUILayout.LabelField("Source Information", EditorStyles.boldLabel);
                var sources = translationData.Metadata.GetSources(selectedKey);
                if (sources.Count > 0)
                {
                    foreach (var source in sources)
                    {
                        // Source type with icon and path
                        string iconName = source.sourceType switch
                        {
                            TextSourceType.Scene => "SceneAsset Icon",
                            TextSourceType.Prefab => "Prefab Icon",
                            TextSourceType.Script => "cs Script Icon",
                            TextSourceType.ScriptableObject => "ScriptableObject Icon",
                            _ => "TextAsset Icon"
                        };

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            // Icon and type
                            GUILayout.Label(EditorGUIUtility.IconContent(iconName), GUILayout.Width(20), GUILayout.Height(18));
                            EditorGUILayout.LabelField($"{source.sourceType}", EditorStyles.boldLabel);
                        }

                        EditorGUI.indentLevel++;

                        // Source path (clickable if asset exists)
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.PrefixLabel("Source");
                            if (GUILayout.Button(source.sourcePath, EditorStyles.linkLabel))
                            {
                                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(source.sourcePath);
                                if (asset != null)
                                {
                                    Selection.activeObject = asset;
                                    EditorGUIUtility.PingObject(asset);
                                }
                            }
                        }

                        // Object path if it exists
                        if (!string.IsNullOrEmpty(source.objectPath))
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                EditorGUILayout.PrefixLabel("Object Path");
                                EditorGUILayout.LabelField(source.objectPath);
                            }
                        }

                        // Component and field info
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.PrefixLabel("Component");
                            EditorGUILayout.LabelField(source.componentName);
                        }
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.PrefixLabel("Field");
                            EditorGUILayout.LabelField(source.fieldName);
                        }

                        // Inactive state if relevant
                        if (source.wasInactive)
                        {
                            EditorGUILayout.LabelField("State: Inactive", EditorGUIStyleUtility.WarningLabelStyle);
                        }

                        EditorGUI.indentLevel--;
                        EditorGUILayout.Space(5);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No source information available for this key.", MessageType.Info);
                }

                EditorGUILayout.Space(10);

                // Context Information
                EditorGUILayout.LabelField("Context Information", EditorStyles.boldLabel);
                var context = translationData.Metadata.GetContext(selectedKey);

                // Context categories
                foreach (string key in translationData.Metadata.TextCategories.Keys)
                {
                    if (!context.ContainsKey(key))
                    {
                        context[key] = "";
                    }
                    string oldCategory = context[key];
                    var categoryDropdown = new TextCategoryDropdown();
                    if (!translationData.Metadata.TextCategories.ContainsKey(key))
                    {
                        translationData.Metadata.TextCategories[key] = new List<string>();
                    }
                    string newValue = categoryDropdown.Draw(key, selectedKey.GetHashCode(), context[key], translationData.Metadata.TextCategories[key], translationData, (newCategory) => {
                        if (newCategory != oldCategory)
                        {
                            translationData.Metadata.UpdateTextCategory(key, oldCategory, newCategory);
                        }
                    });
                    
                    if (newValue != oldCategory)
                    {
                        translationData.Metadata.UpdateContext(selectedKey, key, newValue);
                        EditorUtility.SetDirty(translationData);
                    }
                }

                // Category management section
                EditorGUILayout.Space(5);
                showCategoryManagement = EditorGUILayout.Foldout(showCategoryManagement, "Category Management", true);
                if (showCategoryManagement)
                {
                    EditorGUI.indentLevel++;
                    
                    // Add new category
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PrefixLabel("Add New Category");
                        newCategoryName = EditorGUILayout.TextField(newCategoryName);
                        if (GUILayout.Button("Add", GUILayout.Width(60)) && !string.IsNullOrWhiteSpace(newCategoryName))
                        {
                            if (!translationData.Metadata.TextCategories.ContainsKey(newCategoryName))
                            {
                                translationData.Metadata.TextCategories[newCategoryName] = new List<string>();
                                context[newCategoryName] = "";
                                translationData.Metadata.SetContext(selectedKey, context);
                                newCategoryName = "";
                                GUI.FocusControl(null);
                                EditorUtility.SetDirty(translationData);
                            }
                        }
                    }

                    // Remove categories
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Remove Categories:");
                    List<string> categoriesToRemove = new List<string>();

                    foreach (var category in translationData.Metadata.TextCategories.Keys)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(category, GUILayout.ExpandWidth(true));
                            if (GUILayout.Button("Remove", GUILayout.Width(60)))
                            {
                                if (EditorUtility.DisplayDialog("Remove Category",
                                    $"Are you sure you want to remove the category '{category}'?", "Remove", "Cancel"))
                                {
                                    categoriesToRemove.Add(category);
                                }
                            }
                        }
                    }

                    // Process removals after the loop
                    foreach (var category in categoriesToRemove)
                    {
                        translationData.Metadata.TextCategories.Remove(category);
                        context.Remove(category);
                        translationData.Metadata.SetContext(selectedKey, context);
                        EditorUtility.SetDirty(translationData);
                    }

                    EditorGUI.indentLevel--;
                }

                // Main context field
                EditorGUILayout.LabelField("Additional Context:");
                string newManualContext = EditorGUILayout.TextArea(
                    context.ContainsKey("Manual") ? context["Manual"] : "", 
                    GUILayout.Height(60)
                );
                if (newManualContext != (context.ContainsKey("Manual") ? context["Manual"] : ""))
                {
                    translationData.Metadata.UpdateContext(selectedKey, "Manual", newManualContext);
                    EditorUtility.SetDirty(translationData);
                }

                // Show auto-generated context
                string translationContext = translationData.Metadata.GetTranslationContext(selectedKey);
                if (!string.IsNullOrEmpty(translationContext))
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Combined Context:", EditorStyles.boldLabel);
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.TextArea(translationContext, GUILayout.Height(40));
                    EditorGUI.EndDisabledGroup();
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // Parameters section
            EditorGUILayout.LabelField("Parameters", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Parameters allow you to insert dynamic values into your translations using {paramName} syntax. There are three ways to use parameters:\n\n" +
                "1. Direct code usage:\n" +
                "   TranslationManager.Translate(key, (\"paramName\", value));\n" +
                "   Example: TranslationManager.Translate(\"Hello {playerName}!\", (\"playerName\", player.Name));\n\n" +
                "2. Extension method:\n" +
                "   Translations.Translate(key, (\"paramName\", value));\n\n" +
                "3. UI Components:\n" +
                "   - Add TranslatedTMP component to your TextMeshPro object\n" +
                "   - Set parameters in the inspector or via SetText() method\n" +
                "   - Use tmpText.SetTextTranslated() extension method", 
                EditorStyles.wordWrappedMiniLabel
            );
            EditorGUILayout.Space(5);

            var parameters = translationData.GetKeyParameters(selectedKey);
            EditorGUI.BeginChangeCheck();
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Show current parameters
            for (int i = 0; i < parameters.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                parameters[i] = EditorGUILayout.TextField(parameters[i]);
                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    parameters.RemoveAt(i);
                    i--;
                }
                EditorGUILayout.EndHorizontal();
            }

            // Add new parameter button
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Add Parameter", GUILayout.Width(100)))
                {
                    parameters.Add($"param{parameters.Count}");
                }
                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.EndVertical();

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(translationData, "Update Translation Parameters");
                translationData.keyParameters[selectedKey] = parameters;
                EditorUtility.SetDirty(translationData);
            }

            EditorGUILayout.Space(10);

            // Parameter usage hint
            if (parameters.Count > 0)
            {
                EditorGUILayout.HelpBox($"Available parameters: {string.Join(", ", parameters.Select(p => $"{{{p}}}"))}", MessageType.Info);
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Translations", EditorStyles.boldLabel);

            // Show translations for other languages
            for (int i = 0; i < translationData.languageDataDictionary.Length; i++)
            {
                string language = translationData.supportedLanguages[i + 1]; // +1 to skip English
                var assetRef = translationData.languageDataDictionary[i];
                
                string assetPath = AssetDatabase.GUIDToAssetPath(assetRef.AssetGUID);
                LanguageData languageData = AssetDatabase.LoadAssetAtPath<LanguageData>(assetPath);

                if (languageData != null && keyIndex < languageData.allText.Count)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(language, GUILayout.Width(150));

                    EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                    
                    var translationEntry = languageData.allText[keyIndex];
                    EditorGUI.BeginChangeCheck();

                    string newTranslation = EditorGUIStyleUtility.DrawExpandingTextArea(
                        translationEntry,
                        EditorGUIUtility.currentViewWidth
                    );
                    
                    // Check parameters inline
                    var usedParameters = ExtractParametersFromText(translationEntry);
                    var missingParameters = parameters.Except(usedParameters).ToList();
                    var extraParameters = usedParameters.Except(parameters).ToList();

                    if (missingParameters.Any() || extraParameters.Any())
                    {
                        if (missingParameters.Any())
                        {
                            EditorGUILayout.LabelField(
                                $"Missing: {string.Join(", ", missingParameters)}", 
                                EditorGUIStyleUtility.WarningLabelStyle
                            );
                        }
                        if (extraParameters.Any())
                        {
                            EditorGUILayout.LabelField(
                                $"Extra: {string.Join(", ", extraParameters)}", 
                                EditorGUIStyleUtility.WarningLabelStyle
                            );
                        }
                    }

                    EditorGUILayout.EndVertical();

                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(languageData, "Update Translation");
                        languageData.allText[keyIndex] = newTranslation;
                        EditorUtility.SetDirty(languageData);
                    }

                    if (GUILayout.Button("Auto", GUILayout.Width(60)))
                    {
                        if (!string.IsNullOrEmpty(deeplApiKey))
                        {
                            TranslateSingleField(selectedKey, language, keyIndex, languageData);
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("DeepL Translation", 
                                "Please configure your DeepL API key in the DeepL tab first.", "OK");
                        }
                    }

                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(language, GUILayout.Width(150));
                    EditorGUILayout.HelpBox($"No translation data available for {language}", MessageType.Warning);
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.Space(10);

            // Add translate all button
            if (!string.IsNullOrEmpty(deeplApiKey))
            {
                if (GUILayout.Button("Translate All Languages"))
                {
                    _ = TranslateAllLanguagesForKey(selectedKey);
                }
            }
        }

        private List<string> ExtractParametersFromText(string text)
        {
            var parameters = new List<string>();
            
            // Return empty list if text is null or empty
            if (string.IsNullOrEmpty(text))
            {
                return parameters;
            }
            
            var regex = new Regex(@"\{([^}]+)\}");
            var matches = regex.Matches(text);
            
            foreach (Match match in matches)
            {
                parameters.Add(match.Groups[1].Value);
            }
            
            return parameters;
        }
    }
} 