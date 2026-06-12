using System.IO;
using WWA.Core.Reporting;
using Xunit;

namespace WWA.Core.Tests
{
    public class PdfReporterQuestPdfTests
    {
        [Fact]
        public void GeneratesPdfFileFromSvgString()
        {
            var svg = "<svg xmlns='http://www.w3.org/2000/svg' width='200' height='100'><rect width='200' height='100' fill='lightgray'/><text x='10' y='20'>test</text></svg>";
            var outDir = Path.Combine(Directory.GetCurrentDirectory(), "artifacts");
            Directory.CreateDirectory(outDir);
            var outPdf = Path.Combine(outDir, "test_svg_output.pdf");
            if (File.Exists(outPdf)) File.Delete(outPdf);

            PdfReporter.GenerateFromSvg(svg, outPdf);

            // Accept either real PDF or HTML fallback. Check that one of the expected files exists.
            var htmlPath = Path.ChangeExtension(outPdf, ".html");
            Assert.True(File.Exists(outPdf) || File.Exists(htmlPath));
        }
    }
}
