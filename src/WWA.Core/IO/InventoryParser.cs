using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using WWA.Core.Models;

namespace WWA.Core.IO
{
    /// <summary>
    /// Minimal, defensive parser for a small inventory text format.
    /// Format: one board spec per line: "{length} x {width} [x {quantity}] # {grade}"
    /// Lines beginning with '#' or empty lines are ignored.
    /// </summary>
    public static class InventoryParser
    {
        private static readonly Regex DimensionPattern = new(
            @"^(?<value>[0-9]+(?:\.[0-9]+)?)\s*(?:[A-Za-z'""]+)?$",
            RegexOptions.CultureInvariant);

        public static Inventory Parse(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("path must be provided", nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException("Inventory file not found", path);

            var lines = File.ReadAllLines(path);
            var inventory = new Inventory();

            for (int i = 0; i < lines.Length; i++)
            {
                var raw = lines[i].Trim();
                var lineNumber = i + 1;
                if (string.IsNullOrEmpty(raw)) continue;
                if (raw.StartsWith("#")) continue;

                string? grade = null;
                var commentIndex = raw.IndexOf('#');
                if (commentIndex >= 0)
                {
                    grade = raw.Substring(commentIndex + 1).Trim();
                    raw = raw.Substring(0, commentIndex).Trim();
                }

                var parts = raw.Split('x');
                if (parts.Length < 2 || parts.Length > 3)
                {
                    throw new FormatException($"Malformed inventory line {lineNumber}: expected '<length> x <width> [x <quantity>]' but got '{lines[i]}'");
                }

                var lengthRaw = parts[0].Trim();
                var widthRaw = parts[1].Trim();
                if (string.IsNullOrEmpty(lengthRaw) || string.IsNullOrEmpty(widthRaw))
                {
                    throw new FormatException($"Malformed inventory line {lineNumber}: length or width missing in '{lines[i]}'");
                }

                var length = ParseDimension(lengthRaw, lineNumber);
                var width = ParseDimension(widthRaw, lineNumber);
                var quantity = parts.Length == 3 ? ParseQuantity(parts[2].Trim(), lineNumber) : 1;

                inventory.Add(new Board(length, width, null, string.IsNullOrWhiteSpace(grade) ? null : grade, quantity));
            }

            return inventory;
        }

        private static double ParseDimension(string raw, int lineNumber)
        {
            var match = DimensionPattern.Match(raw);
            if (!match.Success) throw new FormatException($"Malformed dimension on line {lineNumber}: '{raw}'");
            if (!double.TryParse(match.Groups["value"].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var val))
            {
                throw new FormatException($"Unable to parse number on line {lineNumber}: '{raw}'");
            }

            return val;
        }

        private static int ParseQuantity(string raw, int lineNumber)
        {
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity))
            {
                throw new FormatException($"Malformed quantity on line {lineNumber}: '{raw}'");
            }

            if (quantity <= 0)
            {
                throw new FormatException($"Quantity must be at least 1 on line {lineNumber}: '{raw}'");
            }

            return quantity;
        }
    }
}
