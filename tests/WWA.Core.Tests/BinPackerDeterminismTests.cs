using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using WWA.Core.BinPacking;
using WWA.Core.Interfaces;
using WWA.Core.Models;
using Xunit;

namespace WWA.Core.Tests
{
    /// <summary>
    /// Focused determinism and reproducibility tests for the 1D bin-packers (issue #3).
    ///
    /// Each test proves one narrow aspect of the reproducibility contract:
    ///   - same inputs + same seed  →  bit-identical results across N independent calls
    ///   - same inputs + no seed    →  bit-identical results (BFD is a pure algorithm)
    ///   - different packer object instances produce the same output (no hidden static state)
    ///   - placement offsets — not just aggregate stats — are identical between runs
    ///   - the seed is echoed back in PackingResult.DeterministicSeedUsed
    ///   - different seeds produce different tie-breaking orderings
    ///   - large / stress inputs are also reproducible
    ///
    /// Design note: cut-list and inventory objects are created ONCE per test and shared
    /// across all requests in that test so that item GUIDs remain stable, enabling
    /// byte-for-byte JSON comparison of the results.
    /// </summary>
    public class BinPackerDeterminismTests
    {
        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        /// <summary>Creates a reusable standard cut-list and inventory.</summary>
        private static (CutList cutList, Inventory inv) StandardInputs()
        {
            var cl = new CutList();
            cl.Add(new CutItem(24, quantity: 3) { Description = "shelf" });
            cl.Add(new CutItem(36, quantity: 2) { Description = "side" });
            cl.Add(new CutItem(18, quantity: 4) { Description = "brace" });
            cl.Add(new CutItem(48, quantity: 1) { Description = "top" });

            var inv = new Inventory();
            inv.Add(new Board(96, 6, quantity: 2));
            inv.Add(new Board(72, 6, quantity: 1));

            return (cl, inv);
        }

        private static PackingRequest Req(CutList cl, Inventory inv, int? seed = null)
            => new() { CutList = cl, Inventory = inv, Constraints = new Constraints(), Seed = seed };

