using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WWA.Core.Models;

namespace WWA.Core.Reporting
{
    public class SvgRenderer
    {
        /// <summary>
        /// Render the packing result to a single SVG string. Units are pixels; default scale is 10 px per inch.
        /// Color-codes placements and marks remnants. Includes a small legend for unplaced items.
        /// </summary>
        public string Render(PackingResult result, double pxPerInch = 10.0)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            const int margin = 10;
            var sb = new StringBuilder();

            // Compute overall width as max board length in inches * scale
            var boards = result.Allocations;
            double maxBoardLengthIn = 0;
            double totalHeightPx = margin;

            var boardSizes = new List<(double lengthIn, double heightIn)>();

            foreach (var b in boards)
            {
                double lengthIn = b.OriginalBoardLength;
                double heightIn = 0.0;
                if (b.Placements2D.Any())
                {
                    heightIn = b.Placements2D.Max(p => p.YOffset + p.Width);
                }
                else
                {
                    // fallback: if no placements, assume a small height
                    heightIn = 6.0;
                }

                boardSizes.Add((lengthIn, heightIn));
                if (lengthIn > maxBoardLengthIn) maxBoardLengthIn = lengthIn;

                totalHeightPx += (heightIn * pxPerInch) + margin;
            }

            int svgWidth = (int)Math.Ceiling(maxBoardLengthIn * pxPerInch) + margin * 2;
            int svgHeight = (int)Math.Ceiling(totalHeightPx) + 20;

            sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{svgWidth}\" height=\"{svgHeight}\">\n");

            // Define simple palette
            var palette = new[] { "#4CAF50", "#2196F3", "#FF9800", "#9C27B0", "#F44336", "#00BCD4", "#8BC34A" };

            // Build a consistent label->color mapping across the whole SVG
            var allPlacements = result.Allocations.SelectMany(b => b.Placements2D ?? Enumerable.Empty<Placement2D>());
            var labels = allPlacements.Select(p => (p.CutItem?.Description ?? p.CutItemId.ToString()) ?? "Unknown").Distinct().ToList();
            var labelColor = new Dictionary<string, string>();
            for (int i = 0; i < labels.Count; i++) labelColor[labels[i]] = palette[i % palette.Length];

            int boardIndex = 0;
            double yOffsetPx = margin;
 
            foreach (var b in boards)
            {
                var (lengthIn, heightIn) = boardSizes[boardIndex];
                double boardW = lengthIn * pxPerInch;
                double boardH = Math.Max(1.0, heightIn) * pxPerInch;

                // Board background
                sb.AppendLine($"  <g id=\"board-{boardIndex}\">\n");
                sb.AppendLine($"    <rect x=\"{margin}\" y=\"{yOffsetPx}\" width=\"{boardW}\" height=\"{boardH}\" fill=\"#F5F5F5\" stroke=\"#333\" stroke-width=\"1\" rx=\"2\" />");
                sb.AppendLine($"    <text x=\"{margin + 4}\" y=\"{yOffsetPx + 14}\" font-family=\"Arial,Helvetica,sans-serif\" font-size=12 fill=\"#222\">Board {boardIndex} ({lengthIn}in x {Math.Round(boardH/pxPerInch,2)}in)</text>");

                // Draw placements
                foreach (var p in b.Placements2D)
                {
                    double x = margin + p.XOffset * pxPerInch;
                    double y = yOffsetPx + p.YOffset * pxPerInch;
                    double w = p.Length * pxPerInch;
                    double h = p.Width * pxPerInch;
                    string rawLabel = (p.CutItem?.Description ?? p.CutItemId.ToString()) ?? "Unknown";
                    var color = labelColor.ContainsKey(rawLabel) ? labelColor[rawLabel] : palette[0];

                    sb.AppendLine($"    <rect x=\"{x}\" y=\"{y}\" width=\"{w}\" height=\"{h}\" fill=\"{color}\" fill-opacity=\"0.85\" stroke=\"#111\" stroke-width=\"0.5\" />");

                    string label = rawLabel;
                    // short label
                    if (label != null && label.Length > 20) label = label.Substring(0, 17) + "...";
                    sb.AppendLine($"    <text x=\"{x + 3}\" y=\"{y + 12}\" font-family=\"Arial,Helvetica,sans-serif\" font-size=10 fill=\"#000\">{System.Security.SecurityElement.Escape(label)}</text>");
                }

                // Draw remnant area if present (RemnantLength is along length from the right)
                if (b.RemnantLength > 0.0)
                {
                    double remW = b.RemnantLength * pxPerInch;
                    double remX = margin + boardW - remW;
                    sb.AppendLine($"    <rect x=\"{remX}\" y=\"{yOffsetPx}\" width=\"{remW}\" height=\"{boardH}\" fill=\"#BDBDBD\" fill-opacity=\"0.5\" stroke=\"#777\" stroke-dasharray=\"4 2\" />");
                    sb.AppendLine($"    <text x=\"{remX + 4}\" y=\"{yOffsetPx + boardH - 4}\" font-family=\"Arial,Helvetica,sans-serif\" font-size=10 fill=\"#222\">Remnant {b.RemnantLength}in</text>");
                }

                sb.AppendLine($"  </g>\n");

                yOffsetPx += boardH + margin;
                boardIndex++;
            }

