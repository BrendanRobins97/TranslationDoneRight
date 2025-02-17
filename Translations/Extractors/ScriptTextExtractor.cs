#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;

namespace PSS
{
    public class ScriptTextExtractor : ITextExtractor
    {
        public TextSourceType SourceType => TextSourceType.Script;
        public int Priority => 80;
        public bool EnabledByDefault => true;
        public string Description => "Extracts text from all C# scripts in the project, finding Translate() and TranslateString() function calls.";

        public HashSet<string> ExtractText(TranslationMetadata metadata)
        {
            var extractedText = new HashSet<string>();
            string[] scriptPaths = Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories);

            // Match both Translations.Translate("text") and "text".TranslateString()
            var patterns = new[]
            {
                new { Pattern = @"Translations\.Translate\(\s*""([^""]+)""\s*\)", Method = "Translate()" },
                new { Pattern = @"""([^""]+)""\s*\.TranslateString\(\s*\)", Method = "TranslateString()" }
            };

            foreach (string scriptPath in scriptPaths)
            {
                string scriptContent = File.ReadAllText(scriptPath);
                
                foreach (var pattern in patterns)
                {
                    var regex = new Regex(pattern.Pattern);
                    MatchCollection matches = regex.Matches(scriptContent);

                    foreach (Match match in matches)
                    {
                        if (match.Groups.Count > 1)
                        {
                            string matchedText = match.Groups[1].Value;
                            extractedText.Add(matchedText);
                            
                            // Get line number for better context
                            int lineNumber = GetLineNumber(scriptContent, match.Index);
                            
                            var sourceInfo = new TextSourceInfo
                            {
                                sourceType = TextSourceType.Script,
                                sourcePath = scriptPath,
                                componentName = Path.GetFileNameWithoutExtension(scriptPath),
                                fieldName = $"{pattern.Method} call at line {lineNumber}"
                            };
                            metadata.AddSource(matchedText, sourceInfo);
                        }
                    }
                }
            }

            return extractedText;
        }

        private int GetLineNumber(string content, int position)
        {
            int lineNumber = 1;
            for (int i = 0; i < position; i++)
            {
                if (content[i] == '\n')
                {
                    lineNumber++;
                }
            }
            return lineNumber;
        }
    }
}
#endif 