# Quick Start: General IFC Analysis

> **No API key required.** General analysis reads IFC files directly and does not call any AI service.  
> If you need question-based AI experiments instead, see [README.md](README.md).

## Run General Analysis on All Models

This will analyze all IFC models in the specified folder and generate comprehensive reports.

### Basic Command

```bash
dotnet run --project ExperimentRunner\ExperimentRunner.csproj --general ExperimentRunner\models analysis_output
```

### What You Get

The analysis generates 6 different output files:

1. **analysis_full_[timestamp].json** - Complete results with all details
2. **analysis_summary_[timestamp].json** - Condensed summary
3. **analysis_summary_[timestamp].csv** - Overview table (open in Excel)
4. **analysis_issues_[timestamp].csv** - All issues with details
5. **analysis_categories_[timestamp].csv** - Issues grouped by category
6. **analysis_confidence_[timestamp].csv** - Data quality metrics

### Analysis Includes

For each model, the system checks:

- ✓ **Structure** - Building storeys, entity hierarchy
- ✓ **Geometry** - Geometric representation completeness
- ✓ **Properties** - Property sets and data
- ✓ **Naming** - Entity naming consistency
- ✓ **Semantic** - Classification and proxy elements

### Example Output

```
═══════════════════════════════════════════════════════════
ANALYSIS SUMMARY
═══════════════════════════════════════════════════════════
Total models analyzed: 10
Successful: 10
Failed: 0

Total issues found: 122
  - Errors: 0
  - Warnings: 90
  - Infos: 32

All results saved to: C:\path\to\analysis_output
═══════════════════════════════════════════════════════════
```

## Next Steps

- Open the CSV files in Excel/Sheets for easy filtering and analysis
- Use the JSON files for programmatic processing
- Review issues by category to identify patterns across models

See [ExperimentRunner/GENERAL_ANALYSIS_README.md](ExperimentRunner/GENERAL_ANALYSIS_README.md) for detailed documentation.
