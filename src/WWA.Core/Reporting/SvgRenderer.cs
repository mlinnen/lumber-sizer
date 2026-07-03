using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static System.FormattableString;
using WWA.Core.Models;

namespace WWA.Core.Reporting
{
    public class SvgRenderer
    {
        private const double DefaultBoardHeightInches = 6.0;
        private const double ScaleSectionHeightPx = 32.0;
        private const double LegendHeightPx = 70.0;
        private const double UnplacedHeaderHeightPx = 32.0;
        private const double UnplacedLineHeightPx = 14.0;

        private static void AppendSvgLine(StringBuilder sb, FormattableString line) => sb.AppendLine(Invariant(line));

        private static string EscapeXml(string? value) => System.Security.SecurityElement.Escape(value) ?? string.Empty;

        private static string GetPlacementLabel(Guid cutItemId, CutItem? cutItem)
            => (cutItem?.Description ?? cutItemId.ToString()) ?? "Unknown";

        private static double GetPlacementWidth(CutItem? cutItem, double fallbackWidthInches)
            => Math.Max(1.0, cutItem?.Width ?? fallbackWidthInches);

        private static double GetBoardHeight(BoardAllocation allocation, IEnumerable<(double xOffset, double yOffset, double length, double width, string label)> placements)
        {
            if (allocation.OriginalBoardWidth > 0.0)
            {
                return allocation.OriginalBoardWidth;
            }

            var usedHeight = placements.Any()
                ? placements.Max(p => p.yOffset + p.width)
                : 0.0;

            if (allocation.Placements2D.Any())
            {
                return usedHeight > 0.0 ? usedHeight : DefaultBoardHeightInches;
            }

            return Math.Max(DefaultBoardHeightInches, usedHeight);
        }

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
            double contentHeightPx = 0.0;

            var boardPlacements = new List<List<(double xOffset, double yOffset, double length, double width, string label)>>();
            var boardSizes = new List<(double lengthIn, double heightIn)>();

            foreach (var b in boards)
            {
                var fallbackBoardHeightIn = b.OriginalBoardWidth > 0.0 ? b.OriginalBoardWidth : DefaultBoardHeightInches;
                var renderPlacements = b.Placements2D.Any()
                    ? b.Placements2D
                        .Select(p => (
                            xOffset: p.XOffset,
                            yOffset: p.YOffset,
                            length: p.Length,
                            width: p.Width,
                            label: GetPlacementLabel(p.CutItemId, p.CutItem)))
                        .ToList()
                    : b.Placements
                        .Select(p => (
                            xOffset: p.Offset,
                            yOffset: 0.0,
                            length: p.Length,
                            width: GetPlacementWidth(p.CutItem, fallbackBoardHeightIn),
                            label: GetPlacementLabel(p.CutItemId, p.CutItem)))
                        .ToList();

                double lengthIn = b.OriginalBoardLength;
                double heightIn = GetBoardHeight(b, renderPlacements);

                boardPlacements.Add(renderPlacements);
                boardSizes.Add((lengthIn, heightIn));
                if (lengthIn > maxBoardLengthIn) maxBoardLengthIn = lengthIn;

                contentHeightPx += (heightIn * pxPerInch) + margin;
            }

            var contentTopPx = margin + ScaleSectionHeightPx;
            var unplacedHeightPx = result.UnplacedItems.Any()
                ? UnplacedHeaderHeightPx + (result.UnplacedItems.Count * UnplacedLineHeightPx)
                : 0.0;
            var legendY = contentTopPx + contentHeightPx + unplacedHeightPx;
            int svgWidth = (int)Math.Ceiling(maxBoardLengthIn * pxPerInch) + margin * 2;
            int svgHeight = (int)Math.Ceiling(legendY + LegendHeightPx + margin);

            AppendSvgLine(sb, $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{svgWidth}\" height=\"{svgHeight}\">");

            // Define simple palette
            var palette = new[] { "#4CAF50", "#2196F3", "#FF9800", "#9C27B0", "#F44336", "#00BCD4", "#8BC34A" };

            // Build a consistent label->color mapping across the whole SVG
            var labels = boardPlacements
                .SelectMany(p => p)
                .Select(p => p.label)
                .Distinct()
                .ToList();
            var labelColor = new Dictionary<string, string>();
            for (int i = 0; i < labels.Count; i++) labelColor[labels[i]] = palette[i % palette.Length];

            int boardIndex = 0;
            double yOffsetPx = contentTopPx;
 
