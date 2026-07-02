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
