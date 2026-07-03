# Keaton history

Seed: Assigned to Woodworking Agent project on 2026-06-10 by Mike Linnen. Initial tasks: implement CLI bin-packing, data storage, and inference integration.

## 2026-06-21 — Issue #3 Session
- Mike Linnen resumed work on issue #3 on branch `feat/maxrects-packer`.
- Keaton launched in background for **deterministic 1D packer implementation**.
- Milestone 1 ownership confirmed: core bin-packing engine + tests.

## 2026-06-21 — Issue #3 Completion
- Added `PackingStrategy` enum (`BestFitDecreasing`, `FirstFitDecreasing`, `FirstFit`) to `PackingModels.cs` and `PackingRequest.Strategy`.
- Refactored `FullPacker` to dispatch on chosen strategy; BFD remains default.
- Implemented seeded Fisher-Yates shuffle for tie-group ordering; full determinism contract established.
- Added 9 new tests in `FullPackerTests.cs` covering strategy variants and determinism.
- Decision archived to decisions.md.


## 2026-06-21 — Issue #3 Final Approval
- Issue #3 passed final re-review after Ripley and Dallas revision cycles.
- Keaton's `PackingStrategy` enum and `FullPacker` refactor confirmed approved and in final commit scope.
- Final test result: 66 passed, 0 failed.

## 2026-06-21 — Issue #3 Committed (Scribe record)
- Issue #3 approved slice committed to `feat/maxrects-packer` at commit `9535995`.
- Keaton's `PackingModels.cs` (PackingStrategy enum) and `FullPacker.cs` (strategy dispatch + seeded Fisher-Yates) are in the committed set.
- `FullPackerTests.cs` with 9 new strategy/determinism tests also committed.
- Final: 66 tests passed, 0 failed.

## 2026-06-21 — PR #14 Merged into master (Scribe record)
- PR #14 (feat/maxrects-packer) merged into master as squash commit ecf3715.
- Branch feat/maxrects-packer deleted after merge.
- Issue #3 auto-closed by GitHub via PR closing reference.
- All 7 CI checks green at merge time.
- Keaton's PackingStrategy enum, FullPacker refactor, and determinism tests are now on master.

## 2026-07-02 — Inventory File Input Added
- Added optional `--inventory <path>` support on `export-pdf` plus `InventoryParser` for simple text inventory files alongside the cut list.
- Chosen inventory format: `<length> x <width> [x <quantity>] # optional grade`.
- Reviewer follow-up found bare `--inventory` needed explicit value validation; final approval came after downstream revisions.
- Non-state repo files pending coordinator handling include `README.md`, `samples\sample_cutlists\README.txt`, `samples\sample_cutlists\simple_inventory.txt`, `src\WWA.Cli\Program.cs`, `src\WWA.Core\IO\InventoryParser.cs`, `tests\WWA.Core.Tests\CliIntegrationTests.cs`, and `tests\WWA.Core.Tests\InventoryParserTests.cs`.

## 2026-07-02 — CLI Strategy Flag Approved
- Approved `export-pdf --strategy <best-fit-decreasing|first-fit-decreasing|first-fit>` wiring to `PackingRequest.Strategy`.
- Omitting `--strategy` preserves the existing `BestFitDecreasing` default behavior.
- Strategy selection remains limited to the `full` packer.
- Non-state repo files pending coordinator handling: `src\WWA.Cli\Program.cs`, `tests\WWA.Core.Tests\CliIntegrationTests.cs`, `README.md`.

## 2026-07-02 — Skia PDF Export Fix Approved
- Approved fix for the HAS_SKIA PDF export failure by quoting all generated SVG attribute values and formatting numeric output invariantly in `src\WWA.Core\Reporting\SvgRenderer.cs`.
- Added focused regression coverage in `tests\WWA.Core.Tests\SvgRendererTests.cs` for XML well-formedness and invariant numeric attributes.
- Validation recorded: Skia-enabled targeted tests passed, Skia-enabled build passed, and the `export-pdf` repro now emits a PDF instead of falling back to HTML.
- Non-state repo files pending coordinator handling: `src\WWA.Core\Reporting\SvgRenderer.cs`, `tests\WWA.Core.Tests\SvgRendererTests.cs`.

