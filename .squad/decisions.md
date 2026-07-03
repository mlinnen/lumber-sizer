# Squad Decisions

## Active Decisions

### [2026-06-10] Initial Project Plan — Ripley
**Author:** Ripley  
**Summary:** Map PRD sections to 4 milestones; establish Milestone 1 scope and ownership.

**Details:**
- Map PRD sections to 4 milestones: CLI/bin-packing; PDF generation; LLM/agent reasoning; Cross-platform GUI.
- Milestone 1 scope: CLI app, deterministic bin-packing engine, text input format, unit tests, CI, sample datasets.
- Ownership: keaton (core bin-packing + tests), hockney (CLI/infra + CI), dallas (ONNX/inference integration later milestones).
- Branch naming: `feature/m1-cli-<short>`, `fix/m1-<short>`, `chore/docs-<short>`.

**Rationale:** Aligns with PRD Roadmap, emphasizes privacy (local-first), deterministic reproducibility, and incremental delivery.

---

### [2026-06-21] PackingStrategy Enum Added to FullPacker — Keaton
**Author:** Keaton  
**Summary:** Added `PackingStrategy` enum and wired it into `PackingRequest`; refactored `FullPacker` to dispatch on the chosen strategy.

**Details:**
- Added `PackingStrategy` enum (`BestFitDecreasing`, `FirstFitDecreasing`, `FirstFit`) to `PackingModels.cs` and `PackingRequest.Strategy` property.
- Refactored `FullPacker` to support all three strategies; BFD remains the default — no callers break.
- Determinism contract: without seed, identical inputs → identical outputs on every run and platform; with seed, tie-groups shuffled via seeded Fisher-Yates, board ties resolved by same RNG instance.
- Files changed: `src/WWA.Core/Models/PackingModels.cs`, `src/WWA.Core/BinPacking/FullPacker.cs`, `tests/WWA.Core.Tests/FullPackerTests.cs` (9 new tests).
- Future: a `--strategy` CLI flag could expose `PackingStrategy` variants on the `full` packer without new classes.

**Rationale:** Issue #3 explicitly requests strategies alongside seedable RNG. A single enum in the request is the lightest integration point; avoids parallel class hierarchies; consistent with existing project patterns.

---

### [2026-06-21] Determinism Test Coverage for 1D Bin-Packer — Hockney
**Author:** Hockney  
**Summary:** Established test conventions for reproducibility coverage of both `FullPacker` and `DeterministicPackerStub`; all 45 tests pass.

**Details:**
- Shared input objects per test: `CutList` and `Inventory` created once per test, shared across all `PackingRequest` instances; keeps item/board GUIDs stable for byte-for-byte JSON comparisons.
- `PackingSignature()` helper serialises only allocations, placements, totals, and seed echo — excludes leftover board IDs — so determinism tests remain valid for both packers without production changes.
- Ten-run loop per reproducibility test surfaces accidental non-determinism (e.g., `DateTime.Now`, thread-local state, `Dictionary` ordering) that a two-shot test could miss.
- Seed-sensitivity test uses 6-item tie group; seeds 1 and 9999 empirically verified to produce different permutations.
- File added: `tests/WWA.Core.Tests/BinPackerDeterminismTests.cs` (17 new tests). Test suite: 45 passed, 0 failed, 0 skipped.
- Known gaps (non-blocking): `DeterministicPackerStub` leftover boards do not copy source `Board.Id` into remnants; stub silently drops unplaced items. Documented in `PackingSignature()`.

**Rationale:** Broad 10-run loops catch the class of accidental non-determinism that pair comparisons miss; shared input objects keep GUIDs stable across runs without synthetic fixed-ID helpers.

---

### [2026-06-21] Issue #3 Correctness Revision — Ripley
**Author:** Ripley (Lead)  
**Summary:** Fixed four reviewer-blocking bugs in the 1D bin-packing implementation via deterministic Id derivation.

**Details:**
- **Critical bug:** Board.Quantity > 1 expansion reused the same Board instance (same Id) for every physical copy; all copies shared one BoardAllocation, collapsing offsets and remnant lengths.
- **Medium bug:** `DeterministicPackerStub` never assigned `result.TotalWasteLength` (always 0).
- **Medium bug:** Expanded CutItem copies all received the template's Id, making multi-quantity placements indistinguishable.
- **Tests:** Determinism tests validated reproducibility but could pass on consistently wrong output (no structural assertions).
- Fix: Introduced `DeriveId(Guid templateId, int copyIndex)` in both `FullPacker` and `DeterministicPackerStub`. copyIndex == 0 returns templateId unchanged; copyIndex > 0 XORs the copy index into the last 4 bytes of the Guid byte array — collision-resistant, deterministic, backward-compatible.
- Added 8 new regression tests; all 66 tests pass.

