using System.Collections.Generic;
using UnityEngine;
using System;

namespace PSS
{
    public interface ITextExtractor
    {
        /// <summary>
        /// Gets the type of text source this extractor handles
        /// </summary>
        TextSourceType SourceType { get; }

        /// <summary>
        /// Extracts text from the source type this extractor handles
        /// </summary>
        /// <param name="includeInactive">Whether to include inactive GameObjects in the extraction</param>
        /// <param name="metadata">The metadata instance to store source information</param>
        /// <returns>A HashSet of extracted text</returns>
        HashSet<string> ExtractText(TranslationMetadata metadata);

        /// <summary>
        /// Gets the priority of this extractor. Higher priority extractors run first.
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Gets whether this extractor is enabled by default
        /// </summary>
        bool EnabledByDefault { get; }

        /// <summary>
        /// Gets a description of what this extractor does
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the relevant sources for this extractor
        /// </summary>
        /// <param name="metadata">The metadata containing sources</param>
        /// <returns>The list of sources to use</returns>
        static ExtractionSourcesList GetRelevantSources(ITextExtractor extractor, TranslationMetadata metadata)
        {
            var extractorName = extractor.GetType().Name;
            
            // Check if there are extractor-specific sources first
            if (metadata.extractorSources != null && 
                metadata.extractorSources.TryGetValue(extractorName, out var extractorSources) && 
                extractorSources.Items.Count > 0)
            {
                // Use only extractor-specific sources
                return extractorSources.Items;
            }
            
            // Fall back to global sources
            return metadata.extractionSources;
        }
        
        /// <summary>
        /// Helper method to process all sources or fall back to processing everything if no sources specified.
        /// This centralizes the common extraction logic pattern across extractors.
        /// </summary>
        /// <typeparam name="T">The type of source item (e.g. string GUID or asset path)</typeparam>
        /// <param name="extractor">The extractor instance</param>
        /// <param name="metadata">The metadata containing sources</param>
        /// <param name="processAllMethod">Method to call when processing everything (no sources specified)</param>
        /// <param name="processBySourceMethod">Method to call to process within specific sources</param>
        /// <returns>The extracted text</returns>
        static HashSet<string> ProcessSourcesOrAll<T>(
            ITextExtractor extractor,
            TranslationMetadata metadata,
            Func<HashSet<string>> processAllMethod,
            Func<ExtractionSourcesList, HashSet<string>> processBySourceMethod)
        {
            var sources = GetRelevantSources(extractor, metadata);
            
            // If no sources specified, search entire project
            if (sources == null || sources.Items.Count == 0)
            {
                return processAllMethod();
            }
            
            // Search only within specified sources
            return processBySourceMethod(sources);
        }
    }
} 