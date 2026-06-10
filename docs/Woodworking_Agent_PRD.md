
# Product Requirements Document: Woodworking Agent (WWA)

## 1. Executive Summary
The Woodworking Agent (WWA) is a cross-platform, local-first C# application designed to optimize lumber procurement and board cutting. It enables woodworkers to input project cut lists, calculate optimal board usage based on lumber yard inventory, and generate detailed, visual PDF cut sheets—all while respecting custom constraints like preserving long remnants for future projects.

## 2. Goals & Objectives
* **Optimize Lumber Yield:** Minimize wood waste using local bin-packing algorithms.
* **Preserve Remnants:** Allow users to enforce constraints (e.g., "save 8ft sections") to keep usable inventory for future carving/projects.
* **Local-First Privacy:** Keep all project data, recipes, and carving patterns local to the user's machine using secure sandboxing.
* **Cross-Platform:** Ensure parity in logic and output across Windows and macOS.

## 3. Core Features
### 3.1. Project Input & Logic
* **Cut List Definition:** Ability to input required dimensions via text-based files.
* **Agentic Reasoning:** Use **Semantic Kernel** (or Microsoft Agent Framework) with local LLMs (Phi-3.5) to interpret dimensions and optimize layout.
* **Constraint Enforcement:** Support custom rules (e.g., remnant length requirements).

### 3.2. Lumber Yard Integration
* **Local Web IQ:** Use agentic tools to scan local lumber yard inventories for available dimensions and stock.

### 3.3. Output Generation
* **PDF Cut Sheets:** Generate professional-grade PDFs using **QuestPDF**.
* **Visual Diagrams:** Include visual representations of board cuts, clearly marking saved remnants.

## 4. Technical Stack
* **Language:** C# (.NET 8/9).
* **Agent Logic:** Microsoft Agent Framework (MAF) / Semantic Kernel.
* **Inference Engine:** ONNX Runtime (running Phi-3.5 or Aion locally).
* **PDF Generation:** QuestPDF (SkiaSharp-based, cross-platform).
* **Data Storage:** SQLite (Local storage).
* **Cross-Platform UI (Future):** Avalonia UI or Uno Platform.
* **Development Phase 1:** CLI-based orchestration.

## 5. User Workflow
1. **Define:** User creates a text file with required wood dimensions.
2. **Optimize:** The WWA app calculates the best "bin-packing" scenario based on lumber yard availability and remnant constraints.
3. **Execute:** The app generates a `Project_CutList.pdf` showing exactly which boards to buy and where to make cuts.
4. **Acquire:** User takes the PDF to the lumber yard to purchase the exact material needed.

## 6. Constraints & Requirements
* **Privacy:** No cloud-based processing of project designs or inventory lists.
* **Environment:** Must run on Windows (NPU/GPU optimized) and macOS (Apple Silicon).
* **Reliability:** The bin-packing logic must be deterministic and reproducible.

## 7. Roadmap
* **Milestone 1:** CLI tool with basic bin-packing logic and console output.
* **Milestone 2:** Integration with QuestPDF for visual cut-sheet generation.
* **Milestone 3:** Integration of Semantic Kernel/LLM for advanced constraint reasoning.
* **Milestone 4:** GUI development using Avalonia.
