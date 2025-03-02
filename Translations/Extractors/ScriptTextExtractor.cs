#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;
using System.Linq;

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
            
            // If no sources specified, search entire project
            if (metadata.extractionSources == null || metadata.extractionSources.Count == 0)
            {
                string[] scriptGuids = AssetDatabase.FindAssets("t:Script");
                ProcessScripts(scriptGuids, extractedText, metadata);
                return extractedText;
            }

            // Search only within specified sources
            foreach (var source in metadata.extractionSources)
            {
                string searchFolder = source.type == ExtractionSourceType.Folder ? source.folderPath : Path.GetDirectoryName(AssetDatabase.GetAssetPath(source.asset));
                if (string.IsNullOrEmpty(searchFolder)) continue;

                // Normalize path
                searchFolder = searchFolder.Replace('\\', '/').TrimStart('/');
                if (!searchFolder.StartsWith("Assets/"))
                    searchFolder = "Assets/" + searchFolder;

                string[] scriptGuids = AssetDatabase.FindAssets("t:Script", new[] { searchFolder });
                ProcessScripts(scriptGuids, extractedText, metadata);
            }

            return extractedText;
        }

        private void ProcessScripts(string[] scriptGuids, HashSet<string> extractedText, TranslationMetadata metadata)
        {
            var patterns = new[]
            {
                new { Pattern = @"Translations\.Translate\(\s*""([^""]+)""\s*\)", Method = "Translate()" },
                new { Pattern = @"""([^""]+)""\s*\.TranslateString\(\s*\)", Method = "TranslateString()" },
                new { Pattern = @"SetTextTranslated\(\s*""([^""]+)""\s*[,)]", Method = "SetTextTranslated()" },
                new { Pattern = @"Translations\.Format\(\s*""([^""]+)""\s*,([^)]*)\)", Method = "Format()" },
            };

            foreach (string guid in scriptGuids)
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(guid);
                string scriptContent = File.ReadAllText(scriptPath);
                
                foreach (var pattern in patterns)
                {
                    var regex = new Regex(pattern.Pattern, RegexOptions.Multiline);
                    MatchCollection matches = regex.Matches(scriptContent);

                    foreach (Match match in matches)
                    {
                        if (match.Groups.Count > 1)
                        {
                            string matchedText = match.Groups[1].Value;
                            extractedText.Add(matchedText);
                            
                            var sourceInfo = new TextSourceInfo
                            {
                                sourceType = TextSourceType.Script,
                                sourcePath = scriptPath,
                                componentName = Path.GetFileNameWithoutExtension(scriptPath),
                                fieldName = $"{pattern.Method} call at line {GetLineNumber(scriptContent, match.Index)}"
                            };
                            metadata.AddSource(matchedText, sourceInfo);

                            // For FormatTranslated, also extract string arguments
                            if (pattern.Method == "FormatTranslated()" && match.Groups.Count > 2)
                            {
                                var args = match.Groups[2].Value;
                                var stringArgPattern = new Regex(@"""([^""]+)""");
                                var argMatches = stringArgPattern.Matches(args);
                                foreach (Match argMatch in argMatches)
                                {
                                    if (argMatch.Groups.Count > 1)
                                    {
                                        string argText = argMatch.Groups[1].Value;
                                        extractedText.Add(argText);
                                        
                                        var argSourceInfo = new TextSourceInfo
                                        {
                                            sourceType = TextSourceType.Script,
                                            sourcePath = scriptPath,
                                            componentName = Path.GetFileNameWithoutExtension(scriptPath),
                                            fieldName = $"{pattern.Method} argument at line {GetLineNumber(scriptContent, match.Index + argMatch.Index)}"
                                        };
                                        metadata.AddSource(argText, argSourceInfo);
                                    }
                                }
                            }
                        }
                    }
                }
            }
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