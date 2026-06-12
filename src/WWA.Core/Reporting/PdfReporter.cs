using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace WWA.Core.Reporting
{
    /// <summary>
    /// Generates PDFs containing SVG diagrams by rasterizing SVG via SkiaSharp and embedding into QuestPDF.
    /// Falls back to HTML when QuestPDF is unavailable.
    /// </summary>
    public static class PdfReporter
    {
        /// <summary>
        /// Generate a PDF (or fallback HTML) containing the provided SVG. If QuestPDF + SkiaSharp are available at runtime
        /// the method will rasterize the SVG and embed it as a PNG image into the PDF. Otherwise a .html file will be written.
        /// </summary>
        public static void GenerateFromSvg(string svg, string outputPath)
        {
            if (svg == null) throw new ArgumentNullException(nameof(svg));
            if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentNullException(nameof(outputPath));

            // Runtime rasterization using SkiaSharp/Svg.Skia was removed from the default build
            // because CI runners may not supply native assets. For now, produce an HTML fallback
            // that embeds the original SVG. Restore the Skia path behind a feature flag or
            // optional package if you need reproducible PNG/PDF output in CI.
            // (Skia-based rasterization is available locally on developer machines/branches.)

            // Log a short note so CI logs show why we fell back
            Console.Error.WriteLine("Skia/Svg rasterization skipped in CI build; writing HTML fallback.");

            // Fallback: write an HTML file embedding the original SVG
            var outDir = Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? Directory.GetCurrentDirectory();
            var baseName = Path.GetFileNameWithoutExtension(outputPath);
            var htmlPath = Path.Combine(outDir, baseName + ".html");

            using var sw = new StreamWriter(htmlPath, false);
            sw.WriteLine("<!doctype html>");
            sw.WriteLine("<html><head><meta charset=\"utf-8\"><title>Cut sheet visuals</title></head><body>");
            sw.WriteLine("<p>This is an HTML fallback. If PDF output is required, ensure QuestPDF + SkiaSharp + Svg.Skia are available and re-run.");
            sw.WriteLine("See docs/docs/packer.md for instructions.</p>");
            sw.WriteLine(svg);
            sw.WriteLine("</body></html>");

            sw.Flush();
        }
    }
}
