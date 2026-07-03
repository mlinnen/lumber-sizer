# Dallas history

Seed: Assigned to Woodworking Agent project on 2026-06-10 by Mike Linnen. Initial tasks: PDF visuals, cut-sheet layout, GUI prototyping.

## 2026-06-21 — Issue #3 Remnant Test Fix (reviewer-lockout revision)
- Replaced geometrically ambiguous remnant test (30in+10in on 48in boards) with geometry-enforced scenario (two 40in cuts on two 48in boards).
- New scenario guarantees exactly 2 allocations; added `Assert.Equal(2, res.Allocations.Count)` hard gate and concrete remnant length assertion.
- No production code changed. All 66 tests pass.
- Decision archived: `[2026-06-21] Fix Remnant Test Gap in Issue #3 Slice`.

## 2026-06-21 — Issue #3 Committed (Scribe record)
- Issue #3 approved slice committed to `feat/maxrects-packer` at commit `9535995`.
- Dallas's remnant test fix (geometry-enforced scenario in `FullPackerTests.cs`) is part of the committed set.
- Final: 66 tests passed, 0 failed.

## 2026-06-21 — PR #14 Merged into master (Scribe record)
- PR #14 (feat/maxrects-packer) merged into master as squash commit ecf3715.
- Branch feat/maxrects-packer deleted after merge.
- Issue #3 auto-closed by GitHub via PR closing reference.
- All 7 CI checks green at merge time.
- Dallas's geometry-enforced remnant test fix (FullPackerTests.cs) is now on master.

## 2026-07-02 — Inventory Parser Hardening Revision
- Final reviewer-driven revision tightened `InventoryParser` so dimensions must match the whole token and malformed signed or garbage-prefixed values fail fast.
- Negative inventory dimensions now raise `FormatException`; regression coverage added in `tests\WWA.Core.Tests\InventoryParserTests.cs`.
- This revision cleared the final approval for the inventory-input slice.

## 2026-07-02T20:26:53.292-04:00 — Issue #15 final approval archived
- Dallas's final README revision cleared approval for issue #15's downloadable GitHub Actions artifact workflow.
- Archived final decision records the exact publish scope (`master`, `feature/*`, `v*`, `workflow_dispatch`), the `macos-13` `osx-x64` smoke-test lane, and the framework-dependent `HAS_SKIA` artifact/runtime caveats.
- Non-state repo files pending coordinator handling: `.github\workflows\dotnet-ci.yml`, `README.md`.
