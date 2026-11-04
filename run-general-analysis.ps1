# PowerShell script to run general IFC analysis
# Usage: .\run-general-analysis.ps1 [models-folder] [output-folder]

param(
    [string]$ModelsFolder = "ExperimentRunner\models",
    [string]$OutputFolder = "analysis_output"
)

Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║       Running IFC General Analysis                      ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""
Write-Host "Models folder: $ModelsFolder" -ForegroundColor Yellow
Write-Host "Output folder: $OutputFolder" -ForegroundColor Yellow
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

Write-Host "Found $($ifcFiles.Count) IFC file(s) to analyze" -ForegroundColor Green
Write-Host ""

# Run the analysis
dotnet run --project ExperimentRunner\ExperimentRunner.csproj --general $ModelsFolder $OutputFolder

# Check if successful
if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "✓ Analysis complete! Check the output folder for results." -ForegroundColor Green
    Write-Host ""
    Write-Host "Quick open commands:" -ForegroundColor Cyan
    Write-Host "  explorer $OutputFolder" -ForegroundColor Gray
    Write-Host "  code $OutputFolder" -ForegroundColor Gray
}
else {
    Write-Host ""
    Write-Host "✗ Analysis failed with exit code: $LASTEXITCODE" -ForegroundColor Red
}
