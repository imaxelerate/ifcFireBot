using Xbim.Ifc4.Interfaces;

namespace ifcFireAnalyzer;

public static class ReportGenerator
{
    public static void Print(List<Issue> issues, string? targetEntity = null)
    {
        Console.WriteLine("\n╔════════════════════════════════════════╗");

        if (string.IsNullOrEmpty(targetEntity))
        {
            Console.WriteLine("║   IFC Model Analysis Report            ║");
        }
        else
        {
            var title = $"║   {targetEntity} Analysis Report";
            var padding = 43 - title.Length;
            Console.WriteLine(title + new string(' ', padding) + "║");
        }

        Console.WriteLine("╚════════════════════════════════════════╝\n");

        if (!issues.Any())
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ No issues detected - model appears well-structured");
            Console.ResetColor();
            return;
        }

        var errors = issues.Where(i => i.Severity == "ERROR").ToList();
        var warnings = issues.Where(i => i.Severity == "WARNING").ToList();
        var info = issues.Where(i => i.Severity == "INFO").ToList();

        // Summary by severity
        Console.WriteLine($"Summary: {errors.Count} errors, {warnings.Count} warnings, {info.Count} info");

        // Summary by category
        var categoryGroups = issues
            .GroupBy(i => i.Category)
            .OrderByDescending(g => g.Count())
            .ToList();

        Console.WriteLine("\nIssues by Category:");
        foreach (var group in categoryGroups)
        {
            Console.WriteLine($"  • {group.Key}: {group.Count()}");
        }

        Console.WriteLine("\n" + new string('─', 80));

        PrintIssues(errors, ConsoleColor.Red);
        PrintIssues(warnings, ConsoleColor.Yellow);
        PrintIssues(info, ConsoleColor.Cyan);
    }

    private static void PrintIssues(List<Issue> issues, ConsoleColor color)
    {
        foreach (var issue in issues)
        {
            Console.ForegroundColor = color;
            Console.Write($"[{issue.Severity}] ");
            Console.ResetColor();

            // Print category
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write($"[{issue.Category}] ");
            Console.ResetColor();

            Console.Write(issue.Message);

            if (!string.IsNullOrEmpty(issue.EntityId))
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"        EntityId: {issue.EntityId}");
                Console.ResetColor();
            }

            Console.WriteLine();
        }
    }

    public static void Print(AnalysisResult result)
    {
        var issues = result.Issues;
        
        // Show confidence indicators if available
        if (result.ConfidenceIndicators.Any())
        {
            Console.WriteLine("\n📊 Konfidensindikatorer:");
            Console.WriteLine($"   Entitetstyp: {result.TargetEntityType}");
            Console.WriteLine($"   Antal entiteter: {result.EntityCount}");
            Console.WriteLine($"   Konfidensgrad: {result.ConfidenceLevel}");
            Console.WriteLine();
            
            foreach (var indicator in result.ConfidenceIndicators)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"   • [{indicator.Category}] {indicator.Description}");
                Console.ResetColor();
            }
            Console.WriteLine();
        }
        
        // Group and display issues
        var grouped = issues.GroupBy(i => i.Category).OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n═══ {group.Key} ═══");
            Console.ResetColor();

            foreach (var issue in group)
            {
                var color = issue.Severity switch
                {
                    "ERROR" => ConsoleColor.Red,
                    "WARNING" => ConsoleColor.Yellow,
                    _ => ConsoleColor.White
                };

                Console.ForegroundColor = color;
                Console.Write($"[{issue.Severity}] ");
                Console.ResetColor();
                Console.WriteLine(issue.Message);

                if (!string.IsNullOrEmpty(issue.EntityId))
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"        EntityId: {issue.EntityId}");
                    Console.ResetColor();
                }
            }
        }
    }
}