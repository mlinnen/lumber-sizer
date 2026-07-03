using System;
using System.IO;
using Xunit;
using WWA.Core.Reporting;

namespace WWA.Core.Tests
{
    public class PdfReporterTests
    {
        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory!);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                dir = dir.Parent;
            }
            if (dir == null) throw new InvalidOperationException("Could not locate repository root (.git folder)");
            return dir.FullName;
        }

        [Fact]
        public void GenerateFromSvg_WritesPdf_WhenSkiaSucceeds_OtherwiseHtmlFallback()
        {
            var repoRoot = FindRepoRoot();
            var artifacts = Path.Combine(repoRoot, "artifacts");
            Directory.CreateDirectory(artifacts);

            var outPdf = Path.Combine(artifacts, "test_output.pdf");
            var htmlPath = Path.Combine(artifacts, "test_output.html");
            var svg = "<svg><rect width=\"10\" height=\"10\"/></svg>";

            try
            {
                if (File.Exists(outPdf)) File.Delete(outPdf);
                if (File.Exists(htmlPath)) File.Delete(htmlPath);

                PdfReporter.GenerateFromSvg(svg, outPdf);

                if (File.Exists(outPdf))
                {
                    Assert.False(File.Exists(htmlPath));
                    return;
                }

                Assert.True(File.Exists(htmlPath));
                var content = File.ReadAllText(htmlPath);
                Assert.Contains("<svg", content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                if (File.Exists(outPdf)) File.Delete(outPdf);
                if (File.Exists(htmlPath)) File.Delete(htmlPath);
            }
        }
    }
}
