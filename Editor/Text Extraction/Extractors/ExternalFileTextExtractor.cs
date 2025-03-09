#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Xml;
using System.Threading.Tasks;
using System;

namespace Translations
{
    /// <summary>
    /// Extracts translatable text from external files (JSON, CSV, XML, TXT)
    /// </summary>
    public class ExternalFileTextExtractor : ITextExtractor
    {
        // File extension type cache to avoid redundant string comparisons
        private enum FileType
        {
            JSON,
            CSV,
            XML,
            TXT,
            Unsupported
        }
        
        // Cache for file types to avoid string comparisons
        private static readonly Dictionary<string, FileType> _fileTypeCache = new Dictionary<string, FileType>();
        
        [System.Serializable]
        private class LocalizationEntry
        {
            public string text;
            public string[] texts;
            public LocalizationEntry[] children;
            // Dynamic dictionary representation
            public List<LocalizationKeyValue> values;
        }

        [System.Serializable]
        private class LocalizationKeyValue
        {
            public string key;
            public string value;
        }

        public TextSourceType SourceType => TextSourceType.ExternalFile;
        public int Priority => 60; // Lower priority than built-in extractors
        public bool EnabledByDefault => true;
        public string Description => "Extracts text from external files (JSON, CSV, XML, TXT) in configured directories.";

        // Configurable settings that can be modified in the editor
        private static readonly string[] DefaultSearchPaths = new[]
        {
            "Assets/Localization",
            "Assets/Resources/Localization",
            "Assets/Data/Localization"
        };

        private static readonly string[] SupportedExtensions = new[]
        {
            ".json",
            ".csv",
            ".xml",
            ".txt"
        };

        // CSV column names that likely contain translatable text
        private static readonly string[] TextColumnIdentifiers = new[]
        {
            "text",
            "string",
            "message",
            "description",
            "content",
            "translation",
            "english",
            "default"
        };
        
        // Precomputed set for faster lookups
        private static readonly HashSet<string> TextColumnIdentifiersSet;
        
        static ExternalFileTextExtractor()
        {
            // Initialize file type cache
            _fileTypeCache[".json"] = FileType.JSON;
            _fileTypeCache[".csv"] = FileType.CSV;
            _fileTypeCache[".xml"] = FileType.XML;
            _fileTypeCache[".txt"] = FileType.TXT;
            
            // Initialize text column identifiers set
            TextColumnIdentifiersSet = new HashSet<string>(TextColumnIdentifiers.Select(id => id.ToLower()));
        }

        public HashSet<string> ExtractText(TranslationMetadata metadata)
        {
            var extractedText = new HashSet<string>();
            
            return ITextExtractor.ProcessSourcesOrAll<string[]>(
                this,
                metadata,
                () => {
                    // Process default search paths
                    ProcessDefaultPaths(extractedText, metadata);
                    return extractedText;
                },
                (sources) => {
                    // Process only external files within specified sources
                    ProcessSourceList(sources, extractedText, metadata);
                    return extractedText;
                }
            );
        }
        
        private void ProcessDefaultPaths(HashSet<string> extractedText, TranslationMetadata metadata)
        {
            HashSet<string> filesToProcess = new HashSet<string>();
            
            foreach (string basePath in DefaultSearchPaths)
            {
                if (!Directory.Exists(basePath)) continue;

                // Get all files with supported extensions in the directory and its subdirectories
                var files = SupportedExtensions
                    .SelectMany(ext => Directory.GetFiles(basePath, $"*{ext}", SearchOption.AllDirectories));
                
                foreach (var file in files)
                {
                    filesToProcess.Add(file);
                }
            }

            ProcessFiles(filesToProcess, extractedText, metadata);
        }
        
