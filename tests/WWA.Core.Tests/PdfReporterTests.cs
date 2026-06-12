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
        public void GenerateFromSvg_Writes_Html_Fallback_When_No_QuestPDF()
        {
            var repoRoot = FindRepoRoot();
            var artifacts = Path.Combine(repoRoot, "artifacts");
            Directory.CreateDirectory(artifacts);

            var outPdf = Path.Combine(artifacts, "test_output.pdf");
            var svg = "<svg><rect width=\"10\" height=\"10\"/></svg>";

            PdfReporter.GenerateFromSvg(svg, outPdf);

            var htmlPath = Path.Combine(artifacts, "test_output.html");
            Assert.True(File.Exists(htmlPath));
            var content = File.ReadAllText(htmlPath);
            Assert.Contains("<svg", content, StringComparison.OrdinalIgnoreCase);
        }
    }
}
