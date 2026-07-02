# lumber-sizer

This repository currently ships a CLI-first .NET application for parsing simple cut lists, packing parts onto boards, rendering the result to SVG, and exporting either a real PDF or an HTML fallback. The broader product vision still lives in `docs\Woodworking_Agent_PRD.md`, but this README describes the executable behavior that exists today.

## What the application does today

- Reads a text cut list from disk.
- Builds a default inventory of five `96in x 48in` boards.
- Runs one of three CLI-selectable packers:
  - `full` -> `FullPacker` (1D, default)
  - `deterministic` -> `DeterministicPackerStub`
  - `two-d` -> `TwoDPacker`
- Renders the packing result to SVG.
- Writes output through `PdfReporter`:
  - when built with `HAS_SKIA`, writes the requested PDF path
  - otherwise writes an HTML file with the same base name as the requested output path

If you run the CLI with no arguments, it prints the current command help and exits.

## Current CLI surface

Entrypoint: `src\WWA.Cli\Program.cs`

```text
dotnet run --project .\src\WWA.Cli\WWA.Cli.csproj -- export-pdf <input-cutlist> <output-pdf-or-html> [--packer deterministic|full|two-d] [--seed N]
```

Current behavior notes:

- `export-pdf` is the only implemented command.
- `--packer full` uses `FullPacker` with its default `PackingStrategy.BestFitDecreasing`.
- `--seed` is optional and is passed into the selected packer for deterministic tie-breaking.
- Unknown packer values fall back to `full`.
- The CLI does **not** currently accept an inventory file, remnant-preservation flags, or a CLI flag for `PackingStrategy`.

## Cut-list input format

Parser: `src\WWA.Core\IO\CutListParser.cs`

- One piece per line: `<length> x <width> # optional description`
- Blank lines are ignored.
- Lines beginning with `#` are ignored.
- Numeric parsing is unit-tolerant: the parser extracts the number from values such as `12in`.

Example:

```text
12in x 2in # leg
24in x 6in # shelf
```

Sample file: `samples\sample_cutlists\simple_cutlist.txt`

## Packers in the repository

Core code lives under `src\WWA.Core`.

### Wired to the CLI

- `FullPacker`: deterministic 1D packer. Its API supports `BestFitDecreasing`, `FirstFitDecreasing`, and `FirstFit`, but the CLI currently uses the default strategy only.
- `DeterministicPackerStub`: simple deterministic baseline packer.
- `TwoDPacker`: deterministic shelf-based 2D packer.

### Present in code but not exposed by the CLI

- `GuillotinePacker`
- `MaxRectsPacker`

## Output behavior

Reporting code lives under `src\WWA.Core\Reporting`.

- `SvgRenderer` produces the cut-sheet SVG.
- `PdfReporter.GenerateFromSvg(...)` decides whether the final artifact is a real PDF or an HTML fallback.
- On a normal build without `HAS_SKIA`, asking for `report.pdf` produces `report.html` next to the requested path.

Example run against the sample input:

```powershell
dotnet run --project .\src\WWA.Cli\WWA.Cli.csproj -- export-pdf .\samples\sample_cutlists\simple_cutlist.txt .\artifacts\cli_export_sample.html --packer full --seed 12345
```

## Repository structure

```text
src\
  WWA.slnx
  WWA.Cli\
  WWA.Core\
tests\
  WWA.Core.Tests\
samples\
  sample_cutlists\
docs\
tools\
  packer-bench\
```

## Build and test

Target framework: `net10.0` (`src\WWA.Cli\WWA.Cli.csproj`, `src\WWA.Core\WWA.Core.csproj`, `tests\WWA.Core.Tests\WWA.Core.Tests.csproj`)

Restore:

```powershell
dotnet restore .\src\WWA.slnx
```

Build:

```powershell
dotnet build .\src\WWA.slnx --no-restore --configuration Release
```

`src\WWA.slnx` currently includes the app projects only, so run tests from the test project directly:

Test:

```powershell
dotnet test .\tests\WWA.Core.Tests\WWA.Core.Tests.csproj --configuration Release --verbosity normal
```

Optional PDF-enabled build/test lane (matches the repository's Skia-enabled CI path):

```powershell
dotnet build .\src\WWA.slnx --no-restore --configuration Release -p:DefineConstants=HAS_SKIA
dotnet test .\tests\WWA.Core.Tests\WWA.Core.Tests.csproj --configuration Release -p:DefineConstants=HAS_SKIA --verbosity normal
```

## Current scope vs roadmap

Today the repository is a local CLI packing/export tool. The PRD describes broader goals such as agentic reasoning, lumber-yard integration, SQLite persistence, and a future GUI, but those are not wired into the current executable.
