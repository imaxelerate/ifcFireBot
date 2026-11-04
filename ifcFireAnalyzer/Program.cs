namespace ifcFireAnalyzer;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("IFC Problem Detector v1.0\n");

        if (args.Length == 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: No IFC file path provided");
            Console.ResetColor();
            Console.WriteLine("\nUsage: ifcFireAnalyzer <path-to-ifc-file> [entity-type]");
            Console.WriteLine("\nExamples:");
            Console.WriteLine("  ifcFireAnalyzer C:\\Models\\building.ifc");
            Console.WriteLine("  ifcFireAnalyzer C:\\Models\\building.ifc IfcWall");
            Console.WriteLine("  ifcFireAnalyzer C:\\Models\\building.ifc IfcBuildingStorey");
            Console.WriteLine("\nSupported entity types:");
            Console.WriteLine("  - IfcWall, IfcSlab, IfcDoor, IfcWindow");
            Console.WriteLine("  - IfcStair, IfcBuildingStorey, IfcSpace");
            Console.WriteLine("  - IfcBuildingElementProxy, IfcBeam, IfcColumn");
            Console.WriteLine("  - Or any valid IFC entity type");
            return;
        }

        var path = args[0];
        var targetEntity = args.Length > 1 ? args[1] : null;

        if (!File.Exists(path))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: File not found: {path}");
            Console.ResetColor();
            return;
        }

        try
        {
            Console.WriteLine($"Analyzing: {Path.GetFileName(path)}");
            Console.WriteLine($"Location: {Path.GetDirectoryName(path)}");
            
            if (!string.IsNullOrEmpty(targetEntity))
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"Target Entity: {targetEntity}");
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine("Target Entity: All (general analysis)");
            }
            
            Console.WriteLine();

            var analyzer = new IFCAnalyzer();
            var result = analyzer.Analyze(path, targetEntity);

            ReportGenerator.Print(result);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nFatal error during analysis: {ex.Message}");
            Console.ResetColor();
            Console.WriteLine($"\nStack trace:\n{ex.StackTrace}");
        }
    }
}
