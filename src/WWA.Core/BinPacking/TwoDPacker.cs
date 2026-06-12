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

            // Sort by longer side descending to prefer large pieces
            var groups = expanded.GroupBy(e => Math.Round(Math.Max(e.Item.Length, e.Item.Width ?? 0), 6)).OrderByDescending(g => g.Key);
            var sorted = new List<ExpandedItem>();
            foreach (var g in groups)
            {
                var list = g.ToList();
                if (rng != null && list.Count > 1)
                {
                    for (int i = list.Count - 1; i > 0; i--)
                    {
                        int j = rng.Next(i + 1);
                        var tmp = list[i]; list[i] = list[j]; list[j] = tmp;
                    }
                }
                else
                {
                    list = list.OrderBy(x => x.OriginalIndex).ToList();
                }
                sorted.AddRange(list);
            }

            var result = new PackingResult();
            result.DeterministicSeedUsed = request.Seed;

            foreach (var exp in sorted)
            {
                var item = exp.Item;

                double bestRemAfter = double.MaxValue;
                BoardState? bestBoard = null;
                Shelf? bestShelf = null;
                bool bestRotated = false;
                double chosenWidth = 0;
                double chosenLength = 0;

                // Consider both orientations (if allowed)
                var orientations = new List<(double w, double l, bool rotated)>();
                if (item.Width.HasValue)
                {
                    orientations.Add((item.Width.Value, item.Length, false));
                    if (item.AllowRotated) orientations.Add((item.Length, item.Width.Value, true));
                }
                else
                {
                    // width not specified: treat as flexible, try no-rotation only (length stays)
                    orientations.Add((0.0, item.Length, false));
                }

                foreach (var bs in boardStates)
                {
                    // quick board usability check
                    if (!bs.Board.IsUsableFor(item)) continue;

                    foreach (var o in orientations)
                    {
                        double reqW = o.w; // 0 means flexible
                        double reqL = o.l;

                        // Try existing shelves
                        foreach (var sh in bs.Shelves)
                        {
                            // shelf height must accomodate item width (or width unspecified)
                            if (reqW > 0 && reqW > sh.Height + 1e-9) continue;
                            if (sh.RemainingLength + 1e-9 < reqL) continue;

                            double remAfter = sh.RemainingLength - reqL;
                            if (request.Constraints.PreserveLongRemnants)
                            {
                                // prefer leaving small remnant (< MinRemnantLength)
                                if (remAfter >= request.Constraints.MinRemnantLength)
                                {
                                    // penalize by adding a small epsilon to remAfter to deprioritize
                                    remAfter += 1e3;
                                }
                            }

                            if (remAfter < bestRemAfter - 1e-9)
                            {
                                bestRemAfter = remAfter;
                                bestBoard = bs;
                                bestShelf = sh;
                                bestRotated = o.rotated;
                                chosenWidth = Math.Max(reqW, sh.Height);
                                chosenLength = reqL;
                            }
                            else if (Math.Abs(remAfter - bestRemAfter) <= 1e-9)
                            {
                                // tie: deterministic choice between boards/shelves
                                // choose lower board index unless rng specified
                                if (bestBoard != null)
                                {
                                    if (rng != null)
                                    {
                                        if (rng.Next(2) == 0)
                                        {
                                            bestBoard = bs; bestShelf = sh; bestRotated = o.rotated; chosenWidth = Math.Max(reqW, sh.Height); chosenLength = reqL;
                                        }
                                    }
                                    else
                                    {
                                        if (bs.Index < bestBoard.Index)
                                        {
                                            bestBoard = bs; bestShelf = sh; bestRotated = o.rotated; chosenWidth = Math.Max(reqW, sh.Height); chosenLength = reqL;
                                        }
                                    }
                                }
                            }
                        }

                                // Try creating a new shelf at bottom
                                double usedHeight = bs.TotalShelvesHeight;
                        if (reqW == 0.0)
                        {
                            // flexible width: create a full-height shelf at Y=usedHeight with height board.Width-usedHeight
                            reqW = bs.Board.Width - usedHeight;
                        }

                        if (reqW <= bs.Board.Width - usedHeight + 1e-9 && bs.Board.Length + 1e-9 >= reqL)
                        {
                            double remAfter = bs.Board.Length - reqL;
                            if (request.Constraints.PreserveLongRemnants)
                            {
                                if (remAfter >= request.Constraints.MinRemnantLength) remAfter += 1e3;
                            }

                            if (remAfter < bestRemAfter - 1e-9)
                            {
                                bestRemAfter = remAfter;
                                bestBoard = bs;
                                bestShelf = null; // indicate new shelf
                                bestRotated = o.rotated;
                                chosenWidth = reqW;
                                chosenLength = reqL;
                            }
                            else if (Math.Abs(remAfter - bestRemAfter) <= 1e-9)
                            {
                                if (bestBoard != null)
                                {
                                    if (rng != null)
                                    {
                                        if (rng.Next(2) == 0)
                                        {
                                            bestBoard = bs; bestShelf = null; bestRotated = o.rotated; chosenWidth = reqW; chosenLength = reqL;
                                        }
                                    }
                                    else
                                    {
                                        if (bs.Index < bestBoard.Index)
                                        {
                                            bestBoard = bs; bestShelf = null; bestRotated = o.rotated; chosenWidth = reqW; chosenLength = reqL;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (bestBoard == null)
                {
                    // no fit
                    result.UnplacedItems.Add(item);
                    continue;
                }

                // perform placement on bestBoard/bestShelf
                Shelf targetShelf = bestShelf ?? bestBoard.CreateShelf(chosenWidth);
                double xOffset = targetShelf.XOffset;
                double yOffset = targetShelf.YOffset;

                targetShelf.XOffset += chosenLength;
                targetShelf.RemainingLength = Math.Max(0.0, targetShelf.RemainingLength - chosenLength);
                // update board-level cached metrics
                bestBoard.MaxUsedAlongLength = Math.Max(bestBoard.MaxUsedAlongLength, targetShelf.XOffset);

                // record allocation
                var alloc = result.Allocations.FirstOrDefault(a => a.BoardId == bestBoard.Board.Id);
                if (alloc == null)
                {
                    alloc = new BoardAllocation { BoardId = bestBoard.Board.Id, OriginalBoardLength = bestBoard.OriginalLength };
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
            }

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
