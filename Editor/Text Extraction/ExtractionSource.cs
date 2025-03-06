using UnityEngine;
using System;

namespace Translations
{
    /// <summary>
    /// Defines a source location for text extraction
    /// </summary>
    [Serializable]
    public class ExtractionSource
    {
        public ExtractionSourceType type;
        public string folderPath;
        public UnityEngine.Object asset;
        public bool recursive = true;
    }

    /// <summary>
    /// Type of extraction source
    /// </summary>
    public enum ExtractionSourceType
    {
        Folder,
        Asset
    }
} 