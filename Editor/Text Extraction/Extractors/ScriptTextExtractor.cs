#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace Translations
{
    public class ScriptTextExtractor : BaseTextExtractor
    {
        public override TextSourceType SourceType => TextSourceType.Script;
        public override int Priority => 80;
        public override bool EnabledByDefault => true;
        public override string Description => "Extracts text from all C# scripts in the project, finding Translate() and TranslateString() function calls.";

        // Cache of compiled regex patterns to avoid recompilation
        private static readonly Regex[] _compiledPatterns;
        private static readonly Regex _stringArgPattern;
        private static readonly string[] _methodNames;
        
        // Quick filter regex to identify files that might contain translation calls
        // This is a simple pattern that's faster than the detailed ones
        private static readonly Regex _quickFilterRegex;
        
        // Cache for line break indices to speed up line number lookups
        private class ScriptCache
        {
            public string Content;
            public List<int> LineBreakIndices;
            
            public ScriptCache(string content)
            {
                Content = content;
                // Pre-compute line break positions
                LineBreakIndices = new List<int>();
                LineBreakIndices.Add(0); // First line starts at 0
                
                for (int i = 0; i < content.Length; i++)
                {
                    if (content[i] == '\n')
                    {
                        LineBreakIndices.Add(i + 1);
                    }
                }
            }
            
            public int GetLineNumber(int position)
            {
                // Binary search through line break indices for faster lookup
                int index = LineBreakIndices.BinarySearch(position);
                if (index < 0)
                {
                    // If position not found exactly, BinarySearch returns complement of insertion point
                    index = ~index - 1;
                }
                return index + 1; // +1 because line numbers are 1-based
            }
        }
        
        // Initialize regex patterns statically for better performance
        static ScriptTextExtractor()
        {
            var patternStrings = new []
            {
                @"Translations\.Translate\(\s*""([^""]+)""\s*\)",
                @"""([^""]+)""\s*\.TranslateString\(\s*\)",
                @"SetTextTranslated\(\s*""([^""]+)""\s*[,)]",
                @"Translations\.Format\(\s*""([^""]+)""\s*,([^)]*)\)"
            };
            
            _methodNames = new []
            {
                "Translate()",
                "TranslateString()",
                "SetTextTranslated()",
                "Format()"
            };
            
            // Compile all the patterns once
            _compiledPatterns = new Regex[patternStrings.Length];
            for (int i = 0; i < patternStrings.Length; i++)
            {
                _compiledPatterns[i] = new Regex(patternStrings[i], RegexOptions.Compiled | RegexOptions.Multiline);
            }
            
            // Compile the string argument pattern
            _stringArgPattern = new Regex(@"""([^""]+)""", RegexOptions.Compiled);
            
            // Quick filter regex to identify files that might contain translation calls
            _quickFilterRegex = new Regex(
                @"(Translate|TranslateString|SetTextTranslated|Format)\s*\(", 
                RegexOptions.Compiled);
        }

        public override HashSet<string> ExtractText(TranslationMetadata metadata)
        {
            HashSet<string> extractedText = new HashSet<string>();
            
            return ITextExtractor.ProcessSourcesOrAll<string[]>(
                this,
                metadata,
                () => {
                    // Process all scripts
                    string[] scriptGuids = AssetDatabase.FindAssets("t:Script");
                    ProcessScripts(scriptGuids, extractedText, metadata);
                    return extractedText;
                },
                (sources) => {
                    // Process only scripts within specified sources
                    ProcessSourceList(sources, extractedText, metadata);
                    return extractedText;
                }
            );
        }

        private void ProcessSourceList(ExtractionSourcesList sources, HashSet<string> extractedText, TranslationMetadata metadata)
        {
            List<string> scriptGuids = new List<string>();
            
            // Find all scripts without reporting progress
            foreach (var source in sources.Items)
            {
                string searchFolder = source.type == ExtractionSourceType.Folder ? source.folderPath : Path.GetDirectoryName(AssetDatabase.GetAssetPath(source.asset));
                if (string.IsNullOrEmpty(searchFolder)) continue;

                // Normalize path
                searchFolder = searchFolder.Replace('\\', '/').TrimStart('/');
                if (!searchFolder.StartsWith("Assets/"))
                    searchFolder = "Assets/" + searchFolder;

                string[] guids = AssetDatabase.FindAssets("t:Script", new[] { searchFolder });
                scriptGuids.AddRange(guids);
            }
            
            // Process all found scripts
            ProcessScripts(scriptGuids.ToArray(), extractedText, metadata);
        }

        private void ProcessScripts(string[] scriptGuids, HashSet<string> extractedText, TranslationMetadata metadata, float progressOffset = 0f)
        {
            if (scriptGuids.Length == 0)
            {
                ITextExtractor.ReportProgress(this, 1f);
                return;
            }

            float progressIncrement = 1f / scriptGuids.Length;
            float currentProgress = 0f;

            // Dictionary to store script data to avoid multiple disk reads
            var scriptDataByGuid = new Dictionary<string, (string path, string content)>();

            // First pass - read all script files that might contain translations
            for (int i = 0; i < scriptGuids.Length; i++)
            {
                string guid = scriptGuids[i];
                
                // This must be done on the main thread
                string scriptPath = AssetDatabase.GUIDToAssetPath(guid);
                
                // Only process the file if it should be processed
                if (TextExtractor.ShouldProcessPath(scriptPath, metadata, GetType()))
                {
                    try
                    {
                        // Quick check - read just first 8KB to see if it might contain translation calls
                        using (var reader = new StreamReader(scriptPath))
                        {
                            char[] buffer = new char[8192]; // 8KB buffer
                            int read = reader.Read(buffer, 0, buffer.Length);
                            string sample = new string(buffer, 0, read);
                            
                            if (_quickFilterRegex.IsMatch(sample))
                            {
                                // If we found a match in the preview, read the entire file
                                reader.BaseStream.Position = 0;
                                string content = reader.ReadToEnd();
                                scriptDataByGuid[guid] = (scriptPath, content);
                            }
                            // If no match in preview, we might still have translation calls later in the file
                            // Only read the whole file if it's small enough (less than 100KB to limit memory usage)
                            else if (new FileInfo(scriptPath).Length < 102400) 
                            {
                                reader.BaseStream.Position = 0;
                                string content = reader.ReadToEnd();
                                
                                // Check the full content
                                if (_quickFilterRegex.IsMatch(content))
                                {
                                    scriptDataByGuid[guid] = (scriptPath, content);
                                }
                            }
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"Failed to read script {scriptPath}: {e.Message}");
                    }
                }

                currentProgress += progressIncrement * 0.3f; // 30% for reading files
                ITextExtractor.ReportProgress(this, currentProgress);
            }

            // Second pass - process all loaded scripts for text extraction
            var scriptPaths = scriptDataByGuid.Keys.ToArray();
            float processingIncrement = (progressIncrement * 0.7f) / Math.Max(scriptPaths.Length, 1); // 70% for processing
            
            foreach (string guid in scriptPaths)
            {
                var (scriptPath, scriptContent) = scriptDataByGuid[guid];
                
                // Create script cache for line number lookups
                var scriptCache = new ScriptCache(scriptContent);
                
                // Process each regex pattern
                for (int patternIndex = 0; patternIndex < _compiledPatterns.Length; patternIndex++)
                {
                    var regex = _compiledPatterns[patternIndex];
                    MatchCollection matches = regex.Matches(scriptContent);
                    string methodName = _methodNames[patternIndex];
                    
                    foreach (Match match in matches)
                    {
                        if (match.Groups.Count > 1)
                        {
                            string matchedText = match.Groups[1].Value;
                            extractedText.Add(matchedText);
                            
                            // Add source information to metadata
                            var sourceInfo = new TextSourceInfo
                            {
                                sourceType = TextSourceType.Script,
                                sourcePath = scriptPath
                            };
                            
                            metadata.AddSource(matchedText, sourceInfo);
                            
                            // For Format, also extract string arguments
                            if (methodName == "Format()" && match.Groups.Count > 2)
                            {
                                var args = match.Groups[2].Value;
                                var argMatches = _stringArgPattern.Matches(args);
                                foreach (Match argMatch in argMatches)
                                {
                                    if (argMatch.Groups.Count > 1)
                                    {
                                        string argText = argMatch.Groups[1].Value;
                                        extractedText.Add(argText);
                                        
                                        var argSourceInfo = new TextSourceInfo
                                        {
                                            sourceType = TextSourceType.Script,
                                            sourcePath = scriptPath
                                        };
                                        
                                        metadata.AddSource(argText, argSourceInfo);
                                    }
                                }
                            }
                        }
                    }
                }
                
                currentProgress += processingIncrement;
                ITextExtractor.ReportProgress(this, currentProgress);
            }
            
            // Ensure we reach 100%
            ITextExtractor.ReportProgress(this, 1f);
        }
    }
}
#endif 