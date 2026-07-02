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

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