**Rationale:** Guid.NewGuid() would break determinism tests (different BoardIds each run). XOR derivation is bit-exact across runs and collision-resistant for practical inventory sizes.

---

### [2026-06-21] Fix Remnant Test Gap in Issue #3 Slice — Dallas
**Author:** Dallas (reviewer-lockout revision)  
**Summary:** Replaced a geometrically ambiguous remnant test scenario with one that guarantees two allocations by board geometry.

**Details:**
- Previous scenario (30in + 10in on two 48in qty-2 boards): BFD placed both cuts on Board 0 — only one allocation produced; remnant loop never exercised independent per-board accounting.
- New scenario: two 40in cuts on two 48in boards. 40in[0] → Board 0 (8in left); 40in[1] cannot fit on Board 0 → forced to Board 1 (8in left). Exactly 2 allocations guaranteed by geometry.
- Added `Assert.Equal(2, res.Allocations.Count)` hard gate before remnant loop; concrete `Assert.Equal(8.0, a.RemnantLength)` inside loop.
- No production code changed; all 66 tests pass.

**Rationale:** Geometry-enforced allocation count removes test flakiness risk from BFD's tie-breaking behavior; concrete assertions replace trivially-passing empty loops.

---

### [2026-06-21] Issue #3 Slice Finalization — Ripley
**Author:** Ripley (Lead)  
**Summary:** Defined precise in-scope and excluded files for the issue #3 commit; reverted rendering-pipeline artifacts.

**Details:**
- Reverted `artifacts/test_output.html` and `artifacts/visual_sample.svg` to HEAD before staging — belong to the rendering pipeline, not the determinism engine.
- In-scope files: `src/WWA.Core/BinPacking/FullPacker.cs`, `src/WWA.Core/Models/PackingModels.cs`, `tests/WWA.Core.Tests/FullPackerTests.cs`, `tests/WWA.Core.Tests/BinPackerDeterminismTests.cs`, `.gitignore` (pre-existing formatting fix + test artifact exclusions).
- Excluded: `artifacts/test_output.html`, `artifacts/visual_sample.svg`, `tools/packer-bench/artifacts/packer_bench.txt`, `.squad/` mutable state files.
- Verification: `dotnet test tests/WWA.Core.Tests/` → **58 tests passed, 0 failed**.

**Rationale:** Clean slice boundaries prevent rendering-pipeline changes from polluting the determinism engine commit history.

---

### [2026-07-02] Clarified implementation-vs-roadmap documentation boundaries — Ripley
**Author:** Ripley  
**Summary:** Clarified implementation-vs-roadmap documentation boundaries.

**Details:**
- Updated repository docs so current implementation details live in `docs\packer.md`, while `docs\Woodworking_Agent_PRD.md` is explicitly labeled as roadmap/vision material.
- Documented current `FullPacker` strategies, seed/no-seed determinism, derived ID behavior, and current CLI export/PDF limits against `src\WWA.Cli\Program.cs`, `src\WWA.Core\BinPacking\FullPacker.cs`, and `src\WWA.Core\Reporting\PdfReporter.cs`.
- Replaced outdated PDF guidance with current `HAS_SKIA` / HTML fallback behavior and clarified present `export-pdf` usage.
- Files changed: `docs\packer.md`, `docs\Woodworking_Agent_PRD.md`.

**Rationale:** Keeps shipped-behavior guidance anchored to the docs that track current implementation while preserving the PRD as forward-looking roadmap material.

---

### [2026-07-02] Root README documents shipped behavior; abandoned doc edits were reverted — Ripley
**Author:** Ripley  
**Summary:** Root README documents shipped behavior; abandoned doc edits were reverted.

**Details:**
- Reverted the uncommitted edits in `docs\packer.md` and `docs\Woodworking_Agent_PRD.md` to abandon the in-progress doc rewrite.
- Added root `README.md` as the current source of truth for shipped CLI behavior, packer wiring, repository layout, input format, output behavior, and verified build/test commands.
- Anchored the README to the implementation in `src\WWA.Cli\Program.cs`, `src\WWA.slnx`, `.github\workflows\dotnet-ci.yml`, and the current HTML/PDF export behavior.
- Documented that tests should be run from `tests\WWA.Core.Tests\WWA.Core.Tests.csproj` because `src\WWA.slnx` only includes the app projects.