## 2026-07-02 — Skia Empty-Path Export Fix Approved
- Approved fix for the HAS_SKIA PDF export failure caused by bare relative output filenames in `src\WWA.Core\Reporting\PdfReporter.cs`.
- Key decision: normalize `outputPath` to a full path before directory creation/output selection, and only create a directory when the directory component is non-blank.
- Added focused HAS_SKIA regression coverage in `tests\WWA.Core.Tests\PdfReporterQuestPdfTests.cs`.
- Validation recorded: Skia-enabled targeted `PdfReporter` tests passed, Skia-enabled CLI build passed, and `export-pdf` with a bare relative filename passed.
- Non-state repo files pending coordinator handling: `src\WWA.Core\Reporting\PdfReporter.cs`, `tests\WWA.Core.Tests\PdfReporterQuestPdfTests.cs`.

## 2026-07-02T19:09:26.337-04:00 — Approved Skia PDF rendering-path fix
- Team archived the approved outcome for the missing board/cut-list diagram in Skia PDF export.
- Root cause was malformed/culture-sensitive SVG generation, not downstream PDF embedding.
- Recorded files: `src\WWA.Core\Reporting\SvgRenderer.cs`, `src\WWA.Core\Reporting\PdfReporter.cs`, `tests\WWA.Core.Tests\SvgRendererTests.cs`, `tests\WWA.Core.Tests\PdfReporterQuestPdfTests.cs`.
- Validation captured: Skia-enabled targeted rendering tests passed; Skia-enabled `export-pdf` produced a PDF without HTML fallback.

## 2026-07-02T19:28:47.206-04:00 — Visible layout fix approved
- Team archived the approved fix for missing board/cut rectangles in Skia-generated PDFs.
- Key finding recorded: `SvgRenderer` only rendered `Placements2D`, while the default `FullPacker` supplies 1D `Placements`.
- Approved slice tracked in `src\WWA.Core\Reporting\SvgRenderer.cs` and `tests\WWA.Core.Tests\SvgRendererTests.cs`.
- Validation captured: Skia-enabled rendering tests passed; Skia-enabled `export-pdf` with the full packer produced a PDF probe before cleanup.


## 2026-07-02T19:48:41.878-04:00 — Board canvas verification confirmed
- Scribe verified Keaton's investigation outcome for the PDF legend-without-canvas report.
- Current HEAD already contains the approved fix in `src\WWA.Core\Reporting\SvgRenderer.cs` with regression coverage in `tests\WWA.Core.Tests\SvgRendererTests.cs`.
- Confirmed root cause: SVG generation for 1D placements depended on `Placements2D` only; the missing board canvas was not caused by rasterization or PDF embedding.
- Validation remained green: HAS_SKIA targeted rendering tests passed, and HAS_SKIA `export-pdf` produced a real PDF.
- No further code changes were required.


## 2026-07-02T19:58:15.216-04:00 — Inventory-board cut-sheet layout fix approved
- Team archived Keaton's approved fix for restoring usable inventory-board layout in PDF/SVG output.
- Key implementation recorded: `OriginalBoardWidth` now flows through `BoardAllocation` from all packers, and `SvgRenderer` reserves non-overlapping space for the board scale, legend, and unplaced sections.
- Validation captured: HAS_SKIA targeted tests passed; Skia repro export passed; Hockney additionally verified targeted and full suites with and without `HAS_SKIA`.
- Non-state repo files pending coordinator handling: `src\WWA.Core\Models\PackingModels.cs`, `src\WWA.Core\BinPacking\FullPacker.cs`, `src\WWA.Core\BinPacking\DeterministicPackerStub.cs`, `src\WWA.Core\BinPacking\TwoDPacker.cs`, `src\WWA.Core\BinPacking\MaxRectsPacker.cs`, `src\WWA.Core\BinPacking\GuillotinePacker.cs`, `src\WWA.Core\Reporting\SvgRenderer.cs`, `tests\WWA.Core.Tests\FullPackerTests.cs`, `tests\WWA.Core.Tests\SvgRendererTests.cs`.