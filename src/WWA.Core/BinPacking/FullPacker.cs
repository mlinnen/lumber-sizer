using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WWA.Core.Interfaces;
using WWA.Core.Models;

namespace WWA.Core.BinPacking
{
    /// <summary>
    /// Deterministic 1D Best-Fit Decreasing (BFD) packer.
    /// - Sorts items by length descending (ties deterministically broken by seed when provided).
    /// - For each item, places it on the board with the smallest remaining length that still fits (best-fit).
    /// - If PreserveLongRemnants is true, prefers placements that leave a remnant >= MinRemnantLength.
    /// - Deterministic when request.Seed is provided.
    /// </summary>
    public class FullPacker : IPacker
    {
        public Task<PackingResult> PackAsync(PackingRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            request.Constraints ??= new Constraints();
            request.Constraints.Validate();

            // Prepare RNG for any tie-breaking only when seed provided
            Random? rng = null;
            if (request.Seed.HasValue) rng = new Random(request.Seed.Value);

            // Expand inventory into board states (one entry per physical board)
            var boardStates = new List<BoardState>();
            int boardIndex = 0;
            foreach (var b in request.Inventory.EnumerateAvailable())
            {
                for (int q = 0; q < Math.Max(1, b.Quantity); q++)
                {
                    boardStates.Add(new BoardState { Board = b, Remaining = b.Length, OriginalLength = b.Length, Index = boardIndex++ });
                }
            }

            // Expand cut items by quantity
            var expanded = new List<ExpandedItem>();
            int itemIndex = 0;
            foreach (var it in request.CutList.Items)
            {
                for (int i = 0; i < Math.Max(1, it.Quantity); i++)
                {
                    var copy = new CutItem(it.Length, it.Width, 1, it.AllowRotated, it.Description) { Id = it.Id };
                    expanded.Add(new ExpandedItem { Item = copy, OriginalIndex = itemIndex++ });
                }
            }

            // Sort items: Best-Fit Decreasing -> length desc. Ties: stable by OriginalIndex, but if seed provided, shuffle ties deterministically.
            var groups = expanded.GroupBy(e => Math.Round(e.Item.Length, 6)).OrderByDescending(g => g.Key);
            var sorted = new List<ExpandedItem>();
            foreach (var g in groups)
            {
                var list = g.ToList();
                if (rng != null && list.Count > 1)
                {
                    // deterministic shuffle within tie-group
                    for (int i = list.Count - 1; i > 0; i--)
                    {
                        int j = rng.Next(i + 1);
                        var tmp = list[i]; list[i] = list[j]; list[j] = tmp;
                    }
                }
                else
                {
                    // preserve original order
                    list = list.OrderBy(x => x.OriginalIndex).ToList();
                }

                sorted.AddRange(list);
            }

            var result = new PackingResult();
            result.DeterministicSeedUsed = request.Seed;

            foreach (var exp in sorted)
            {
                var item = exp.Item;
                // Find candidate boards that can host this item
                var candidates = boardStates.Where(bs => bs.Remaining + 1e-9 >= item.Length && bs.Board.IsUsableFor(item)).ToList();
                if (!candidates.Any())
                {
                    result.UnplacedItems.Add(item);
                    continue;
                }

                // If PreserveLongRemnants is requested, prefer candidates that leave remnant >= MinRemnantLength
                List<BoardState> preferred = candidates;
                if (request.Constraints.PreserveLongRemnants)
                {
                    // Prefer placements that consume boards below MinRemnantLength (avoid creating long preserved remnants)
                    var pref = candidates.Where(bs => (bs.Remaining - item.Length) < request.Constraints.MinRemnantLength).ToList();
                    if (pref.Any()) preferred = pref;
                }

                // Best-fit: choose candidate with minimal remaining after placement
                double bestRemAfter = double.MaxValue;
                var bestList = new List<BoardState>();
                foreach (var bs in preferred)
                {
                    double remAfter = bs.Remaining - item.Length;
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

                BoardState chosen;
                if (bestList.Count == 1)
                {
                    chosen = bestList[0];
                }
                else
                {
                    // deterministic tie-break: use board Index order unless rng provided, then randomly choose among equals using rng
                    if (rng != null)
                    {
                        int pick = rng.Next(bestList.Count);
                        chosen = bestList[pick];
                    }
                    else
                    {
                        chosen = bestList.OrderBy(b => b.Index).First();
                    }
                }

                // Place on chosen board
                var allocation = result.Allocations.FirstOrDefault(a => a.BoardId == chosen.Board.Id);
                if (allocation == null)
                {
                    allocation = new BoardAllocation { BoardId = chosen.Board.Id, OriginalBoardLength = chosen.OriginalLength };
                    result.Allocations.Add(allocation);
                }

                var offset = chosen.OriginalLength - chosen.Remaining;
                allocation.Placements.Add(new Placement { CutItemId = item.Id, CutItem = item, Offset = offset, Rotated = false, Length = item.Length });
                chosen.Remaining -= item.Length;
            }

            // Build leftovers/remnants
            double totalOriginal = 0;
            double totalLeftover = 0;
            foreach (var bs in boardStates)
            {
                totalOriginal += bs.OriginalLength;
                if (bs.Remaining > 0)
                {
                    totalLeftover += bs.Remaining;
                    var remBoard = new Board(bs.Remaining, bs.Board.Width, bs.Board.Thickness, bs.Board.Grade, 1) { Id = bs.Board.Id };
                    result.Leftovers.Add(remBoard);
                }
            }

            result.TotalUsedLength = totalOriginal - totalLeftover;
            result.TotalWasteLength = totalLeftover;
            result.WastePercent = totalOriginal <= 0 ? 0 : (totalLeftover / totalOriginal) * 100.0;

            // Fill remnant lengths into allocations
            foreach (var a in result.Allocations)
            {
                var bs = boardStates.FirstOrDefault(b => b.Board.Id == a.BoardId);
                if (bs != null) a.RemnantLength = Math.Max(0, bs.Remaining);
            }

            return Task.FromResult(result);
        }

        private class BoardState
        {
            public Board Board { get; set; } = null!;
            public double Remaining { get; set; }
            public double OriginalLength { get; set; }
            public int Index { get; set; }
        }

        private class ExpandedItem
        {
            public CutItem Item { get; set; } = null!;
            public int OriginalIndex { get; set; }
        }
    }
}