            // Unplaced items note
            if (result.UnplacedItems.Any())
            {
                sb.AppendLine($"  <g id=\"unplaced\">\n");
                sb.AppendLine($"    <text x=\"{margin}\" y=\"{yOffsetPx + 14}\" font-family=\"Arial,Helvetica,sans-serif\" font-size=12 fill=\"#B71C1C\">Unplaced items:</text>");
                int i = 0;
                foreach (var u in result.UnplacedItems)
                {
                    string label = u.Description ?? u.Id.ToString();
                    if (label.Length > 80) label = label.Substring(0, 77) + "...";
                    sb.AppendLine($"    <text x=\"{margin + 8}\" y=\"{yOffsetPx + 32 + i * 14}\" font-family=\"Arial,Helvetica,sans-serif\" font-size=11 fill=\"#333\">- {System.Security.SecurityElement.Escape(label)}</text>");
                    i++;
                }

                sb.AppendLine($"  </g>\n");
            }

            // Add a simple scale bar at the top
            try
            {
                double scaleY = 8; // px from top margin
                double barX = margin;
                double barY = 6;
                double barHeight = 10;
                double inchesPerTick = 12.0;
                int ticks = (int)Math.Ceiling(maxBoardLengthIn / inchesPerTick);
                sb.AppendLine($"  <g id=\"scale\">\n");
                sb.AppendLine($"    <rect x=\"{barX}\" y=\"{barY}\" width=\"{(maxBoardLengthIn * pxPerInch)}\" height=\"{barHeight}\" fill=\"#EEE\" stroke=\"#999\" stroke-width=0.5 />");
                for (int t = 0; t <= ticks; t++)
                {
                    double x = barX + t * inchesPerTick * pxPerInch;
                    sb.AppendLine($"    <line x1=\"{x}\" y1=\"{barY}\" x2=\"{x}\" y2=\"{barY + barHeight}\" stroke=\"#666\" stroke-width=1 />");
                    sb.AppendLine($"    <text x=\"{x + 2}\" y=\"{barY + barHeight + 12}\" font-family=\"Arial,Helvetica,sans-serif\" font-size=10 fill=\"#333\">{t * inchesPerTick}in</text>");
                }
                sb.AppendLine($"  </g>\n");
            }
            catch { }

            // Legend box
            try
            {
                double legendX = Math.Max(margin + (maxBoardLengthIn * pxPerInch) + 10, margin + 300);
                double legendY = margin;
                sb.AppendLine($"  <g id=\"legend\">\n");
                sb.AppendLine($"    <rect x=\"{margin}\" y=\"{svgHeight - 80}\" width=\"{svgWidth - margin * 2}\" height=\"70\" fill=\"#FAFAFA\" stroke=\"#CCC\" stroke-width=1 />");
                sb.AppendLine($"    <text x=\"{margin + 6}\" y=\"{svgHeight - 64}\" font-family=\"Arial,Helvetica,sans-serif\" font-size=12 fill=\"#222\">Legend</text>");
                int li = 0;
                foreach (var kv in labelColor)
                {
                    double lx = margin + 8 + (li % 4) * 240;
                    double ly = svgHeight - 48 + (li / 4) * 18;
                    sb.AppendLine($"    <rect x=\"{lx}\" y=\"{ly - 10}\" width=\"12\" height=\"12\" fill=\"{kv.Value}\" />");
                    sb.AppendLine($"    <text x=\"{lx + 18}\" y=\"{ly}\" font-family=\"Arial,Helvetica,sans-serif\" font-size=11 fill=\"#333\">{System.Security.SecurityElement.Escape(kv.Key)}</text>");
                    li++;
                    if (li > 15) break; // avoid huge legends
                }
                sb.AppendLine($"  </g>\n");
            }
            catch { }

            sb.AppendLine("</svg>");
            return sb.ToString();
        }
    }
}
