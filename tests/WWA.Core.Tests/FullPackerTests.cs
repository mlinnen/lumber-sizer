using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using WWA.Core.BinPacking;
using WWA.Core.Models;
using Xunit;

namespace WWA.Core.Tests
{
    public class FullPackerTests
    {
        [Fact]
        public async Task Simple_Exact_Fit_One_Board_Used()
        {
            var packer = new FullPacker();
            var cutList = new CutList();
            cutList.Add(new CutItem(30) { Quantity = 2 });
            cutList.Add(new CutItem(36) { Quantity = 1 });

            var inv = new Inventory();
            inv.Add(new Board(96, 5, quantity: 1));

            var req = new PackingRequest { CutList = cutList, Inventory = inv, Constraints = new Constraints() };
            var res = await packer.PackAsync(req);

            Assert.NotNull(res);
            Assert.Equal(1, res.TotalBoardsUsed);
            Assert.Equal(0, res.TotalWasteLength, 3);
        }

        [Fact]
        public async Task Determinism_Same_Seed_Produces_Identical_PackingResult_JSON()
        {
            var packer = new FullPacker();
            var cutList = new CutList();
            cutList.Add(new CutItem(10) { Quantity = 5 });
            cutList.Add(new CutItem(15) { Quantity = 3 });

            var inv = new Inventory();
            inv.Add(new Board(100, 5, quantity: 1));
            inv.Add(new Board(50, 5, quantity: 1));

            var req1 = new PackingRequest { CutList = cutList, Inventory = inv, Constraints = new Constraints(), Seed = 12345 };
            var req2 = new PackingRequest { CutList = cutList, Inventory = inv, Constraints = new Constraints(), Seed = 12345 };

            var r1 = await packer.PackAsync(req1);
            var r2 = await packer.PackAsync(req2);

            var opts = new JsonSerializerOptions { WriteIndented = false };
            var j1 = JsonSerializer.Serialize(r1, opts);
            var j2 = JsonSerializer.Serialize(r2, opts);
            Assert.Equal(j1, j2);
        }

        [Fact]
        public async Task Remnant_Preservation_Prefers_Keeping_Long_Remnant()
        {
            var packer = new FullPacker();

            // Two boards: one 100in, one 140in. MinRemnantLength = 50.
            var inv = new Inventory();
            var b1 = new Board(100, 5, quantity: 1); // prefer to use this to avoid cutting the 140 into small remnant
            var b2 = new Board(140, 5, quantity: 1);
            inv.Add(b1);
            inv.Add(b2);

            var cutList = new CutList();
            cutList.Add(new CutItem(60) { Quantity = 1 });
            cutList.Add(new CutItem(40) { Quantity = 1 });

            var constraints = new Constraints { MinRemnantLength = 50, PreserveLongRemnants = true };
            var req = new PackingRequest { CutList = cutList, Inventory = inv, Constraints = constraints };
            var res = await packer.PackAsync(req);

            // The 60+40 pieces exactly sum 100; algorithm should prefer placing them both on the 100in board
            Assert.True(res.TotalBoardsUsed >= 1);
            // Check that at least one allocation has remnant 0 (exact fit) rather than splitting the 140
            Assert.Contains(res.Allocations, a => a.RemnantLength <= 1e-6 && a.OriginalBoardLength == 100);
        }

        [Fact]
        public async Task Item_Longer_Than_Any_Board_Is_Unplaced()
        {
            var packer = new FullPacker();
            var cutList = new CutList();
            cutList.Add(new CutItem(200) { Quantity = 1 });

            var inv = new Inventory();
            inv.Add(new Board(96, 5, quantity: 1));

            var req = new PackingRequest { CutList = cutList, Inventory = inv, Constraints = new Constraints() };
            var res = await packer.PackAsync(req);

            Assert.Single(res.UnplacedItems);
            Assert.Equal(200, res.UnplacedItems.First().Length);
        }
    }
}
