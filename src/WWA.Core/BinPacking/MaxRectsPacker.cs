using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WWA.Core.Interfaces;
using WWA.Core.Models;

namespace WWA.Core.BinPacking
{
    /// <summary>
    /// Simple MaxRects packer using Best Short Side Fit heuristic.
    /// - Maintains a list of free rectangles per board.
    /// - Chooses placement minimizing short-side leftover, then area waste.
    /// - Deterministic when Seed provided.
    /// </summary>
    public class MaxRectsPacker : IPacker
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

            // sort by area descending
            var sorted = expanded.OrderByDescending(e => e.Item.Length * (e.Item.Width ?? 1.0)).ThenBy(e => e.OriginalIndex).ToList();
            if (rng != null)
            {
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
                    bs.FreeRects.Add(new Rect { X = 0, Y = 0, W = b.Length, H = b.Width });
                    boardStates.Add(bs);
                }
            }

            foreach (var exp in sorted)
            {
                var item = exp.Item;
                BoardState? bestBoard = null;
                Rect? bestRect = null;
                bool bestRot = false;
                double bestShortFit = double.MaxValue;
                double bestAreaWaste = double.MaxValue;

                foreach (var bs in boardStates)
                {
                    boardChecks++;
                    if (!bs.Board.IsUsableFor(item)) continue;

                    foreach (var r in bs.FreeRects)
                    {
                        rectChecks++;

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
                                double leftoverX = r.W - reqL;
                                double leftoverY = r.H - reqW;
                                double shortSide = Math.Min(leftoverX, leftoverY);
                                double areaWaste = (r.W * r.H) - (reqL * (item.Width ?? r.H));

                                if (request.Constraints.PreserveLongRemnants)
                                {
                                    if (leftoverX >= request.Constraints.MinRemnantLength) areaWaste += 1e3;
                                }

                                bool better = false;
                                if (shortSide < bestShortFit - 1e-9) better = true;
                                else if (Math.Abs(shortSide - bestShortFit) <= 1e-9 && areaWaste < bestAreaWaste - 1e-9) better = true;

                                if (better)
                                {
                                    bestShortFit = shortSide;
                                    bestAreaWaste = areaWaste;
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

                // place
                double placeL = exp.Item.Length;
                double placeW = exp.Item.Width ?? bestRect.H;
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

                // split free rects (MaxRects split)
                var newRects = new List<Rect>();
                foreach (var r in bestBoard.FreeRects.ToList())
                {
                    if (!RectIntersects(r, bestRect))
                    {
                        continue;
                    }

                    if (r.X < bestRect.X + placeL && r.X + r.W > bestRect.X)
                    {
                        // top
                        if (r.Y < bestRect.Y + placeW && r.Y + r.H > bestRect.Y)
                        {
                            // split horizontally
                            if (r.Y < bestRect.Y)
                            {
                                newRects.Add(new Rect { X = r.X, Y = r.Y, W = r.W, H = bestRect.Y - r.Y });
                            }
                            if (r.Y + r.H > bestRect.Y + placeW)
                            {
                                newRects.Add(new Rect { X = r.X, Y = bestRect.Y + placeW, W = r.W, H = (r.Y + r.H) - (bestRect.Y + placeW) });
                            }
                        }
                    }

                    if (r.Y < bestRect.Y + placeW && r.Y + r.H > bestRect.Y)
                    {
                        if (r.X < bestRect.X + placeL && r.X + r.W > bestRect.X)
                        {
                            // split vertically
                            if (r.X < bestRect.X)
                            {
                                newRects.Add(new Rect { X = r.X, Y = r.Y, W = bestRect.X - r.X, H = r.H });
                            }
                            if (r.X + r.W > bestRect.X + placeL)
                            {
                                newRects.Add(new Rect { X = bestRect.X + placeL, Y = r.Y, W = (r.X + r.W) - (bestRect.X + placeL), H = r.H });
                            }
                        }
                    }

                    bestBoard.FreeRects.Remove(r);
                }

                // add new rects
                foreach (var nr in newRects)
                {
                    if (nr.W > 1e-9 && nr.H > 1e-9)
                        bestBoard.FreeRects.Add(nr);
                }

                PruneFreeRects(bestBoard);
            }

            sw.Stop();
            placeMs = sw.ElapsedMilliseconds;

            double totalOriginal = 0;
            double totalLeft = 0;
            foreach (var bs in boardStates)
            {
                totalOriginal += bs.OriginalLength;
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

        private bool RectIntersects(Rect a, Rect? b)
        {
            if (b == null) return false;
            return !(a.X >= b.X + b.W || a.X + a.W <= b.X || a.Y >= b.Y + b.H || a.Y + a.H <= b.Y);
        }

        private void PruneFreeRects(BoardState bs)
        {
            var list = bs.FreeRects;
            // remove contained
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var a = list[i];
                for (int j = 0; j < list.Count; j++)
                {
                    if (i == j) continue;
                    var b = list[j];
                    if (RectContains(b, a)) { list.RemoveAt(i); break; }
                }
            }

            // merge adjacent
            const double eps = 1e-9;
            bool mergedAny;
            do
            {
                mergedAny = false;
                for (int i = 0; i < list.Count; i++)
                {
                    var a = list[i];
                    if (a == null) continue;
                    for (int j = i + 1; j < list.Count; j++)
                    {
                        var b = list[j];
                        if (b == null) continue;
                        if (Math.Abs(a.Y - b.Y) < eps && Math.Abs(a.H - b.H) < eps)
                        {
                            if (Math.Abs(a.X + a.W - b.X) < eps) { a.W += b.W; list.RemoveAt(j); mergedAny = true; break; }
                            if (Math.Abs(b.X + b.W - a.X) < eps) { b.W += a.W; list[i] = b; list.RemoveAt(j); mergedAny = true; break; }
                        }
                        if (Math.Abs(a.X - b.X) < eps && Math.Abs(a.W - b.W) < eps)
                        {
                            if (Math.Abs(a.Y + a.H - b.Y) < eps) { a.H += b.H; list.RemoveAt(j); mergedAny = true; break; }
                            if (Math.Abs(b.Y + b.H - a.Y) < eps) { b.H += a.H; list[i] = b; list.RemoveAt(j); mergedAny = true; break; }
                        }
                    }
                    if (mergedAny) break;
                }
            } while (mergedAny);
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
