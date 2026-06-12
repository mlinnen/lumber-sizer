using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WWA.Core.Interfaces;
using WWA.Core.Models;

namespace WWA.Core.BinPacking
{
    /// <summary>
    /// Simple deterministic 2D shelf-based packer.
    /// - Places items into horizontal shelves stacked along the board width (Y axis).
    /// - Items placed left-to-right along board length (X axis).
    /// - Deterministic tie-breaking via request.Seed when provided.
    /// This is intentionally small and easy to reason about for M1->M2 transition.
    /// </summary>
    public class TwoDPacker : IPacker
    {
        public Task<PackingResult> PackAsync(PackingRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            request.Constraints ??= new Constraints();
            request.Constraints.Validate();

            Random? rng = null;
            if (request.Seed.HasValue) rng = new Random(request.Seed.Value);

            // Expand boards
            var boardStates = new List<BoardState>();
            int boardIndex = 0;
            foreach (var b in request.Inventory.EnumerateAvailable())
            {
                for (int q = 0; q < Math.Max(1, b.Quantity); q++)
                {
                    boardStates.Add(new BoardState { Board = b, Index = boardIndex++, OriginalLength = b.Length, TotalShelvesHeight = 0.0, MaxUsedAlongLength = 0.0 });
                }
            }

            // Expand items
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

            // Sort by longer side (max of length,width) descending, then by area to prefer large pieces
            var swSort = System.Diagnostics.Stopwatch.StartNew();
            var sorted = expanded
                .OrderByDescending(e => Math.Max(e.Item.Length, e.Item.Width ?? 0.0))
                .ThenByDescending(e => e.Item.Length * (e.Item.Width ?? 1.0))
                .ThenBy(e => e.OriginalIndex)
                .ToList();
            // apply deterministic shuffle within exact-equality spans when rng provided
            if (rng != null)
            {
                for (int i = 0; i < sorted.Count; i++)
                {
                    int j = rng.Next(i, sorted.Count);
                    var tmp = sorted[i]; sorted[i] = sorted[j]; sorted[j] = tmp;
                }
            }
            swSort.Stop();

            var result = new PackingResult();
            result.DeterministicSeedUsed = request.Seed;

            // profiling counters
            long boardChecks = 0;
            long shelfChecks = 0;
            long orientationChecks = 0;
            long newShelfCreates = 0;
            long placementSuccesses = 0;

            var swPlace = System.Diagnostics.Stopwatch.StartNew();

            // Greedy per-board packing: iterate boards and try to place as many remaining items as possible on each board.
            var remaining = new List<ExpandedItem>(sorted);

            foreach (var bs in boardStates)
            {
                // iterate copy since we'll remove placed items
                var toCheck = new List<ExpandedItem>(remaining);
                foreach (var exp in toCheck)
                {
                    var item = exp.Item;

                    double bestRemAfter = double.MaxValue;
                    Shelf? bestShelf = null;
                    bool bestRotated = false;
                    double chosenWidth = 0;
                    double chosenLength = 0;

                    // Consider orientations
                    var orientations = new List<(double w, double l, bool rotated)>();
                    if (item.Width.HasValue)
                    {
                        orientations.Add((item.Width.Value, item.Length, false));
                        if (item.AllowRotated) orientations.Add((item.Length, item.Width.Value, true));
                    }
                    else
                    {
                        orientations.Add((0.0, item.Length, false));
                    }

                    // Quick board usability
                    boardChecks++;
                    if (!bs.Board.IsUsableFor(item)) continue;

                    foreach (var o in orientations)
                    {
                        orientationChecks++;
                        double reqW = o.w;
                        double reqL = o.l;

                        // existing shelves
                        foreach (var sh in bs.Shelves)
                        {
                            shelfChecks++;
                            if (reqW > 0 && reqW > sh.Height + 1e-9) continue;
                            if (sh.RemainingLength + 1e-9 < reqL) continue;

                            double remAfter = sh.RemainingLength - reqL;
                            if (request.Constraints.PreserveLongRemnants && remAfter >= request.Constraints.MinRemnantLength) remAfter += 1e3;

                            if (remAfter < bestRemAfter - 1e-9)
                            {
                                bestRemAfter = remAfter;
                                bestShelf = sh;
                                bestRotated = o.rotated;
                                chosenWidth = Math.Max(reqW, sh.Height);
                                chosenLength = reqL;
                            }
                        }

                        // try new shelf
                        double usedHeight = bs.TotalShelvesHeight;
                        if (reqW == 0.0)
                        {
                            double desired = item.Width ?? 6.0;
                            reqW = Math.Min(bs.Board.Width - usedHeight, Math.Max(desired, 6.0));
                        }

                        if (reqW <= bs.Board.Width - usedHeight + 1e-9 && bs.Board.Length + 1e-9 >= reqL)
                        {
                            double remAfter = bs.Board.Length - reqL;
                            if (request.Constraints.PreserveLongRemnants && remAfter >= request.Constraints.MinRemnantLength) remAfter += 1e3;

                            if (remAfter < bestRemAfter - 1e-9)
                            {
                                bestRemAfter = remAfter;
                                bestShelf = null; // indicate new shelf
                                bestRotated = o.rotated;
                                chosenWidth = reqW;
                                chosenLength = reqL;
                            }
                        }
                    }

                    if (bestShelf == null && bestRemAfter == double.MaxValue)
                    {
                        // couldn't place on this board
                        continue;
                    }

                    // place on bs
                    Shelf targetShelf;
                    if (bestShelf == null)
                    {
                        targetShelf = bs.CreateShelf(chosenWidth);
                        newShelfCreates++;
                    }
                    else
                    {
                        targetShelf = bestShelf;
                    }

                    double xOffset = targetShelf.XOffset;
                    double yOffset = targetShelf.YOffset;

                    targetShelf.XOffset += chosenLength;
                    targetShelf.RemainingLength = Math.Max(0.0, targetShelf.RemainingLength - chosenLength);
                    bs.MaxUsedAlongLength = Math.Max(bs.MaxUsedAlongLength, targetShelf.XOffset);

                    var alloc = result.Allocations.FirstOrDefault(a => a.BoardId == bs.Board.Id);
                    if (alloc == null)
                    {
                        alloc = new BoardAllocation { BoardId = bs.Board.Id, OriginalBoardLength = bs.OriginalLength };
                        result.Allocations.Add(alloc);
                    }

                    alloc.Placements2D.Add(new Placement2D
                    {
                        CutItemId = item.Id,
                        CutItem = item,
                        XOffset = xOffset,
                        YOffset = yOffset,
                        Width = chosenWidth,
                        Length = chosenLength,
                        Rotated = bestRotated
                    });

                    placementSuccesses++;
                    // remove from remaining
                    remaining.Remove(exp);
                }

                if (remaining.Count == 0) break;
            }

            // any leftover items are unplaced
            foreach (var exp in remaining) result.UnplacedItems.Add(exp.Item);

            // Build leftovers and metrics
            double totalOriginal = 0;
            double totalLeftover = 0;
            foreach (var bs in boardStates)
            {
                totalOriginal += bs.OriginalLength;
                double usedAlongLength = 0;
                if (bs.Shelves.Any()) usedAlongLength = bs.Shelves.Max(s => s.XOffset);
                double rem = Math.Max(0.0, bs.OriginalLength - usedAlongLength);
                if (rem > 0)
                {
                    totalLeftover += rem;
                    var remBoard = new Board(rem, bs.Board.Width, bs.Board.Thickness, bs.Board.Grade, 1) { Id = bs.Board.Id };
                    result.Leftovers.Add(remBoard);
                }

                var alloc = result.Allocations.FirstOrDefault(a => a.BoardId == bs.Board.Id);
                if (alloc != null) alloc.RemnantLength = Math.Max(0.0, rem);
            }

            result.TotalUsedLength = totalOriginal - totalLeftover;
            result.TotalWasteLength = totalLeftover;
            result.WastePercent = totalOriginal <= 0 ? 0 : (totalLeftover / totalOriginal) * 100.0;

            swPlace.Stop();
            try { Console.WriteLine($"TwoDPacker: sortMs={swSort.ElapsedMilliseconds}, placeMs={swPlace.ElapsedMilliseconds}, boards={boardChecks}, shelvesChecked={shelfChecks}, orientations={orientationChecks}, newShelves={newShelfCreates}, placements={placementSuccesses}"); } catch { }

            // publish counters in result for programmatic inspection
            try
            {
                result.Counters["sortMs"] = swSort.ElapsedMilliseconds;
                result.Counters["placeMs"] = swPlace.ElapsedMilliseconds;
                result.Counters["boardChecks"] = boardChecks;
                result.Counters["shelfChecks"] = shelfChecks;
                result.Counters["orientationChecks"] = orientationChecks;
                result.Counters["newShelves"] = newShelfCreates;
                result.Counters["placements"] = placementSuccesses;
            }
            catch { }

            return Task.FromResult(result);
        }

        private class BoardState
        {
            public Board Board { get; set; } = null!;
            public int Index { get; set; }
            public double OriginalLength { get; set; }
            public List<Shelf> Shelves { get; } = new List<Shelf>();
            // cached totals to avoid repeated LINQ sums
            public double TotalShelvesHeight { get; set; }
            public double MaxUsedAlongLength { get; set; }

            public Shelf CreateShelf(double height)
            {
                double y = TotalShelvesHeight;
                var sh = new Shelf { Height = height, YOffset = y, XOffset = 0, RemainingLength = Board.Length };
                Shelves.Add(sh);
                TotalShelvesHeight += height;
                return sh;
            }
        }

        private class Shelf
        {
            public double Height { get; set; }
            public double YOffset { get; set; }
            public double XOffset { get; set; }
            public double RemainingLength { get; set; }
        }

        private class ExpandedItem
        {
            public CutItem Item { get; set; } = null!;
            public int OriginalIndex { get; set; }
        }
    }
}
