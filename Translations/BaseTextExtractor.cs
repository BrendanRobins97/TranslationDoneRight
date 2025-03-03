#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace PSS
{
    public abstract class BaseTextExtractor : ITextExtractor
    {
        public abstract TextSourceType SourceType { get; }
        public abstract int Priority { get; }
        public abstract bool EnabledByDefault { get; }
        public abstract string Description { get; }
        
        public abstract HashSet<string> ExtractText(TranslationMetadata metadata);
        

        /// <summary>
        /// Checks if a path should be processed based on sources
        /// </summary>
        /// <param name="assetPath">The path to check</param>
        /// <param name="sources">The list of sources</param>
        /// <returns>True if the path should be processed</returns>
        protected bool ShouldProcessPath(string assetPath, ExtractionSourcesList sources)
        {
            if (sources == null || sources.Items.Count == 0)
                return true;
            
            assetPath = assetPath.Replace('\\', '/').TrimStart('/');
            if (!assetPath.StartsWith("Assets/"))
                assetPath = "Assets/" + assetPath;
                
            foreach (var source in sources.Items)
            {
                if (source.type == ExtractionSourceType.Folder)
                {
                    string folderPath = source.folderPath?.Replace('\\', '/').TrimStart('/') ?? "";
                    if (!folderPath.StartsWith("Assets/"))
                        folderPath = "Assets/" + folderPath;

                    if (source.recursive)
                    {
                        if (assetPath.StartsWith(folderPath))
                            return true;
                    }
                    else
                    {
                        string directory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
                        if (directory == folderPath)
                            return true;
                    }
                }
                else if (source.type == ExtractionSourceType.Asset && source.asset != null)
                {
                    string sourcePath = AssetDatabase.GetAssetPath(source.asset)?.Replace('\\', '/');
                    if (assetPath == sourcePath)
                        return true;
                }
            }
            
            return false;
        }
    }
}
#endif 