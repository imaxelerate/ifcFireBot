using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ifcFireAnalyzer;

namespace ExperimentRunner
{
    /// <summary>
    /// Runs general analysis on all IFC models without any specific questions
    /// </summary>
    public class GeneralAnalysisRunner
    {
        /// <summary>
        /// Run general analysis on all models in the specified folder
        /// </summary>
        public static List<ModelAnalysisRecord> RunAllModels(string modelsFolder)
        {
            var results = new List<ModelAnalysisRecord>();
            var files = Directory.GetFiles(modelsFolder, "*.ifc");

            if (!files.Any())
            {
                Console.WriteLine($"[ERROR] No IFC files found in folder: {modelsFolder}");
                return results;
            }

            Console.WriteLine($"Found {files.Length} IFC model(s) to analyze");
            Console.WriteLine();

            int modelIndex = 1;
            foreach (var file in files)
            {
                var modelName = Path.GetFileName(file);
                Console.WriteLine($"[{modelIndex}/{files.Length}] Analyzing: {modelName}");

                try
                {
                    var record = AnalyzeModel(file, modelName);
                    results.Add(record);

                    Console.WriteLine($"  ✓ Complete - {record.TotalIssues} issues found ({record.ErrorCount} errors, {record.WarningCount} warnings)");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ✗ Failed: {ex.Message}");

                    // Add error record
                    results.Add(new ModelAnalysisRecord
                    {
                        ModelName = modelName,
                        FilePath = file,
                        AnalysisStatus = "FAILED",
                        ErrorMessage = ex.Message
                    });
                }

                Console.WriteLine();
                modelIndex++;
            }

            return results;
        }

        /// <summary>
        /// Analyze a single model
        /// </summary>
        private static ModelAnalysisRecord AnalyzeModel(string filePath, string modelName)
        {
            var analyzer = new IFCAnalyzer();

            // Run general analysis (no target entity type)
            var analysisResult = analyzer.Analyze(filePath, targetEntityType: null);

            var record = new ModelAnalysisRecord
            {
                ModelName = modelName,
                FilePath = filePath,
                AnalysisStatus = "SUCCESS",
                ConfidenceLevel = analysisResult.ConfidenceLevel,
                TotalIssues = analysisResult.Issues.Count,
                ErrorCount = analysisResult.Issues.Count(i => i.Severity == "ERROR"),
                WarningCount = analysisResult.Issues.Count(i => i.Severity == "WARNING"),
                InfoCount = analysisResult.Issues.Count(i => i.Severity == "INFO")
            };

            // Group issues by category
            record.IssuesByCategory = analysisResult.Issues
                .Where(i => !string.IsNullOrEmpty(i.Category))
                .GroupBy(i => i.Category)
                .ToDictionary(g => g.Key, g => g.Count());

            // Group issues by severity
            record.IssuesBySeverity = analysisResult.Issues
                .Where(i => !string.IsNullOrEmpty(i.Severity))
                .GroupBy(i => i.Severity)
                .ToDictionary(g => g.Key, g => g.Count());

            // Collect all issues as detailed list
            record.AllIssues = analysisResult.Issues
                .Select(i => new IssueDetail
                {
                    Severity = i.Severity,
                    Category = i.Category,
                    Description = i.Message,
                    GlobalId = i.EntityId
                })
                .ToList();

            // Collect confidence indicators
            record.ConfidenceIndicators = analysisResult.ConfidenceIndicators
                .Select(ci => new ConfidenceIndicatorDetail
                {
                    Category = ci.Category,
                    Description = ci.Description,
                    Metadata = ci.Metadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString() ?? "")
                })
                .ToList();

            return record;
        }
    }

    /// <summary>
    /// Record for each analyzed model
    /// </summary>
    public class ModelAnalysisRecord
    {
        public string ModelName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string AnalysisStatus { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }

        public string ConfidenceLevel { get; set; } = string.Empty;
        public int TotalIssues { get; set; }
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public int InfoCount { get; set; }

        public Dictionary<string, int> IssuesByCategory { get; set; } = new();
        public Dictionary<string, int> IssuesBySeverity { get; set; } = new();

        public List<IssueDetail> AllIssues { get; set; } = new();
        public List<ConfidenceIndicatorDetail> ConfidenceIndicators { get; set; } = new();
    }

    /// <summary>
    /// Detailed issue information
    /// </summary>
    public class IssueDetail
    {
        public string Severity { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? GlobalId { get; set; }
    }

    /// <summary>
    /// Confidence indicator information
    /// </summary>
    public class ConfidenceIndicatorDetail
    {
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, string> Metadata { get; set; } = new();
    }
}
