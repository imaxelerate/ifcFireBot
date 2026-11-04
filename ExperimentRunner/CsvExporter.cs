using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ExperimentRunner
{
    /// <summary>
    /// Exports experiment results to CSV format for analysis
    /// </summary>
    public static class CsvExporter
    {
        public static void Export(string path, List<ResultRecord> records)
        {
            using var writer = new StreamWriter(path);

            // CSV Header
            writer.WriteLine(
                "Model," +
                "QuestionId," +
                "Question," +
                "Answer," +
                "TargetEntityType," +
                "EntityCount," +
                "DetectedIssueCategories," +
                "GeometryMissing," +
                "PropertiesMissing," +
                "UnnamedEntities," +
                "ConfidenceLevel," +
                "TotalIssues," +
                "ErrorCount," +
                "WarningCount"
            );

            // CSV Rows
            foreach (var r in records)
            {
                var categoriesString = r.DetectedIssueCategories.Any() 
                    ? string.Join(" | ", r.DetectedIssueCategories) 
                    : string.Empty;

                writer.WriteLine(
                    $"{EscapeCsv(r.ModelName)}," +
                    $"{EscapeCsv(r.QuestionId)}," +
                    $"{EscapeCsv(r.Question)}," +
                    $"{EscapeCsv(r.Answer)}," +
                    $"{EscapeCsv(r.TargetEntityType)}," +
                    $"{r.EntityCount}," +
                    $"{EscapeCsv(categoriesString)}," +
                    $"{r.GeometryMissing}," +
                    $"{r.PropertiesMissing}," +
                    $"{r.UnnamedEntities}," +
                    $"{EscapeCsv(r.ConfidenceLevel)}," +
                    $"{r.TotalIssues}," +
                    $"{r.ErrorCount}," +
                    $"{r.WarningCount}"
                );
            }

            Console.WriteLine($"✓ Results exported to: {path}");
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            // Escape quotes and wrap in quotes if contains comma, quote, or newline
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }
    }
}