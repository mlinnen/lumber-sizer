# Ripley history

Seed: Assigned to Woodworking Agent project on 2026-06-10 by Mike Linnen. Initial focus: decompose PRD, propose milestones, and design CLI-first architecture.

## 2026-06-21 — Post-Issue-#3 Cleanup Pass
- Removed generated untracked export HTML files and runtime_simple_cutlist test files.
- Updated `.gitignore`: added patterns for CLI/e2e export HTML files and runtime_simple_cutlist files; fixed malformed test results ignore lines.
- Left `artifacts/test_output.html` and `artifacts/visual_sample.svg` unchanged (ambiguous tracked artifacts).
- Preserved `tools/packer-bench/artifacts/packer_bench.txt`.


## 2026-06-21 — Issue #3 Final Approval
- Issue #3 passed final re-review; no significant remaining issues found.
- Correctness revision shipped: `DeriveId()` XOR-based deterministic Id derivation fixed Board.Quantity collapse, CutItem Id uniqueness, and weak test assertions.
- Slice finalization: reverted rendering-pipeline artifacts; defined precise in-scope file list for commit.
- Approved commit scope: `FullPacker.cs`, `DeterministicPackerStub.cs`, `PackingModels.cs`, `FullPackerTests.cs`, `BinPackerDeterminismTests.cs`, `.gitignore`.
- Final test result: 66 passed, 0 failed.

## 2026-06-21 — Issue #3 Committed (Scribe record)
- Issue #3 approved slice committed to `feat/maxrects-packer` at commit `9535995`.
- Ripley's correctness revision (`DeriveId()` XOR-based Id derivation) in `FullPacker.cs` and `DeterministicPackerStub.cs` committed.
- Slice finalization decision honored: rendering-pipeline artifacts excluded from commit; `.gitignore` update included.
- Final: 66 tests passed, 0 failed.

## 2026-06-21 — packer-bench Artifact Ignore Cleanup
- Added `tools/packer-bench/artifacts/` ignore pattern to `.gitignore`.
- Removed `tools/packer-bench/artifacts/packer_bench.txt` from git index; file retained locally.
- Committed as `chore: ignore packer-bench generated artifacts`.
- No source or test files modified.

## 2026-06-21 — PR #14 Merged into master (Scribe record)
- PR #14 (feat/maxrects-packer) merged into master as squash commit ecf3715.
- Branch feat/maxrects-packer deleted after merge.
- Issue #3 auto-closed by GitHub via PR closing reference.
- All 7 CI checks green at merge time.
- Ripley's DeriveId() XOR correctness revision, slice finalization, and .gitignore update are now on master.

## 2026-06-21 — Post-PR-#14-Merge .squad Cleanup
- Committed remaining `.squad` history updates that were outstanding after the PR #14 merge.
- Commit `f9c9b93`: `chore: record PR #14 merge in agent histories.`
- Working tree clean after the cleanup commit.

## [2026-06-21T23:37:39-04:00] Post-merge final logging pass

- Committed final `.squad` history updates after PR #14 push (commit `0973227`).
- Commit message: `chore: update agent histories after PR #14 push`.
- Pushed `master` to `origin`; working tree clean, branch matches `origin/master`.

## 2026-07-02 — Documentation Audit Sync
- Audited repository documentation against current implementation.
- Updated `docs\packer.md` and `docs\Woodworking_Agent_PRD.md` to separate shipped behavior from roadmap guidance.
- Clarified supported `FullPacker` strategies, determinism/derived-ID behavior, and current `export-pdf` / PDF fallback limits.

## 2026-07-02 — README Sync / Doc Revert
- Reverted the uncommitted edits in `docs\packer.md` and `docs\Woodworking_Agent_PRD.md` to abandon the in-progress doc rewrite.
- Added root `README.md` documenting current CLI execution, packer wiring, repository layout, input/output behavior, and verified restore/build/test commands.
- Non-state repo file pending coordinator handling: `README.md`.

