using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Text.RegularExpressions;
using WWA.Core.Models;

namespace WWA.Core.IO
{
    /// <summary>
    /// Minimal, defensive parser for a very small cut-list text format.
    /// Format: one piece per line: "{length} x {width} # {description}"
    /// Lines beginning with '#' or empty lines are ignored.
    /// </summary>
    public static class CutListParser
    {
        public static CutList Parse(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("path must be provided", nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException("Cutlist file not found", path);

            var lines = File.ReadAllLines(path);
            var cutList = new CutList();

            for (int i = 0; i < lines.Length; i++)
            {
                var raw = lines[i].Trim();
                var lineNumber = i + 1;
                if (string.IsNullOrEmpty(raw)) continue;
                if (raw.StartsWith("#")) continue;

                // Expect: <length> x <width> [# description]
                // Split off an inline comment
                string? description = null;
                var commentIndex = raw.IndexOf('#');
                if (commentIndex >= 0)
                {
                    description = raw.Substring(commentIndex + 1).Trim();
                    raw = raw.Substring(0, commentIndex).Trim();
                }

                // Now raw should contain something like "12in x 2in"
                var parts = raw.Split('x');
                if (parts.Length != 2)
                {
                    throw new FormatException($"Malformed cut-list line {lineNumber}: expected '<length> x <width>' but got '{lines[i]}'");
                }

                var lengthRaw = parts[0].Trim();
                var widthRaw = parts[1].Trim();
                if (string.IsNullOrEmpty(lengthRaw) || string.IsNullOrEmpty(widthRaw))
                {
                    throw new FormatException($"Malformed cut-list line {lineNumber}: length or width missing in '{lines[i]}'");
                }

                double length = ParseDimension(lengthRaw, lineNumber);
                double? width = ParseDimensionNullable(widthRaw, lineNumber);

                var item = new CutItem(length, width, 1)
                {
                    Description = description
                };

                cutList.Items.Add(item);
            }

            return cutList;
        }

        private static double ParseDimension(string raw, int lineNumber)
        {
            var match = Regex.Match(raw, "([0-9]+(\\.[0-9]+)?)");
            if (!match.Success) throw new FormatException($"Malformed dimension on line {lineNumber}: '{raw}'");
            if (!double.TryParse(match.Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var val))
            {
                throw new FormatException($"Unable to parse number on line {lineNumber}: '{raw}'");
            }
            return val;
        }

        private static double? ParseDimensionNullable(string raw, int lineNumber)
        {
            var match = Regex.Match(raw, "([0-9]+(\\.[0-9]+)?)");
            if (!match.Success) return null;
            if (!double.TryParse(match.Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var val))
            {
                throw new FormatException($"Unable to parse number on line {lineNumber}: '{raw}'");
            }
            return val;
        }
    }
}