**Rationale:** Preserve roadmap docs from partial rewrites while giving contributors a current, implementation-backed top-level reference for executing, building, and testing the shipped application.

---

### [2026-07-02] Add optional text inventory file input to export-pdf — Keaton
**Author:** Keaton  
**Summary:** Add optional text inventory file input to export-pdf.

**Details:**
- Introduced optional `--inventory <path>` on `export-pdf` and added `src\WWA.Core\IO\InventoryParser.cs` to parse a simple text inventory file alongside the cut list.
- Inventory format is `<length> x <width> [x <quantity>] # optional grade`, matching the cut-list parser's line-oriented comment-friendly conventions while tolerating simple unit suffixes.
- Omitting `--inventory` preserves the default stock inventory of five `96in x 48in` grade `A` boards for backward-compatible CLI behavior.
- Files changed: `src\WWA.Cli\Program.cs`, `src\WWA.Core\IO\InventoryParser.cs`, `README.md`, `tests\WWA.Core.Tests\CliIntegrationTests.cs`, `tests\WWA.Core.Tests\InventoryParserTests.cs`.

**Rationale:** Adds real inventory input with minimal workflow overhead by reusing the existing plain-text cut-list style, while preserving current behavior for users who do not supply an inventory file.

---

### [2026-07-02] Reject bare --inventory and preserve default stock only when flag is omitted — Ripley
**Author:** Ripley  
**Summary:** Reject bare `--inventory` and preserve default stock only when the flag is omitted.

**Details:**
- Tightened `export-pdf` argument parsing so `--inventory` must be followed by a non-blank, non-option path token; bare or blank usage is now a usage error.
- On invalid `--inventory` usage, the CLI emits an explicit error plus usage text and exits with code 2 instead of silently falling back to default stock.
- The existing default inventory fallback remains only when `--inventory` is omitted entirely, preserving backward compatibility for older invocations.
- Files changed: `src\WWA.Cli\Program.cs`, `tests\WWA.Core.Tests\CliIntegrationTests.cs`.

**Rationale:** Reviewer-driven validation closes the silent-fallback bug and makes the new optional inventory flag behave predictably without changing legacy CLI behavior when the flag is absent.

---

### [2026-07-02] InventoryParser now requires whole-token dimensions so signed or garbage-prefixed values fail fast — Dallas
**Author:** Dallas  
**Summary:** InventoryParser now requires whole-token dimensions so signed or garbage-prefixed values fail fast.

**Details:**
- Tightened `InventoryParser` dimension parsing to require the entire trimmed token to match a valid unsigned dimension instead of extracting any numeric substring.
- Signed or garbage-prefixed values such as `-96in` now raise `FormatException` rather than being normalized or partially accepted.
- Added regression coverage for negative length and width inventory lines in `tests\WWA.Core.Tests\InventoryParserTests.cs`.
- Files changed: `src\WWA.Core\IO\InventoryParser.cs`, `tests\WWA.Core.Tests\InventoryParserTests.cs`.

**Rationale:** Reviewer-driven input hardening keeps the inventory file format explicit and rejects malformed signed dimensions instead of silently accepting invalid stock data.

---

### [2026-07-02] Expose PackingStrategy on the CLI — Keaton
**Author:** Keaton  
**Summary:** Expose PackingStrategy on the CLI.

**Details:**
- Added optional `--strategy <best-fit-decreasing|first-fit-decreasing|first-fit>` to `export-pdf` in `src\WWA.Cli\Program.cs`.
- The flag maps lower-case hyphenated CLI values directly to `PackingRequest.Strategy`.
- Omitting `--strategy` preserves the existing `BestFitDecreasing` behavior by leaving `PackingRequest.Strategy` at its default.
- Strategy selection support is limited to the `full` packer.
- Added CLI integration coverage for default behavior, explicit `first-fit` selection, and invalid strategy values in `tests\WWA.Core.Tests\CliIntegrationTests.cs`.
- Updated `README.md` usage and behavior documentation.

**Rationale:** Exposes the existing `PackingStrategy` variants on the CLI without breaking existing behavior, while keeping the new flag constrained to the only packer implementation that supports strategy selection.

---

