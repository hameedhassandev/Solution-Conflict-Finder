using System;
using System.Collections.Generic;
using System.IO;

namespace SolutionComponentsAnalyzer.UI.Infrastructure
{
    /// <summary>Remembers URLs and user names between runs. Passwords are never saved.</summary>
    internal static class UserSettings
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SolutionConflictFinder",
            "settings.txt");

        private static Dictionary<string, string> _values;

        public static string Get(string key)
        {
            Load();

            string value;
            return _values.TryGetValue(key, out value) ? value : string.Empty;
        }

        public static void Set(string key, string value)
        {
            Load();
            _values[key] = value ?? string.Empty;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));

                var lines = new List<string>();
                foreach (var pair in _values)
                    lines.Add(pair.Key + "=" + pair.Value);

                File.WriteAllLines(FilePath, lines);
            }
            catch (Exception)
            {
                // remembering values is a convenience only
            }
        }

        private static void Load()
        {
            if (_values != null)
                return;

            _values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (!File.Exists(FilePath))
                    return;

                foreach (var line in File.ReadAllLines(FilePath))
                {
                    var index = line.IndexOf('=');
                    if (index > 0)
                        _values[line.Substring(0, index)] = line.Substring(index + 1);
                }
            }
            catch (Exception)
            {
                // ignore a corrupt settings file
            }
        }
    }
}
