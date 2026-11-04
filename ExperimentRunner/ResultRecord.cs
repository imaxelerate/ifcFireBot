using System;
using System.Collections.Generic;

namespace ExperimentRunner
{
    /// <summary>
    /// Stores experiment results for each model/question combination
    /// </summary>
    public class ResultRecord
    {
        public string ModelName { get; set; } = string.Empty;
        public string QuestionId { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;

        // Confidence indicators from AnalysisResult
        public int GeometryMissing { get; set; }
        public int PropertiesMissing { get; set; }
        public int UnnamedEntities { get; set; }
        public string ConfidenceLevel { get; set; } = string.Empty;
        public int TotalIssues { get; set; }
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }

        // Entity-specific metadata
        public string TargetEntityType { get; set; } = string.Empty;
        public int EntityCount { get; set; }

        // Issue categories detected during analysis
        public List<string> DetectedIssueCategories { get; set; } = new();
    }
}
