# IFC FireBot - Experiment Runner

## Overview
The Experiment Runner provides two modes for analyzing IFC models:

1. **Question-Based Mode** - Benchmarks the bot's question-answering capabilities using AI
2. **General Analysis Mode** - Performs structural and quality analysis without AI

Both modes can process multiple IFC models in batch and export detailed results.

## Quick Start

### Using PowerShell Scripts (Recommended)

**Question-Based Experiments:**
```powershell
.\run-question-experiments.ps1
```

**General Analysis:**
```powershell
.\run-general-analysis.ps1
```

See [EXPERIMENT_SCRIPTS_GUIDE.md](../EXPERIMENT_SCRIPTS_GUIDE.md) for detailed usage.

### Using Direct Commands

**Question-Based Mode:**
```bash
dotnet run --project ExperimentRunner\ExperimentRunner.csproj [models-folder] [output-csv]
```
The program will prompt for your HuggingFace API key on startup (input is masked).  
Alternatively, set the `HUGGINGFACE_TOKEN` environment variable to skip the prompt:
```powershell
$env:HUGGINGFACE_TOKEN = "hf_your_token_here"
dotnet run --project ExperimentRunner\ExperimentRunner.csproj
```

**General Analysis Mode** (no API key needed):
```bash
dotnet run --project ExperimentRunner\ExperimentRunner.csproj --general [models-folder] [output-folder]
```

## Folder Structure
```
ExperimentRunner/
├── Program.cs                      # Main entry point
├── ExperimentRunner.cs             # Question-based experiment logic
├── GeneralAnalysisRunner.cs        # General analysis logic
├── Question.cs                     # Question definition
├── ResultRecord.cs                 # Question-based result structure
├── CsvExporter.cs                  # CSV export for question-based mode
├── JsonAnalysisExporter.cs         # JSON export for general analysis
├── CsvAnalysisExporter.cs          # CSV export for general analysis
└── models/                         # IFC models folder
    ├── building1.ifc
    ├── building2.ifc
    └── ...
```

## Mode 1: Question-Based Experiments

### What It Does
- Tests the AI's ability to answer natural language questions
- Uses OpenAI API to interpret questions and calculate answers
- Provides confidence indicators for each answer
- Analyzes data quality issues that may affect answer accuracy

### Usage

Default paths:
```bash
dotnet run
```

Custom paths:
```bash
dotnet run path/to/models output_results.csv
```

### Benchmark Questions
Default questions defined in `Program.cs`:
- Q1: How many storeys does the building have?
- Q2: What is the total area of all building storeys?
- Q3: What is the building height?
- Q4: Does the building have a balcony?
- Q5: Does the building have an attic?
- Q6: What is the maximum roof height of all building storeys?
- Q7: How many building storeys are beneath ground level?

### Output
Single CSV file: `experiment_results.csv`

**Columns:**
- **ModelName**: IFC file name
- **QuestionId**: Question identifier (Q1, Q2, etc.)
- **Question**: Full question text
- **Answer**: Bot's calculated answer
- **TargetEntityType**: IFC entity type (IfcBuildingStorey, etc.)
- **EntityCount**: Number of entities found
- **GeometryMissing**: Count of entities without geometry
- **PropertiesMissing**: Count of entities without properties
- **UnnamedEntities**: Count of unnamed entities
- **ConfidenceLevel**: High/Medium/Low confidence rating
- **TotalIssues**: Total analysis issues found
- **ErrorCount/WarningCount**: Issues by severity
- **DetectedIssueCategories**: Categories of detected issues

### Requirements
- OpenAI API key configured
- ~3 seconds per question (rate limiting)
- For 10 models × 7 questions = ~3.5 minutes

## Mode 2: General Analysis

### What It Does
- Performs structural and quality analysis on all models
- Checks geometry, properties, naming, structure, semantics
- No AI required - analyzes IFC structure directly
- Generates multiple output formats for different analysis needs

### Usage

Default paths:
```bash
dotnet run --general
```

Custom paths:
```bash
dotnet run --general path/to/models output_folder
```

Or use the PowerShell script:
```powershell
.\run-general-analysis.ps1 ExperimentRunner\models analysis_output
```

### Analysis Coverage

The general analysis checks for:

**Structure:**
- Missing building storeys
- Missing stairs
- Unusual storey heights
- Entity hierarchy issues

**Geometry:**
- Elements without geometric representation
- Breakdown by element type

**Properties:**
- Missing property sets
- Standard property set compliance

**Naming:**
- Unnamed entities
- Naming consistency

**Semantic:**
- High number of proxy elements
- Classification issues

### Output

Six timestamped files in the output folder:

1. **analysis_full_[timestamp].json** - Complete analysis with all details
2. **analysis_summary_[timestamp].json** - Condensed summary
3. **analysis_summary_[timestamp].csv** - Model-level summary (Excel-ready)
4. **analysis_issues_[timestamp].csv** - All issues with full details
5. **analysis_categories_[timestamp].csv** - Matrix of models vs issue categories
6. **analysis_confidence_[timestamp].csv** - Confidence indicators and metrics

### Requirements
- No API key needed
- Fast execution (~2 seconds per model)
- No rate limiting

## Comparison

| Feature | Question-Based | General Analysis |
|---------|---------------|------------------|
| **Speed** | Slow (~21s per model) | Fast (~2s per model) |
| **API Required** | Yes (OpenAI) | No |
| **Cost** | Uses API credits | Free |
| **Purpose** | Answer specific questions | Quality assessment |
| **Output** | 1 CSV file | 6 files (CSV + JSON) |

## Documentation

- **Quick Reference**: See [../EXPERIMENT_SCRIPTS_GUIDE.md](../EXPERIMENT_SCRIPTS_GUIDE.md)
- **General Analysis Details**: See [GENERAL_ANALYSIS_README.md](GENERAL_ANALYSIS_README.md)
- **Implementation Details**: See [../IMPLEMENTATION_SUMMARY.md](../IMPLEMENTATION_SUMMARY.md)

## Tips

### For Benchmark Testing
Use question-based mode to test AI capabilities:
```powershell
.\run-question-experiments.ps1
```

### For Quality Control
Use general analysis for fast, free quality checks:
```powershell
.\run-general-analysis.ps1
```

### For Complete Assessment
Run both modes to get comprehensive insights:
```powershell
# Fast quality check first
.\run-general-analysis.ps1

# Then detailed Q&A analysis
.\run-question-experiments.ps1
```
- **ErrorCount**: Critical errors found
- **WarningCount**: Warnings found

## Customization

### Add Custom Questions
Edit `Program.cs`:
```csharp
var questions = new List<Question> { new Question("Q9", "your custom question here?"), // ... };
```

### Modify Confidence Indicators
Edit `ResultRecord.cs` to add custom metrics based on your needs.

## Integration with ifcFireBot
The runner integrates directly with:
- **AI System** (`Ai.cs`, `PromptBuilder.cs`): Parses questions into intents
- **IFC Fetcher** (`FetchIfcEnteties.cs`): Retrieves IFC data
- **Calculator** (`Calculate.cs`): Computes answers
- **Analyzer** (`IFCAnalyzer`): Evaluates model quality and confidence

## Requirements
- .NET 8 SDK
- HuggingFace API token (entered interactively when the program starts, or pre-set as
  the `HUGGINGFACE_TOKEN` environment variable for automated/batch use)
- IFC models in the `models/` folder