        private void ProcessSourceList(ExtractionSourcesList sources, HashSet<string> extractedText, TranslationMetadata metadata)
        {
            HashSet<string> filesToProcess = new HashSet<string>();
            
            foreach (var source in sources.Items)
            {
                if (source.type == ExtractionSourceType.Folder)
                {
                    string folderPath = source.folderPath;
                    if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath)) continue;
                    
                    // Get all supported files
                    foreach (var ext in SupportedExtensions)
                    {
                        var files = Directory.GetFiles(folderPath, $"*{ext}", source.recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
                        foreach (var file in files)
                        {
                            if (TextExtractor.ShouldProcessPath(file, metadata, GetType()))
                            {
                                filesToProcess.Add(file);
                            }
                        }
                    }
                }
                else if (source.type == ExtractionSourceType.Asset && source.asset != null)
                {
                    string assetPath = AssetDatabase.GetAssetPath(source.asset);
                    string ext = Path.GetExtension(assetPath).ToLower();
                    
                    if (SupportedExtensions.Contains(ext) && 
                        TextExtractor.ShouldProcessPath(assetPath, metadata, GetType()))
                    {
                        filesToProcess.Add(assetPath);
                    }
                }
            }
            
            ProcessFiles(filesToProcess, extractedText, metadata);
        }
        
        private void ProcessFiles(IEnumerable<string> filePaths, HashSet<string> extractedText, TranslationMetadata metadata)
        {
            // Convert to list for easier processing
            var fileList = filePaths.ToList();
            
            // Thread local collections
            var localResults = new Dictionary<string, HashSet<string>>();
            var localSourceInfos = new Dictionary<string, List<KeyValuePair<string, TextSourceInfo>>>();
            var lockObject = new object();
            
            // Process files in sequential batches to avoid UI thread issues
            int batchSize = 20; // Process 20 files at a time
            for (int startIndex = 0; startIndex < fileList.Count; startIndex += batchSize)
            {
                int endIndex = Math.Min(startIndex + batchSize, fileList.Count);
                
                // Process this batch of files sequentially - this is safer for Unity
                for (int i = startIndex; i < endIndex; i++)
                {
                    string filePath = fileList[i];
                    var fileLocalResults = new HashSet<string>();
                    var fileLocalSourceInfos = new List<KeyValuePair<string, TextSourceInfo>>();
                    
                    // Process the file
                    try
                    {
                        ProcessSingleFileInternal(filePath, fileLocalResults, fileLocalSourceInfos);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"Error processing {filePath}: {e.Message}");
                    }
                    
                    // Store the results
                    lock (lockObject)
                    {
                        localResults[filePath] = fileLocalResults;
                        localSourceInfos[filePath] = fileLocalSourceInfos;
                    }
                }
            }
            
            // Merge all results on main thread
            foreach (var result in localResults.Values)
            {
                extractedText.UnionWith(result);
            }
            
