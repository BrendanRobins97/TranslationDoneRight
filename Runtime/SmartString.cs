using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Translations
{
    /// <summary>
    /// Handles smart string processing with placeholder support for translations
    /// </summary>
    public class SmartString
    {
        // Regex to match placeholders like {variable:formatter:options}
        private static readonly Regex PlaceholderRegex = new Regex(@"\{([^{}]+|(?<Open>\{)|(?<-Open>\}))+(?(Open)(?!))\}", RegexOptions.Compiled);
        
        // Placeholder token format for translation process
        private const string PlaceholderToken = "###PH{0}###";
        
        /// <summary>
        /// Extract placeholders from a string and replace them with tokens for translation
        /// </summary>
        public static (string tokenizedText, Dictionary<string, string> placeholders) ExtractPlaceholders(string text)
        {
            if (string.IsNullOrEmpty(text))
                return (text, new Dictionary<string, string>());
                
            var placeholders = new Dictionary<string, string>();
            int index = 0;
            
            string tokenizedText = PlaceholderRegex.Replace(text, match => {
                string token = string.Format(PlaceholderToken, index);
                placeholders.Add(token, match.Value);
                index++;
                return token;
            });
            
            return (tokenizedText, placeholders);
        }
        
        /// <summary>
        /// Restore placeholders in translated text by replacing tokens with original placeholders
        /// </summary>
        public static string RestorePlaceholders(string translatedText, Dictionary<string, string> placeholders)
        {
            if (string.IsNullOrEmpty(translatedText) || placeholders == null || placeholders.Count == 0)
                return translatedText;
                
            string result = translatedText;
            
            foreach (var placeholder in placeholders)
            {
                result = result.Replace(placeholder.Key, placeholder.Value);
            }
            
            return result;
        }
        
        /// <summary>
        /// Process a smart string by replacing placeholders with values from the provided arguments
        /// </summary>
        public static string Format(string text, IDictionary<string, object> args)
        {
            if (string.IsNullOrEmpty(text) || args == null || args.Count == 0)
                return text;
                
            string result = text;
            
            // Process each placeholder
            foreach (Match match in PlaceholderRegex.Matches(text))
            {
                string placeholder = match.Value;
                string content = placeholder.Substring(1, placeholder.Length - 2); // Remove { and }
                
                // Parse the placeholder parts (variable:formatter:options)
                string[] parts = content.Split(new[] { ':' }, 3);
                string variable = parts[0].Trim();
                
                if (args.TryGetValue(variable, out object value))
                {
                    if (parts.Length == 1)
                    {
                        // Simple variable replacement
                        result = result.Replace(placeholder, value?.ToString() ?? string.Empty);
                    }
                    else
                    {
                        // Apply formatter
                        string formatter = parts.Length > 1 ? parts[1].Trim() : string.Empty;
                        string options = parts.Length > 2 ? parts[2].Trim() : string.Empty;
                        
                        string formattedValue = ApplyFormatter(formatter, value, options);
                        result = result.Replace(placeholder, formattedValue);
                    }
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Apply a formatter to a value based on the formatter type and options
        /// </summary>
        private static string ApplyFormatter(string formatter, object value, string options)
        {
            switch (formatter.ToLowerInvariant())
            {
                case "plural":
                case "p":
                    return ApplyPluralFormatter(value, options);
                    
                case "select":
                case "s":
                    return ApplySelectFormatter(value, options);
                    
                default:
                    return value?.ToString() ?? string.Empty;
            }
        }
        
        /// <summary>
        /// Apply the plural formatter based on the count value
        /// </summary>
        private static string ApplyPluralFormatter(object value, string options)
        {
            if (value == null || string.IsNullOrEmpty(options))
                return string.Empty;
                
            // Try to parse the value as a number
            if (!int.TryParse(value.ToString(), out int count))
                return value.ToString();
                
            // Split options into singular and plural forms
            string[] forms = SplitPipes(options);
            
            if (forms.Length == 0)
                return string.Empty;
                
            if (forms.Length == 1 || count == 1)
                return forms[0].Replace("{}", count.ToString());
                
            return forms[1].Replace("{}", count.ToString());
        }
        
        /// <summary>
        /// Apply the select formatter based on a key value
        /// </summary>
        private static string ApplySelectFormatter(object value, string options)
        {
            if (value == null || string.IsNullOrEmpty(options))
                return string.Empty;
                
            string key = value.ToString().ToLowerInvariant();
            
            // Parse options which are in the format: key1{value1}key2{value2}...
            string pattern = @"(\w+)\{([^{}]+|(?<Open>\{)|(?<-Open>\}))+(?(Open)(?!))\}";
            var matches = Regex.Matches(options, pattern);
            
            foreach (Match match in matches)
            {
                string optionKey = match.Groups[1].Value.ToLowerInvariant();
                string optionValue = match.Groups[2].Value;
                
                if (optionKey == key || (optionKey == "other" && string.IsNullOrEmpty(key)))
                    return optionValue;
            }
            
            // Look for "other" as a fallback
            foreach (Match match in matches)
            {
                if (match.Groups[1].Value.ToLowerInvariant() == "other")
                    return match.Groups[2].Value;
            }
            
            return value.ToString();
        }
        
        /// <summary>
        /// Split a string by pipe characters, respecting nested braces
        /// </summary>
        private static string[] SplitPipes(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new string[0];
                
            var result = new List<string>();
            var current = new StringBuilder();
            int braceCount = 0;
            
            foreach (char c in text)
            {
                if (c == '{')
                    braceCount++;
                else if (c == '}')
                    braceCount--;
                else if (c == '|' && braceCount == 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                    continue;
                }
                
                current.Append(c);
            }
            
            result.Add(current.ToString());
            return result.ToArray();
        }
    }
} 