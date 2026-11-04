# General Analysis Experiment

This folder contains tools to run general IFC analysis on all models without specific questions.

## Files

- **RunGeneralAnalysis.cs** - Main program to run batch analysis on all models
- **GeneralAnalysisRunner.cs** - Core analysis logic
- **JsonAnalysisExporter.cs** - Export results to JSON format
- **CsvAnalysisExporter.cs** - Export results to CSV format
- **ModelAnalysisRecord.cs** (in GeneralAnalysisRunner.cs) - Data model for analysis results

# General Analysis Experiment

This experiment runs general IFC analysis on all models without specific questions.

## Usage

The ExperimentRunner now supports two modes:

### 1. Question-Based Mode (Original)

Runs experiments with specific questions on all models.  
**Requires a HuggingFace API key** — the program will prompt for it on startup
(input is masked). You can also pre-set the `HUGGINGFACE_TOKEN` environment variable
to skip the prompt:
```powershell
$env:HUGGINGFACE_TOKEN = "hf_your_token_here"
```

Then run:
```bash
dotnet run --project ExperimentRunner\ExperimentRunner.csproj
```

Or with custom folders:
```bash
dotnet run --project ExperimentRunner\ExperimentRunner.csproj <models-folder> <output-file>
```

### 2. General Analysis Mode (New)

Runs general IFC analysis without questions on all models:

```bash
dotnet run --project ExperimentRunner\ExperimentRunner.csproj --general
```

With custom folders:
```bash
dotnet run --project ExperimentRunner\ExperimentRunner.csproj --general <models-folder> <output-folder>
```

#### Examples

Using default folders (models → analysis_output):
```bash
dotnet run --project ExperimentRunner\ExperimentRunner.csproj --general
```

Using the ExperimentRunner\models folder:
```bash
dotnet run --project ExperimentRunner\ExperimentRunner.csproj --general ExperimentRunner\models analysis_output
```

Custom paths:
```bash
dotnet run --project ExperimentRunner\ExperimentRunner.csproj --general "C:\MyModels" "C:\Results"
```

## Output Files

The program generates multiple output files with timestamps:

1. **analysis_full_YYYYMMDD_HHMMSS.json**
   - Complete analysis results in JSON format
   - Includes all issues, confidence indicators, and metadata
   - Best for programmatic processing

2. **analysis_summary_YYYYMMDD_HHMMSS.json**
   - Condensed summary in JSON format
   - Overview of each model's analysis
   - Good for quick review

3. **analysis_summary_YYYYMMDD_HHMMSS.csv**
   - Summary in CSV format
   - Easy to open in Excel/Sheets
   - Columns: ModelName, Status, ConfidenceLevel, TotalIssues, ErrorCount, WarningCount, InfoCount

4. **analysis_issues_YYYYMMDD_HHMMSS.csv**
   - All issues listed with details
   - Columns: ModelName, Severity, Category, Description, GlobalId
   - Useful for filtering and sorting issues

5. **analysis_categories_YYYYMMDD_HHMMSS.csv**
   - Issues grouped by category for each model
   - Shows count of issues in each category (Structure, Geometry, Properties, etc.)
   - Good for comparing models

6. **analysis_confidence_YYYYMMDD_HHMMSS.csv**
   - Confidence indicators with metadata
   - Shows data quality metrics for each model

## Example Output

Console output shows progress and summary:

```
╔══════════════════════════════════════════════════════════╗
║          IFC General Analysis - Batch Runner             ║
╚══════════════════════════════════════════════════════════╝

Running general analysis on all models in: models
Found 10 IFC model(s) to analyze

[1/10] Analyzing: building_01.ifc
  ✓ Complete - 15 issues found (2 errors, 8 warnings)

[2/10] Analyzing: AC20-FZK-Haus.ifc
  ✓ Complete - 8 issues found (0 errors, 5 warnings)

...

═══════════════════════════════════════════════════════════
ANALYSIS SUMMARY
═══════════════════════════════════════════════════════════
Total models analyzed: 10
Successful: 10
Failed: 0

Total issues found: 127
  - Errors: 15
  - Warnings: 85
  - Infos: 27

All results saved to: C:\path\to\analysis_output
═══════════════════════════════════════════════════════════
```

## What Gets Analyzed

The general analysis checks:

- **Structure**: Building storeys, missing entities, hierarchy issues
- **Geometry**: Elements without geometric representation
- **Properties**: Missing property sets
- **Naming**: Unnamed entities, naming consistency
- **Semantic**: Proxy elements, classification issues

For each model, you'll get:
- Total issue count by severity (Error/Warning/Info)
- Issues grouped by category
- Confidence indicators showing data quality metrics
- Detailed issue list with descriptions
