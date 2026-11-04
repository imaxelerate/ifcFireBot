using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ExperimentRunner
{
    /// <summary>
    /// Export general analysis results to CSV format
    /// </summary>
    public class CsvAnalysisExporter
    {
        /// <summary>
        /// Export summary of analysis results to CSV
        /// </summary>
        public static void ExportSummary(string outputPath, List<ModelAnalysisRecord> records)
        {
            var csv = new StringBuilder();

            // Header
            csv.AppendLine("ModelName,Status,ConfidenceLevel,TotalIssues,ErrorCount,WarningCount,InfoCount,ErrorMessage");

            // Data rows
            foreach (var record in records)
            {
                csv.AppendLine($"{EscapeCsv(record.ModelName)}," +
                             $"{record.AnalysisStatus}," +
                             $"{EscapeCsv(record.ConfidenceLevel)}," +
                             $"{record.TotalIssues}," +
                             $"{record.ErrorCount}," +
                             $"{record.WarningCount}," +
                             $"{record.InfoCount}," +
                             $"{EscapeCsv(record.ErrorMessage ?? "")}");
            }

            File.WriteAllText(outputPath, csv.ToString());
        }

        /// <summary>
        /// Export detailed issues to CSV
        /// </summary>
        public static void ExportDetailedIssues(string outputPath, List<ModelAnalysisRecord> records)
        {
            var csv = new StringBuilder();

            // Header
            csv.AppendLine("ModelName,Severity,Category,Description,GlobalId");

            // Data rows
            foreach (var record in records)
            {
                foreach (var issue in record.AllIssues)
                {
                    csv.AppendLine($"{EscapeCsv(record.ModelName)}," +
                                 $"{issue.Severity}," +
                                 $"{EscapeCsv(issue.Category)}," +
                                 $"{EscapeCsv(issue.Description)}," +
                                 $"{EscapeCsv(issue.GlobalId ?? "")}");
                }
            }

            File.WriteAllText(outputPath, csv.ToString());
        }

        /// <summary>
        /// Export issues grouped by category to CSV
        /// </summary>
        public static void ExportIssuesByCategory(string outputPath, List<ModelAnalysisRecord> records)
        {
            var csv = new StringBuilder();

            // Get all unique categories
            var allCategories = records
                .SelectMany(r => r.IssuesByCategory.Keys)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            // Header
            csv.Append("ModelName");
            foreach (var category in allCategories)
            {
                csv.Append($",{EscapeCsv(category)}");
            }
            csv.AppendLine();

            // Data rows
            foreach (var record in records)
            {
                csv.Append(EscapeCsv(record.ModelName));
                foreach (var category in allCategories)
                {
                    var count = record.IssuesByCategory.GetValueOrDefault(category, 0);
                    csv.Append($",{count}");
                }
                csv.AppendLine();
            }

            File.WriteAllText(outputPath, csv.ToString());
        }

        /// <summary>
        /// Export confidence indicators to CSV
        /// </summary>
        public static void ExportConfidenceIndicators(string outputPath, List<ModelAnalysisRecord> records)
        {
            var csv = new StringBuilder();

            // Header
            csv.AppendLine("ModelName,IndicatorCategory,Description,MetadataKey,MetadataValue");

            // Data rows
            foreach (var record in records)
            {
                foreach (var indicator in record.ConfidenceIndicators)
                {
                    foreach (var metadata in indicator.Metadata)
                    {
                        csv.AppendLine($"{EscapeCsv(record.ModelName)}," +
                                     $"{EscapeCsv(indicator.Category)}," +
                                     $"{EscapeCsv(indicator.Description)}," +
                                     $"{EscapeCsv(metadata.Key)}," +
                                     $"{EscapeCsv(metadata.Value)}");
                    }

                    // If no metadata, still output the indicator
                    if (!indicator.Metadata.Any())
                    {
                        csv.AppendLine($"{EscapeCsv(record.ModelName)}," +
                                     $"{EscapeCsv(indicator.Category)}," +
                                     $"{EscapeCsv(indicator.Description)},,");
                    }
                }
            }

            File.WriteAllText(outputPath, csv.ToString());
        }

        /// <summary>
        /// Escape CSV values properly
        /// </summary>
        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            // If the value contains comma, quote, or newline, wrap it in quotes and escape quotes
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }
    }
}
