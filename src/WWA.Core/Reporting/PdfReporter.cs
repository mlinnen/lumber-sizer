using System;
using System.IO;
using System.Reflection;
using System.Text;
using SkiaSharp;
using Svg.Skia;

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

            // Attempt to rasterize SVG using Svg.Skia + SkiaSharp
            try
            {
                using var svgStream = new MemoryStream(Encoding.UTF8.GetBytes(svg));
                var svgRenderer = new SKSvg();
                svgRenderer.Load(svgStream);

                var picture = svgRenderer.Picture;
                if (picture == null)
                    throw new InvalidOperationException("SVG could not be parsed into a drawable picture.");

                var cull = picture.CullRect;
                int width = Math.Max(1, (int)Math.Ceiling(cull.Width));
                int height = Math.Max(1, (int)Math.Ceiling(cull.Height));

                // Scale up for decent resolution
                int scale = 2;
                using var bitmap = new SKBitmap(width * scale, height * scale, SKColorType.Rgba8888, SKAlphaType.Premul);
                using var canvas = new SKCanvas(bitmap);
                canvas.Scale(scale);
                canvas.Clear(SKColors.Transparent);
                canvas.DrawPicture(picture);
                canvas.Flush();

                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                var pngBytes = data.ToArray();

                // If QuestPDF is available, embed image bytes into PDF
                var questType = Type.GetType("QuestPDF.Fluent.Document, QuestPDF");
                if (questType != null)
                {
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
                                    col.Item().Text("Cut-sheet diagram").SemiBold().FontSize(12);
                                    col.Item().Image(new MemoryStream(pngBytes));
                                });
                            });
                        });

                        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
                        doc.GeneratePdf(outputPath);
                        return;
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("QuestPDF embedding failed: " + ex.Message);
                        // fall through to HTML fallback
                    }
                }
            }
            catch (Exception ex)
            {
                // If rasterization fails, log and fall back to HTML.
                Console.Error.WriteLine("SVG rasterization failed: " + ex.Message);
            }

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
