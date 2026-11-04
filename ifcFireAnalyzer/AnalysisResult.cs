namespace ifcFireAnalyzer;

public class AnalysisResult
{
    public List<Issue> Issues { get; set; } = new();
    public List<ConfidenceIndicator> ConfidenceIndicators { get; set; } = new();
    public string? TargetEntityType { get; set; }
    public int EntityCount { get; set; }
    
    // Optional: Add this if you decide aggregation is useful later
    public string ConfidenceLevel => CalculateConfidenceLevel();
    
    private string CalculateConfidenceLevel()
    {
        // Can be implemented later if needed
        if (!ConfidenceIndicators.Any()) return "High";
        if (ConfidenceIndicators.Count > 5) return "Low";
        if (ConfidenceIndicators.Count > 2) return "Medium";
        return "High";
    }
}