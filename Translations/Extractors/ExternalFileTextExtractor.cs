#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Xml;

namespace PSS
{
    /// <summary>
    /// Extracts translatable text from external files (JSON, CSV, XML, TXT)
    /// </summary>
    public class ExternalFileTextExtractor : ITextExtractor
    {
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

        public TextSourceType SourceType => TextSourceType.Script;
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

        public HashSet<string> ExtractText(TranslationMetadata metadata)
        {
            var extractedText = new HashSet<string>();

            foreach (string basePath in DefaultSearchPaths)
            {
                if (!Directory.Exists(basePath)) continue;

                // Get all files with supported extensions in the directory and its subdirectories
                var files = SupportedExtensions
                    .SelectMany(ext => Directory.GetFiles(basePath, $"*{ext}", SearchOption.AllDirectories));

                foreach (string filePath in files)
                {
                    string extension = Path.GetExtension(filePath).ToLower();
                    try
                    {
                        switch (extension)
                        {
                            case ".json":
                                ExtractFromJson(filePath, extractedText, metadata);
                                break;
                            case ".csv":
                                ExtractFromCsv(filePath, extractedText, metadata);
                                break;
                            case ".xml":
                                ExtractFromXml(filePath, extractedText, metadata);
                                break;
                            case ".txt":
                                ExtractFromTxt(filePath, extractedText, metadata);
                                break;
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"Failed to extract text from {filePath}: {e.Message}");
                    }
                }
            }

            return extractedText;
        }

        private void ExtractFromJson(string filePath, HashSet<string> extractedText, TranslationMetadata metadata)
        {
            string jsonContent = File.ReadAllText(filePath);

            try
            {
                // Try to parse as a single entry first
                var entry = JsonUtility.FromJson<LocalizationEntry>(jsonContent);
                ExtractFromLocalizationEntry(entry, "", filePath, extractedText, metadata);
            }
            catch
            {
                try
                {
                    // Try to parse as an array of entries
                    var wrapper = JsonUtility.FromJson<LocalizationWrapper>("{\"items\":" + jsonContent + "}");
                    if (wrapper?.items != null)
                    {
                        foreach (var entry in wrapper.items)
                        {
                            ExtractFromLocalizationEntry(entry, "", filePath, extractedText, metadata);
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Failed to parse JSON file {filePath}: {e.Message}");
                }
            }
        }

        [System.Serializable]
        private class LocalizationWrapper
        {
            public LocalizationEntry[] items;
        }

        private void ExtractFromLocalizationEntry(LocalizationEntry entry, string path, string filePath, HashSet<string> extractedText, TranslationMetadata metadata)
        {
            if (entry == null) return;

            // Extract single text
            if (!string.IsNullOrWhiteSpace(entry.text))
            {
                AddExternalText(entry.text, filePath, path + "/text", extractedText, metadata);
            }

            // Extract text array
            if (entry.texts != null)
            {
                for (int i = 0; i < entry.texts.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(entry.texts[i]))
                    {
                        AddExternalText(entry.texts[i], filePath, $"{path}/texts[{i}]", extractedText, metadata);
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
                        AddExternalText(kv.value, filePath, $"{path}/{kv.key}", extractedText, metadata);
                    }
                }
            }

            // Recursively extract from children
            if (entry.children != null)
            {
                for (int i = 0; i < entry.children.Length; i++)
                {
                    ExtractFromLocalizationEntry(entry.children[i], $"{path}/children[{i}]", filePath, extractedText, metadata);
                }
            }
        }

        private void ExtractFromCsv(string filePath, HashSet<string> extractedText, TranslationMetadata metadata)
        {
            string[] lines = File.ReadAllLines(filePath);
            if (lines.Length == 0) return;

            // Try to identify text columns by header
            string[] headers = lines[0].Split(',');
            var textColumnIndices = new List<int>();

            for (int i = 0; i < headers.Length; i++)
            {
                string header = headers[i].Trim('"').ToLower();
                if (TextColumnIdentifiers.Any(id => header.Contains(id)))
                {
                    textColumnIndices.Add(i);
                }
            }

            // If no text columns identified, assume all columns might contain text
            if (textColumnIndices.Count == 0)
            {
                textColumnIndices.AddRange(Enumerable.Range(0, headers.Length));
            }

            // Process each line
            for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
            {
                string[] columns = SplitCsvLine(lines[lineIndex]);
                foreach (int colIndex in textColumnIndices)
                {
                    if (colIndex < columns.Length)
                    {
                        string text = columns[colIndex].Trim('"');
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            AddExternalText(text, filePath, $"Row {lineIndex}, Column {headers[colIndex]}", extractedText, metadata);
                        }
                    }
                }
            }
        }

        private string[] SplitCsvLine(string line)
        {
            // Handle CSV escaping and quoting
            var result = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (line[i] == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(line[i]);
                }
            }
            result.Add(current.ToString());
            return result.ToArray();
        }

        private void ExtractFromXml(string filePath, HashSet<string> extractedText, TranslationMetadata metadata)
        {
            var doc = new XmlDocument();
            doc.Load(filePath);
            ExtractXmlNodeText(doc.DocumentElement, "", filePath, extractedText, metadata);
        }

        private void ExtractXmlNodeText(XmlNode node, string path, string filePath, HashSet<string> extractedText, TranslationMetadata metadata)
        {
            // Extract text content if it's not just whitespace
            if (node.NodeType == XmlNodeType.Text && !string.IsNullOrWhiteSpace(node.Value))
            {
                AddExternalText(node.Value.Trim(), filePath, path, extractedText, metadata);
            }

            // Extract attribute values
            if (node.Attributes != null)
            {
                foreach (XmlAttribute attr in node.Attributes)
                {
                    if (!string.IsNullOrWhiteSpace(attr.Value))
                    {
                        AddExternalText(attr.Value, filePath, $"{path}/@{attr.Name}", extractedText, metadata);
                    }
                }
            }

            // Recurse through child nodes
            foreach (XmlNode child in node.ChildNodes)
            {
                ExtractXmlNodeText(child, $"{path}/{child.Name}", filePath, extractedText, metadata);
            }
        }

        private void ExtractFromTxt(string filePath, HashSet<string> extractedText, TranslationMetadata metadata)
        {
            string[] lines = File.ReadAllLines(filePath);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    // For txt files, try to detect if it's a key-value format
                    var keyValueMatch = Regex.Match(line, @"^([^=:]+)[=:]\s*(.+)$");
                    if (keyValueMatch.Success)
                    {
                        string value = keyValueMatch.Groups[2].Value.Trim();
                        AddExternalText(value, filePath, $"Line {i + 1}: {keyValueMatch.Groups[1].Value.Trim()}", extractedText, metadata);
                    }
                    else
                    {
                        AddExternalText(line, filePath, $"Line {i + 1}", extractedText, metadata);
                    }
                }
            }
        }

        private void AddExternalText(string text, string filePath, string location, HashSet<string> extractedText, TranslationMetadata metadata)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            extractedText.Add(text);
            
            var sourceInfo = new TextSourceInfo
            {
                sourceType = TextSourceType.Script,
                sourcePath = filePath,
                componentName = Path.GetFileName(filePath),
                fieldName = location
            };
            metadata.AddSource(text, sourceInfo);
        }
    }
}
#endif 