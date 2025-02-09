using UnityEngine;
using UnityEditor;

namespace PSS
{
    public static class EditorGUIStyleUtility
    {
        private static GUIStyle expandingTextAreaStyle;
        private static GUIStyle warningLabelStyle;

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