### [2026-07-02] Fix Skia rasterization by emitting well-formed invariant SVG attributes — Keaton
**Author:** Keaton  
**Summary:** Fix Skia rasterization by emitting well-formed invariant SVG attributes.

**Details:**
- Updated `src\WWA.Core\Reporting\SvgRenderer.cs` to quote all generated SVG attribute values and format numeric output invariantly.
- Root cause: unquoted numeric attributes such as `font-size=12` and `stroke-width=0.5` were tolerated by the HTML fallback path but rejected by Svg.Skia's XML parser during rasterization.
- Added focused coverage in `tests\WWA.Core.Tests\SvgRendererTests.cs` for XML well-formedness and invariant numeric attribute output.
- Validation: Skia-enabled targeted tests passed; Skia-enabled build passed; the `export-pdf` repro command now produces a PDF instead of the HTML fallback artifact.
- Files changed: `src\WWA.Core\Reporting\SvgRenderer.cs`, `tests\WWA.Core.Tests\SvgRendererTests.cs`.

**Rationale:** Generated SVG must be well-formed XML with culture-invariant numeric attributes so Svg.Skia can parse and rasterize it reliably during PDF export.

---

### [2026-07-02] Normalize PDF export output paths before Skia writes files — Keaton
**Author:** Keaton  
**Summary:** Normalize PDF export output paths before Skia writes files.

**Details:**
- Normalized `outputPath` to a full path before directory creation and output selection in `src\WWA.Core\Reporting\PdfReporter.cs`.
- Only create a directory when the directory component is non-blank, so bare relative filenames such as `report.pdf` no longer trigger `ArgumentException` in the HAS_SKIA path.
- Added focused HAS_SKIA regression coverage in `tests\WWA.Core.Tests\PdfReporterQuestPdfTests.cs` to verify a relative output filename now emits a PDF instead of forcing the HTML fallback.
- Validation: Skia-enabled targeted `PdfReporter` tests passed; Skia-enabled CLI build passed; `export-pdf` using a bare relative filename passed.
- Files changed: `src\WWA.Core\Reporting\PdfReporter.cs`, `tests\WWA.Core.Tests\PdfReporterQuestPdfTests.cs`.

**Rationale:** Skia PDF export must tolerate bare relative output filenames by resolving them before directory creation/output setup, while skipping directory creation for empty directory components.

---

### [2026-07-02] Approved Skia PDF rendering-path fix — Keaton
**Author:** Keaton  
**Summary:** Approved the rendering-path fix for the missing board/cut-list diagram in Skia PDF export.

**Details:**
- Investigation confirmed the missing board/cut-list diagram originated in the SVG stage rather than in PDF embedding.
- `src\WWA.Core\Reporting\SvgRenderer.cs` was updated to emit well-formed, quoted, culture-invariant SVG that browsers and `Svg.Skia` both accept reliably.
- `src\WWA.Core\Reporting\PdfReporter.cs` retained the related output-path normalization so bare relative PDF filenames still succeed in the HAS_SKIA path.
- Files changed: `src\WWA.Core\Reporting\SvgRenderer.cs`, `src\WWA.Core\Reporting\PdfReporter.cs`, `tests\WWA.Core.Tests\SvgRendererTests.cs`, `tests\WWA.Core.Tests\PdfReporterQuestPdfTests.cs`.
- Validation: Skia-enabled targeted rendering tests passed, and a Skia-enabled `export-pdf` run produced a PDF without falling back to HTML.

**Rationale:** Records the approved root cause and fix path so future PDF-export regressions start at SVG well-formedness and invariant numeric output before investigating downstream PDF composition.

---

### [2026-07-02] Restore visible board layout in Skia-generated PDFs — Keaton
**Author:** Keaton  
**Summary:** Fix the missing visible board layout in Skia-generated PDFs by rendering 1D placements in the SVG output.

**Details:**
- Investigation found `src\WWA.Core\Reporting\SvgRenderer.cs` only drew `BoardAllocation.Placements2D`, while the default `FullPacker` and related 1D packers populate `BoardAllocation.Placements`.
- When `Placements2D` is empty, the renderer now projects 1D placements into visible cut rectangles and labels so the generated SVG contains the board layout that the Skia PDF path rasterizes.
- Focused regression coverage in `tests\WWA.Core.Tests\SvgRendererTests.cs` verifies 1D placements are rendered in the SVG output.
- Validation: Skia-enabled rendering tests passed; a Skia-enabled `export-pdf` run with the full packer produced a PDF probe before cleanup.
- Files changed: `src\WWA.Core\Reporting\SvgRenderer.cs`, `tests\WWA.Core.Tests\SvgRendererTests.cs`.

