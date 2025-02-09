using System.Collections.Generic;
using UnityEngine;

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
    }
} 