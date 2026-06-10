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
            var sample = Path.Combine(repoRoot, "samples", "sample_cutlists", "simple_cutlist.txt");
            var rawLines = File.ReadAllLines(sample);
                        Console.WriteLine($"Sample path used: {sample}");
                        Console.WriteLine($"Raw lines: {rawLines.Length}");
                        Assert.Equal(2, rawLines.Length);

                        var list = CutListParser.Parse(sample);
                        Assert.NotNull(list);
                        Assert.Equal(2, list.Items.Count);
            Assert.Equal("12in", list.Items[0].Length);
            Assert.Equal("2in", list.Items[0].Width);
            Assert.Equal("leg", list.Items[0].Description);
            Assert.Equal("24in", list.Items[1].Length);
            Assert.Equal("6in", list.Items[1].Width);
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
