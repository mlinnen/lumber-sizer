# lumber-sizer

This repository currently ships a CLI-first .NET application for parsing simple cut lists, packing parts onto boards, rendering the result to SVG, and exporting either a real PDF or an HTML fallback. The broader product vision still lives in `docs\Woodworking_Agent_PRD.md`, but this README describes the executable behavior that exists today.

## What the application does today

- Reads a text cut list from disk.
- Reads an optional text inventory file from disk, or falls back to five default `96in x 48in` boards.
- Runs one of three CLI-selectable packers:
  - `full` -> `FullPacker` (1D, default)
  - `deterministic` -> `DeterministicPackerStub`
  - `two-d` -> `TwoDPacker`
- Renders the packing result to SVG.
- Writes output through `PdfReporter`:
  - when built with `HAS_SKIA` **and** the required native Skia dependencies are available at runtime, writes the requested PDF path
  - otherwise writes an HTML fallback with the same base name as the requested output path

If you run the CLI with no arguments, it prints the current command help and exits.

## Current CLI surface

Entrypoint: `src\WWA.Cli\Program.cs`

```text
dotnet run --project .\src\WWA.Cli\WWA.Cli.csproj -- export-pdf <input-cutlist> <output-pdf-or-html> [--inventory path] [--packer deterministic|full|two-d] [--strategy best-fit-decreasing|first-fit-decreasing|first-fit] [--seed N]
```

Current behavior notes:

- `export-pdf` is the only implemented command.
- `--inventory` is optional; when omitted, the CLI keeps using one default inventory entry of `96in x 48in x 5` with grade `A`.
- `--packer full` uses `FullPacker`.
- `--strategy` is optional and only applies to the `full` packer:
  - `best-fit-decreasing` -> `PackingStrategy.BestFitDecreasing` (default)
  - `first-fit-decreasing` -> `PackingStrategy.FirstFitDecreasing`
  - `first-fit` -> `PackingStrategy.FirstFit`
- Omitting `--strategy` preserves the existing `FullPacker` default of `PackingStrategy.BestFitDecreasing`.
- `--seed` is optional and is passed into the selected packer for deterministic tie-breaking.
- Unknown packer values fall back to `full`.
- The CLI still does **not** accept remnant-preservation flags.

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

## Inventory input format

Parser: `src\WWA.Core\IO\InventoryParser.cs`

- One board spec per line: `<length> x <width> [x <quantity>] # optional grade`
- Blank lines are ignored.
- Lines beginning with `#` are ignored.
- Length and width parsing are unit-tolerant, just like the cut-list parser.
- Quantity is optional and defaults to `1`.

Example:

```text
96in x 48in x 5 # A
120in x 12in
```

Sample file: `samples\sample_cutlists\simple_inventory.txt`

## Packers in the repository

Core code lives under `src\WWA.Core`.

### Wired to the CLI

- `FullPacker`: deterministic 1D packer. The CLI can select `BestFitDecreasing`, `FirstFitDecreasing`, or `FirstFit` through `--strategy`; omitting the flag keeps the default `BestFitDecreasing`.
- `DeterministicPackerStub`: simple deterministic baseline packer.
- `TwoDPacker`: deterministic shelf-based 2D packer.

### Present in code but not exposed by the CLI

- `GuillotinePacker`
- `MaxRectsPacker`

## Output behavior

Reporting code lives under `src\WWA.Core\Reporting`.

- `SvgRenderer` produces the cut-sheet SVG.
- `PdfReporter.GenerateFromSvg(...)` produces a real PDF only when the app is built with `HAS_SKIA` and the required native Skia dependencies load at runtime; otherwise it writes an HTML fallback.
- On a normal build without `HAS_SKIA`, asking for `report.pdf` produces `report.html` next to the requested path.
- A published `HAS_SKIA` artifact can still fall back to HTML on a target machine that only has .NET installed but is missing the native Skia prerequisites.

Example run against the sample input:

```powershell
dotnet run --project .\src\WWA.Cli\WWA.Cli.csproj -- export-pdf .\samples\sample_cutlists\simple_cutlist.txt .\artifacts\cli_export_sample.html --packer full --strategy best-fit-decreasing --seed 12345
```

With an explicit inventory file:

```powershell
dotnet run --project .\src\WWA.Cli\WWA.Cli.csproj -- export-pdf .\samples\sample_cutlists\simple_cutlist.txt .\artifacts\cli_export_sample.html --inventory .\samples\sample_cutlists\simple_inventory.txt --packer full --strategy first-fit-decreasing --seed 12345
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

## GitHub Actions publish artifacts

`.github\workflows\dotnet-ci.yml` keeps the existing plain `Release` and `HAS_SKIA` validation lanes, then publishes workflow artifacts only after both lanes pass. `.github\workflows\squad-release.yml` builds the same `HAS_SKIA` publish outputs for GitHub Releases and attaches the Windows and Linux packages to the published release.

- Publish triggers: pushes to `master`, pushes to `feature/*`, `v*` tag pushes, and manual `workflow_dispatch`
- Pull requests remain validation-only; they do not upload application artifacts
- Shipped configuration: `dotnet publish` in `Release` with `HAS_SKIA`, so the downloadable artifacts include the PDF code path instead of the plain HTML-fallback-only build
- Published runtimes: `win-x64` and `linux-x64`
- Artifact names follow `wwa-cli-release-has-skia-<rid>`
- GitHub Releases receive `lumber-sizer-<tag>-win-x64.zip` and `lumber-sizer-<tag>-linux-x64.tar.gz` when a release is published, or when `squad-release.yml` is run manually for an existing tag
- The published outputs are framework-dependent and `--self-contained false`, so the matching .NET 10 runtime must be installed on the target machine
- PDF export from those artifacts also depends on native platform libraries; the current Skia CI lane installs Linux packages `libfontconfig1`, `libfreetype6`, `libx11-6`, `libxrandr2`, `libxrender1`, and `libxext6`, macOS Homebrew packages `fontconfig`, `freetype`, `cairo`, and `libpng`, and Windows `vcredist140`
- If those native dependencies are missing, the published app may still start, but `export-pdf` can fall back to writing HTML instead of a PDF

## Current scope vs roadmap

Today the repository is a local CLI packing/export tool. The PRD describes broader goals such as agentic reasoning, lumber-yard integration, SQLite persistence, and a future GUI, but those are not wired into the current executable.
