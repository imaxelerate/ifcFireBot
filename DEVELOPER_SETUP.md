# Developer Setup Guide

This document is for developers picking up the `ifcFireBot` project. It covers prerequisites, how to build and run the solution, how to publish the customer-facing exe, and how to use the ExperimentRunner.

---

## Prerequisites

| Requirement | Version | Notes |
|---|---|---|
| [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8) | 8.x | Required to build and publish |
| Visual Studio 2022+ or VS Code | Any | Solution file opens in VS 2022+ |
| HuggingFace account + API key | — | Only needed for Q&A mode and ExperimentRunner |
| Windows x64 | — | Publish target is `win-x64`; source is cross-platform |

---

## Solution Structure

```
ifcFireBot/               ← Customer-facing interactive console app
ifcFireAnalyzer/          ← IFC analysis engine (class library)
ExperimentRunner/         ← Developer batch experiment runner
```

See [README.md](README.md) for a full project map and architecture overview.

---

## Build

From the repo root:

```powershell
dotnet build
```

All three projects build together. Restore is automatic.

---

## Run (development)

```powershell
dotnet run --project ifcFireBot/ifcFireBot.csproj
```

The app will prompt for an IFC file path and (in Q&A mode) a HuggingFace API key at runtime.

---

## Publish — customer-facing single-file exe

```powershell
dotnet publish ifcFireBot/ifcFireBot.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o publish
```

Output lands in `publish/`. The files needed for the customer are:

| File | Purpose |
|---|---|
| `publish/ifcFireBot.exe` | The entire program, self-contained (~85 MB) |
| `publish/ifcFireBot.dll.config` | Assembly binding config required by Xbim |
| `USER_GUIDE.md` | End-user instructions |

See [USER_GUIDE.md](USER_GUIDE.md) for full end-user documentation.

---

## ExperimentRunner

The ExperimentRunner is a developer tool for running batch question-based experiments against a folder of IFC models. It is **not** part of the customer-facing product.

### Setup

1. Place `.ifc` model files in `ExperimentRunner/models/`
2. Set your HuggingFace token as an environment variable (optional — the runner will prompt if absent):

```powershell
$env:HUGGINGFACE_TOKEN = "hf_your_token_here"
```

3. Run:

```powershell
dotnet run --project ExperimentRunner/ExperimentRunner.csproj
```

### Output

Results are written as CSV files to the `ExperimentRunner/` folder. See `example_output.csv` for the column structure. Its models and values are made up for illustration and do not come from the thesis evaluation.

For full usage details see:
- [ExperimentRunner/README.md](ExperimentRunner/README.md)
- [ExperimentRunner/GENERAL_ANALYSIS_README.md](ExperimentRunner/GENERAL_ANALYSIS_README.md)
- [EXPERIMENT_SCRIPTS_GUIDE.md](EXPERIMENT_SCRIPTS_GUIDE.md)

---

## Key dependencies

| Package | Purpose |
|---|---|
| `Xbim.Essentials` / `Xbim.Ifc` | IFC file parsing |
| `Xbim.IO.Esent` | Esent-backed IFC model storage |
| `Betalgo.Ranul.OpenAI` | HuggingFace / OpenAI-compatible API client |
| `YamlDotNet` | Parsing structured YAML responses from the LLM |

All packages are restored automatically via NuGet on build.

---

## Branches

| Branch | Purpose |
|---|---|
| `Main` | Latest stable release — canonical production state |
| `release` | Final delivery branch — merged into Main |
| `AreaProblem` | Full development history |
