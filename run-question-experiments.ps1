# PowerShell script to run question-based IFC experiments
# Usage: .\run-question-experiments.ps1 [models-folder] [output-file]

param(
    [string]$ModelsFolder = "ExperimentRunner\models",
    [string]$OutputFile = "experiment_results.csv"
)

Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║       Running IFC Question-Based Experiments            ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""
Write-Host "Models folder: $ModelsFolder" -ForegroundColor Yellow
Write-Host "Output file:   $OutputFile" -ForegroundColor Yellow
Write-Host ""

# Check if models folder exists
if (-not (Test-Path $ModelsFolder)) {
    Write-Host "[ERROR] Models folder not found: $ModelsFolder" -ForegroundColor Red
    Write-Host "Please specify a valid models folder path." -ForegroundColor Red
    exit 1
}

# Count IFC files
$ifcFiles = Get-ChildItem -Path $ModelsFolder -Filter "*.ifc" -File
if ($ifcFiles.Count -eq 0) {
    Write-Host "[ERROR] No IFC files found in: $ModelsFolder" -ForegroundColor Red
    exit 1
}

Write-Host "Found $($ifcFiles.Count) IFC file(s) to process" -ForegroundColor Green
Write-Host ""
Write-Host "Questions to be asked:" -ForegroundColor Cyan
Write-Host "  Q1: how many storeys does the building have?" -ForegroundColor Gray
Write-Host "  Q2: what is the total area of all building storeys?" -ForegroundColor Gray
Write-Host "  Q3: what is the building height?" -ForegroundColor Gray
Write-Host "  Q4: does the building have a balcony?" -ForegroundColor Gray
Write-Host "  Q5: does the building have an attic?" -ForegroundColor Gray
Write-Host "  Q6: what is the maximum roof height of all building storeys?" -ForegroundColor Gray
Write-Host "  Q7: how many building storeys are beneath ground level?" -ForegroundColor Gray
Write-Host ""
Write-Host "Total questions per model: 7" -ForegroundColor Yellow
Write-Host "Total experiment runs: $($ifcFiles.Count * 7)" -ForegroundColor Yellow
Write-Host ""
Write-Host "⚠️  Note: This will use OpenAI API and may take several minutes" -ForegroundColor Yellow
Write-Host "    (includes 3 second delays between questions to avoid rate limits)" -ForegroundColor Yellow
Write-Host ""

# Ask for confirmation
$confirmation = Read-Host "Continue? (Y/N)"
if ($confirmation -ne 'Y' -and $confirmation -ne 'y') {
    Write-Host "Experiment cancelled." -ForegroundColor Yellow
    exit 0
}

Write-Host ""
Write-Host "Starting experiments..." -ForegroundColor Green
Write-Host ""

# Run the experiments
$startTime = Get-Date
dotnet run --project ExperimentRunner\ExperimentRunner.csproj $ModelsFolder $OutputFile
$endTime = Get-Date
$duration = $endTime - $startTime

# Check if successful
if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "════════════════════════════════════════════════════════════" -ForegroundColor Green
    Write-Host "✓ Experiments complete!" -ForegroundColor Green
    Write-Host "════════════════════════════════════════════════════════════" -ForegroundColor Green
    Write-Host ""
    Write-Host "Duration: $($duration.ToString('hh\:mm\:ss'))" -ForegroundColor Cyan
    Write-Host "Results saved to: $OutputFile" -ForegroundColor Cyan
    Write-Host "Full path: $(Resolve-Path $OutputFile)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Quick open commands:" -ForegroundColor Cyan
    Write-Host "  Open in Excel: start $OutputFile" -ForegroundColor Gray
    Write-Host "  Open in VS Code: code $OutputFile" -ForegroundColor Gray
    Write-Host "  Open folder: explorer $(Split-Path $OutputFile -Parent)" -ForegroundColor Gray
    Write-Host ""

    # Show quick summary if file exists
    if (Test-Path $OutputFile) {
        $lineCount = (Get-Content $OutputFile).Count - 1  # Subtract header
        Write-Host "Quick Stats:" -ForegroundColor Cyan
        Write-Host "  Total result records: $lineCount" -ForegroundColor Gray
        Write-Host ""
    }
} else {
    Write-Host ""
    Write-Host "✗ Experiments failed with exit code: $LASTEXITCODE" -ForegroundColor Red
    Write-Host ""
}