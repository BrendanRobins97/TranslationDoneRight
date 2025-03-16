#if UNITY_EDITOR
using UnityEngine;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

namespace Translations
{
    public class ChangelogEntry
    {
        public string Version { get; set; }
        public string Date { get; set; }
        public Dictionary<string, List<string>> Changes { get; set; }
        public string CommitHash { get; set; }

        public ChangelogEntry()
        {
            Changes = new Dictionary<string, List<string>>();
        }

        public string[] GetAllChanges()
        {
            return Changes.SelectMany(category => 
                category.Value.Select(change => $"{category.Key}: {change}"))
                .ToArray();
        }
    }

    public static class ChangelogParser
    {
        private static readonly Regex VersionRegex = new Regex(@"## \[(.*?)\]( - (\d{4}-\d{2}-\d{2}))?");
        private static readonly Regex CategoryRegex = new Regex(@"### (.+)");

        public static List<ChangelogEntry> ParseChangelog(string changelogPath)
        {
            if (!File.Exists(changelogPath))
            {
                Debug.LogError($"Changelog file not found at: {changelogPath}");
                return new List<ChangelogEntry>();
            }

            var entries = new List<ChangelogEntry>();
            var lines = File.ReadAllLines(changelogPath);
            ChangelogEntry currentEntry = null;
            string currentCategory = null;

            foreach (var line in lines)
            {
                var versionMatch = VersionRegex.Match(line);
                if (versionMatch.Success)
                {
                    if (currentEntry != null)
                    {
                        entries.Add(currentEntry);
                    }

                    currentEntry = new ChangelogEntry
                    {
                        Version = versionMatch.Groups[1].Value,
                        Date = versionMatch.Groups[3].Success ? versionMatch.Groups[3].Value : DateTime.Now.ToString("yyyy-MM-dd")
                    };
                    currentCategory = null;
                    continue;
                }

                var categoryMatch = CategoryRegex.Match(line);
                if (categoryMatch.Success)
                {
                    currentCategory = categoryMatch.Groups[1].Value;
                    if (currentEntry != null && !currentEntry.Changes.ContainsKey(currentCategory))
                    {
                        currentEntry.Changes[currentCategory] = new List<string>();
                    }
                    continue;
                }

                if (currentEntry != null && currentCategory != null)
                {
                    var trimmedLine = line.Trim();
                    if (trimmedLine.StartsWith("- "))
                    {
                        currentEntry.Changes[currentCategory].Add(trimmedLine.Substring(2));
                    }
                }
            }

            if (currentEntry != null)
            {
                entries.Add(currentEntry);
            }

            return entries;
        }

        public static ChangelogEntry GetLatestUnreleasedChanges(string changelogPath)
        {
            var entries = ParseChangelog(changelogPath);
            return entries.FirstOrDefault(e => e.Version.Equals("Unreleased", StringComparison.OrdinalIgnoreCase));
        }

        public static ChangelogEntry GetLatestReleasedChanges(string changelogPath)
        {
            var entries = ParseChangelog(changelogPath);
            return entries.FirstOrDefault(e => !e.Version.Equals("Unreleased", StringComparison.OrdinalIgnoreCase));
        }
    }
}
#endif 