        /// <summary>
        /// Serialises the allocation-level portion of a result (offsets, lengths, placement
        /// order, totals).  Leftover board IDs are intentionally excluded because the
        /// DeterministicPackerStub assigns new GUIDs to remnant boards; only FullPacker
        /// copies the original board ID into remnants.
        /// </summary>
        private static string PackingSignature(PackingResult r)
        {
            var obj = new
            {
                r.TotalUsedLength,
                r.TotalWasteLength,
                r.WastePercent,
                r.DeterministicSeedUsed,
                UnplacedCount = r.UnplacedItems.Count,
                Allocations = r.Allocations.Select(a => new
                {
                    a.BoardId,
                    a.OriginalBoardLength,
                    a.RemnantLength,
                    Placements = a.Placements.Select(p => new
                    {
                        p.CutItemId,
                        p.Offset,
                        p.Length,
                        p.Rotated
                    })
                })
            };
            return JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = false });
        }

        // ------------------------------------------------------------------
        // FullPacker — reproducibility
        // ------------------------------------------------------------------

        [Fact]
        public async Task FullPacker_SameSeed_TenRuns_AllProduceIdenticalResult()
        {
            var packer = new FullPacker();
            var (cl, inv) = StandardInputs();
            const int seed = 42;

            string? reference = null;
            for (int run = 0; run < 10; run++)
            {
                var res = await packer.PackAsync(Req(cl, inv, seed));
                var sig = PackingSignature(res);
                reference ??= sig;
                Assert.Equal(reference, sig);
            }
        }

        [Fact]
        public async Task FullPacker_NoSeed_TenRuns_AllProduceIdenticalResult()
        {
            // BFD is a pure deterministic algorithm; without a seed the tie-break
            // always falls back to board index order, so results must be stable.
            var packer = new FullPacker();
            var (cl, inv) = StandardInputs();

            string? reference = null;
            for (int run = 0; run < 10; run++)
            {
                var res = await packer.PackAsync(Req(cl, inv));
                var sig = PackingSignature(res);
                reference ??= sig;
                Assert.Equal(reference, sig);
            }
        }

        [Fact]
        public async Task FullPacker_SameSeed_DifferentInstances_ProduceIdenticalResult()
        {
            // Two separately constructed FullPacker instances must produce the same
            // output for the same request; proves there is no mutable static state.
            var (cl, inv) = StandardInputs();
            const int seed = 77;

            var r1 = await new FullPacker().PackAsync(Req(cl, inv, seed));
            var r2 = await new FullPacker().PackAsync(Req(cl, inv, seed));

            Assert.Equal(PackingSignature(r1), PackingSignature(r2));
        }

        [Fact]
        public async Task FullPacker_SameSeed_PlacementOffsetsAreIdentical()
        {
            // Aggregate stats (waste %) are an insufficient proxy; verify that every
            // individual placement offset matches to floating-point precision.
            var packer = new FullPacker();
            var (cl, inv) = StandardInputs();
            const int seed = 12345;

            var r1 = await packer.PackAsync(Req(cl, inv, seed));
            var r2 = await packer.PackAsync(Req(cl, inv, seed));

            var offsets1 = r1.Allocations.SelectMany(a => a.Placements).Select(p => p.Offset).ToList();
            var offsets2 = r2.Allocations.SelectMany(a => a.Placements).Select(p => p.Offset).ToList();

            Assert.Equal(offsets1.Count, offsets2.Count);
            for (int i = 0; i < offsets1.Count; i++)
                Assert.Equal(offsets1[i], offsets2[i], precision: 9);
        }

        [Fact]
        public async Task FullPacker_SameSeed_WasteMetricsAreIdentical()
        {
            var packer = new FullPacker();
            var (cl, inv) = StandardInputs();
            const int seed = 99;

            var r1 = await packer.PackAsync(Req(cl, inv, seed));
            var r2 = await packer.PackAsync(Req(cl, inv, seed));

            Assert.Equal(r1.TotalUsedLength,  r2.TotalUsedLength,  precision: 9);
            Assert.Equal(r1.TotalWasteLength, r2.TotalWasteLength, precision: 9);
            Assert.Equal(r1.WastePercent,     r2.WastePercent,     precision: 9);
            Assert.Equal(r1.UnplacedItems.Count, r2.UnplacedItems.Count);
        }

        [Fact]
        public async Task FullPacker_SeedIsRecordedInResult()
        {
            const int seed = 55555;
            var (cl, inv) = StandardInputs();
            var res = await new FullPacker().PackAsync(Req(cl, inv, seed));
            Assert.Equal(seed, res.DeterministicSeedUsed);
        }

        [Fact]
        public async Task FullPacker_NullSeed_ResultHasNullSeed()
        {
            var (cl, inv) = StandardInputs();
            var res = await new FullPacker().PackAsync(Req(cl, inv));
            Assert.Null(res.DeterministicSeedUsed);
        }

        [Fact]
        public async Task FullPacker_DifferentSeeds_ProduceDifferentTieBreakerOrder()
        {
            // Six items of identical length form one tie-group.  The seeded Fisher-Yates
            // shuffle within that group must produce different permutations for different
            // seeds; verify reproducibility of seed=1 AND that seed=9999 yields a
            // different ordering.
            var packer = new FullPacker();
            var cl = new CutList();
            for (int i = 0; i < 6; i++)
                cl.Add(new CutItem(10, quantity: 1) { Description = $"piece-{i}" });
            var inv = new Inventory();
            inv.Add(new Board(96, 6, quantity: 1));

            var r1a = await packer.PackAsync(Req(cl, inv, seed: 1));
            var r1b = await packer.PackAsync(Req(cl, inv, seed: 1));    // same seed, second run
            var r2  = await packer.PackAsync(Req(cl, inv, seed: 9999));

            // Reproducibility: seed=1 must be stable
            Assert.Equal(PackingSignature(r1a), PackingSignature(r1b));

            // Seed sensitivity: seed=9999 must produce a different placement order
            var ids1 = r1a.Allocations.SelectMany(a => a.Placements).Select(p => p.CutItemId).ToList();
            var ids2 = r2.Allocations.SelectMany(a => a.Placements).Select(p => p.CutItemId).ToList();
            Assert.False(ids1.SequenceEqual(ids2),
                "Seeds 1 and 9999 should produce different orderings for 6 equal-length items");
        }

        [Fact]
        public async Task FullPacker_LargeInput_SameSeed_ThreeRunsIdentical()
        {
            // Stress: 20 item types × 2 quantity = 40 expanded items across 8 boards.
            var packer = new FullPacker();
            const int seed = 98765;

            var cl = new CutList();
            var rng = new Random(1); // fixed construction seed — unrelated to the packing seed
            for (int i = 0; i < 20; i++)
                cl.Add(new CutItem(Math.Round(rng.NextDouble() * 40 + 10, 1), quantity: 2));

            var inv = new Inventory();
            inv.Add(new Board(96, 6, quantity: 5));
            inv.Add(new Board(120, 6, quantity: 3));

            var r1 = await packer.PackAsync(Req(cl, inv, seed));
            var r2 = await packer.PackAsync(Req(cl, inv, seed));
            var r3 = await packer.PackAsync(Req(cl, inv, seed));

            Assert.Equal(PackingSignature(r1), PackingSignature(r2));
            Assert.Equal(PackingSignature(r2), PackingSignature(r3));
        }

        // ------------------------------------------------------------------
        // DeterministicPackerStub — reproducibility
        // ------------------------------------------------------------------

        [Fact]
        public async Task Stub_SameSeed_TenRuns_AllProduceIdenticalResult()
        {
            var packer = new DeterministicPackerStub();
            var (cl, inv) = StandardInputs();
            const int seed = 42;

            string? reference = null;
            for (int run = 0; run < 10; run++)
            {
                var res = await packer.PackAsync(Req(cl, inv, seed));
                var sig = PackingSignature(res);
                reference ??= sig;
                Assert.Equal(reference, sig);
            }
        }

        [Fact]
        public async Task Stub_NoSeed_TenRuns_AllProduceIdenticalResult()
        {
            // Without a seed the stub preserves insertion order; output must be stable.
            var packer = new DeterministicPackerStub();
            var (cl, inv) = StandardInputs();

            string? reference = null;
            for (int run = 0; run < 10; run++)
            {
                var res = await packer.PackAsync(Req(cl, inv));
                var sig = PackingSignature(res);
                reference ??= sig;
                Assert.Equal(reference, sig);
            }
        }

        [Fact]
        public async Task Stub_SameSeed_DifferentInstances_ProduceIdenticalResult()
        {
            var (cl, inv) = StandardInputs();
            const int seed = 333;

            var r1 = await new DeterministicPackerStub().PackAsync(Req(cl, inv, seed));
            var r2 = await new DeterministicPackerStub().PackAsync(Req(cl, inv, seed));

            Assert.Equal(PackingSignature(r1), PackingSignature(r2));
        }

        [Fact]
        public async Task Stub_SameSeed_PlacementOffsetsAreIdentical()
        {
            var packer = new DeterministicPackerStub();
            var (cl, inv) = StandardInputs();
            const int seed = 12345;

            var r1 = await packer.PackAsync(Req(cl, inv, seed));
            var r2 = await packer.PackAsync(Req(cl, inv, seed));

            var offsets1 = r1.Allocations.SelectMany(a => a.Placements).Select(p => p.Offset).ToList();
            var offsets2 = r2.Allocations.SelectMany(a => a.Placements).Select(p => p.Offset).ToList();

            Assert.Equal(offsets1.Count, offsets2.Count);
            for (int i = 0; i < offsets1.Count; i++)
                Assert.Equal(offsets1[i], offsets2[i], precision: 9);
        }

        [Fact]
        public async Task Stub_SeedIsRecordedInResult()
        {
            const int seed = 7777;
            var (cl, inv) = StandardInputs();
            var res = await new DeterministicPackerStub().PackAsync(Req(cl, inv, seed));
            Assert.Equal(seed, res.DeterministicSeedUsed);
        }

        [Fact]
        public async Task Stub_NullSeed_ResultHasNullSeed()
        {
            var (cl, inv) = StandardInputs();
            var res = await new DeterministicPackerStub().PackAsync(Req(cl, inv));
            Assert.Null(res.DeterministicSeedUsed);
        }

        [Fact]
        public async Task Stub_WithSeed_OrderDiffersFromNoSeed()
        {
            // Without a seed the stub keeps insertion order; a seeded Fisher-Yates
            // shuffle over 8 identical-length items must produce a different sequence.
            var packer = new DeterministicPackerStub();

            var cl = new CutList();
            for (int i = 0; i < 8; i++)
                cl.Add(new CutItem(10, quantity: 1) { Description = $"p{i}" });
            var inv = new Inventory();
            inv.Add(new Board(96, 6, quantity: 1));

            var rNoSeed = await packer.PackAsync(Req(cl, inv));
            var rSeeded  = await packer.PackAsync(Req(cl, inv, seed: 42));

            var idsNoSeed = rNoSeed.Allocations.SelectMany(a => a.Placements).Select(p => p.CutItemId).ToList();
            var idsSeeded  = rSeeded.Allocations.SelectMany(a => a.Placements).Select(p => p.CutItemId).ToList();

            Assert.Equal(idsNoSeed.Count, idsSeeded.Count);
            Assert.False(idsNoSeed.SequenceEqual(idsSeeded),
                "Seed=42 must shuffle 8 equal-length items differently from insertion order");
        }

        // ------------------------------------------------------------------
        // Cross-packer contract
        // ------------------------------------------------------------------

        [Fact]
        public async Task BothPackers_SameSeed_HonourSeedContract()
        {
            // Both packers must return non-null results with non-negative metrics
            // and correctly echo back the seed they were given.
            const int seed = 1234;
            var (cl, inv) = StandardInputs();

            var fullRes = await new FullPacker().PackAsync(Req(cl, inv, seed));
            var stubRes = await new DeterministicPackerStub().PackAsync(Req(cl, inv, seed));

            Assert.NotNull(fullRes);
            Assert.NotNull(stubRes);
            Assert.True(fullRes.WastePercent >= 0);
            Assert.True(stubRes.WastePercent >= 0);
            Assert.Equal(seed, fullRes.DeterministicSeedUsed);
            Assert.Equal(seed, stubRes.DeterministicSeedUsed);
        }

        // ------------------------------------------------------------------
        // Correctness regression tests (issue #3 reviewer findings)
        // ------------------------------------------------------------------

        /// <summary>
        /// Reviewer probe: 2 physical boards from the same template (quantity=2), 3 cuts of 40in
        /// on 96in boards. Before the fix, all 3 cuts collapsed into a single allocation with
        /// duplicate Offset=0. After the fix each physical board has its own allocation and
        /// offsets are correct.
        /// </summary>
        [Fact]
        public async Task FullPacker_QuantityExpandedBoards_DoNotCollapseAllocations()
        {
            var packer = new FullPacker();
            var cl = new CutList();
            cl.Add(new CutItem(40) { Quantity = 3 }); // 3 × 40in = 120in total
            var inv = new Inventory();
            inv.Add(new Board(96, 5, quantity: 2)); // 2 × 96in = 192in available

            var res = await packer.PackAsync(Req(cl, inv));

            Assert.Empty(res.UnplacedItems);
            Assert.Equal(2, res.Allocations.Count);
            Assert.Equal(2, res.Allocations.Select(a => a.BoardId).Distinct().Count());
            Assert.Equal(3, res.Allocations.SelectMany(a => a.Placements).Count());
            // Offsets across both allocations must not all be zero (second cut on a board)
            var allOffsets = res.Allocations.SelectMany(a => a.Placements).Select(p => p.Offset).ToList();
            Assert.True(allOffsets.Any(o => o > 1e-9),
                "At least one placement should have a non-zero offset (second cut on a board)");
        }

        [Fact]
        public async Task Stub_QuantityExpandedBoards_DoNotCollapseAllocations()
        {
            var packer = new DeterministicPackerStub();
            var cl = new CutList();
            cl.Add(new CutItem(40) { Quantity = 3 }); // 3 × 40in = 120in
            var inv = new Inventory();
            inv.Add(new Board(96, 5, quantity: 2)); // 2 × 96in = 192in

            var res = await packer.PackAsync(Req(cl, inv));

            // Stub silently ignores items it cannot place; all 3 should fit here.
            Assert.Equal(2, res.Allocations.Count);
            Assert.Equal(2, res.Allocations.Select(a => a.BoardId).Distinct().Count());
            Assert.Equal(3, res.Allocations.SelectMany(a => a.Placements).Count());
        }

        /// <summary>
        /// DeterministicPackerStub must set TotalWasteLength (it was previously always zero).
        /// </summary>
        [Fact]
        public async Task Stub_TotalWasteLength_IsCorrectlySet()
        {
            var packer = new DeterministicPackerStub();
            var cl = new CutList();
            cl.Add(new CutItem(30) { Quantity = 1 });
            var inv = new Inventory();
            inv.Add(new Board(50, 5, quantity: 1)); // 20in leftover

            var res = await packer.PackAsync(Req(cl, inv));

            Assert.Equal(20.0, res.TotalWasteLength, precision: 9);
            Assert.Equal(30.0, res.TotalUsedLength, precision: 9);
        }

        /// <summary>
        /// When a CutItem has Quantity > 1 each expanded copy must receive a distinct
        /// CutItemId so that individual placements are distinguishable.
        /// </summary>
        [Theory]
        [InlineData("full")]
        [InlineData("stub")]
        public async Task QuantityExpandedCutItems_HaveUniqueIds(string packer)
        {
            IPacker p = packer == "full" ? new FullPacker() : new DeterministicPackerStub();
            var cl = new CutList();
            cl.Add(new CutItem(20) { Quantity = 4 }); // 4 physical copies from one template
            var inv = new Inventory();
            inv.Add(new Board(96, 5, quantity: 1));

            var res = await p.PackAsync(Req(cl, inv));

            var cutIds = res.Allocations.SelectMany(a => a.Placements).Select(p2 => p2.CutItemId).ToList();
            Assert.Equal(4, cutIds.Count);
            Assert.Equal(4, cutIds.Distinct().Count());
        }

        /// <summary>
        /// Allocation remnant lengths must correctly reflect the remaining space on each
        /// physical board, not the first board found by a shared Id lookup.
        ///
        /// Scenario: two 40in cuts on a quantity-2 board of 48in.
        /// Under BFD, the first 40in goes to Board 0 (8in left); the second 40in cannot
        /// fit on Board 0 and is forced onto Board 1 (8in left).  Both physical boards
        /// must appear as separate allocations, each with RemnantLength == 8.
        /// This invariant cannot be verified unless Allocations.Count == 2.
        /// </summary>
        [Fact]
        public async Task FullPacker_QuantityExpandedBoards_RemnantLengthsAreCorrect()
        {
            // Two 40in cuts on two 48in boards (quantity-2 template).
            // BFD: 40in[0] → Board 0 (48-40=8 left); 40in[1] cannot fit on Board 0 (only 8in
            // left) so it must go to Board 1 (48-40=8 left).
            // This guarantees exactly 2 allocations — one per physical board.
            var packer = new FullPacker();
            var cl = new CutList();
            cl.Add(new CutItem(40) { Quantity = 2 });
            var inv = new Inventory();
            inv.Add(new Board(48, 5, quantity: 2));

            var res = await packer.PackAsync(Req(cl, inv));

            Assert.Empty(res.UnplacedItems);
            // Both physical boards must be used — one cut per board.
            Assert.Equal(2, res.Allocations.Count);
            // Each board's remnant must be tracked independently (not collapsed via a shared Id).
            foreach (var a in res.Allocations)
            {
                double used = a.Placements.Sum(p => p.Length);
                double expectedRemnant = a.OriginalBoardLength - used;
                Assert.Equal(expectedRemnant, a.RemnantLength, precision: 9);
                Assert.Equal(8.0, a.RemnantLength, precision: 9);
            }
        }
    }
}
