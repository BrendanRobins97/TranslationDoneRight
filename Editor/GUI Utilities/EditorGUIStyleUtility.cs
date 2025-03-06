using UnityEngine;
using UnityEditor;

namespace Translations
{
    public static class EditorGUIStyleUtility
    {
        private static GUIStyle expandingTextAreaStyle;
        private static GUIStyle warningLabelStyle;
        private static GUIStyle headerLabelStyle;
        private static GUIStyle foldoutHeaderStyle;
        private static GUIStyle cardStyle;

        public static GUIStyle ExpandingTextAreaStyle
        {
            get
            {
                if (expandingTextAreaStyle == null)
                {
                    expandingTextAreaStyle = new GUIStyle(EditorStyles.textArea)
                    {
                        wordWrap = true,
                        stretchHeight = true,
                        fixedHeight = 0
                    };
                }
                return expandingTextAreaStyle;
            }
        }

        public static GUIStyle WarningLabelStyle
        {
            get
            {
                if (warningLabelStyle == null)
                {
                    warningLabelStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        wordWrap = true,
                        normal = { textColor = Color.yellow }
                    };
                }
                return warningLabelStyle;
            }
        }

        public static GUIStyle HeaderLabel
        {
            get
            {
                if (headerLabelStyle == null)
                {
                    headerLabelStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 18,
                        margin = new RectOffset(4, 0, 0, 4),
                        padding = new RectOffset(4, 0, 0, 4),
                        alignment = TextAnchor.MiddleLeft
                    };
                }
                return headerLabelStyle;
            }
        }

        public static GUIStyle FoldoutHeader
        {
            get
            {
                if (foldoutHeaderStyle == null)
                {
                    foldoutHeaderStyle = new GUIStyle(EditorStyles.foldout)
                    {
                        fontSize = 12,
                        fontStyle = FontStyle.Bold,
                        margin = new RectOffset(4, 4, 4, 4),
                        padding = new RectOffset(18, 2, 2, 2)
                    };
                }
                return foldoutHeaderStyle;
            }
        }

        public static GUIStyle CardStyle
        {
            get
            {
                if (cardStyle == null)
                {
                    cardStyle = new GUIStyle(EditorStyles.helpBox)
                    {
                        margin = new RectOffset(4, 4, 2, 2),
                        padding = new RectOffset(8, 8, 8, 8)
                    };
                }
                return cardStyle;
            }
        }

        public static float GetExpandedHeight(string content, float width, float padding = 250f)
        {
            float contentHeight = ExpandingTextAreaStyle.CalcHeight(new GUIContent(content), width - padding);
            float minHeight = EditorGUIUtility.singleLineHeight * 2;
            return Mathf.Max(minHeight, contentHeight);
        }

        public static string DrawExpandingTextArea(string content, float width, float padding = 250f)
        {
            float height = GetExpandedHeight(content, width, padding);
            return EditorGUILayout.TextArea(
                content,
                ExpandingTextAreaStyle,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(height)
            );
        }
    }
} 