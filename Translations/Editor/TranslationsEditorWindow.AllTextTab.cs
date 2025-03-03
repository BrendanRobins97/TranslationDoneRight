using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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

        private HashSet<string> selectedKeys = new HashSet<string>();
        private string lastControlClickedKey = null; // Track last control-clicked key

        private SearchSettings searchSettingsInstance;
        private SerializedObject searchSettings;
        private SerializedProperty searchFilterProp;

        [System.Serializable]
        private class SearchSettings : ScriptableObject
        {
            public string searchFilter = "";

            private void OnValidate()
            {
                // This will be called when Unity performs an undo/redo operation
                var window = EditorWindow.GetWindow<TranslationsEditorWindow>();
                if (window != null)
                {
                    window.OnSearchSettingsValidated();
                }
            }
        }

        private void OnSearchSettingsValidated()
        {
            // Update the search filter from the serialized property
            if (searchSettingsInstance != null && searchSettings != null)
            {
                searchSettings.Update();
                searchFilter = searchFilterProp.stringValue;
                UpdateFilteredKeys();
                Repaint();
            }
        }

        private float GetCardWidth()
        {
            // Account for scrollbar width, padding, and spacing
            float scrollbarWidth = 20f;
            float horizontalSpacing = 20f; // Total horizontal spacing (left and right)
            float availableWidth = position.width - scrollbarWidth - horizontalSpacing;
            return availableWidth;
        }

        private float GetHalfCardWidth()
        {
            return GetCardWidth() / 2;
        }

        private void DrawAllTextTab()
        {
            // Header
            EditorGUILayout.Space(10);
            GUILayout.Label("Translation Management", EditorGUIStyleUtility.HeaderLabel);
            EditorGUILayout.Space(5);

            bool needsSetupStill = false;
            // Check if languages are set up
            if (translationData.supportedLanguages == null || translationData.supportedLanguages.Count <= 1)
            {
                EditorGUILayout.LabelField("Before managing translations, you need to set up your supported languages.", EditorStyles.wordWrappedLabel);
                EditorGUILayout.Space(10);
                
                var buttonStyle = new GUIStyle(EditorStyles.miniButton)
                {
                    fontSize = 12,
                    fixedHeight = 30,
                    padding = new RectOffset(15, 15, 8, 8),
                    alignment = TextAnchor.MiddleCenter
                };

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Go to Languages Tab", buttonStyle, GUILayout.Width(200)))
                {
                    currentTab = Tab.Languages;
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                needsSetupStill = true;
            }

            // Check if there are any text entries
            if (translationData.allKeys == null || translationData.allKeys.Count == 0)
            {
                EditorGUILayout.LabelField("No text entries found in your project. Use the Text Extraction tool to scan your project for translatable text.", EditorStyles.wordWrappedLabel);
                EditorGUILayout.Space(10);
                
                var buttonStyle = new GUIStyle(EditorStyles.miniButton)
                {
                    fontSize = 12,
                    fixedHeight = 30,
                    padding = new RectOffset(15, 15, 8, 8),
                    alignment = TextAnchor.MiddleCenter
                };

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Go to Text Extraction", buttonStyle, GUILayout.Width(200)))
                {
                    currentTab = Tab.TextExtraction;
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                needsSetupStill = true;
            }

            if (needsSetupStill)
            {
                return;
            }
    
            // Controls Section
            using (new EditorGUILayout.VerticalScope(EditorGUIStyleUtility.CardStyle, GUILayout.Width(GetCardWidth())))
            {
                // View Controls Row
                using (new EditorGUILayout.HorizontalScope())
                {
                    var halfCardWidth = GetHalfCardWidth();
                    // Left side - View Mode
                    using (new EditorGUILayout.HorizontalScope(GUILayout.Width(halfCardWidth - 25)))
                    {
                        EditorGUILayout.LabelField("View Mode:", GUILayout.Width(70));
                        TextViewMode newMode = (TextViewMode)EditorGUILayout.EnumPopup(currentTextViewMode, GUILayout.Width(100));
                        if (newMode != currentTextViewMode)
                        {
                            currentTextViewMode = newMode;
                            selectedKeys.Clear();
                        }
                    }

                    GUILayout.Space((halfCardWidth - 50) / 2);

                    // Right side - Scale (only for grid view)
                    using (new EditorGUILayout.HorizontalScope(GUILayout.Width((halfCardWidth - 25) / 2)))
                    {
                        if (currentTextViewMode == TextViewMode.Grid)
                        {
                            EditorGUILayout.LabelField("Scale:", GUILayout.Width(45));
                            float newScale = EditorGUILayout.Slider(gridViewScale, 0.5f, 2f, GUILayout.Width((halfCardWidth - 25) / 2));
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
                    using (new EditorGUILayout.HorizontalScope(GUILayout.Width(GetHalfCardWidth() - 25)))
                    {
                        EditorGUILayout.LabelField("Search:", GUILayout.Width(50));

                        // Create a horizontal group for search field and clear button
                        EditorGUILayout.BeginHorizontal();
                        
                        // Update serialized search settings
                        searchSettings.Update();
                        
                        EditorGUI.BeginChangeCheck();
                        
                        // Make search box taller (3 lines)
                        GUIStyle multilineSearchStyle = new GUIStyle(EditorStyles.textField)
                        {
                            wordWrap = true
                        };
                        
                        float searchBoxHeight = EditorGUIUtility.singleLineHeight * 3;
                        searchFilterProp.stringValue = EditorGUILayout.TextArea(
                            searchFilterProp.stringValue, 
                            multilineSearchStyle, 
                            GUILayout.ExpandWidth(true),
                            GUILayout.Height(searchBoxHeight)
                        );
                        
                        if (EditorGUI.EndChangeCheck())
                        {
                            searchSettings.ApplyModifiedProperties();
                            searchFilter = searchFilterProp.stringValue;
                            UpdateFilteredKeys();
                        }

                        // Add clear button (x)
                        if (!string.IsNullOrEmpty(searchFilter))
                        {
                            var clearButtonStyle = new GUIStyle(EditorStyles.miniButton)
                            {
                                fontSize = 9,
                                padding = new RectOffset(4, 4, 0, 1),
                                margin = new RectOffset(2, 0, 2, 2),
                                fixedWidth = 16,
                                fixedHeight = 16
                            };

                            if (GUILayout.Button("×", clearButtonStyle))
                            {
                                searchFilterProp.stringValue = "";
                                searchSettings.ApplyModifiedProperties();
                                searchFilter = "";
                                UpdateFilteredKeys();
                                GUI.FocusControl(null);
                                
                                // If there are selected items, scroll to the first one
                                if (selectedKeys.Count > 0)
                                {
                                    EditorApplication.delayCall += () => {
                                        int selectedIndex = filteredKeysList.IndexOf(selectedKeys.First());
                                        if (selectedIndex >= 0)
                                        {
                                            if (currentTextViewMode == TextViewMode.Detailed)
                                            {
                                                float targetY = selectedIndex * KEY_ROW_HEIGHT;
                                                textScrollPosition.y = Mathf.Max(0, targetY - KEY_ROW_HEIGHT);
                                            }
                                            else
                                            {
                                                float targetY = 0;
                                                for (int i = 0; i < selectedIndex; i++)
                                                {
                                                    targetY += rowHeights[filteredKeysList[i]] + ROW_SPACING;
                                                }
                                                gridScrollPosition.y = Mathf.Max(0, targetY - rowHeights[filteredKeysList[selectedIndex]]);
                                            }
                                            Repaint();
                                        }
                                    };
                                }
                            }
                        }
                        
                        EditorGUILayout.EndHorizontal();

                        // Help icon with tooltip
                        var helpContent = EditorGUIUtility.IconContent("_Help");
                        helpContent.tooltip = "Search Tips:\n" +
                            "• Multiple terms: 'cancel button' (finds both words)\n" +
                            "• Exact phrase: \"cancel button\" (finds exact phrase)\n" +
                            "• OR search: 'cancel|close' (finds either)\n" +
                            "• Wildcard: 'button*' (starts with 'button')\n" +
                            "• Case sensitive: add '/c' at the end";
                        
                        GUILayout.Space(-5); // Reduce space between text field and icon
                        if (GUILayout.Button(helpContent, EditorStyles.label, GUILayout.Width(20)))
                        {
                            // Optional: Could add additional help functionality when clicked
                        }
                    }

                    GUILayout.Space(10);

                    // Right side - Toggle Filters in a vertical layout
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(GetHalfCardWidth() - 25)))
                    {
                        EditorGUILayout.LabelField("Filter Options:", EditorStyles.boldLabel);
                        
                        showMissingOnly = EditorGUILayout.ToggleLeft(
                            new GUIContent("Needs Translations", "Show only keys that need translations"),
                            showMissingOnly
                        );
                        
                        showNewOnly = EditorGUILayout.ToggleLeft(
                            new GUIContent("New", "Show only keys with New state"),
                            showNewOnly
                        );
                        
                        showMissingStateOnly = EditorGUILayout.ToggleLeft(
                            new GUIContent("Missing", "Show only keys with Missing state"),
                            showMissingStateOnly
                        );
                        
                        if (GUI.changed)
                        {
                            UpdateFilteredKeys();
                            Repaint();
                        }
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
            using (new EditorGUILayout.VerticalScope(EditorGUIStyleUtility.CardStyle, GUILayout.Width(GetCardWidth() - 320)))
            {
                if (selectedKeys.Count > 0)
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

            EditorGUILayout.Space(10);
            
            // Create a container for the entire grid view
            using (new EditorGUILayout.VerticalScope(EditorGUIStyleUtility.CardStyle, GUILayout.Width(position.width - 25)))
            {
                EditorGUILayout.Space(5);

                // Apply scale to measurements
                float columnWidth = Mathf.Round(BASE_COLUMN_WIDTH * gridViewScale);
                float baseRowHeight = Mathf.Round(BASE_ROW_HEIGHT * gridViewScale);
                float rowSpacing = Mathf.Round(ROW_SPACING * gridViewScale);
                int fontSize = Mathf.RoundToInt(BASE_FONT_SIZE * gridViewScale);

                // Draw translation buttons if DeepL is configured
                if (!string.IsNullOrEmpty(deeplApiKey))
                {
                    EditorGUILayout.BeginHorizontal();
                    
                    // Left side - Empty space or DeepL buttons
                    using (new EditorGUILayout.HorizontalScope(GUILayout.Width(position.width / 2 - 35)))
                    {
                        if (selectedKeys.Count > 0)
                        {
                            GUILayout.FlexibleSpace();
                            var buttonStyle = new GUIStyle(EditorStyles.miniButton)
                            {
                                fontSize = Mathf.RoundToInt(11),
                                fixedHeight = 30,
                                padding = new RectOffset(10, 10, 5, 5),
                                margin = new RectOffset(5, 5, 0, 0),
                                alignment = TextAnchor.MiddleCenter
                            };

                            // Draw "All" button first
                            if (GUILayout.Button($"Translate All ({selectedKeys.Count})", buttonStyle, GUILayout.Width(250), GUILayout.Height(30)))
                            {
                                foreach (var selectedKey in selectedKeys)
                                {
                                    _ = TranslateAllLanguagesForKey(selectedKey);
                                }
                            }

                            GUILayout.Space(10);

                            // Check if any selected items have missing translations and count them
                            int missingCount = selectedKeys.Count(k => NeedsTranslations(k));
                            if (missingCount > 0)
                            {
                                if (GUILayout.Button($"Translations Needed ({missingCount})", buttonStyle, GUILayout.Width(250), GUILayout.Height(30)))
                                {
                                    foreach (var selectedKey in selectedKeys)
                                    {
                                        _ = TranslateMissingLanguagesForKey(selectedKey);
                                    }
                                }
                            }
                            GUILayout.FlexibleSpace();
                        }
                        else
                        {
                            GUILayout.Label("Select items to translate", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(400), GUILayout.Height(25.5f));
                        }
                    }

                    // Right side - Show Detailed View button
                    using (new EditorGUILayout.HorizontalScope(GUILayout.Width(position.width / 2 - 35)))
                    {
                        GUILayout.FlexibleSpace();
                        if (selectedKeys.Count > 0)
                        {
                            var buttonStyle = new GUIStyle(EditorStyles.miniButton)
                            {
                                fontSize = Mathf.RoundToInt(11),
                                fixedHeight = 30,
                                padding = new RectOffset(10, 10, 5, 5),
                                margin = new RectOffset(5, 5, 0, 0),
                                alignment = TextAnchor.MiddleCenter
                            };

                            if (GUILayout.Button($"Show Detailed View ({selectedKeys.Count})", buttonStyle, GUILayout.Width(200)))
                            {
                                if (selectedKeys.Count > 1)
                                {
                                    // Create OR-style filter from selected keys and switch to detailed view
                                    searchFilter = string.Join("|", selectedKeys.Select(k => $"\"{k}\""));
                                    searchFilterProp.stringValue = searchFilter;
                                    searchSettings.ApplyModifiedProperties();
                                    UpdateFilteredKeys();
                                }
                                currentTextViewMode = TextViewMode.Detailed;
                                // Ensure the first selected item is visible
                                if (selectedKeys.Count > 0)
                                {
                                    EditorApplication.delayCall += () => {
                                        int selectedIndex = filteredKeysList.IndexOf(selectedKeys.First());
                                        if (selectedIndex >= 0)
                                        {
                                            float targetY = selectedIndex * KEY_ROW_HEIGHT;
                                            textScrollPosition.y = Mathf.Max(0, targetY - KEY_ROW_HEIGHT);
                                            Repaint();
                                        }
                                    };
                                }
                            }
                        }
                    }

                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.Space(5);
                }

                // Calculate visible rows based on zoom
                int visibleGridRows = Mathf.RoundToInt(Mathf.Clamp(
                    BASE_VISIBLE_GRID_ROWS / gridViewScale,
                    BASE_VISIBLE_GRID_ROWS,
                    MAX_VISIBLE_GRID_ROWS
                ));

                // Make sure filtered list is up to date
                if (filteredKeysList == null || filteredKeysList.Count == 0)
                {
                    UpdateFilteredKeys();
                }

                // Calculate total content height including spacing
                float totalContentHeight = 0;
                foreach (var key in filteredKeysList)
                {
                    if (!rowHeights.TryGetValue(key, out float height))
                    {
                        height = baseRowHeight;
                        rowHeights[key] = height;
                    }
                    totalContentHeight += height + rowSpacing;  // Add spacing after each row
                }
                
                // Calculate total content width based on number of languages
                float totalContentWidth = columnWidth * (translationData.supportedLanguages.Count);
                
                // Get the available view height (subtract header height)
                float viewHeight = position.height * 0.7f - baseRowHeight;
                
                // Create the scroll view rect with padding
                Rect totalRect = GUILayoutUtility.GetRect(0, viewHeight);
                
                // Account for scrollbar width and padding
                float scrollbarWidth = 16f; // Standard Unity scrollbar width
                float horizontalPadding = 20f; // Left + Right padding
                
                totalRect = new Rect(
                    totalRect.x + 10,  // Add left padding
                    totalRect.y,
                    totalRect.width - horizontalPadding,  // Subtract total padding
                    totalRect.height
                );
                
                // Adjust content width to account for scrollbar and padding
                float availableWidth = totalRect.width - scrollbarWidth;
                float minContentWidth = totalContentWidth;
                totalContentWidth = Mathf.Max(minContentWidth, availableWidth);
                
                // Draw header row above scroll view
                Rect headerRect = new Rect(totalRect.x, totalRect.y, totalRect.width, baseRowHeight);
                
                // Draw header background
                EditorGUI.DrawRect(headerRect, new Color(0.2f, 0.2f, 0.2f, 0.3f));
                
                // Begin clipping for header content
                GUI.BeginClip(headerRect);
                
                // Draw header content with scroll offset
                DrawGridHeader(new Rect(
                    -gridScrollPosition.x,  // Offset by scroll position
                    0,
                    totalContentWidth,
                    baseRowHeight
                ), columnWidth, fontSize);
                
                GUI.EndClip();

                // Create scroll view rect below header
                Rect scrollViewRect = new Rect(
                    totalRect.x,
                    totalRect.y + baseRowHeight,
                    totalRect.width,
                    totalRect.height - baseRowHeight
                );

                // Create content rect for scrollable area
                Rect contentRect = new Rect(0, 0, totalContentWidth, totalContentHeight);

                // Begin the scrollable area with both vertical and horizontal scrolling
                Vector2 tempScrollPos = GUI.BeginScrollView(scrollViewRect, gridScrollPosition, contentRect, true, true);
                if (tempScrollPos != gridScrollPosition)
                {
                    gridScrollPosition = tempScrollPos;
                    Repaint();
                }

                if (filteredKeysList.Count > 0)
                {
                    // Calculate which items should be visible
                    float scrollY = gridScrollPosition.y;
                    float currentY = 0; // Start at 0 since header is now separate
                    int startIndex = 0;
                    
                    // Find start index
                    while (startIndex < filteredKeysList.Count && currentY + rowHeights[filteredKeysList[startIndex]] + rowSpacing < scrollY)
                    {
                        currentY += rowHeights[filteredKeysList[startIndex]] + rowSpacing;
                        startIndex++;
                    }

                    // Find end index
                    int endIndex = startIndex;
                    float visibleHeight = scrollViewRect.height;
                    int visibleCount = 0;
                    
                    while (endIndex < filteredKeysList.Count && visibleCount < visibleGridRows)
                    {
                        if (currentY > scrollY + visibleHeight)
                            break;
                        endIndex++;
                        if (endIndex < filteredKeysList.Count)
                            currentY += rowHeights[filteredKeysList[endIndex]] + rowSpacing;
                        visibleCount++;
                    }

                    // Create base text style with scaled font
                    var baseTextStyle = CreateGridTextStyle(fontSize);

                    // Reset currentY for drawing
                    currentY = 0;
                    for (int i = 0; i < startIndex; i++)
                    {
                        currentY += rowHeights[filteredKeysList[i]] + rowSpacing;
                    }

                    // Draw only visible rows
                    for (int i = startIndex; i < endIndex; i++)
                    {
                        string key = filteredKeysList[i];
                        float rowHeight = rowHeights[key];
                        Rect rowRect = new Rect(0, currentY, totalContentWidth, rowHeight);
                        
                        // Skip if row is not visible
                        if (rowRect.yMax < scrollY || rowRect.y > scrollY + scrollViewRect.height)
                        {
                            currentY += rowHeight + rowSpacing;
                            continue;
                        }

                        float newHeight = DrawGridViewRow(key, i, rowRect, columnWidth, rowHeight, baseTextStyle);
                        // Update height if it changed by more than 1 pixel in either direction
                        if (Mathf.Abs(newHeight - rowHeight) > 1)
                        {
                            rowHeights[key] = Mathf.Clamp(newHeight, MIN_ROW_HEIGHT * gridViewScale, MAX_ROW_HEIGHT * gridViewScale);
                            // Force layout update
                            Repaint();
                        }
                        
                        currentY += rowHeight + rowSpacing;
                    }
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
            
            // Selection column header (empty)
            float selectionColumnWidth = 24 * gridViewScale;
            GUI.Label(new Rect(xOffset, 0, selectionColumnWidth, scaledHeaderHeight), "", headerStyle);
            xOffset += selectionColumnWidth;
            
            // Default language column
            GUI.Label(new Rect(xOffset, 0, columnWidth, scaledHeaderHeight), translationData.defaultLanguage, headerStyle);
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

            bool isSelected = selectedKeys.Contains(key);

            // First pass: Calculate maximum height needed for all cells
            // Default language column height
            var readOnlyStyle = new GUIStyle(baseTextStyle)
            {
                richText = true,
                normal = { textColor = new Color(0.5f, 0.5f, 0.5f, 1f) }
            };
            var keyContent = new GUIContent(key);
            float keyHeight = readOnlyStyle.CalcHeight(keyContent, columnWidth - 4);  // Subtract some padding
            maxHeight = Mathf.Max(maxHeight, keyHeight);

            // Other languages height calculation
            bool hasMissingTranslations = false;
            for (int i = 0; i < translationData.languageDataDictionary.Length; i++)
            {
                var assetRef = translationData.languageDataDictionary[i];
                string assetPath = AssetDatabase.GUIDToAssetPath(assetRef.AssetGUID);
                LanguageData languageData = AssetDatabase.LoadAssetAtPath<LanguageData>(assetPath);
                int keyIndex = translationData.allKeys.IndexOf(key);
                
                if (languageData != null && keyIndex >= 0 && keyIndex < languageData.allText.Count)
                {
                    string translation = languageData.allText[keyIndex];
                    if (string.IsNullOrEmpty(translation) || translation == key)
                    {
                        hasMissingTranslations = true;
                    }
                    var content = new GUIContent(translation);
                    float cellHeight = baseTextStyle.CalcHeight(content, columnWidth - 4);  // Subtract some padding
                    maxHeight = Mathf.Max(maxHeight, cellHeight);
                }
            }

            // Clamp the maximum height
            maxHeight = Mathf.Clamp(maxHeight, MIN_ROW_HEIGHT * gridViewScale, MAX_ROW_HEIGHT * gridViewScale);

            // Draw row background for the full width
            if (isSelected)
            {
                EditorGUI.DrawRect(rowRect, new Color(0.2f, 0.4f, 0.9f, 0.1f));
            }
            else if (rowIndex % 2 == 0)
            {
                EditorGUI.DrawRect(rowRect, new Color(0.5f, 0.5f, 0.5f, 0.1f));
            }

            // Draw selection column
            float selectionColumnWidth = 24 * gridViewScale;
            var selectionRect = new Rect(xOffset, rowRect.y, selectionColumnWidth, maxHeight);
            
            // Draw selection indicator
            var indicatorStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(13 * gridViewScale)  // Slightly larger for the circle
            };

            if (isSelected)
            {
                // Draw circle
                indicatorStyle.normal.textColor = new Color(0.2f, 0.4f, 0.9f, 1f);
                GUI.Label(selectionRect, "○", indicatorStyle);
                
                // Draw checkmark slightly larger and higher
                indicatorStyle.fontSize = Mathf.RoundToInt(11 * gridViewScale);
                var checkRect = new Rect(
                    selectionRect.x,
                    selectionRect.y - 1,
                    selectionRect.width,
                    selectionRect.height
                );
                GUI.Label(checkRect, "✓", indicatorStyle);
            }
            else
            {
                indicatorStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                GUI.Label(selectionRect, "○", indicatorStyle);
            }

            // Handle selection click
            if (Event.current.type == EventType.MouseDown && selectionRect.Contains(Event.current.mousePosition))
            {
                HandleSelection(key);
                Event.current.Use();
                GUI.changed = true;
            }
            else if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
            {
                float relativeX = Event.current.mousePosition.x - rowRect.x;
                bool isClickingNonEditableArea = relativeX < ((!string.IsNullOrEmpty(deeplApiKey) ? 45 : 0) + 24) * gridViewScale;
                
                if (isClickingNonEditableArea)
                {
                    HandleSelection(key);
                    Event.current.Use();
                    GUI.changed = true;
                }
            }

            xOffset += selectionColumnWidth;

            // Draw default language column
            EditorGUI.SelectableLabel(
                new Rect(xOffset, rowRect.y, columnWidth - 2, maxHeight),
                key,
                readOnlyStyle
            );
            xOffset += columnWidth;

            // Draw other languages
            for (int i = 0; i < translationData.languageDataDictionary.Length; i++)
            {
                DrawGridViewCell(
                    new Rect(xOffset, rowRect.y, columnWidth - 2, maxHeight),
                    i,
                    key,
                    baseTextStyle
                );
                xOffset += columnWidth;
            }

            // Draw selection borders if selected
            if (isSelected)
            {
                var borderColor = new Color(0.2f, 0.4f, 0.9f, 0.8f);
                float borderWidth = 1f;
                
                EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.y, rowRect.width, borderWidth), borderColor);
                EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.y + rowRect.height - borderWidth, rowRect.width, borderWidth), borderColor);
                EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.y, borderWidth, rowRect.height), borderColor);
                EditorGUI.DrawRect(new Rect(rowRect.x + rowRect.width - borderWidth, rowRect.y, borderWidth, rowRect.height), borderColor);
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

            // Handle mouse down outside of text area to clear focus
            if (Event.current.type == EventType.MouseDown && !cellRect.Contains(Event.current.mousePosition))
            {
                GUI.FocusControl(null);
            }

            // Create a unique control name for this text area
            string controlName = $"TranslationCell_{key}_{languageIndex}";
            GUI.SetNextControlName(controlName);

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
            // Make sure filtered list is up to date
            if (filteredKeysList == null || filteredKeysList.Count == 0)
            {
                UpdateFilteredKeys();
            }

            // Calculate total height of all items
            cachedTotalHeight = filteredKeysList.Count * KEY_ROW_HEIGHT;

            // Begin scrollable area with total height
            Rect scrollViewRect = GUILayoutUtility.GetRect(0, position.height * 0.7f);
            Rect contentRect = new Rect(0, 0, scrollViewRect.width - 16, cachedTotalHeight); // 16 is scrollbar width

            textScrollPosition = GUI.BeginScrollView(scrollViewRect, textScrollPosition, contentRect);

            // Create styles for the labels
            var newLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.3f, 0.7f, 0.3f) },
                fontSize = 9,
                alignment = TextAnchor.MiddleRight,
                padding = new RectOffset(0, 2, 0, 0)
            };

            var missingLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.7f, 0.3f, 0.3f) },
                fontSize = 9,
                alignment = TextAnchor.MiddleRight,
                padding = new RectOffset(0, 2, 0, 0)
            };

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
                    bool isSelected = selectedKeys.Contains(key);

                    // Draw selection background
                    if (isSelected)
                    {
                        EditorGUI.DrawRect(itemRect, new Color(0.2f, 0.4f, 0.9f, 0.5f));
                        // Draw a border around the selected item for extra emphasis
                        var borderRect = new Rect(itemRect.x, itemRect.y, itemRect.width, 1);
                        EditorGUI.DrawRect(borderRect, new Color(0.2f, 0.4f, 0.9f, 0.8f));
                        borderRect.y = itemRect.y + itemRect.height - 1;
                        EditorGUI.DrawRect(borderRect, new Color(0.2f, 0.4f, 0.9f, 0.8f));
                    }
                    else if (i % 2 == 0)
                    {
                        EditorGUI.DrawRect(itemRect, new Color(0.5f, 0.5f, 0.5f, 0.1f));
                    }

                    // Calculate rects for key text and labels
                    float labelWidth = 35; // Width for each label
                    float padding = 5;
                    Rect textRect = new Rect(itemRect.x + padding, itemRect.y, itemRect.width - (labelWidth * 2) - (padding * 3), itemRect.height);
                    Rect missingLabelRect = new Rect(textRect.xMax + padding, itemRect.y, labelWidth, itemRect.height);
                    Rect newLabelRect = new Rect(missingLabelRect.xMax + padding, itemRect.y, labelWidth, itemRect.height);

                    // Draw the truncated key text
                    string displayText = key;
                    GUIStyle keyStyle = new GUIStyle(EditorStyles.label);
                    
                    // Make the text slightly yellow if it needs translations
                    if (NeedsTranslations(key))
                    {
                        keyStyle.normal.textColor = new Color(0.9f, 0.82f, 0.4f); // Slightly yellow
                    }
                    
                    float maxWidth = textRect.width;
                    if (keyStyle.CalcSize(new GUIContent(displayText)).x > maxWidth)
                    {
                        while (keyStyle.CalcSize(new GUIContent(displayText + "...")).x > maxWidth && displayText.Length > 0)
                        {
                            displayText = displayText.Substring(0, displayText.Length - 1);
                        }
                        displayText += "...";
                    }
                    
                    EditorGUI.LabelField(textRect, displayText, keyStyle);

                    // Draw the state labels
                    if (translationData.Metadata.IsMissingText(key))
                    {
                        EditorGUI.LabelField(missingLabelRect, "missing", missingLabelStyle);
                    }
                    if (translationData.Metadata.IsNewText(key))
                    {
                        EditorGUI.LabelField(newLabelRect, "new", newLabelStyle);
                    }

                    // Handle mouse events for selection
                    if (Event.current.type == EventType.MouseDown && itemRect.Contains(Event.current.mousePosition))
                    {
                        if (Event.current.button == 0) // Left click
                        {
                            HandleSelection(key);
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
                int currentIndex = selectedKeys.Count > 0 ? filteredKeysList.IndexOf(selectedKeys.First()) : -1;

                switch (Event.current.keyCode)
                {
                    case KeyCode.UpArrow:
                        if (currentIndex > 0)
                        {
                            selectedKeys.Clear();
                            selectedKeys.Add(filteredKeysList[currentIndex - 1]);
                            handled = true;
                        }
                        break;

                    case KeyCode.DownArrow:
                        if (currentIndex < filteredKeysList.Count - 1)
                        {
                            selectedKeys.Clear();
                            selectedKeys.Add(filteredKeysList[currentIndex + 1]);
                            handled = true;
                        }
                        break;

                    case KeyCode.Home:
                        if (filteredKeysList.Count > 0)
                        {
                            selectedKeys.Clear();
                            selectedKeys.Add(filteredKeysList[0]);
                            handled = true;
                        }
                        break;

                    case KeyCode.End:
                        if (filteredKeysList.Count > 0)
                        {
                            selectedKeys.Clear();
                            selectedKeys.Add(filteredKeysList[filteredKeysList.Count - 1]);
                            handled = true;
                        }
                        break;
                }

                if (handled)
                {
                    // Ensure selected item is visible
                    if (selectedKeys.Count > 0)
                    {
                        int selectedIndex = filteredKeysList.IndexOf(selectedKeys.First());
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
            // Start with all non-canonical texts
            var initialKeys = translationData.allKeys
                .Where(k => !translationData.HasDifferentCanonicalVersion(k));

            // Apply missing translations filter if enabled
            if (showMissingOnly)
            {
                initialKeys = initialKeys.Where(NeedsTranslations);
            }
            
            // Apply New state filter if enabled
            if (showNewOnly)
            {
                initialKeys = initialKeys.Where(k => translationData.Metadata.IsNewText(k));
            }
            
            // Apply Missing state filter if enabled
            if (showMissingStateOnly)
            {
                initialKeys = initialKeys.Where(k => translationData.Metadata.IsMissingText(k));
            }

            // If no search filter, use the current filtered list
            if (string.IsNullOrEmpty(searchFilter))
            {
                filteredKeysList = initialKeys.ToList();
                return;
            }

            bool isCaseSensitive = searchFilter.EndsWith("/c");
            string filterText = isCaseSensitive ? searchFilter.Replace("/c", "").Trim() : searchFilter.ToLower();

            // Split into OR conditions if pipe exists
            var orTerms = filterText.Split('|').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t));

            var filteredKeys = new HashSet<string>();

            foreach (var orTerm in orTerms)
            {
                var searchTerms = new List<string>();
                var exactPhrases = new List<string>();

                // Extract exact phrases (quoted text)
                var phraseMatches = System.Text.RegularExpressions.Regex.Matches(orTerm, "\"([^\"]*)\"");
                foreach (System.Text.RegularExpressions.Match match in phraseMatches)
                {
                    exactPhrases.Add(match.Groups[1].Value);
                }

                // Remove exact phrases from search text and split remaining into terms
                string remainingText = System.Text.RegularExpressions.Regex.Replace(orTerm, "\"[^\"]*\"", "").Trim();
                if (!string.IsNullOrEmpty(remainingText))
                {
                    searchTerms.AddRange(remainingText.Split(' ').Where(t => !string.IsNullOrEmpty(t)));
                }

                // Find keys that match all search terms and exact phrases
                var matchingKeys = initialKeys.Where(key =>
                {
                    string searchableKey = isCaseSensitive ? key : key.ToLower();

                    // Check exact phrases - must match exactly
                    foreach (var phrase in exactPhrases)
                    {
                        string searchPhrase = isCaseSensitive ? phrase : phrase.ToLower();
                        if (searchableKey != searchPhrase) // Changed from Contains to exact match
                            return false;
                    }

                    // If we have exact phrases and no additional terms, we're done
                    if (exactPhrases.Count > 0 && searchTerms.Count == 0)
                        return true;

                    // Check individual terms - fuzzy match
                    foreach (var term in searchTerms)
                    {
                        string searchTerm = isCaseSensitive ? term : term.ToLower();
                        
                        // Handle wildcard searches
                        if (term.Contains("*"))
                        {
                            var pattern = "^" + System.Text.RegularExpressions.Regex.Escape(searchTerm).Replace("\\*", ".*") + "$";
                            if (!System.Text.RegularExpressions.Regex.IsMatch(searchableKey, pattern))
                                return false;
                        }
                        else if (!searchableKey.Contains(searchTerm))
                        {
                            return false;
                        }
                    }

                    return true;
                });

                filteredKeys.UnionWith(matchingKeys);
            }

            filteredKeysList = filteredKeys.ToList();
        }

        private void DrawTranslationsForKey()
        {
            if (selectedKeys.Count == 0) return;

            EditorGUILayout.LabelField($"Translations for: {string.Join(", ", selectedKeys)}", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Get the index of the selected key
            int keyIndex = translationData.allKeys.IndexOf(selectedKeys.First());
            if (keyIndex == -1)
            {
                EditorGUILayout.HelpBox("Selected key not found in translation data.", MessageType.Error);
                return;
            }
            
            // Show default language text first
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(translationData.defaultLanguage, GUILayout.Width(150));
            GUI.enabled = false;
            EditorGUILayout.TextField(selectedKeys.First());
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Translation Buttons
            if (!string.IsNullOrEmpty(deeplApiKey))
            {
                EditorGUILayout.BeginVertical(EditorGUIStyleUtility.CardStyle);
                EditorGUILayout.BeginHorizontal();
                
                // Left side - Translation buttons
                EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                {
                    if (selectedKeys.Count > 0)
                    {
                        GUILayout.FlexibleSpace();
                        var buttonStyle = new GUIStyle(EditorStyles.miniButton)
                        {
                            fontSize = Mathf.RoundToInt(11),
                            fixedHeight = 30,
                            padding = new RectOffset(10, 10, 5, 5),
                            margin = new RectOffset(5, 5, 0, 0),
                            alignment = TextAnchor.MiddleCenter
                        };

                        // Draw "All" button first
                        if (GUILayout.Button($"Translate All ({selectedKeys.Count})", buttonStyle, GUILayout.Width(200)))
                        {
                            foreach (var selectedKey in selectedKeys)
                            {
                                _ = TranslateAllLanguagesForKey(selectedKey);
                            }
                        }

                        GUILayout.Space(10);

                        // Check if any selected items have missing translations and count them
                        int missingCount = selectedKeys.Count(k => NeedsTranslations(k));
                        if (missingCount > 0)
                        {
                            if (GUILayout.Button($"Translations Needed ({missingCount})", buttonStyle, GUILayout.Width(200)))
                            {
                                foreach (var selectedKey in selectedKeys)
                                {
                                    _ = TranslateMissingLanguagesForKey(selectedKey);
                                }
                            }
                        }
                        GUILayout.FlexibleSpace();
                    }
                    else
                    {
                        GUILayout.FlexibleSpace();
                        GUILayout.Label("Select items to translate", EditorStyles.centeredGreyMiniLabel);
                        GUILayout.FlexibleSpace();
                    }
                }
                EditorGUILayout.EndHorizontal();

                // Right side - Show Grid View button
                EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                {
                    GUILayout.FlexibleSpace();
                    if (selectedKeys.Count > 0)
                    {
                        var buttonStyle = new GUIStyle(EditorStyles.miniButton)
                        {
                            fontSize = Mathf.RoundToInt(11),
                            fixedHeight = 30,
                            padding = new RectOffset(10, 10, 5, 5),
                            margin = new RectOffset(5, 5, 0, 0),
                            alignment = TextAnchor.MiddleCenter
                        };

                        if (GUILayout.Button($"Show Grid View ({selectedKeys.Count})", buttonStyle, GUILayout.Width(200)))
                        {
                            if (selectedKeys.Count > 1)
                            {
                                // Create OR-style filter from selected keys and switch to grid view
                                searchFilter = string.Join("|", selectedKeys.Select(k => $"\"{k}\""));
                                searchFilterProp.stringValue = searchFilter;
                                searchSettings.ApplyModifiedProperties();
                            }
                            currentTextViewMode = TextViewMode.Grid;
                            // Ensure the first selected item is visible
                            if (selectedKeys.Count > 0)
                            {
                                EditorApplication.delayCall += () => {
                                    int selectedIndex = filteredKeysList.IndexOf(selectedKeys.First());
                                    if (selectedIndex >= 0)
                                    {
                                        float targetY = 0;
                                        for (int i = 0; i < selectedIndex; i++)
                                        {
                                            targetY += rowHeights[filteredKeysList[i]] + ROW_SPACING;
                                        }
                                        gridScrollPosition.y = Mathf.Max(0, targetY - rowHeights[filteredKeysList[selectedIndex]]);
                                        Repaint();
                                    }
                                };
                            }
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(15);
            }
            // Details Section (Source Info and Context)
            showSourceInformation = EditorGUILayout.Foldout(showSourceInformation, "Details", true, EditorGUIStyleUtility.FoldoutHeader);
            if (showSourceInformation)
            {
                EditorGUI.indentLevel++;

                // Source Information
                EditorGUILayout.LabelField("Source Information", EditorStyles.boldLabel);
                var sources = translationData.Metadata.GetSources(selectedKeys.First());
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
                            TextSourceType.ExternalFile => "TextAsset Icon",
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

                // Context Information with help icon
                using (new EditorGUILayout.HorizontalScope())
                {
                    var contextHelpContent = EditorGUIUtility.IconContent("_Help");
                    contextHelpContent.tooltip = "Context System Help:\n\n" +
                        "The context system helps translators understand how and where text is used:\n\n" +
                        "• Categories save time by providing preset contexts for common text types\n" +
                        "• Perfect for repetitive fields like Locations, Item Types, or UI Elements\n" +
                        "• Manual context can be added for unique or special cases\n" +
                        "• All context is sent to DeepL to improve translation accuracy\n\n" +
                        "Example:\n" +
                        "When translating 'Start', the context helps DeepL understand if it's:\n" +
                        "- A button label in the UI\n" +
                        "- A verb in dialog\n" +
                        "- Part of a tutorial\n\n" +
                        "This context ensures more accurate translations across languages.";
                    GUILayout.Label(contextHelpContent, EditorStyles.label, GUILayout.Width(20));
                    GUILayout.Space(-3);
                    EditorGUILayout.LabelField("Context Information", EditorStyles.boldLabel);
                }
                var context = translationData.Metadata.GetContext(selectedKeys.First());

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
                    string newValue = categoryDropdown.Draw(key, selectedKeys.First().GetHashCode(), context[key], translationData.Metadata.TextCategories[key], translationData, (newCategory) => {
                        if (newCategory != oldCategory)
                        {
                            translationData.Metadata.UpdateTextCategory(key, oldCategory, newCategory);
                        }
                    });
                    
                    if (newValue != oldCategory)
                    {
                        translationData.Metadata.UpdateContext(selectedKeys.First(), key, newValue);
                        EditorUtility.SetDirty(translationData);
                    }
                }

                // Category management section
                EditorGUILayout.Space(5);
                using (new EditorGUILayout.HorizontalScope())
                {
                    var categoryHelpContent = EditorGUIUtility.IconContent("_Help");
                    categoryHelpContent.tooltip = "Category Management Help:\n\n" +
                        "Categories provide quick context templates for common text types:\n\n" +
                        "• Locations: 'Main Menu', 'Settings Screen', 'Inventory Panel'\n" +
                        "• Item Types: 'Weapon', 'Consumable', 'Quest Item'\n" +
                        "• Dialog Types: 'NPC Greeting', 'Quest Dialog', 'Tutorial Tip'\n" +
                        "• UI Elements: 'Button', 'Label', 'Tooltip'\n\n" +
                        "Instead of typing context manually each time, just select a category\n" +
                        "and choose from predefined values. The system automatically generates\n" +
                        "natural-sounding context using your format template.";
                    GUILayout.Label(categoryHelpContent, EditorStyles.label, GUILayout.Width(20));
                    GUILayout.Space(-3);
                    showCategoryManagement = EditorGUILayout.Foldout(showCategoryManagement, "Category Management", true);
                }
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
                                translationData.Metadata.AddCategory(newCategoryName, new CategoryTemplate { 
                                    format = "This text appears in {value}" 
                                });
                                newCategoryName = "";
                                GUI.FocusControl(null);
                                EditorUtility.SetDirty(translationData);
                            }
                        }
                    }

                    // Remove categories and edit formats
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Manage Categories:", EditorStyles.boldLabel);
                    List<string> categoriesToRemove = new List<string>();

                    foreach (var category in translationData.Metadata.TextCategories.Keys)
                    {
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        
                        // Category name and remove button
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(category, EditorStyles.boldLabel);
                            if (GUILayout.Button("Remove", GUILayout.Width(60)))
                            {
                                if (EditorUtility.DisplayDialog("Remove Category",
                                    $"Are you sure you want to remove the category '{category}'?", "Remove", "Cancel"))
                                {
                                    categoriesToRemove.Add(category);
                                }
                            }
                        }

                        // Format editor
                        if (translationData.Metadata.CategoryTemplates.TryGetValue(category, out var template))
                        {
                            EditorGUI.BeginChangeCheck();
                            
                            // Format template label with help icon
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                var helpContent = EditorGUIUtility.IconContent("_Help");
                                helpContent.tooltip = "Format Template Help:\n" +
                                    "• Use {value} where you want the actual value to appear\n" +
                                    "• The text will automatically end with a period\n" +
                                    "• Example: 'This text appears in {value}' becomes 'This text appears in Main Menu.'\n" +
                                    "• Keep it natural and descriptive to help translators understand the context";
                                GUILayout.Label(helpContent, EditorStyles.label, GUILayout.Width(20));
                                GUILayout.Space(-3);
                                EditorGUILayout.LabelField("Format Template:", EditorStyles.miniLabel);
                            }
                            
                            string newFormat = EditorGUILayout.TextField(template.format);
                            
                            if (EditorGUI.EndChangeCheck())
                            {
                                template.format = newFormat;
                                translationData.Metadata.UpdateCategoryTemplate(category, template);
                                EditorUtility.SetDirty(translationData);
                            }
                        }

                        EditorGUILayout.EndVertical();
                        EditorGUILayout.Space(5);
                    }

                    // Process removals after the loop
                    foreach (var category in categoriesToRemove)
                    {
                        translationData.Metadata.TextCategories.Remove(category);
                        translationData.Metadata.CategoryTemplates.Remove(category);
                        context.Remove(category);
                        translationData.Metadata.SetContext(selectedKeys.First(), context);
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
                    translationData.Metadata.UpdateContext(selectedKeys.First(), "Manual", newManualContext);
                    EditorUtility.SetDirty(translationData);
                }

                // Show auto-generated context
                string translationContext = translationData.Metadata.GetTranslationContext(selectedKeys.First());
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
           
            EditorGUILayout.LabelField("Translations", EditorStyles.boldLabel);

            // Show translations for other languages
            for (int i = 0; i < translationData.supportedLanguages.Count - 1; i++)
            {
                string language = translationData.supportedLanguages[i + 1]; // +1 to skip default language
                var assetRef = translationData.languageDataDictionary[i];
                
                string assetPath = AssetDatabase.GUIDToAssetPath(assetRef.AssetGUID);
                LanguageData languageData = AssetDatabase.LoadAssetAtPath<LanguageData>(assetPath);
                if (languageData != null && keyIndex < languageData.allText.Count)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(language, GUILayout.Width(150));

                    var translationEntry = languageData.allText[keyIndex];
                    EditorGUI.BeginChangeCheck();

                    // Handle mouse down outside of text area to clear focus
                    if (Event.current.type == EventType.MouseDown)
                    {
                        Rect lastRect = GUILayoutUtility.GetLastRect();
                        if (!lastRect.Contains(Event.current.mousePosition))
                        {
                            GUI.FocusControl(null);
                        }
                    }

                    // Create a unique control name for this text area
                    string controlName = $"TranslationField_{language}_{keyIndex}";
                    GUI.SetNextControlName(controlName);

                    string newTranslation = EditorGUIStyleUtility.DrawExpandingTextArea(
                        translationEntry,
                        EditorGUIUtility.currentViewWidth
                    );

                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(languageData, "Update Translation");
                        languageData.allText[keyIndex] = newTranslation;
                        EditorUtility.SetDirty(languageData);
                    }

                    // Create tooltip content showing what will be sent to DeepL
                    string tooltipContent = "";
                    if (!string.IsNullOrEmpty(deeplApiKey))
                    {
                        string textToTranslate = selectedKeys.First();
                        string context = includeContextInTranslation ? translationData.Metadata.GetTranslationContext(textToTranslate) : "";
                        tooltipContent = "DeepL Query Preview:\n\n" +
                            $"Text: \"{textToTranslate}\"\n" +
                            $"Target Language: {language}\n" +
                            (includeContextInTranslation ? $"Context: \"{context}\"\n" : "No context will be sent\n") +
                            $"Formality: {(formalityPreference ? "More formal" : "Less formal")}\n" +
                            $"Preserve Formatting: {(preserveFormatting ? "Yes" : "No")}";
                    }
                    else
                    {
                        tooltipContent = "DeepL API key not configured";
                    }

                    // Create button content with tooltip
                    var buttonContent = new GUIContent("Auto", tooltipContent);
                    if (GUILayout.Button(buttonContent, GUILayout.Width(50)))
                    {
                        if (!string.IsNullOrEmpty(deeplApiKey))
                        {
                            TranslateSingleField(selectedKeys.First(), language, keyIndex, languageData);
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

        private void HandleSelection(string key)
        {
            bool multiSelect = Event.current.control || Event.current.command;
            bool rangeSelect = Event.current.shift;

            if (rangeSelect && selectedKeys.Count > 0)
            {
                // Get the starting key for range selection
                string startKey = lastControlClickedKey ?? selectedKeys.First();
                int startIdx = filteredKeysList.IndexOf(startKey);
                int endIdx = filteredKeysList.IndexOf(key);

                // Swap if needed to ensure correct range
                if (startIdx > endIdx)
                {
                    int temp = startIdx;
                    startIdx = endIdx;
                    endIdx = temp;
                }

                // Clear current selection if not also holding control/command
                if (!multiSelect)
                    selectedKeys.Clear();

                // Select all keys in the range
                for (int i = startIdx; i <= endIdx; i++)
                {
                    if (i >= 0 && i < filteredKeysList.Count)
                    {
                        selectedKeys.Add(filteredKeysList[i]);
                    }
                }
            }
            else if (multiSelect)
            {
                // Toggle selection and update last control-clicked key
                if (!selectedKeys.Add(key))  // Returns false if already present
                {
                    selectedKeys.Remove(key);
                    // If we're removing the last control-clicked key, clear it
                    if (key == lastControlClickedKey)
                        lastControlClickedKey = selectedKeys.FirstOrDefault();
                }
                else
                {
                    lastControlClickedKey = key;
                }
            }
            else
            {
                // Single select
                if (selectedKeys.Count == 1 && selectedKeys.Contains(key))
                {
                    selectedKeys.Clear();  // Deselect if already selected
                    lastControlClickedKey = null;
                }
                else
                {
                    selectedKeys.Clear();
                    selectedKeys.Add(key);
                    lastControlClickedKey = key;
                }
            }
        }

        private bool NeedsTranslations(string key)
        {
            int keyIndex = translationData.allKeys.IndexOf(key);
            
            for (int i = 0; i < translationData.languageDataDictionary.Length; i++)
            {
                var assetRef = translationData.languageDataDictionary[i];
                string assetPath = AssetDatabase.GUIDToAssetPath(assetRef.AssetGUID);
                LanguageData languageData = AssetDatabase.LoadAssetAtPath<LanguageData>(assetPath);
                
                if (languageData != null && keyIndex >= 0 && keyIndex < languageData.allText.Count)
                {
                    string translation = languageData.allText[keyIndex];
                    // Consider it needing translations if it's empty or whitespace
                    if (string.IsNullOrWhiteSpace(translation))
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }
    }
} 