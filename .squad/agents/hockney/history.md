# Hockney history

Seed: Assigned to Woodworking Agent project on 2026-06-10 by Mike Linnen. Initial tasks: draft tests for Milestone 1 and validate determinism across platforms.

## 2026-06-21 — Issue #3 Session
- Mike Linnen resumed work on issue #3 on branch `feat/maxrects-packer`.
- Hockney launched in background for **determinism/reproducibility tests**.
- Milestone 1 ownership confirmed: CLI/infra + CI, determinism validation.

## 2026-06-21 — Issue #3 Completion
- Added `BinPackerDeterminismTests.cs` with 17 new tests for `FullPacker` and `DeterministicPackerStub`.
- Established test conventions: shared input objects per test, `PackingSignature()` helper, 10-run reproducibility loops.
- Verified seed-sensitivity with seeds 1 and 9999 on 6-item tie group.
- Full test suite: **45 passed, 0 failed, 0 skipped**.
- Documented two known `DeterministicPackerStub` gaps; neither blocks issue #3.
- Decision archived to decisions.md.


## 2026-06-21 — Issue #3 Final Approval
- Issue #3 passed final re-review after Ripley and Dallas revision cycles.
- Hockney's `BinPackerDeterminismTests.cs` confirmed approved and in final commit scope.
- Final test result: 66 passed, 0 failed (expanded from 45 during revision cycles).

## 2026-06-21 — Issue #3 Committed (Scribe record)
- Issue #3 approved slice committed to `feat/maxrects-packer` at commit `9535995`.
- Hockney's `BinPackerDeterminismTests.cs` (17 determinism tests) is in the committed set.
- Final: 66 tests passed, 0 failed.

## 2026-06-21 — PR #14 Merged into master (Scribe record)
- PR #14 (feat/maxrects-packer) merged into master as squash commit ecf3715.
- Branch feat/maxrects-packer deleted after merge.
- Issue #3 auto-closed by GitHub via PR closing reference.
- All 7 CI checks green at merge time.
- Hockney's BinPackerDeterminismTests.cs (17 determinism tests) is now on master.

## 2026-07-02 — Inventory Input Review Chain
- Rejected Keaton's initial inventory-input slice because `--inventory` accepted missing or blank values without a usage error.
- Rejected Ripley's follow-up because signed inventory dimensions were still accepted by parser token handling.
- Approved Dallas's final revision after whole-token dimension validation rejected signed dimensions and preserved the intended optional-flag behavior.

## 2026-07-02 — CLI Strategy Coverage Approved
- Approved CLI integration coverage for `export-pdf --strategy`.
- Tests cover default BFD behavior, explicit `first-fit` selection, and invalid strategy values.
- Strategy support is documented as `full`-packer-only and keeps existing behavior unchanged when the flag is omitted.
- Non-state repo files pending coordinator handling: `src\WWA.Cli\Program.cs`, `tests\WWA.Core.Tests\CliIntegrationTests.cs`, `README.md`.

## 2026-07-02 — Skia SVG Regression Coverage Approved
- Approved targeted `SvgRendererTests.cs` coverage for XML well-formedness and invariant numeric SVG attribute output.
- Validation chain for the Skia export fix recorded as passing: Skia-enabled targeted tests, Skia-enabled build, and `export-pdf` repro.
- Non-state repo files pending coordinator handling: `tests\WWA.Core.Tests\SvgRendererTests.cs`, `src\WWA.Core\Reporting\SvgRenderer.cs`.

## 2026-07-02 — Skia Empty-Path Regression Coverage Approved
- Approved the targeted HAS_SKIA `PdfReporter` regression coverage for bare relative output filenames in `tests\WWA.Core.Tests\PdfReporterQuestPdfTests.cs`.
- Recorded the empty-directory guard decision in `src\WWA.Core\Reporting\PdfReporter.cs`: normalize to a full path first, then create a directory only when the directory component is non-blank.
- Validation chain recorded as passing: Skia-enabled targeted `PdfReporter` tests, Skia-enabled CLI build, and bare-relative-filename `export-pdf` repro.
- Non-state repo files pending coordinator handling: `src\WWA.Core\Reporting\PdfReporter.cs`, `tests\WWA.Core.Tests\PdfReporterQuestPdfTests.cs`.

## 2026-07-02T19:09:26.337-04:00 — Team update on Skia PDF rendering fix
- Approved outcome archived for the missing board/cut-list diagram in Skia PDF export.
- Decision record now points future debugging at SVG well-formedness/invariant formatting before PDF composition.
- Validation captured: Skia-enabled targeted rendering tests passed; Skia-enabled `export-pdf` produced a PDF without HTML fallback.

## 2026-07-02T19:28:47.206-04:00 — Visible layout regression coverage archived
- Team archived the approved visible-layout fix for the Skia PDF path.
- Regression coverage now records that 1D `BoardAllocation.Placements` must render when `Placements2D` is empty.
- Validation chain captured: Skia-enabled rendering tests passed; Skia-enabled `export-pdf` with the full packer produced a PDF probe before cleanup.
