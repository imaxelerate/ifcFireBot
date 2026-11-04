using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExperimentRunner
{
    /// <summary>
    /// Export general analysis results to JSON format
    /// </summary>
    public class JsonAnalysisExporter
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// Export analysis results to JSON file
        /// </summary>
        public static void Export(string outputPath, List<ModelAnalysisRecord> records)
        {
            var exportData = new
            {
                generatedAt = DateTime.UtcNow,
                totalModels = records.Count,
                successfulAnalyses = records.Count(r => r.AnalysisStatus == "SUCCESS"),
                failedAnalyses = records.Count(r => r.AnalysisStatus == "FAILED"),
                summary = new
                {
                    totalIssues = records.Sum(r => r.TotalIssues),
                    totalErrors = records.Sum(r => r.ErrorCount),
                    totalWarnings = records.Sum(r => r.WarningCount),
                    totalInfos = records.Sum(r => r.InfoCount)
                },
                models = records
            };

            var json = JsonSerializer.Serialize(exportData, JsonOptions);
            File.WriteAllText(outputPath, json);
        }

        /// <summary>
        /// Export analysis results to a pretty-printed summary JSON file
        /// </summary>
        public static void ExportSummary(string outputPath, List<ModelAnalysisRecord> records)
        {
            var summaryData = records.Select(r => new
            {
                modelName = r.ModelName,
                status = r.AnalysisStatus,
                errorMessage = r.ErrorMessage,
                confidenceLevel = r.ConfidenceLevel,
                issues = new
                {
                    total = r.TotalIssues,
                    errors = r.ErrorCount,
                    warnings = r.WarningCount,
                    infos = r.InfoCount
                },
                issuesByCategory = r.IssuesByCategory,
                issuesBySeverity = r.IssuesBySeverity,
                confidenceIndicatorCount = r.ConfidenceIndicators.Count
            }).ToList();

            var json = JsonSerializer.Serialize(summaryData, JsonOptions);
            File.WriteAllText(outputPath, json);
        }
    }
}
