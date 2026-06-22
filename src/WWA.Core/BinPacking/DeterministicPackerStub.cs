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

            // Expand inventory boards: each physical copy gets a deterministic unique Id so
            // that allocations never collapse across quantity-expanded copies of the same board.
            var boardStates = new List<BoardState>();
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
                        OriginalLength = b.Length
                    });
                }
            }

            // Expand cut items by quantity; each copy gets a deterministic unique Id so that
            // multi-quantity placements are distinguishable in the result.
            var expanded = new List<CutItem>();
            foreach (var it in request.CutList.Items)
            {
                for (int i = 0; i < Math.Max(1, it.Quantity); i++)
                {
                    expanded.Add(new CutItem(it.Length, it.Width, 1, it.AllowRotated, it.Description) { Id = DeriveId(it.Id, i) });
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
                    var allocation = result.Allocations.FirstOrDefault(a => a.BoardId == bs.PhysicalBoardId);
                    if (allocation == null)
                    {
                        allocation = new BoardAllocation { BoardId = bs.PhysicalBoardId, OriginalBoardLength = bs.OriginalLength };
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
                    var leftoverBoard = new Board(rem, bs.Board.Width, bs.Board.Thickness, bs.Board.Grade, 1)
                        { Id = bs.PhysicalBoardId };
                    result.Leftovers.Add(leftoverBoard);
                }
            }

            result.TotalUsedLength = totalOriginal - totalLeftover;
            result.TotalWasteLength = totalLeftover;
            result.WastePercent = totalOriginal <= 0 ? 0 : (totalLeftover / totalOriginal) * 100.0;

            // Fill remnant lengths into allocations
            foreach (var a in result.Allocations)
            {
                var bs = boardStates.FirstOrDefault(b => b.PhysicalBoardId == a.BoardId);
                if (bs != null) a.RemnantLength = Math.Max(0, bs.Remaining);
            }

            return Task.FromResult(result);
        }

        private class BoardState
        {
            public Board Board { get; set; } = null!;
            public Guid PhysicalBoardId { get; set; }
            public double Remaining { get; set; }
            public double OriginalLength { get; set; }
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