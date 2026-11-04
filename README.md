# ifcFireBot

A C# / .NET 8 command-line tool that answers plain-language questions about IFC building models, and warns when the model's data quality makes an answer unreliable.

Built as my master's thesis (20 weeks, 2026) together with a fire-safety engineering firm and an IT consultancy.
Thesis: [read it on DiVA](https://umu.diva-portal.org/smash/record.jsf?pid=diva2:2080611)

---

## Why it exists

Before fire-safety engineers can write fire-protection documentation, they answer the same questions about every new building model: How many storeys? Is there a balcony? What is the building height? Today they find these answers by hand in the IFC file, which takes time for every project.

The information is usually in the model. Extracting it reliably is the hard part, because different tools and authors encode the same building in different ways. A wrong answer can have regulatory consequences, so the tool has to say when an answer can be trusted, not just give one.

## The key design decision

**The language model never produces the answer.**

```
Question in plain language
        │
        ▼
LLM (Llama 3 8B) ── turns the question into a structured YAML intent
        │
        ▼
Deterministic C# query engine ── fetches and calculates every fact from the IFC model
        │
        ├──► Answer
        └──► Data-quality diagnostics ── warns about issues that may affect this answer
```

- The LLM handles what it is good at: understanding language.
- The query engine handles the facts. Given the same intent and model, it returns the same result, and each answer can be traced back to elements in the model.
- The LLM output is constrained to a fixed set of entity types and question types, with few-shot examples and a low temperature, so it can be parsed reliably.

## What the evaluation showed

The system was evaluated on 70 runs (7 benchmark questions × 10 IFC models) against ground truth checked by a fire-safety engineer. All 70 intermediate YAML intents were inspected by hand.

The main finding: **accuracy was limited mainly by the data in the models, not by the query logic.** Three mechanisms caused most failures:

1. **Misclassified elements.** A staircase saved as a generic `IfcBuildingElementProxy` is invisible to a query for `IfcStair`.
2. **Varying modelling conventions.** The same storey can be named `Ground Floor`, `Level 01` or `0` depending on the author.
3. **Missing information.** The element exists, but the property the question depends on was never filled in.

Adding more fallback rules to the query engine could not fix these. So instead of continuing to patch the engine, I built the diagnostics module (`ifcFireAnalyzer`) that detects these conditions and reports them alongside each answer. Full results are in the thesis.

**Status:** research prototype, delivered to both partners in July 2026. It is not production software and is not being developed further.

---

## Table of contents

- [Modes](#modes)
- [Solution structure](#solution-structure)
- [Prerequisites](#prerequisites)
- [Building the solution](#building-the-solution)
- [Running ifcFireBot](#running-ifcfirebot)
- [Running ExperimentRunner](#running-experimentrunner)
- [Publishing a standalone executable](#publishing-a-standalone-executable)
- [Q&A pipeline in detail](#qa-pipeline-in-detail)
- [Key dependencies](#key-dependencies)
- [Documentation index](#documentation-index)

---

## Modes

### Mode 1: Analyze

Scans an IFC file and produces a structured data-quality report: missing names, missing geometry, missing property sets, duplicate GUIDs, storey assignment issues and more, grouped by severity (ERROR / WARNING / INFO).

No API key or internet connection required.

### Mode 2: Q&A

Takes natural-language questions about an IFC model and returns calculated answers, for example:

- *"How many storeys does the building have?"*
- *"What is the total area of all IfcSpace elements?"*
- *"What is the building height?"*

After each answer, a confidence section reports data-quality issues in the model that may have affected the result.

Requires a Hugging Face API key (see [USER_GUIDE.md](USER_GUIDE.md)).

---

## Solution structure

```
ifcFireBot/                         solution root
│
├── ifcFireBot/                     main console application
│   ├── Program.cs                  startup, mode selection, orchestration
│   └── Models/
│       ├── Ai.cs                   Hugging Face API connection
│       ├── PromptBuilder.cs        system prompt and YAML response parser
│       ├── Intent.cs               parsed question intent (entity, type, filter)
│       ├── FetchIfcEnteties.cs     retrieves IFC elements by intent
│       ├── Calculate.cs            dispatches to the right calculator
│       ├── ICalculator.cs          common interface for all calculators
│       ├── Calculators/            one class per question type
│       │   ├── AreaCalculator.cs
│       │   ├── BuildingHeightCalculator.cs
│       │   ├── CountCalculator.cs
│       │   ├── ExistsCalculator.cs
│       │   ├── HeightCalculator.cs
│       │   ├── ListCalculator.cs
│       │   ├── UndergroundFloorCalculator.cs
│       │   └── VolumeCalculator.cs
│       ├── Helpers/                IFC-specific utilities
│       │   ├── ElevationHelper.cs
│       │   ├── GeometryHelper.cs
│       │   ├── IfcPropertyHelper.cs
│       │   └── StoreyFilterHelper.cs
│       ├── IOutputWriter.cs        output abstraction
│       ├── ConsoleOutputWriter.cs  coloured console output
│       ├── StructuredOutputWriter.cs  structured output for batch runs
│       └── AllowedValues.cs        valid IFC entity names and question types
│
├── ifcFireAnalyzer/                class library: data-quality analysis engine
│   ├── IFCAnalyzer.cs              analysis entry point (Analyze method)
│   ├── AnalysisResult.cs           result model (issues and confidence indicators)
│   ├── Issue.cs                    single issue (severity, category, description)
│   ├── ConfidenceIndicator.cs      confidence metric with metadata
│   └── ReportGenerator.cs          console report printer
│
├── ExperimentRunner/               batch benchmark tool (developer use)
│   ├── Program.cs                  entry point, two modes (Q&A batch / general analysis)
│   ├── ExperimentRunner.cs         question-based experiments
│   ├── GeneralAnalysisRunner.cs    batch data-quality analysis
│   ├── CsvExporter.cs, CsvAnalysisExporter.cs, JsonAnalysisExporter.cs
│   ├── example_output.csv          illustrative output format (made-up data)
│   └── models/                     put your own IFC models here (not committed)
│
├── System and Solution Architectures Graphs/   Mermaid architecture diagrams
├── DEVELOPER_SETUP.md
├── USER_GUIDE.md
└── README.md
```

No building models are included. The models used in the thesis belong to the partner company.

---

## Prerequisites

| Requirement | Version |
|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | 8.0 |
| Windows | 10 or later (64-bit), required for the Xbim native libraries |
| Hugging Face account | Free, only needed for Q&A mode |

---

## Building the solution

From the repository root:

```powershell
dotnet build
```

Release build:

```powershell
dotnet build -c Release
```

The solution builds three outputs: `ifcFireAnalyzer.dll`, `ifcFireBot.exe` and `ExperimentRunner.exe`.

---

## Running ifcFireBot

```powershell
dotnet run --project ifcFireBot\ifcFireBot.csproj
```

On startup the program shows a mode selector:

```
╔══════════════════════════════════════════════╗
║          Welcome to ifcFireBot               ║
╚══════════════════════════════════════════════╝

Please select a mode:

  [1]    Analyze Mode  — scan IFC file for data quality issues (no API key needed)
  [2]    Q&A Mode      — ask questions about your IFC model using AI
  [exit] Exit          — close the program  (also works at any prompt)

Enter 1, 2, or 'exit':
```

**Analyze mode:** enter the path to an `.ifc` file and the scan runs. Then choose to analyze another file (`y`), return to the main menu (`return`) or exit (`exit`).

**Q&A mode:** enter the path to an `.ifc` file, then your Hugging Face API key (input is masked). The key is kept in memory for the session only. If you return to Q&A mode, press Enter to reuse it. It is never written to disk.

### Commands during a Q&A session

| Input | Effect |
|---|---|
| Any natural-language question | Query the IFC model |
| `debug` | Toggle debug mode (shows the raw intent and matched elements) |
| `return` | Back to the main menu |
| `exit` | Close the program |

---

## Running ExperimentRunner

A research tool for benchmarking the system against many IFC models in batch.

### Question-based batch experiments (uses the LLM)

Runs a fixed set of questions against every `.ifc` file in a folder and exports the results to CSV. See `ExperimentRunner/example_output.csv` for the column format.

```powershell
dotnet run --project ExperimentRunner\ExperimentRunner.csproj [models-folder] [output-csv]
```

The program asks for your Hugging Face API key at startup. For automated runs, set it as an environment variable first:

```powershell
$env:HUGGINGFACE_TOKEN = "hf_your_token_here"
dotnet run --project ExperimentRunner\ExperimentRunner.csproj
```

Defaults: `models\` folder, `experiment_results.csv`.

### General analysis batch (no LLM, no API key)

Runs the data-quality analyzer against every model in a folder and exports six timestamped files (CSV and JSON).

```powershell
dotnet run --project ExperimentRunner\ExperimentRunner.csproj --general [models-folder] [output-folder]
```

Defaults: `models\` folder, `analysis_output\` folder.

See [ExperimentRunner/README.md](ExperimentRunner/README.md) for full documentation.

---

## Publishing a standalone executable

Produces a single self-contained `.exe` with the .NET 8 runtime included, so the user needs nothing installed.

```powershell
dotnet publish "ifcFireBot\ifcFireBot.csproj" `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o "publish"
```

Output: `publish\ifcFireBot.exe` (about 81 MB).

> Do not publish with `-p:PublishTrimmed=true`. The Xbim libraries rely on reflection and fail at runtime when trimmed.

---

## Q&A pipeline in detail

1. **Intent parsing.** The question is sent to Meta-Llama-3-8B-Instruct through the Hugging Face router. The model returns a YAML block with `IfcEntity`, `QuestionType` and an optional `EntityFilter`.
2. **IFC fetch.** `FetchIfcEnteties` retrieves all instances of the requested entity type from the open model and applies any name filter.
3. **Calculation.** `Calculate.Answer` dispatches to one of eight calculators that implement `ICalculator`:

   | QuestionType | What it computes |
   |---|---|
   | `Count` | Number of matching elements |
   | `Exists` | Yes / no presence check |
   | `List` | Names of all matching elements |
   | `Area` | Sum of floor areas (m²), from properties or, when missing, from geometry |
   | `Volume` | Sum of volumes (m³) |
   | `Height` | Storey or space ceiling heights |
   | `BuildingHeight` | Total height from ground to roof |
   | `UndergroundFloors` | Number of storeys below ground level |

4. **Confidence check.** `IFCAnalyzer` re-scans the model for the target entity type and adds a confidence section when it finds issues that may have affected the answer.

---

## Key dependencies

| Package | Purpose |
|---|---|
| [Xbim.Essentials](https://docs.xbim.net/) | IFC parsing (IFC2x3, IFC4, IFC4x3) |
| [Betalgo.Ranul.OpenAI](https://github.com/betalgo/openai) | OpenAI-compatible client, used against the Hugging Face router |
| [YamlDotNet](https://github.com/aaubry/YamlDotNet) | Parses the YAML intent from the LLM |

**LLM:** [Meta-Llama-3-8B-Instruct](https://huggingface.co/meta-llama/Meta-Llama-3-8B-Instruct) via the Hugging Face router at `https://router.huggingface.co/v1`.

---

## Documentation index

| Document | Audience | Content |
|---|---|---|
| `README.md` | Everyone | This file |
| `USER_GUIDE.md` | End users | How to use the tool, IFC path tips, API key setup |
| `DEVELOPER_SETUP.md` | Developers | Build, publish and dependency details |
| `System and Solution Architectures Graphs/` | Developers | Mermaid diagrams of all three modules |
| `ExperimentRunner/README.md` | Developers | ExperimentRunner modes, output formats, customisation |

---

## Known limitations

- Geometry-based answers (areas, heights) are much less reliable than semantic ones (presence of a balcony, storey count), mainly because of inconsistent model data.
- Evaluated on 10 models, which is too few for strong statistical conclusions.
- No automated tests yet.
- Windows only, because of the Xbim native libraries.

## How it was built

I designed and wrote the system myself. I used AI coding assistants, mainly GitHub Copilot, and reviewed and tested every change. The partner companies provided the problem, domain expertise and test models.

## Author

Axel Forsaeus, M.Sc. in Engineering, Interaction Technology and Design, Umeå University
[LinkedIn](https://www.linkedin.com/in/axel-forsaeus-b06362138/)
