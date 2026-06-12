using System;
using System.IO;
using System.Reflection;

namespace WWA.Core.Reporting
{
    /// <summary>
    /// Provides a hook to generate a PDF containing SVG diagrams. If QuestPDF is not available,
    /// falls back to writing an HTML file containing the SVG and a brief note on enabling QuestPDF.
    /// </summary>
    public static class PdfReporter
    {
        /// <summary>
        /// Generate a PDF (or fallback HTML) containing the provided SVG. If QuestPDF is available at runtime
        /// the method will attempt to use it. Otherwise a .html file will be written alongside the desired outputPath.
        /// </summary>
        /// <param name="svg">SVG markup</param>
        /// <param name="outputPath">Desired output path (usually .pdf). When falling back, writes .html next to it.</param>
        public static void GenerateFromSvg(string svg, string outputPath)
        {
            if (svg == null) throw new ArgumentNullException(nameof(svg));
            if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentNullException(nameof(outputPath));

            // Try to detect QuestPDF. If present, we would integrate. For M1 we provide a safe fallback stub.
            var questType = Type.GetType("QuestPDF.Fluent.Document, QuestPDF");
            if (questType != null)
            {
                // Lightweight, reliable QuestPDF integration: embed SVG as raw text in a PDF page (first-pass).
                // This avoids adding heavy rasterization dependencies in M1 while still producing a PDF.
                try
                {
                    var doc = QuestPDF.Fluent.Document.Create(container =>
                    {
                        container.Page(page =>
                        {
                            page.Size(QuestPDF.Helpers.PageSizes.A4);
                            page.Margin(20);
                            page.PageColor(QuestPDF.Helpers.Colors.White);
                            page.DefaultTextStyle(x => x.FontSize(10));

                            page.Content().Column(col =>
                            {
                                col.Item().Text("Cut-sheet SVG (raw):").SemiBold().FontSize(12);
                                col.Item().Text(svg).FontSize(8);
                            });
                        });
                    });

                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
                    doc.GeneratePdf(outputPath);
                    return;
                }
                catch (Exception ex)
                {
                    // If QuestPDF fails for any reason, fall back to HTML writer below and surface the exception in logs.
                    Console.Error.WriteLine("QuestPDF integration failed: " + ex.Message);
                }
            }

            // Fallback: write an HTML file that embeds the SVG so users can open in a browser.
            var outDir = Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? Directory.GetCurrentDirectory();
            var baseName = Path.GetFileNameWithoutExtension(outputPath);
            var htmlPath = Path.Combine(outDir, baseName + ".html");

            using var sw = new StreamWriter(htmlPath, false);
            sw.WriteLine("<!doctype html>");
            sw.WriteLine("<html><head><meta charset=\"utf-8\"><title>Cut sheet visuals</title></head><body>");
            sw.WriteLine("<p>This is an HTML fallback. To generate a real PDF, add the QuestPDF NuGet package and enable PdfReporter integration.");
            sw.WriteLine("See docs/docs/packer.md for instructions.</p>");
            sw.WriteLine(svg);
            sw.WriteLine("</body></html>");

            sw.Flush();
        }
    }
}
