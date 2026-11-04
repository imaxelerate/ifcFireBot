# Running Experiments - Quick Reference

This document explains the two PowerShell scripts available for running IFC experiments.

## Overview

There are two experiment modes available:

1. **Question-Based Experiments** - Uses AI to answer specific questions about each model
2. **General Analysis** - Performs structural/quality analysis on all models

## 🤖 Question-Based Experiments

**Script:** `run-question-experiments.ps1`

### What It Does
- Asks 7 predefined questions about each IFC model
- Uses OpenAI API to interpret questions and calculate answers
- Analyzes data quality and confidence for each answer
- Generates a CSV file with all results

### Questions Asked
1. How many storeys does the building have?
2. What is the total area of all building storeys?
3. What is the building height?
4. Does the building have a balcony?
5. Does the building have an attic?
6. What is the maximum roof height of all building storeys?
7. How many building storeys are beneath ground level?

### Usage

Default settings:
```powershell
.\run-question-experiments.ps1
```

Custom folders/files:
```powershell
.\run-question-experiments.ps1 "ExperimentRunner\models" "my_results.csv"
```

With full paths:
```powershell
.\run-question-experiments.ps1 "C:\MyModels" "C:\Results\output.csv"
```

### Output
Single CSV file: `experiment_results.csv`

Columns include:
- ModelName, QuestionId, Question, Answer
- TargetEntityType, EntityCount
- ConfidenceLevel, TotalIssues, ErrorCount, WarningCount
- GeometryMissing, PropertiesMissing, UnnamedEntities
- DetectedIssueCategories

### Time Required
- Uses OpenAI API (requires API key)
- ~3 seconds per question (rate limiting)
- For 10 models × 7 questions = ~3.5 minutes
- Requires confirmation before starting

---

## 📊 General Analysis

**Script:** `run-general-analysis.ps1`

### What It Does
- Performs structural and quality analysis on all models
- No AI required - analyzes IFC structure directly
- Checks geometry, properties, naming, structure, semantics
- Generates 6 different output files

### Usage

Default settings:
```powershell
.\run-general-analysis.ps1
```

Custom folders:
```powershell
.\run-general-analysis.ps1 "ExperimentRunner\models" "analysis_output"
```

With full paths:
```powershell
.\run-general-analysis.ps1 "C:\MyModels" "C:\Results"
```

### Output
6 timestamped files in the output folder:

1. `analysis_full_[timestamp].json` - Complete details
2. `analysis_summary_[timestamp].json` - Summary overview
3. `analysis_summary_[timestamp].csv` - Model summary table
4. `analysis_issues_[timestamp].csv` - All issues with details
5. `analysis_categories_[timestamp].csv` - Issues by category matrix
6. `analysis_confidence_[timestamp].csv` - Quality metrics

### Time Required
- Fast - no API calls
- ~1-2 seconds per model
- For 10 models = ~15-30 seconds
- No confirmation required

---

## Comparison

| Feature | Question-Based | General Analysis |
|---------|---------------|------------------|
| **Speed** | Slow (~21s per model) | Fast (~2s per model) |
| **API Required** | Yes (OpenAI) | No |
| **Cost** | Uses API credits | Free |
| **Purpose** | Answer specific questions | Overall quality assessment |
| **Output Format** | 1 CSV file | 6 files (CSV + JSON) |
| **Use Case** | Benchmark testing, Q&A | Quality control, auditing |

---

## Common Use Cases

### Scenario 1: Benchmark Testing
**Use:** Question-Based Experiments  
**Why:** Need to test AI's ability to answer natural language questions

```powershell
.\run-question-experiments.ps1 "test_models" "benchmark_results.csv"
```

### Scenario 2: Quality Control
**Use:** General Analysis  
**Why:** Need to check model quality without API costs

```powershell
.\run-general-analysis.ps1 "production_models" "quality_reports"
```

### Scenario 3: Complete Assessment
**Use:** Both  
**Why:** Want both Q&A capabilities and quality metrics

```powershell
# First run general analysis (fast, free)
.\run-general-analysis.ps1

# Then run question experiments (slower, uses API)
.\run-question-experiments.ps1
```

---

## Manual Commands

If you prefer not to use the scripts, here are the direct commands:

### Question-Based (manual)
```bash
dotnet run --project ExperimentRunner\ExperimentRunner.csproj ExperimentRunner\models results.csv
```

### General Analysis (manual)
```bash
dotnet run --project ExperimentRunner\ExperimentRunner.csproj --general ExperimentRunner\models analysis_output
```

---

## Troubleshooting

### "No IFC files found"
- Check that the models folder path is correct
- Ensure there are `.ifc` files in the folder

### "API key not found" (Question-Based only)
- Set OpenAI API key in environment variable or config
- See main project documentation for API setup

### Script won't run
- Ensure PowerShell execution policy allows scripts:
  ```powershell
  Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
  ```

### Wrong folder path
- Use full paths if relative paths don't work
- Use quotes around paths with spaces

---

## Next Steps

- For detailed documentation on general analysis: See `ExperimentRunner/GENERAL_ANALYSIS_README.md`
- For implementation details: See `IMPLEMENTATION_SUMMARY.md`
- For quick start guide: See `QUICK_START_GENERAL_ANALYSIS.md`
