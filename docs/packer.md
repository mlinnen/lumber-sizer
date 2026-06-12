FullPacker (1D) - Best-Fit Decreasing (BFD)

Overview

The FullPacker implements a deterministic Best-Fit Decreasing (BFD) 1-dimensional bin-packing algorithm designed for Milestone 1. It packs cut items by length into available boards and returns a PackingResult with allocations, remnants, and metrics.

Key behaviors

- Sorting: Items are sorted by length (descending). Ties are stable but may be deterministically shuffled when a seed is provided.
- Placement: For each item, choose the board that will have the smallest remaining length after placement (best-fit).
- Determinism: When a PackingRequest.Seed is provided, the packer uses a seeded RNG for tie-breaking. The PackingResult.DeterministicSeedUsed records the seed used.
- Remnant preservation: When Constraints.PreserveLongRemnants == true and Constraints.MinRemnantLength > 0, the algorithm prefers placements that consume boards below the MinRemnantLength (i.e., avoid creating long preserved remnants) when possible.

Public API

Example usage:

var packer = new FullPacker();
var req = new PackingRequest { CutList = cutList, Inventory = inventory, Constraints = new Constraints { MinRemnantLength = 48, PreserveLongRemnants = true }, Seed = 12345 };
var result = await packer.PackAsync(req);

Result contents

- Allocations: List of BoardAllocation with Placements (CutItemId, Offset, Length).
- Leftovers: List of Board objects representing remnant pieces (Id corresponds to original board id for traceability).
- UnplacedItems: Items that could not be assigned to any board.
- Metrics: TotalBoardsUsed, TotalUsedLength, TotalWasteLength, WastePercent, DeterministicSeedUsed.

Complexity

- Time: O(n log n + n*m) in the worst case (n = total pieces, m = total board instances). Sorting dominates; each placement scans candidate boards.
- Space: O(n + m) for expanded item and board state lists.

Acceptance criteria (Milestone 1)

- Deterministic behavior when seed supplied: same input + same seed -> identical PackingResult JSON.
- Respects Constraints.MinRemnantLength and PreserveLongRemnants.
- Handles items larger than any board by reporting them in UnplacedItems.

Next steps

- Add 2D packing (width/rotation considerations), more advanced heuristics (e.g., simulated annealing), and optional ILP-based optimal solver for small inputs.

2D packer (TwoDPacker)

- Algorithm: a deterministic shelf-based guillotine-friendly heuristic. Items are placed left-to-right in horizontal shelves stacked along the board width. This is simple, deterministic, and fast for the M1->M2 bridge.
- Orientation: CutItem.AllowRotated is honored; the packer will attempt both orientations where allowed.
- Determinism: PackingRequest.Seed controls tie-breaking (stable shuffle within tie-groups and random selection among equal candidates). PackingResult.DeterministicSeedUsed records the seed used.
- Constraints: Constraints.MinRemnantLength applies to leftover lengths along the length axis; when PreserveLongRemnants is true the packer prefers placements that avoid creating long preserved remnants.
- Output: PackingResult.Allocations[].Placements2D provides placements with XOffset, YOffset, Width, Length and Rotated flag. UnplacedItems lists items that could not fit.

Notes: TwoDPacker is intentionally conservative in scope to keep behavior predictable and easy to test. Future work can add skyline, guillotine-split, or optimal approaches.

Visuals and reporting

This repository includes a simple SVG renderer for PackingResult (SVG output) and an optional PDF hook.

- Generate an SVG from code:
  var renderer = new WWA.Core.Reporting.SvgRenderer();
  var svg = renderer.Render(packingResult);
  File.WriteAllText("visual.svg", svg);

- CLI example (if supported by your build):
  wwa pack --input cutlist.txt --inventory inv.json --output visual.svg

- PDF: A PdfReporter stub exists (WWA.Core.Reporting.PdfReporter). If QuestPDF is available in the project, a follow-up integration can render the SVG into a PDF. By default the stub writes an HTML file next to the requested output to allow quick inspection of visuals.

To enable QuestPDF integration:
1. Add the QuestPDF NuGet package to the WWA.Core project.
2. Implement the integration in WWA.Core.Reporting.PdfReporter.GenerateFromSvg to create a QuestPDF document and render the SVG (or rasterize) into a PDF.

