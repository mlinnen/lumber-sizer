using System.Threading.Tasks;
using WWA.Core.IO;
using WWA.Core.BinPacking;
using Xunit;

namespace WWA.Core.Tests
{
    public class FullPackerIntegrationTests
    {
        [Fact]
        public async Task Parse_Sample_Cutlist_And_Run_FullPacker()
        {
            string? path = null;
            var dir = new System.IO.DirectoryInfo(System.IO.Directory.GetCurrentDirectory());
            while (dir != null)
            {
                var candidate = System.IO.Path.Combine(dir.FullName, "samples", "sample_cutlists", "simple_cutlist.txt");
                if (System.IO.File.Exists(candidate)) { path = candidate; break; }
                dir = dir.Parent;
            }
            Assert.False(string.IsNullOrEmpty(path), "sample cutlist not found in repo tree");
            var cutList = CutListParser.Parse(path);
            var inv = new WWA.Core.Models.Inventory();
            inv.Add(new WWA.Core.Models.Board(96, 6, quantity: 1));

            var req = new WWA.Core.Models.PackingRequest { CutList = cutList, Inventory = inv, Constraints = new WWA.Core.Models.Constraints() };
            var packer = new FullPacker();
            var res = await packer.PackAsync(req);

            Assert.NotNull(res);
            Assert.True(res.Allocations.Count >= 0); // just ensure it runs without exception and returns structure
        }
    }
}
