using System;
using System.IO;
using Xunit;
using WWA.Core.IO;
using WWA.Core.Models;

namespace WWA.Core.Tests
{
    public class CutListParserTests
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
        public void Parse_SampleReadme_ReturnsTwoItems()
        {
            var repoRoot = FindRepoRoot();
                        // Create a small runtime sample file in the test folder to avoid build/copy issues
                        var sample = Path.Combine(repoRoot, "tests", "WWA.Core.Tests", $"runtime_simple_cutlist_{Guid.NewGuid()}.txt");
                        using (var fs = new System.IO.FileStream(sample, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.ReadWrite))
                        using (var sw = new System.IO.StreamWriter(fs))
                        {
                            sw.Write("12in x 2in # leg\r\n24in x 6in # shelf\r\n");
                        }

                        var rawLines = File.ReadAllLines(sample);
                        Assert.Equal(2, rawLines.Length);

                        var list = CutListParser.Parse(sample);
                        Assert.NotNull(list);
                        Assert.Equal(2, list.Items.Count);
                        Assert.Equal(12.0, list.Items[0].Length);
                        Assert.Equal(2.0, list.Items[0].Width);
                        Assert.Equal("leg", list.Items[0].Description);
                        Assert.Equal(24.0, list.Items[1].Length);
                        Assert.Equal(6.0, list.Items[1].Width);
                        Assert.Equal("shelf", list.Items[1].Description);
        }

        [Fact]
        public void Parse_Malformed_ThrowsFormatException()
        {
            var repoRoot = FindRepoRoot();
            var sample = Path.Combine(repoRoot, "tests", "WWA.Core.Tests", "malformed_cutlist.txt");
            Assert.Throws<FormatException>(() => CutListParser.Parse(sample));
        }
    }
}
