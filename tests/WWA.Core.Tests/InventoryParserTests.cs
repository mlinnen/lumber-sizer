using System;
using System.IO;
using System.Linq;
using WWA.Core.IO;
using Xunit;

namespace WWA.Core.Tests
{
    public class InventoryParserTests
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
        public void Parse_ValidInventory_ReturnsBoards()
        {
            var repoRoot = FindRepoRoot();
            var sample = Path.Combine(repoRoot, "tests", "WWA.Core.Tests", $"runtime_inventory_{Guid.NewGuid()}.txt");
            try
            {
                File.WriteAllText(sample, "# Inventory\r\n96in x 48in x 5 # A\r\n120in x 12in\r\n");

                var inventory = InventoryParser.Parse(sample);
                var boards = inventory.EnumerateAvailable().ToList();

                Assert.Equal(2, boards.Count);
                Assert.Equal(96.0, boards[0].Length);
                Assert.Equal(48.0, boards[0].Width);
                Assert.Equal(5, boards[0].Quantity);
                Assert.Equal("A", boards[0].Grade);
                Assert.Equal(120.0, boards[1].Length);
                Assert.Equal(12.0, boards[1].Width);
                Assert.Equal(1, boards[1].Quantity);
                Assert.Null(boards[1].Grade);
            }
            finally
            {
                if (File.Exists(sample)) File.Delete(sample);
            }
        }

        [Fact]
        public void Parse_MalformedQuantity_ThrowsFormatException()
        {
            var repoRoot = FindRepoRoot();
            var sample = Path.Combine(repoRoot, "tests", "WWA.Core.Tests", $"runtime_bad_inventory_{Guid.NewGuid()}.txt");
            try
            {
                File.WriteAllText(sample, "96in x 48in x many\r\n");

                Assert.Throws<FormatException>(() => InventoryParser.Parse(sample));
            }
            finally
            {
                if (File.Exists(sample)) File.Delete(sample);
            }
        }

        [Theory]
        [InlineData("-96in x 48in x 5")]
        [InlineData("96in x -48in x 5")]
        public void Parse_NegativeDimension_ThrowsFormatException(string inventoryLine)
        {
            var repoRoot = FindRepoRoot();
            var sample = Path.Combine(repoRoot, "tests", "WWA.Core.Tests", $"runtime_negative_inventory_{Guid.NewGuid()}.txt");
            try
            {
                File.WriteAllText(sample, inventoryLine + "\r\n");

                var ex = Assert.Throws<FormatException>(() => InventoryParser.Parse(sample));
                Assert.Contains("Malformed dimension on line 1", ex.Message, StringComparison.Ordinal);
            }
            finally
            {
                if (File.Exists(sample)) File.Delete(sample);
            }
        }
    }
}
