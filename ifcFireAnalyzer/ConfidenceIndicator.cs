namespace ifcFireAnalyzer;

public class ConfidenceIndicator
{
    public string Category { get; set; }
    public string Description { get; set; }
    public Dictionary<string, object> Metadata { get; set; }

    public ConfidenceIndicator(string category, string description, Dictionary<string, object>? metadata = null)
    {
        Category = category;
        Description = description;
        Metadata = metadata ?? new Dictionary<string, object>();
    }
}