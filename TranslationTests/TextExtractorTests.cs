#if UNITY_EDITOR
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace PSS.Tests.Editor
{
    [TestFixture]
    [Category("EditMode")]
    public class TextExtractorTests
    {
        private string TEST_SCRIPT_PATH;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Use Application.dataPath to get the absolute path to the Assets folder
            TEST_SCRIPT_PATH = Path.Combine("Assets", "Scripts", "TranslationTests", "TestTranslationScript.cs");
            
            // Ensure the test script exists
            Assert.That(File.Exists(TEST_SCRIPT_PATH), Is.True, 
                $"Test script not found at {TEST_SCRIPT_PATH}. Make sure the TestTranslationScript.cs exists in the correct location.");
        }

        [Test]
        public void ExtractText_FromTestScript_ExtractsCorrectStrings()
        {
            // Arrange
            var expectedStrings = new HashSet<string>
            {
                "Hello World",
                "Test String 2",
                "Main Menu",
                "Dynamic Test String",
                "Dialog Text",
                @"Multi
                Line
                String"
            };
            TextExtractor.Metadata = new TranslationMetadata();
            // Act
            var extractedText = TextExtractor.ExtractTextFromType<ScriptTextExtractor>();

            Debug.Log(extractedText.Count);
            // Assert
            CollectionAssert.AreEquivalent(expectedStrings, extractedText.Intersect(expectedStrings),
                "Not all expected strings were extracted");

            // Verify non-translated string was not extracted
            Assert.That(extractedText.Contains("Should Not Extract"), Is.False,
                "Extracted a string that should not have been translated");
            
            // Verify string interpolation was not extracted
            Assert.That(extractedText.Contains("Invalid {0}"), Is.False,
                "Extracted a string interpolation that should have been ignored");
        }

        [Test]
        public void ExtractText_WithMetadata_TracksSourcesCorrectly()
        {
            // Arrange
            var metadata = new TranslationMetadata();
            TextExtractor.Metadata = metadata;

            // Act
            var extractedText = TextExtractor.ExtractTextFromType<ScriptTextExtractor>();

            // Assert
            foreach (var text in extractedText)
            {
                var sources = metadata.GetSources(text);
                Assert.That(sources, Is.Not.Empty, $"No source tracked for text: {text}");
                Assert.That(sources.Any(s => s.sourcePath == TEST_SCRIPT_PATH), Is.True,
                    $"Source file not tracked for text: {text}");
            }
        }

        [Test]
        public void ExtractText_FromTestScript_HandlesMultiLineStrings()
        {
            // Arrange
            string expectedMultiLineString = @"Multi
Line
String";

            // Act
            var extractedText = TextExtractor.ExtractTextFromType<ScriptTextExtractor>();

            // Assert
            Assert.That(extractedText.Contains(expectedMultiLineString), Is.True,
                "Failed to extract multi-line string correctly");
        }
    }
}
#endif 