            foreach (var b in boards)
            {
                var (lengthIn, heightIn) = boardSizes[boardIndex];
                double boardW = lengthIn * pxPerInch;
                double boardH = Math.Max(1.0, heightIn) * pxPerInch;

                // Board background
                AppendSvgLine(sb, $"  <g id=\"board-{boardIndex}\">");
                AppendSvgLine(sb, $"    <rect x=\"{margin}\" y=\"{yOffsetPx}\" width=\"{boardW}\" height=\"{boardH}\" fill=\"#F5F5F5\" stroke=\"#333\" stroke-width=\"1\" rx=\"2\" />");
                AppendSvgLine(sb, $"    <text x=\"{margin + 4}\" y=\"{yOffsetPx + 14}\" font-family=\"Arial,Helvetica,sans-serif\" font-size=\"12\" fill=\"#222\">Board {boardIndex} ({lengthIn}in x {Math.Round(boardH / pxPerInch, 2)}in)</text>");

                // Draw placements
                foreach (var p in boardPlacements[boardIndex])
                {
                    double x = margin + p.xOffset * pxPerInch;
                    double y = yOffsetPx + p.yOffset * pxPerInch;
                    double w = p.length * pxPerInch;
                    double h = p.width * pxPerInch;
                    string rawLabel = p.label;
                    var color = labelColor.ContainsKey(rawLabel) ? labelColor[rawLabel] : palette[0];

                    AppendSvgLine(sb, $"    <rect x=\"{x}\" y=\"{y}\" width=\"{w}\" height=\"{h}\" fill=\"{color}\" fill-opacity=\"0.85\" stroke=\"#111\" stroke-width=\"0.5\" />");

                    string label = rawLabel;
                    // short label
                    if (label != null && label.Length > 20) label = label.Substring(0, 17) + "...";
                    AppendSvgLine(sb, $"    <text x=\"{x + 3}\" y=\"{y + 12}\" font-family=\"Arial,Helvetica,sans-serif\" font-size=\"10\" fill=\"#000\">{EscapeXml(label)}</text>");
                }

                // Draw remnant area if present (RemnantLength is along length from the right)
                if (b.RemnantLength > 0.0)
                {
                    double remW = b.RemnantLength * pxPerInch;
                    double remX = margin + boardW - remW;
                    AppendSvgLine(sb, $"    <rect x=\"{remX}\" y=\"{yOffsetPx}\" width=\"{remW}\" height=\"{boardH}\" fill=\"#BDBDBD\" fill-opacity=\"0.5\" stroke=\"#777\" stroke-dasharray=\"4 2\" />");
                    AppendSvgLine(sb, $"    <text x=\"{remX + 4}\" y=\"{yOffsetPx + boardH - 4}\" font-family=\"Arial,Helvetica,sans-serif\" font-size=\"10\" fill=\"#222\">Remnant {b.RemnantLength}in</text>");
                }

                sb.AppendLine("  </g>");

                yOffsetPx += boardH + margin;
                boardIndex++;
            }

            // Unplaced items note
            if (result.UnplacedItems.Any())
            {
                AppendSvgLine(sb, $"  <g id=\"unplaced\">");
                AppendSvgLine(sb, $"    <text x=\"{margin}\" y=\"{yOffsetPx + 14}\" font-family=\"Arial,Helvetica,sans-serif\" font-size=\"12\" fill=\"#B71C1C\">Unplaced items:</text>");
                int i = 0;
                foreach (var u in result.UnplacedItems)
                {
                    string label = u.Description ?? u.Id.ToString();
                    if (label.Length > 80) label = label.Substring(0, 77) + "...";
                    AppendSvgLine(sb, $"    <text x=\"{margin + 8}\" y=\"{yOffsetPx + 32 + i * 14}\" font-family=\"Arial,Helvetica,sans-serif\" font-size=\"11\" fill=\"#333\">- {EscapeXml(label)}</text>");
                    i++;
                }

                sb.AppendLine("  </g>");
                yOffsetPx += unplacedHeightPx;
            }

            // Add a simple scale bar at the top
            try
            {
                double scaleY = 8; // px from top margin
                double barX = margin;
                double barY = scaleY;
                double barHeight = 10;
                double inchesPerTick = 12.0;
                int ticks = (int)Math.Ceiling(maxBoardLengthIn / inchesPerTick);
                AppendSvgLine(sb, $"  <g id=\"scale\">");
                AppendSvgLine(sb, $"    <rect x=\"{barX}\" y=\"{barY}\" width=\"{(maxBoardLengthIn * pxPerInch)}\" height=\"{barHeight}\" fill=\"#EEE\" stroke=\"#999\" stroke-width=\"0.5\" />");
                for (int t = 0; t <= ticks; t++)
                {
                    double x = barX + t * inchesPerTick * pxPerInch;
                    AppendSvgLine(sb, $"    <line x1=\"{x}\" y1=\"{barY}\" x2=\"{x}\" y2=\"{barY + barHeight}\" stroke=\"#666\" stroke-width=\"1\" />");
                    AppendSvgLine(sb, $"    <text x=\"{x + 2}\" y=\"{barY + barHeight + 12}\" font-family=\"Arial,Helvetica,sans-serif\" font-size=\"10\" fill=\"#333\">{t * inchesPerTick}in</text>");
                }
                sb.AppendLine("  </g>");
            }
            catch { }

            // Legend box
            try
            {
                double legendWidth = svgWidth - margin * 2;
                double legendX = margin;
                AppendSvgLine(sb, $"  <g id=\"legend\">");
                AppendSvgLine(sb, $"    <rect x=\"{legendX}\" y=\"{legendY}\" width=\"{legendWidth}\" height=\"{LegendHeightPx}\" fill=\"#FAFAFA\" stroke=\"#CCC\" stroke-width=\"1\" />");
                AppendSvgLine(sb, $"    <text x=\"{legendX + 6}\" y=\"{legendY + 16}\" font-family=\"Arial,Helvetica,sans-serif\" font-size=\"12\" fill=\"#222\">Legend</text>");
                int li = 0;
                foreach (var kv in labelColor)
                {
                    double lx = legendX + 8 + (li % 4) * 240;
                    double ly = legendY + 34 + (li / 4) * 18;
                    AppendSvgLine(sb, $"    <rect x=\"{lx}\" y=\"{ly - 10}\" width=\"12\" height=\"12\" fill=\"{kv.Value}\" />");
                    AppendSvgLine(sb, $"    <text x=\"{lx + 18}\" y=\"{ly}\" font-family=\"Arial,Helvetica,sans-serif\" font-size=\"11\" fill=\"#333\">{EscapeXml(kv.Key)}</text>");
                    li++;
                    if (li > 15) break; // avoid huge legends
                }
                sb.AppendLine("  </g>");
            }
            catch { }

            sb.AppendLine("</svg>");
            return sb.ToString();
        }
    }
}