            // Add all source info on main thread
            foreach (var sourceList in localSourceInfos.Values)
            {
                foreach (var pair in sourceList)
                {
                    metadata.AddSource(pair.Key, pair.Value);
                }
            }
        }
        
        // Internal implementation that doesn't directly modify shared state
        private void ProcessSingleFileInternal(string filePath, HashSet<string> localExtractedText, List<KeyValuePair<string, TextSourceInfo>> localSourceInfos)
        {
            // Get file extension and determine how to process it
            string ext = Path.GetExtension(filePath).ToLower();
            
            // Use the cached file type lookup
            FileType fileType = FileType.Unsupported;
            if (!_fileTypeCache.TryGetValue(ext, out fileType))
            {
                return; // Unsupported file type
            }
            
            switch (fileType)
            {
                case FileType.JSON:
                    ExtractFromJsonInternal(filePath, localExtractedText, localSourceInfos);
                    break;
                case FileType.CSV:
                    ExtractFromCsvInternal(filePath, localExtractedText, localSourceInfos);
                    break;
                case FileType.XML:
                    ExtractFromXmlInternal(filePath, localExtractedText, localSourceInfos);
                    break;
                case FileType.TXT:
                    ExtractFromTextFileInternal(filePath, localExtractedText, localSourceInfos);
                    break;
            }
        }
        
        // New version that works with local collections
        private void AddExternalTextInternal(string text, string filePath, string location, HashSet<string> localExtractedText, List<KeyValuePair<string, TextSourceInfo>> localSourceInfos)
        {
            // Skip empty or whitespace-only text
            if (string.IsNullOrWhiteSpace(text)) return;
            
            localExtractedText.Add(text);
            
            var sourceInfo = new TextSourceInfo
            {
                sourceType = TextSourceType.ExternalFile,
                sourcePath = filePath,
                componentName = Path.GetFileName(filePath),
                fieldName = location
            };
            
            localSourceInfos.Add(new KeyValuePair<string, TextSourceInfo>(text, sourceInfo));
        }

        // Keep the original ProcessSingleFile for backward compatibility
        private void ProcessSingleFile(string filePath, HashSet<string> extractedText, TranslationMetadata metadata)
        {
            var localExtractedText = new HashSet<string>();
            var localSourceInfos = new List<KeyValuePair<string, TextSourceInfo>>();
            
            ProcessSingleFileInternal(filePath, localExtractedText, localSourceInfos);
            
            // Update the shared collections
            extractedText.UnionWith(localExtractedText);
            foreach (var pair in localSourceInfos)
            {
                metadata.AddSource(pair.Key, pair.Value);
            }
        }

        // Original method for backward compatibility
        private void AddExternalText(string text, string filePath, string location, HashSet<string> extractedText, TranslationMetadata metadata)
        {
            // Skip empty or whitespace-only text
            if (string.IsNullOrWhiteSpace(text)) return;
            
            extractedText.Add(text);
            
            var sourceInfo = new TextSourceInfo
            {
                sourceType = TextSourceType.ExternalFile,
                sourcePath = filePath,
                componentName = Path.GetFileName(filePath),
                fieldName = location
            };
            
            metadata.AddSource(text, sourceInfo);
        }

        // Update JSON extractor to use the internal method
        private void ExtractFromJsonInternal(string filePath, HashSet<string> localExtractedText, List<KeyValuePair<string, TextSourceInfo>> localSourceInfos)
        {
            string jsonContent = File.ReadAllText(filePath);
            
            try
            {
                // Try to parse as a localization entry
                var entry = JsonUtility.FromJson<LocalizationEntry>(jsonContent);
                if (entry != null)
                {
                    ExtractFromLocalizationEntryInternal(entry, "root", filePath, localExtractedText, localSourceInfos);
                    return;
                }
            }
            catch
            {
                // Fall back to regex extraction if JSON parsing fails
            }
            
            // Fall back to regex-based extraction for non-standard JSON
            var stringValueRegex = new Regex(@"""([^""\\]*(?:\\.[^""\\]*)*)""", RegexOptions.Compiled);
            var matches = stringValueRegex.Matches(jsonContent);
            
            foreach (Match match in matches)
            {
                if (match.Groups.Count > 1)
                {
                    string value = match.Groups[1].Value;
                    
                    // Skip likely JSON property names (short, no spaces, etc.)
                    if (value.Length > 3 && !value.All(c => char.IsLetterOrDigit(c) || c == '_'))
                    {
                        AddExternalTextInternal(value, filePath, "json/value", localExtractedText, localSourceInfos);
                    }
                }
            }
        }

        private void ExtractFromLocalizationEntryInternal(LocalizationEntry entry, string path, string filePath, HashSet<string> localExtractedText, List<KeyValuePair<string, TextSourceInfo>> localSourceInfos)
        {
            if (entry == null) return;
            
            // Extract simple text
            if (!string.IsNullOrWhiteSpace(entry.text))
            {
                AddExternalTextInternal(entry.text, filePath, $"{path}/text", localExtractedText, localSourceInfos);
            }

            // Extract text array
            if (entry.texts != null)
            {
                for (int i = 0; i < entry.texts.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(entry.texts[i]))
                    {
                        AddExternalTextInternal(entry.texts[i], filePath, $"{path}/texts[{i}]", localExtractedText, localSourceInfos);
                    }
                }
            }

            // Extract key-value pairs
            if (entry.values != null)
            {
                foreach (var kv in entry.values)
                {
                    if (!string.IsNullOrWhiteSpace(kv.value))
                    {
                        AddExternalTextInternal(kv.value, filePath, $"{path}/{kv.key}", localExtractedText, localSourceInfos);
                    }
                }
            }

            // Recursively extract from children
            if (entry.children != null)
            {
                for (int i = 0; i < entry.children.Length; i++)
                {
                    ExtractFromLocalizationEntryInternal(entry.children[i], $"{path}/children[{i}]", filePath, localExtractedText, localSourceInfos);
                }
            }
        }

        private void ExtractFromCsvInternal(string filePath, HashSet<string> localExtractedText, List<KeyValuePair<string, TextSourceInfo>> localSourceInfos)
        {
            string[] lines = File.ReadAllLines(filePath);
            if (lines.Length == 0) return;

            // Try to identify text columns by header
            string[] headers = lines[0].Split(',');
            var textColumnIndices = new List<int>();

            for (int i = 0; i < headers.Length; i++)
            {
                string header = headers[i].Trim('"').ToLower();
                if (TextColumnIdentifiersSet.Any(id => header.Contains(id)))
                {
                    textColumnIndices.Add(i);
                }
            }

            // If no text columns identified, use all columns except the first (often an ID)
            if (textColumnIndices.Count == 0 && headers.Length > 1)
            {
                for (int i = 1; i < headers.Length; i++)
                {
                    textColumnIndices.Add(i);
                }
            }

            // Process data rows
            for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Split by comma, handling quoted fields correctly
                var fields = SplitCsvLine(line);

                foreach (int colIndex in textColumnIndices)
                {
                    if (colIndex < fields.Length)
                    {
                        string field = fields[colIndex].Trim().Trim('"');
                        if (!string.IsNullOrWhiteSpace(field))
                        {
                            AddExternalTextInternal(field, filePath, $"row {lineIndex}, col {colIndex}", localExtractedText, localSourceInfos);
                        }
                    }
                }
            }
        }
        
        // Helper to correctly split CSV lines handling quoted fields
        private string[] SplitCsvLine(string line)
        {
            List<string> result = new List<string>();
            bool inQuotes = false;
            string field = "";
            
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(field);
                    field = "";
                }
                else
                {
                    field += c;
                }
            }
            
            result.Add(field); // Add the last field
            return result.ToArray();
        }

        private void ExtractFromTextFileInternal(string filePath, HashSet<string> localExtractedText, List<KeyValuePair<string, TextSourceInfo>> localSourceInfos)
        {
            string[] lines = File.ReadAllLines(filePath);
            
            // Process each non-empty line as a potential translatable string
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    AddExternalTextInternal(line, filePath, $"line {i + 1}", localExtractedText, localSourceInfos);
                }
            }
        }

        private void ExtractFromXmlInternal(string filePath, HashSet<string> localExtractedText, List<KeyValuePair<string, TextSourceInfo>> localSourceInfos)
        {
            var doc = new XmlDocument();
            doc.Load(filePath);
            ExtractXmlNodeTextInternal(doc.DocumentElement, "", filePath, localExtractedText, localSourceInfos);
        }

        private void ExtractXmlNodeTextInternal(XmlNode node, string path, string filePath, HashSet<string> localExtractedText, List<KeyValuePair<string, TextSourceInfo>> localSourceInfos)
        {
            // Extract text content if it's not just whitespace
            if (node.NodeType == XmlNodeType.Text && !string.IsNullOrWhiteSpace(node.Value))
            {
                AddExternalTextInternal(node.Value.Trim(), filePath, path, localExtractedText, localSourceInfos);
            }

            // Extract attribute values
            if (node.Attributes != null)
            {
                foreach (XmlAttribute attr in node.Attributes)
                {
                    if (!string.IsNullOrWhiteSpace(attr.Value))
                    {
                        AddExternalTextInternal(attr.Value, filePath, $"{path}/@{attr.Name}", localExtractedText, localSourceInfos);
                    }
                }
            }

            // Recurse through child nodes
            foreach (XmlNode child in node.ChildNodes)
            {
                ExtractXmlNodeTextInternal(child, $"{path}/{child.Name}", filePath, localExtractedText, localSourceInfos);
            }
        }

        // Original extraction methods for backward compatibility
        private void ExtractFromJson(string filePath, HashSet<string> extractedText, TranslationMetadata metadata)
        {
            var localExtractedText = new HashSet<string>();
            var localSourceInfos = new List<KeyValuePair<string, TextSourceInfo>>();
            
            ExtractFromJsonInternal(filePath, localExtractedText, localSourceInfos);
            
            // Update the shared collections
            extractedText.UnionWith(localExtractedText);
            foreach (var pair in localSourceInfos)
            {
                metadata.AddSource(pair.Key, pair.Value);
            }
        }
        
        private void ExtractFromLocalizationEntry(LocalizationEntry entry, string path, string filePath, HashSet<string> extractedText, TranslationMetadata metadata)
        {
            var localExtractedText = new HashSet<string>();
            var localSourceInfos = new List<KeyValuePair<string, TextSourceInfo>>();
            
            ExtractFromLocalizationEntryInternal(entry, path, filePath, localExtractedText, localSourceInfos);
            
            // Update the shared collections
            extractedText.UnionWith(localExtractedText);
            foreach (var pair in localSourceInfos)
            {
                metadata.AddSource(pair.Key, pair.Value);
            }
        }
        
        private void ExtractFromCsv(string filePath, HashSet<string> extractedText, TranslationMetadata metadata)
        {
            var localExtractedText = new HashSet<string>();
            var localSourceInfos = new List<KeyValuePair<string, TextSourceInfo>>();
            
            ExtractFromCsvInternal(filePath, localExtractedText, localSourceInfos);
            
            // Update the shared collections
            extractedText.UnionWith(localExtractedText);
            foreach (var pair in localSourceInfos)
            {
                metadata.AddSource(pair.Key, pair.Value);
            }
        }
        
        private void ExtractFromTextFile(string filePath, HashSet<string> extractedText, TranslationMetadata metadata)
        {
            var localExtractedText = new HashSet<string>();
            var localSourceInfos = new List<KeyValuePair<string, TextSourceInfo>>();
            
            ExtractFromTextFileInternal(filePath, localExtractedText, localSourceInfos);
            
            // Update the shared collections
            extractedText.UnionWith(localExtractedText);
            foreach (var pair in localSourceInfos)
            {
                metadata.AddSource(pair.Key, pair.Value);
            }
        }
        
        private void ExtractFromXml(string filePath, HashSet<string> extractedText, TranslationMetadata metadata)
        {
            var localExtractedText = new HashSet<string>();
            var localSourceInfos = new List<KeyValuePair<string, TextSourceInfo>>();
            
            ExtractFromXmlInternal(filePath, localExtractedText, localSourceInfos);
            
            // Update the shared collections
            extractedText.UnionWith(localExtractedText);
            foreach (var pair in localSourceInfos)
            {
                metadata.AddSource(pair.Key, pair.Value);
            }
        }
        
        private void ExtractXmlNodeText(XmlNode node, string path, string filePath, HashSet<string> extractedText, TranslationMetadata metadata)
        {
            var localExtractedText = new HashSet<string>();
            var localSourceInfos = new List<KeyValuePair<string, TextSourceInfo>>();
            
            ExtractXmlNodeTextInternal(node, path, filePath, localExtractedText, localSourceInfos);
            
            // Update the shared collections
            extractedText.UnionWith(localExtractedText);
            foreach (var pair in localSourceInfos)
            {
                metadata.AddSource(pair.Key, pair.Value);
            }
        }
    }
}
#endif 