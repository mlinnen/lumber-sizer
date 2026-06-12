using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WWA.Core.Interfaces;
using WWA.Core.Models;

namespace WWA.Core.BinPacking
{
    /// <summary>
    /// Minimal deterministic greedy first-fit packer used for tests and prototyping.
    /// - Expands cut items by Quantity and attempts to place each on the first board with enough remaining length.
    /// - Uses provided seed to shuffle cut items order deterministically when seed is set.
    /// This is intentionally simple and not optimized for production use.
    /// </summary>
    public class DeterministicPackerStub : IPacker
    {
        public Task<PackingResult> PackAsync(PackingRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            // Clone inventory boards into mutable states
            var boardStates = request.Inventory.EnumerateAvailable()
                .SelectMany(b => Enumerable.Repeat(b, b.Quantity))
                .Select(b => new BoardState { Board = b, Remaining = b.Length, OriginalLength = b.Length })
                .ToList();

            // Expand cut items by quantity
            var expanded = new List<CutItem>();
            foreach (var it in request.CutList.Items)
            {
                for (int i = 0; i < Math.Max(1, it.Quantity); i++)
                {
                    expanded.Add(new CutItem(it.Length, it.Width, 1, it.AllowRotated, it.Description) { Id = it.Id });
                }
            }

            // Deterministic shuffle if seed provided
            if (request.Seed.HasValue)
            {
                var rnd = new Random(request.Seed.Value);
                // Fisher-Yates
                for (int i = expanded.Count - 1; i > 0; i--)
                {
                    int j = rnd.Next(i + 1);
                    var tmp = expanded[i]; expanded[i] = expanded[j]; expanded[j] = tmp;
                }
            }

            var result = new PackingResult();
            result.DeterministicSeedUsed = request.Seed;

            foreach (var item in expanded)
            {
                // find first board that can fit the item
                var idx = boardStates.FindIndex(bs => bs.Remaining + 1e-9 >= item.Length && bs.Board.IsUsableFor(item));
                if (idx >= 0)
                {
                    var bs = boardStates[idx];
                    var allocation = result.Allocations.FirstOrDefault(a => a.BoardId == bs.Board.Id);
                    if (allocation == null)
                    {
                        allocation = new BoardAllocation { BoardId = bs.Board.Id, OriginalBoardLength = bs.OriginalLength };
                        result.Allocations.Add(allocation);
                    }

                    var offset = bs.OriginalLength - bs.Remaining;
                    allocation.Placements.Add(new Placement { CutItemId = item.Id, CutItem = item, Offset = offset, Rotated = false, Length = item.Length });
                    bs.Remaining -= item.Length;
                }
                else
                {
                    // no board could fit this item; it becomes leftover as an unfulfilled request (not modeled here)
                    // For this stub, we ignore unplaced items; real implementations should report them.
                }
            }

            // Build remnants/leftovers from boardStates
            double totalOriginal = 0;
            double totalLeftover = 0;
            foreach (var bs in boardStates)
            {
                totalOriginal += bs.OriginalLength;
                if (bs.Remaining > 0)
                {
                    var rem = bs.Remaining;
                    totalLeftover += rem;
                    var leftoverBoard = new Board(rem, bs.Board.Width, bs.Board.Thickness, bs.Board.Grade, 1);
                    result.Leftovers.Add(leftoverBoard);
                }
            }

            result.TotalUsedLength = totalOriginal - totalLeftover;
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
        }
    }
}