## 2026-07-02 — Inventory Flag Validation Revision
- Reviewer rejection required tightening `export-pdf` parsing so bare or blank `--inventory` is a usage error instead of silently falling back to default stock.
- Preserved backward compatibility by keeping the default stock inventory only when `--inventory` is omitted entirely.
- Revision covered `src\WWA.Cli\Program.cs` and `tests\WWA.Core.Tests\CliIntegrationTests.cs`; final approval followed Dallas's parser-hardening pass.

## 2026-07-02 — Skia Export Fix Final Approval
- Approved the PDF export fix after confirming the key decision to quote generated SVG attributes and emit numeric values with invariant formatting so Svg.Skia can parse the SVG as XML.
- Repo files in the approved slice: `src\WWA.Core\Reporting\SvgRenderer.cs`, `tests\WWA.Core.Tests\SvgRendererTests.cs`.
- Validation recorded: Skia-enabled targeted tests passed, Skia-enabled build passed, and the `export-pdf` repro command passed.

## 2026-07-02 — Skia Empty-Path Export Fix Final Approval
- Approved the PDF export fix after confirming the path-handling decision in `src\WWA.Core\Reporting\PdfReporter.cs`: normalize `outputPath` to a full path before directory creation/output selection and skip directory creation when the directory component is blank.
- Approved the focused HAS_SKIA regression coverage in `tests\WWA.Core.Tests\PdfReporterQuestPdfTests.cs` for bare relative output filenames.
- Validation recorded: Skia-enabled targeted `PdfReporter` tests passed, Skia-enabled CLI build passed, and `export-pdf` using a bare relative filename passed.
- Non-state repo files pending coordinator handling: `src\WWA.Core\Reporting\PdfReporter.cs`, `tests\WWA.Core.Tests\PdfReporterQuestPdfTests.cs`.

## 2026-07-02T19:09:26.337-04:00 — Approved Skia PDF rendering-path resolution
- Team archived the approved resolution for the missing board/cut-list diagram in Skia PDF export.
- Decision record consolidates the SVG root cause, the related output-path normalization, and the validated files changed.
- Validation captured: Skia-enabled targeted rendering tests passed; Skia-enabled `export-pdf` produced a PDF without HTML fallback.

## 2026-07-02T19:28:47.206-04:00 — Visible layout root cause archived
- Team archived the approved resolution for the missing visible board layout in Skia-generated PDFs.
- Decision record now points future investigation at `SvgRenderer` coverage for 1D `Placements` before revisiting downstream PDF composition.
- Non-state repo files pending coordinator handling: `src\WWA.Core\Reporting\SvgRenderer.cs`, `tests\WWA.Core.Tests\SvgRendererTests.cs`.


## 2026-07-02T19:58:15.216-04:00 — Approved inventory-board layout decision archived
- Team archived the approved cut-sheet decision for restoring real inventory-board layout in PDF/SVG output.
- Decision record captures the root cause: the renderer lacked source board width and let scale/legend layout overlap the drawing, collapsing usable boards into thin bands.
- Non-state repo files pending coordinator handling: `src\WWA.Core\Models\PackingModels.cs`, `src\WWA.Core\BinPacking\FullPacker.cs`, `src\WWA.Core\BinPacking\DeterministicPackerStub.cs`, `src\WWA.Core\BinPacking\TwoDPacker.cs`, `src\WWA.Core\BinPacking\MaxRectsPacker.cs`, `src\WWA.Core\BinPacking\GuillotinePacker.cs`, `src\WWA.Core\Reporting\SvgRenderer.cs`, `tests\WWA.Core.Tests\FullPackerTests.cs`, `tests\WWA.Core.Tests\SvgRendererTests.cs`.
## 2026-07-02T20:26:53.292-04:00 — Issue #15 reviewer revision archived
- Team archived the approved issue #15 artifact-publish decision after Ripley's reviewer-driven follow-up pass.
- Ripley's revision tightened the README trigger-scope wording and addressed the macOS `osx-x64` smoke-test stability concern that blocked the first draft.
- Final approval came after Dallas's last doc correction on HAS_SKIA runtime prerequisites and HTML fallback behavior.
- Non-state repo files pending coordinator handling: `.github\workflows\dotnet-ci.yml`, `README.md`.
