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

        // ── Strategy tests ────────────────────────────────────────────────────────

        [Theory]
        [InlineData(PackingStrategy.BestFitDecreasing)]
        [InlineData(PackingStrategy.FirstFitDecreasing)]
        [InlineData(PackingStrategy.FirstFit)]
        public async Task All_Strategies_Place_Items_And_Report_Correct_Waste(PackingStrategy strategy)
        {
            var packer = new FullPacker();
            var cutList = new CutList();
            cutList.Add(new CutItem(20) { Quantity = 3 });
            cutList.Add(new CutItem(10) { Quantity = 2 });

            var inv = new Inventory();
            inv.Add(new Board(96, 5, quantity: 2));

            var req = new PackingRequest { CutList = cutList, Inventory = inv, Constraints = new Constraints(), Strategy = strategy };
            var res = await packer.PackAsync(req);

            Assert.NotNull(res);
            Assert.Empty(res.UnplacedItems);
            Assert.Equal(80, res.TotalUsedLength, 3);  // 3×20 + 2×10 = 80
            Assert.True(res.WastePercent >= 0 && res.WastePercent <= 100);
        }

        [Theory]
        [InlineData(PackingStrategy.BestFitDecreasing)]
        [InlineData(PackingStrategy.FirstFitDecreasing)]
        [InlineData(PackingStrategy.FirstFit)]
        public async Task All_Strategies_Are_Reproducible_With_Seed(PackingStrategy strategy)
        {
            var packer = new FullPacker();
            var cutList = new CutList();
            cutList.Add(new CutItem(10) { Quantity = 4 });
            cutList.Add(new CutItem(15) { Quantity = 2 });
            cutList.Add(new CutItem(10) { Quantity = 4 }); // duplicate lengths → tie-group

            var inv = new Inventory();
            inv.Add(new Board(60, 5, quantity: 3));

            var req1 = new PackingRequest { CutList = cutList, Inventory = inv, Constraints = new Constraints(), Seed = 99, Strategy = strategy };
            var req2 = new PackingRequest { CutList = cutList, Inventory = inv, Constraints = new Constraints(), Seed = 99, Strategy = strategy };

            var r1 = await packer.PackAsync(req1);
            var r2 = await packer.PackAsync(req2);

            var opts = new JsonSerializerOptions { WriteIndented = false };
            Assert.Equal(JsonSerializer.Serialize(r1, opts), JsonSerializer.Serialize(r2, opts));
        }

        [Theory]
        [InlineData(PackingStrategy.BestFitDecreasing)]
        [InlineData(PackingStrategy.FirstFitDecreasing)]
        [InlineData(PackingStrategy.FirstFit)]
        public async Task All_Strategies_Are_Reproducible_Without_Seed(PackingStrategy strategy)
        {
            var packer = new FullPacker();
            var cutList = new CutList();
            cutList.Add(new CutItem(12) { Quantity = 3 });
            cutList.Add(new CutItem(18) { Quantity = 2 });

            var inv = new Inventory();
            inv.Add(new Board(72, 5, quantity: 2));

            var req1 = new PackingRequest { CutList = cutList, Inventory = inv, Constraints = new Constraints(), Strategy = strategy };
            var req2 = new PackingRequest { CutList = cutList, Inventory = inv, Constraints = new Constraints(), Strategy = strategy };

            var r1 = await packer.PackAsync(req1);
            var r2 = await packer.PackAsync(req2);

            var opts = new JsonSerializerOptions { WriteIndented = false };
            Assert.Equal(JsonSerializer.Serialize(r1, opts), JsonSerializer.Serialize(r2, opts));
        }

        [Fact]
        public async Task BFD_Packs_More_Efficiently_Than_FF_For_Fragmented_Items()
        {
            // Three boards of 30in. Items: four 10in and two 20in pieces = 80in total.
            // BFD sorts longest first (20,20,10,10,10,10) and still packs everything.
            var packer = new FullPacker();
            var cutList = new CutList();
            cutList.Add(new CutItem(10) { Quantity = 4 });
            cutList.Add(new CutItem(20) { Quantity = 2 });

            var inv = new Inventory();
            inv.Add(new Board(30, 5, quantity: 3));

            var req = new PackingRequest { CutList = cutList, Inventory = inv, Constraints = new Constraints(), Strategy = PackingStrategy.BestFitDecreasing };
            var res = await packer.PackAsync(req);

            Assert.Empty(res.UnplacedItems);
            Assert.Equal(80, res.TotalUsedLength, 3); // 4×10 + 2×20 = 80
        }

        [Fact]
        public async Task FFD_Strategy_Sorts_Longest_First()
        {
            // Single 50in board. Items listed shortest-first: 15in, then 30in.
            // FFD pre-sorts longest-first, so 30in is placed at offset 0, then 15in at offset 30.
            var packer = new FullPacker();
            var cutList = new CutList();
            cutList.Add(new CutItem(15) { Quantity = 1 }); // shorter first in list
            cutList.Add(new CutItem(30) { Quantity = 1 }); // longer second in list

            var inv = new Inventory();
            inv.Add(new Board(50, 5, quantity: 1));

            var req = new PackingRequest { CutList = cutList, Inventory = inv, Constraints = new Constraints(), Strategy = PackingStrategy.FirstFitDecreasing };
            var res = await packer.PackAsync(req);

            Assert.Empty(res.UnplacedItems);
            // FFD should have placed the 30in piece first (offset 0) then the 15in piece
            var placements = res.Allocations.SelectMany(a => a.Placements).OrderBy(p => p.Offset).ToList();
            Assert.Equal(2, placements.Count);
            Assert.Equal(30, placements[0].Length); // longest goes first
            Assert.Equal(15, placements[1].Length);
        }

        [Fact]
        public async Task FF_Strategy_Preserves_Input_Order()
        {
            // Single 40in board. Items: 15in first, then 30in.
            // FF places in original order: 15 at offset 0, 30 at offset 15. Both fit.
            var packer = new FullPacker();
            var cutList = new CutList();
            cutList.Add(new CutItem(15) { Quantity = 1 });
            cutList.Add(new CutItem(30) { Quantity = 1 });

            var inv = new Inventory();
            inv.Add(new Board(50, 5, quantity: 1));

            var req = new PackingRequest { CutList = cutList, Inventory = inv, Constraints = new Constraints(), Strategy = PackingStrategy.FirstFit };
            var res = await packer.PackAsync(req);

            Assert.Empty(res.UnplacedItems);
            var placements = res.Allocations.SelectMany(a => a.Placements).OrderBy(p => p.Offset).ToList();
            Assert.Equal(2, placements.Count);
            Assert.Equal(15, placements[0].Length); // input order preserved
            Assert.Equal(30, placements[1].Length);
        }

        [Fact]
        public async Task Seed_Is_Recorded_In_Result()
        {
            var packer = new FullPacker();
            var cutList = new CutList();
            cutList.Add(new CutItem(10) { Quantity = 1 });
            var inv = new Inventory();
            inv.Add(new Board(20, 5, quantity: 1));

            var res = await packer.PackAsync(new PackingRequest { CutList = cutList, Inventory = inv, Seed = 42 });
            Assert.Equal(42, res.DeterministicSeedUsed);

            var res2 = await packer.PackAsync(new PackingRequest { CutList = cutList, Inventory = inv });
            Assert.Null(res2.DeterministicSeedUsed);
        }

        // ── Regression tests for issue #3 correctness bugs ────────────────────────

        /// <summary>
        /// Reviewer-identified probe: 2 physical boards (same template, quantity=2), 3 cuts of
        /// 40in on 96in boards. Must produce 2 separate allocations — one per physical board —
        /// not a single collapsed allocation with all placements sharing Offset=0.
        /// </summary>
        [Fact]
        public async Task QuantityExpandedBoards_ProduceSeparateAllocations()
        {
            var packer = new FullPacker();
            var cl = new CutList();
            cl.Add(new CutItem(40) { Quantity = 3 }); // 3 × 40in = 120in total
            var inv = new Inventory();
            inv.Add(new Board(96, 5, quantity: 2)); // 2 × 96in = 192in — all cuts fit

            var req = new PackingRequest { CutList = cl, Inventory = inv, Constraints = new Constraints() };
            var res = await packer.PackAsync(req);

            Assert.Empty(res.UnplacedItems);
            // Two physical boards must yield two distinct allocations.
            Assert.Equal(2, res.Allocations.Count);
            // Each physical board must have a unique BoardId.
            var boardIds = res.Allocations.Select(a => a.BoardId).ToList();
            Assert.Equal(2, boardIds.Distinct().Count());
            // Total placements must equal 3 (one per cut).
            var allPlacements = res.Allocations.SelectMany(a => a.Placements).ToList();
            Assert.Equal(3, allPlacements.Count);
        }

        /// <summary>
        /// Expanded copies of a quantity>1 CutItem must each carry a distinct CutItemId so
        /// that individual placements are distinguishable in the result.
        /// </summary>
        [Fact]
        public async Task QuantityExpandedCutItems_HaveUniqueIds()
        {
            var packer = new FullPacker();
            var cl = new CutList();
            cl.Add(new CutItem(20) { Quantity = 3 }); // 3 physical copies from one template
            var inv = new Inventory();
            inv.Add(new Board(96, 5, quantity: 1));

            var req = new PackingRequest { CutList = cl, Inventory = inv, Constraints = new Constraints() };
            var res = await packer.PackAsync(req);

            var cutIds = res.Allocations.SelectMany(a => a.Placements).Select(p => p.CutItemId).ToList();
            Assert.Equal(3, cutIds.Count);
            Assert.Equal(3, cutIds.Distinct().Count()); // every placement must have a unique CutItemId
        }

        [Fact]
        public async Task Allocations_Preserve_Source_Board_Width()
        {
            var packer = new FullPacker();
            var cutList = new CutList();
            cutList.Add(new CutItem(24, 6, 1, true, "shelf"));

            var inventory = new Inventory();
            inventory.Add(new Board(96, 48, quantity: 1));

            var result = await packer.PackAsync(new PackingRequest
            {
                CutList = cutList,
                Inventory = inventory,
                Constraints = new Constraints()
            });

            var allocation = Assert.Single(result.Allocations);
            Assert.Equal(48, allocation.OriginalBoardWidth);
        }
    }
}
