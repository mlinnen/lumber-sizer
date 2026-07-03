using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WWA.Core.Interfaces;
using WWA.Core.Models;

namespace WWA.Core.BinPacking
{
    /// <summary>
    /// Deterministic 1D packer supporting multiple strategies via <see cref="PackingStrategy"/>.
    ///
    /// <list type="bullet">
    ///   <item><term>BestFitDecreasing (default)</term><description>Sort items longest-first; place each on the board with the smallest remaining space that still fits.</description></item>
    ///   <item><term>FirstFitDecreasing</term><description>Sort items longest-first; place each on the first board with enough remaining space.</description></item>
    ///   <item><term>FirstFit</term><description>Preserve original item order; place each on the first board with enough remaining space.</description></item>
    /// </list>
    ///
    /// Determinism guarantees:
    /// <list type="bullet">
    ///   <item>Without a seed: order is fully determined by input order and stable sorts — identical inputs always produce identical outputs.</item>
    ///   <item>With a seed: tie-groups are shuffled with a seeded <see cref="Random"/> (Fisher-Yates), and board ties are resolved via the seeded RNG. Results are bit-exact across runs and platforms for the same seed and inputs.</item>
    /// </list>
    /// </summary>
    public class FullPacker : IPacker
    {
        public Task<PackingResult> PackAsync(PackingRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            request.Constraints ??= new Constraints();
            request.Constraints.Validate();

            Random? rng = request.Seed.HasValue ? new Random(request.Seed.Value) : null;

            // Expand inventory into one board state per physical board.
            // Each physical copy gets a deterministic unique Id derived from the template Id
            // and copy index so that allocations, remnants, and leftovers are never collapsed
            // across boards that share the same template.
            var boardStates = new List<BoardState>();
            int boardIndex = 0;
            foreach (var b in request.Inventory.EnumerateAvailable())
            {
                int copyCount = Math.Max(1, b.Quantity);
                for (int q = 0; q < copyCount; q++)
                {
                    boardStates.Add(new BoardState
                    {
                        Board = b,
                        PhysicalBoardId = DeriveId(b.Id, q),
                        Remaining = b.Length,
                        OriginalLength = b.Length,
                        Index = boardIndex++
                    });
                }
            }

            // Expand cut items by quantity, preserving original order for determinism.
            // Each physical copy gets a deterministic unique Id so that placements are
            // distinguishable even when multiple copies come from the same template item.
            var expanded = new List<ExpandedItem>();
            int itemIndex = 0;
            foreach (var it in request.CutList.Items)
            {
                for (int i = 0; i < Math.Max(1, it.Quantity); i++)
                {
                    var copy = new CutItem(it.Length, it.Width, 1, it.AllowRotated, it.Description) { Id = DeriveId(it.Id, i) };
                    expanded.Add(new ExpandedItem { Item = copy, OriginalIndex = itemIndex++ });
                }
            }

            // Order items according to the requested strategy.
            var ordered = request.Strategy == PackingStrategy.FirstFit
                ? OrderByOriginalIndex(expanded)
                : OrderByDecreasingLength(expanded, rng);

            var result = new PackingResult();
            result.DeterministicSeedUsed = request.Seed;

            foreach (var exp in ordered)
            {
                var item = exp.Item;

                var candidates = boardStates
                    .Where(bs => bs.Remaining + 1e-9 >= item.Length && bs.Board.IsUsableFor(item))
                    .ToList();

                if (!candidates.Any())
                {
                    result.UnplacedItems.Add(item);
                    continue;
                }

                // Apply remnant-preservation preference when requested.
                List<BoardState> preferred = ApplyRemnantPreference(candidates, item.Length, request.Constraints);

                BoardState chosen = request.Strategy == PackingStrategy.BestFitDecreasing
                    ? SelectBestFit(preferred, item.Length, rng)
                    : SelectFirstFit(preferred);

                PlaceItem(result, boardStates, chosen, item);
            }

            FinalizeResult(result, boardStates);
            return Task.FromResult(result);
        }

        // ── Ordering helpers ──────────────────────────────────────────────────────

        private static IEnumerable<ExpandedItem> OrderByOriginalIndex(List<ExpandedItem> items)
            => items.OrderBy(e => e.OriginalIndex);

        private static IEnumerable<ExpandedItem> OrderByDecreasingLength(List<ExpandedItem> items, Random? rng)
        {
            var groups = items
                .GroupBy(e => Math.Round(e.Item.Length, 6))
                .OrderByDescending(g => g.Key);

            var sorted = new List<ExpandedItem>();
            foreach (var g in groups)
            {
                var list = g.ToList();
                if (rng != null && list.Count > 1)
                {
                    // Deterministic Fisher-Yates shuffle within tie-group using seeded RNG.
                    for (int i = list.Count - 1; i > 0; i--)
                    {
                        int j = rng.Next(i + 1);
                        (list[i], list[j]) = (list[j], list[i]);
                    }
                }
                else
                {
                    list = list.OrderBy(x => x.OriginalIndex).ToList();
                }
                sorted.AddRange(list);
            }
            return sorted;
        }

