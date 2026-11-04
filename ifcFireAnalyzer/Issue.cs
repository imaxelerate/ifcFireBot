namespace ifcFireAnalyzer;

public class Issue
{
    public string Severity { get; set; }
    public string Message { get; set; }
    public string Category { get; set; }
    public string? EntityId { get; set; }

    public Issue(string severity, string message, string category, string? entityId = null)
    {
        Severity = severity;
        Message = message;
        Category = category;
        EntityId = entityId;
    }
}