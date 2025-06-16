using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Translations.Examples
{
    public static class TranslationTestManager
    {
        public static TestResult RunTranslationTest(List<string> testStrings, string testName = "Translation Test")
        {
            List<string> missingTranslations = new List<string>();
            int totalTests = testStrings.Count;
            int passedTests = 0;
            
            Debug.Log($"=== {testName} Started ===");
            Debug.Log($"Testing {totalTests} strings for translation availability...");
            
            foreach (string testString in testStrings)
            {
                if (TranslationManager.HasTranslation(testString))
                {
                    passedTests++;
                    Debug.Log($"✓ PASS: '{testString}' has translation");
                }
                else
                {
                    missingTranslations.Add(testString);
                    Debug.LogWarning($"✗ FAIL: '{testString}' missing translation");
                }
            }
            
            // Create and return test result
            TestResult result = new TestResult
            {
                testName = testName,
                totalTests = totalTests,
                passedTests = passedTests,
                failedTests = totalTests - passedTests,
                missingTranslations = missingTranslations,
                successRate = (float)passedTests / totalTests * 100f
            };
            
            LogTestResults(result);
            return result;
        }
        
        public static void LogTestResults(TestResult result)
        {
            Debug.Log($"=== {result.testName} Results ===");
            Debug.Log($"Total Tests: {result.totalTests}");
            Debug.Log($"Passed: {result.passedTests}");
            Debug.Log($"Failed: {result.failedTests}");
            Debug.Log($"Success Rate: {result.successRate:F1}%");
            
            if (result.missingTranslations.Count > 0)
            {
                Debug.LogError($"Missing translations for: {string.Join(", ", result.missingTranslations)}");
            }
            else
            {
                Debug.Log("🎉 All translations found!");
            }
        }
        
        public static void TestTranslationFunctionality(string testTitle, string testSmartString)
        {
            Debug.Log("=== Testing Translation Functionality ===");
            
            // Test basic translation
            string translated = TranslationManager.Translate(testTitle);
            Debug.Log($"Translation test: '{testTitle}' -> '{translated}'");
            
            // Test smart string translation
            var args = new Dictionary<string, object>()
            {
                { "playerName", "TestPlayer" },
                { "level", 42 },
                { "score", 9999 }
            };
            
            string smartTranslated = TranslationManager.TranslateSmart(testSmartString, args);
            Debug.Log($"Smart translation test: '{testSmartString}' -> '{smartTranslated}'");
        }
        
        public static List<string> CollectStringsFromObject(object obj)
        {
            List<string> strings = new List<string>();
            
            if (obj == null) return strings;
            
            var type = obj.GetType();
            var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            foreach (var field in fields)
            {
                // Skip fields marked with NotTranslated attribute
                if (field.GetCustomAttributes(typeof(NotTranslatedAttribute), false).Length > 0)
                    continue;
                    
                var value = field.GetValue(obj);
                if (value == null) continue;
                
                // Handle different field types
                if (field.FieldType == typeof(string))
                {
                    strings.Add((string)value);
                }
                else if (field.FieldType == typeof(List<string>))
                {
                    strings.AddRange((List<string>)value);
                }
                else if (field.FieldType == typeof(string[]))
                {
                    strings.AddRange((string[])value);
                }
                else if (field.FieldType == typeof(Dictionary<string, string>))
                {
                    strings.AddRange(((Dictionary<string, string>)value).Values);
                }
                // Handle nested objects with Translated attribute
                else if (field.FieldType.GetCustomAttributes(typeof(TranslatedAttribute), false).Length > 0)
                {
                    strings.AddRange(CollectStringsFromObject(value));
                }
                // Handle lists of translated objects
                else if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    var genericType = field.FieldType.GetGenericArguments()[0];
                    if (genericType.GetCustomAttributes(typeof(TranslatedAttribute), false).Length > 0)
                    {
                        var list = (System.Collections.IList)value;
                        foreach (var item in list)
                        {
                            strings.AddRange(CollectStringsFromObject(item));
                        }
                    }
                }
            }
            
            // Remove duplicates and empty strings
            return strings.Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
        }
    }
    
    [System.Serializable]
    public class TestResult
    {
        public string testName;
        public int totalTests;
        public int passedTests;
        public int failedTests;
        public float successRate;
        public List<string> missingTranslations;
    }
    
    // Shared test data classes that can be reused across different test scripts
    [System.Serializable]
    [Translated]
    public class TestDialogueData
    {
        public string speakerName;
        public string mainText;
        public List<string> responseOptions = new List<string>();
        
        [NotTranslated]
        public int dialogueID;
        [NotTranslated]
        public bool isCompleted;
    }
    
    [System.Serializable]
    [Translated]
    public class TestItemData
    {
        public string itemName;
        public string itemDescription;
        public string category;
        
        [NotTranslated]
        public int itemID;
        [NotTranslated]
        public float value;
        [NotTranslated]
        public bool isRare;
    }
} 