        // ── Candidate selection ───────────────────────────────────────────────────

        private static List<BoardState> ApplyRemnantPreference(List<BoardState> candidates, double itemLength, Constraints constraints)
        {
            if (!constraints.PreserveLongRemnants) return candidates;

            // Prefer boards where the cut would leave a remnant shorter than MinRemnantLength
            // (i.e., don't create a large remnant by cutting into a long board).
            var pref = candidates
                .Where(bs => (bs.Remaining - itemLength) < constraints.MinRemnantLength)
                .ToList();
            return pref.Any() ? pref : candidates;
        }

        private static BoardState SelectFirstFit(List<BoardState> candidates)
            => candidates.OrderBy(b => b.Index).First();

        private static BoardState SelectBestFit(List<BoardState> candidates, double itemLength, Random? rng)
        {
            double bestRemAfter = double.MaxValue;
            var bestList = new List<BoardState>();

            foreach (var bs in candidates)
            {
                double remAfter = bs.Remaining - itemLength;
                if (remAfter < bestRemAfter - 1e-9)
                {
                    bestRemAfter = remAfter;
                    bestList.Clear();
                    bestList.Add(bs);
                }
                else if (Math.Abs(remAfter - bestRemAfter) <= 1e-9)
                {
                    bestList.Add(bs);
                }
            }

            if (bestList.Count == 1) return bestList[0];

            // Deterministic tie-break: seed-driven random when available, otherwise lowest board index.
            return rng != null
                ? bestList[rng.Next(bestList.Count)]
                : bestList.OrderBy(b => b.Index).First();
        }

        // ── Placement ─────────────────────────────────────────────────────────────

        private static void PlaceItem(PackingResult result, List<BoardState> allBoardStates, BoardState chosen, CutItem item)
        {
            var allocation = result.Allocations.FirstOrDefault(a => a.BoardId == chosen.PhysicalBoardId);
            if (allocation == null)
            {
                allocation = new BoardAllocation
                {
                    BoardId = chosen.PhysicalBoardId,
                    OriginalBoardLength = chosen.OriginalLength,
                    OriginalBoardWidth = chosen.Board.Width
                };
                result.Allocations.Add(allocation);
            }

            var offset = chosen.OriginalLength - chosen.Remaining;
            allocation.Placements.Add(new Placement
            {
                CutItemId = item.Id,
                CutItem = item,
                Offset = offset,
                Rotated = false,
                Length = item.Length
            });
            chosen.Remaining -= item.Length;
        }

        private static void FinalizeResult(PackingResult result, List<BoardState> boardStates)
        {
            double totalOriginal = 0;
            double totalLeftover = 0;
            foreach (var bs in boardStates)
            {
                totalOriginal += bs.OriginalLength;
                if (bs.Remaining > 0)
                {
                    totalLeftover += bs.Remaining;
                    result.Leftovers.Add(new Board(bs.Remaining, bs.Board.Width, bs.Board.Thickness, bs.Board.Grade, 1) { Id = bs.PhysicalBoardId });
                }
            }

            result.TotalUsedLength = totalOriginal - totalLeftover;
            result.TotalWasteLength = totalLeftover;
            result.WastePercent = totalOriginal <= 0 ? 0 : (totalLeftover / totalOriginal) * 100.0;

            foreach (var a in result.Allocations)
            {
                var bs = boardStates.FirstOrDefault(b => b.PhysicalBoardId == a.BoardId);
                if (bs != null) a.RemnantLength = Math.Max(0, bs.Remaining);
            }
        }

        private class BoardState
        {
            public Board Board { get; set; } = null!;
            public Guid PhysicalBoardId { get; set; }
            public double Remaining { get; set; }
            public double OriginalLength { get; set; }
            public int Index { get; set; }
        }

        private class ExpandedItem
        {
            public CutItem Item { get; set; } = null!;
            public int OriginalIndex { get; set; }
        }

        /// <summary>
        /// Derives a deterministic, unique Guid for the nth physical copy of a template entity.
        /// copyIndex=0 returns the template Id unchanged (preserving single-instance identity).
        /// </summary>
        private static Guid DeriveId(Guid templateId, int copyIndex)
        {
            if (copyIndex == 0) return templateId;
            var bytes = templateId.ToByteArray();
            bytes[12] ^= (byte)(copyIndex & 0xFF);
            bytes[13] ^= (byte)((copyIndex >> 8) & 0xFF);
            bytes[14] ^= (byte)((copyIndex >> 16) & 0xFF);
            bytes[15] ^= (byte)((copyIndex >> 24) & 0xFF);
            return new Guid(bytes);
        }
    }
}
