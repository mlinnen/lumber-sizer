using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WWA.Core.Interfaces;
using WWA.Core.Models;

namespace WWA.Core.BinPacking
{
    /// <summary>
    /// Simple guillotine (free-rectangle) packer.
    /// - For each board, maintains a list of free rectangles.
    /// - Places the next item into the best-fitting free rect (min area waste), then splits the rect by a guillotine cut.
    /// - Deterministic when Seed provided.
    /// </summary>
    public class GuillotinePacker : IPacker
    {
        public Task<PackingResult> PackAsync(PackingRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            request.Constraints ??= new Constraints();
            request.Constraints.Validate();

            Random? rng = null;
            if (request.Seed.HasValue) rng = new Random(request.Seed.Value);

            var result = new PackingResult();
            result.DeterministicSeedUsed = request.Seed;

            // profiling counters
            long placeMs = 0;
            long boardChecks = 0;
            long rectChecks = 0;
            long placements = 0;

            var sw = System.Diagnostics.Stopwatch.StartNew();

            // expand items
            var expanded = new List<Expanded>();
            int idx = 0;
            foreach (var it in request.CutList.Items)
            {
                for (int q = 0; q < Math.Max(1, it.Quantity); q++)
                {
                    expanded.Add(new Expanded { Item = new CutItem(it.Length, it.Width, 1, it.AllowRotated, it.Description) { Id = it.Id }, OriginalIndex = idx++ });
                }
            }

            // sort: largest area first
            var sorted = expanded.OrderByDescending(e => e.Item.Length * (e.Item.Width ?? 1.0)).ThenBy(e => e.OriginalIndex).ToList();
            if (rng != null)
            {
                // small deterministic shuffle within equals
                for (int i = 0; i < sorted.Count; i++)
                {
                    int j = rng.Next(i, sorted.Count);
                    var tmp = sorted[i]; sorted[i] = sorted[j]; sorted[j] = tmp;
                }
            }

            // expand boards
            var boardStates = new List<BoardState>();
            int bidx = 0;
            foreach (var b in request.Inventory.EnumerateAvailable())
            {
                for (int q = 0; q < Math.Max(1, b.Quantity); q++)
                {
                    var bs = new BoardState { Board = b, Index = bidx++, OriginalLength = b.Length };
                    // initial free rect is the full board (length x width)
                    bs.FreeRects.Add(new Rect { X = 0, Y = 0, W = b.Length, H = b.Width });
                    boardStates.Add(bs);
                }
            }

            var remaining = new List<Expanded>(sorted);

            foreach (var exp in sorted)
            {
                var item = exp.Item;
                BoardState? bestBoard = null;
                Rect? bestRect = null;
                bool bestRot = false;
                double bestWaste = double.MaxValue;

                foreach (var bs in boardStates)
                {
                    boardChecks++;
                    if (!bs.Board.IsUsableFor(item)) continue;

                    foreach (var r in bs.FreeRects)
                    {
                        rectChecks++;
                        // try both orientations if allowed
                        var candOrients = new List<(double w, double l, bool rot)>();
                        if (item.Width.HasValue)
                        {
                            candOrients.Add((item.Width.Value, item.Length, false));
                            if (item.AllowRotated) candOrients.Add((item.Length, item.Width.Value, true));
                        }
                        else
                        {
                            candOrients.Add((0.0, item.Length, false));
                        }

                        foreach (var o in candOrients)
                        {
                            double reqW = o.w == 0.0 ? item.Width ?? 0.0 : o.w;
                            double reqL = o.l;

                            if (reqL <= r.W + 1e-9 && (item.Width == null || reqW <= r.H + 1e-9))
                            {
                                // compute waste area
                                double waste = (r.W * r.H) - (reqL * (item.Width ?? r.H));
                                // penalize wastes that leave long remnants if requested
                                if (request.Constraints.PreserveLongRemnants)
                                {
                                    double remAlong = r.W - reqL;
                                    if (remAlong >= request.Constraints.MinRemnantLength) waste += 1e3;
                                }

                                if (waste < bestWaste - 1e-9)
                                {
                                    bestWaste = waste;
                                    bestBoard = bs;
                                    bestRect = r;
                                    bestRot = o.rot;
                                }
                            }
                        }
                    }
                }

                if (bestBoard == null || bestRect == null)
                {
                    result.UnplacedItems.Add(item);
                    continue;
                }

                // place into bestRect, splitting into two rects (guillotine) along the shorter leftover edge
                double placeL = exp.Item.Length;
                double placeW = exp.Item.Width ?? bestRect.H; // flexible uses rect height
                var placed = new Placement2D
                {
                    CutItemId = item.Id,
                    CutItem = item,
                    XOffset = bestRect.X,
                    YOffset = bestRect.Y,
                    Width = placeW,
                    Length = placeL,
                    Rotated = bestRot
                };

                var alloc = result.Allocations.FirstOrDefault(a => a.BoardId == bestBoard.Board.Id);
                if (alloc == null)
                {
                    alloc = new BoardAllocation { BoardId = bestBoard.Board.Id, OriginalBoardLength = bestBoard.OriginalLength };
                    result.Allocations.Add(alloc);
                }
                alloc.Placements2D.Add(placed);
                placements++;

                // perform split
                // remove used rect
                bestBoard.FreeRects.Remove(bestRect);

                double remW = bestRect.W - placeL;
                double remH = bestRect.H - placeW;

                // split along longer leftover to try to keep large rectangles
                if (remW >= remH)
                {
                    // vertical cut: create right rect and bottom rect
                    if (remW > 1e-9)
                    {
                        bestBoard.FreeRects.Add(new Rect { X = bestRect.X + placeL, Y = bestRect.Y, W = remW, H = placeW });
                        bestBoard.FreeRects.Add(new Rect { X = bestRect.X + placeL, Y = bestRect.Y + placeW, W = remW, H = remH });
                    }
                    if (remH > 1e-9)
                    {
                        bestBoard.FreeRects.Add(new Rect { X = bestRect.X, Y = bestRect.Y + placeW, W = placeL, H = remH });
                    }
                }
                else
                {
                    // horizontal cut: create bottom rect and right rect
                    if (remH > 1e-9)
                    {
                        bestBoard.FreeRects.Add(new Rect { X = bestRect.X, Y = bestRect.Y + placeW, W = placeL, H = remH });
                        bestBoard.FreeRects.Add(new Rect { X = bestRect.X + placeL, Y = bestRect.Y + placeW, W = remW, H = remH });
                    }
                    if (remW > 1e-9)
                    {
                        bestBoard.FreeRects.Add(new Rect { X = bestRect.X + placeL, Y = bestRect.Y, W = remW, H = placeW });
                    }
                }

                // normalize and merge simple contained rects
                PruneFreeRects(bestBoard);
            }

            sw.Stop();
            placeMs = sw.ElapsedMilliseconds;

            // compute leftovers and metrics
            double totalOriginal = 0;
            double totalLeft = 0;
            foreach (var bs in boardStates)
            {
                totalOriginal += bs.OriginalLength;
                // estimate used along length by max X+W among placements in this board
                double used = 0;
                var alloc = result.Allocations.FirstOrDefault(a => a.BoardId == bs.Board.Id);
                if (alloc != null && alloc.Placements2D.Any()) used = alloc.Placements2D.Max(p => p.XOffset + p.Length);
                double rem = Math.Max(0.0, bs.OriginalLength - used);
                if (rem > 0) { totalLeft += rem; result.Leftovers.Add(new Board(rem, bs.Board.Width) { Id = bs.Board.Id }); }
                if (alloc != null) alloc.RemnantLength = rem;
            }

            result.TotalUsedLength = totalOriginal - totalLeft;
            result.TotalWasteLength = totalLeft;
            result.WastePercent = totalOriginal <= 0 ? 0 : (totalLeft / totalOriginal) * 100.0;

            // publish counters
            try
            {
                result.Counters["placeMs"] = placeMs;
                result.Counters["boardChecks"] = boardChecks;
                result.Counters["rectChecks"] = rectChecks;
                result.Counters["placements"] = placements;
            }
            catch { }

            return Task.FromResult(result);
        }

        private void PruneFreeRects(BoardState bs)
        {
            // remove rectangles that are contained by others and coalesce obvious overlaps
            var list = bs.FreeRects;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var a = list[i];
                for (int j = 0; j < list.Count; j++)
                {
                    if (i == j) continue;
                    var b = list[j];
                    if (a != null && b != null && RectContains(b, a))
                    {
                        list.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        private bool RectContains(Rect a, Rect b)
        {
            return b.X + 1e-9 >= a.X && b.Y + 1e-9 >= a.Y && b.X + b.W <= a.X + a.W + 1e-9 && b.Y + b.H <= a.Y + a.H + 1e-9;
        }

        private class BoardState
        {
            public Board Board { get; set; } = null!;
            public int Index { get; set; }
            public double OriginalLength { get; set; }
            public List<Rect> FreeRects { get; } = new List<Rect>();
        }

        private class Rect
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double W { get; set; }
            public double H { get; set; }
        }

        private class Expanded
        {
            public CutItem Item { get; set; } = null!;
            public int OriginalIndex { get; set; }
        }
    }
}