**Rationale:** The Skia PDF pipeline was faithfully rendering the source SVG; the missing visible layout came from the SVG stage not drawing the default packer’s 1D placements.

---

### [2026-07-02] Restore actual inventory-board cut-sheet layout in PDF/SVG output — Keaton
**Author:** Keaton  
**Summary:** Restore actual inventory-board cut-sheet layout in PDF/SVG output.

**Details:**
- Added `OriginalBoardWidth` to `BoardAllocation` in `src\WWA.Core\Models\PackingModels.cs` and populated it in `src\WWA.Core\BinPacking\FullPacker.cs`, `src\WWA.Core\BinPacking\DeterministicPackerStub.cs`, `src\WWA.Core\BinPacking\TwoDPacker.cs`, `src\WWA.Core\BinPacking\MaxRectsPacker.cs`, and `src\WWA.Core\BinPacking\GuillotinePacker.cs` so renderers can use source inventory dimensions instead of placement extents or fallback bands.
- Updated `src\WWA.Core\Reporting\SvgRenderer.cs` to size each board canvas from the source inventory board width and reserve dedicated vertical space for the top scale, bottom legend, and unplaced-items sections so the layout no longer overlaps itself.
- Added focused regressions in `tests\WWA.Core.Tests\FullPackerTests.cs` and `tests\WWA.Core.Tests\SvgRendererTests.cs` covering board-width propagation and non-overlapping cut-sheet layout for the default PDF/SVG path.
- Validation: HAS_SKIA targeted tests passed; Skia repro export passed; Hockney also verified targeted and full test suites with and without `HAS_SKIA`.
- Outcome: Approved.
- Files changed: `src\WWA.Core\Models\PackingModels.cs`, `src\WWA.Core\BinPacking\FullPacker.cs`, `src\WWA.Core\BinPacking\DeterministicPackerStub.cs`, `src\WWA.Core\BinPacking\TwoDPacker.cs`, `src\WWA.Core\BinPacking\MaxRectsPacker.cs`, `src\WWA.Core\BinPacking\GuillotinePacker.cs`, `src\WWA.Core\Reporting\SvgRenderer.cs`, `tests\WWA.Core.Tests\FullPackerTests.cs`, `tests\WWA.Core.Tests\SvgRendererTests.cs`.

**Rationale:** The renderer was collapsing real inventory boards into thin bands because it lacked the source board width and allowed the scale/legend layout to overlap the drawing. Persisting the original width through packing and reserving dedicated layout space restores usable board-by-board cut sheets in PDF/SVG output.

---

### [2026-07-02] Issue #15 GitHub artifact publishes approved after reviewer-driven revisions — Dallas
**Author:** Dallas  
**Summary:** Approved GitHub Actions artifact publishes for issue #15 after narrowing trigger docs, stabilizing the macOS smoke test, and documenting Skia runtime limits.

**Details:**
- Added a gated `publish_artifacts` job after the existing Release and `HAS_SKIA` validation jobs in `.github\workflows\dotnet-ci.yml`.
- Publish outputs are framework-dependent `HAS_SKIA` CLI artifacts for `win-x64`, `osx-x64`, and `linux-x64`, produced from `dotnet publish` in `Release`.
- Publish scope is intentionally limited to pushes to `master`, pushes to `feature/*`, `v*` tag pushes, and `workflow_dispatch`; PR validation remains build/test-only.
- The macOS publish leg is pinned to `macos-13` so the `osx-x64` artifact smoke test runs on an Intel runner without Rosetta assumptions.
- `README.md` now matches the implemented trigger scope and clarifies that these downloadable artifacts are built with `HAS_SKIA`, remain framework-dependent, and still require platform-native Skia prerequisites for PDF export; without those native libraries, `export-pdf` can fall back to HTML.
- Files changed: `.github\workflows\dotnet-ci.yml`, `README.md`.
- Review chain: Hockney rejected Keaton's initial workflow/docs slice over trigger-scope wording and the macOS cross-arch smoke test; Hockney rejected Ripley's follow-up over overstated README runtime requirements; Hockney approved Dallas's final revision.

**Rationale:** Keeps artifact publishing gated behind successful validation, documents the exact shipping scope, and avoids a fragile macOS smoke test while preserving the intended PDF-capable `HAS_SKIA` distribution path.

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
