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
                int colorIdx = 0;
                foreach (var p in b.Placements2D)
                {
                    double x = margin + p.XOffset * pxPerInch;
                    double y = yOffsetPx + p.YOffset * pxPerInch;
                    double w = p.Length * pxPerInch;
                    double h = p.Width * pxPerInch;
                    var color = palette[colorIdx % palette.Length];

                    sb.AppendLine($"    <rect x=\"{x}\" y=\"{y}\" width=\"{w}\" height=\"{h}\" fill=\"{color}\" fill-opacity=\"0.85\" stroke=\"#111\" stroke-width=\"0.5\" />");

                    string label = p.CutItem?.Description ?? p.CutItemId.ToString();
                    // short label
                    if (label != null && label.Length > 20) label = label.Substring(0, 17) + "...";
                    sb.AppendLine($"    <text x=\"{x + 3}\" y=\"{y + 12}\" font-family=\"Arial,Helvetica,sans-serif\" font-size=10 fill=\"#000\">{System.Security.SecurityElement.Escape(label)}</text>");

                    colorIdx++;
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

            sb.AppendLine("</svg>");
            return sb.ToString();
        }
    }
}
