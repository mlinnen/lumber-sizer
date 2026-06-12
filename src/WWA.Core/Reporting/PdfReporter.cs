using System;
using System.IO;
using System.Reflection;
using System.Text;
#if HAS_SKIA
using SkiaSharp;
using Svg.Skia;
#endif

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

#if HAS_SKIA
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

                int scale = 2;
                using var bitmap = new SKBitmap(width * scale, height * scale, SKColorType.Rgba8888, SKAlphaType.Premul);
                using var canvas = new SKCanvas(bitmap);
                canvas.Scale(scale);
                canvas.Clear(SKColors.Transparent);
                canvas.DrawPicture(picture);
                canvas.Flush();

                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);

                Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
                using var pdf = SKDocument.CreatePdf(outputPath);
                using var img = SKImage.FromEncodedData(data);
                var pageWidth = img.Width;
                var pageHeight = img.Height;

                var pdfCanvas = pdf.BeginPage(pageWidth, pageHeight);
                pdfCanvas.Clear(SKColors.White);
                using var paint = new SKPaint();
                pdfCanvas.DrawImage(img, 0, 0, paint);
                pdf.EndPage();
                pdf.Close();

                return;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Skia rasterization failed: " + ex.Message);
                // fall through to HTML fallback
            }
#else
            // Skia disabled: log and fall back to HTML
            Console.Error.WriteLine("Skia/Svg rasterization disabled (HAS_SKIA not defined); writing HTML fallback.");
#endif

            // Fallback: write an HTML file embedding the original SVG
            var outDir = Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? Directory.GetCurrentDirectory();
            var baseName = Path.GetFileNameWithoutExtension(outputPath);
            var htmlPath = Path.Combine(outDir, baseName + ".html");

            using var sw = new StreamWriter(htmlPath, false);
            sw.WriteLine("<!doctype html>");
            sw.WriteLine("<html><head><meta charset=\"utf-8\"><title>Cut sheet visuals</title>");
            sw.WriteLine("<style>body{font-family:Segoe UI,Arial,Helvetica,sans-serif;margin:20px;background:#fff;color:#222} .notice{background:#fff3cd;border:1px solid #ffeeba;padding:12px;border-radius:6px;margin-bottom:12px} svg{max-width:100%;height:auto;border:1px solid #e6e6e6}</style>");
            sw.WriteLine("</head><body>");
            sw.WriteLine("<div class=\"notice\">This is an HTML fallback. PDF rasterization requires SkiaSharp+Svg.Skia at build/runtime. To enable PDF output, build with HAS_SKIA and install native Skia libs on the runner. See docs/docs/packer.md for instructions.</div>");
            sw.WriteLine(svg);
            sw.WriteLine("</body></html>");

            sw.Flush();
        }
    }
}
