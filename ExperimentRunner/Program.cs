using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ifcFireBot;
using ifcFireBot.Models;

namespace ExperimentRunner
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Check if user wants to run general analysis
            if (args.Length > 0 && args[0].Equals("--general", StringComparison.OrdinalIgnoreCase))
            {
                RunGeneralAnalysisMode(args);
                return;
            }

            // Otherwise run the original question-based experiment
            await RunQuestionBasedExperiment(args);
        }

        static void RunGeneralAnalysisMode(string[] args)
        {
            Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║          IFC General Analysis - Batch Runner             ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            // Configure paths (skip --general flag)
            string modelsFolder = args.Length > 1 ? args[1] : "models";
            string outputFolder = args.Length > 2 ? args[2] : "analysis_output";

            // Validate models folder
            if (!Directory.Exists(modelsFolder))
            {
                Console.WriteLine($"[ERROR] Models folder not found: {modelsFolder}");
                Console.WriteLine("Creating models folder...");
                Directory.CreateDirectory(modelsFolder);
                Console.WriteLine($"Please add IFC files to: {Path.GetFullPath(modelsFolder)}");
                return;
            }

            // Create output folder
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            try
            {
                // Run analysis on all models
                Console.WriteLine($"Running general analysis on all models in: {modelsFolder}");
                Console.WriteLine();

                var results = GeneralAnalysisRunner.RunAllModels(modelsFolder);

                // Generate timestamp for output files
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                // Export results in multiple formats
                Console.WriteLine("Exporting results...");
                Console.WriteLine();

                // 1. Full JSON (detailed)
                var jsonFullPath = Path.Combine(outputFolder, $"analysis_full_{timestamp}.json");
                JsonAnalysisExporter.Export(jsonFullPath, results);
                Console.WriteLine($"  ✓ Full JSON: {Path.GetFullPath(jsonFullPath)}");

                // 2. Summary JSON
                var jsonSummaryPath = Path.Combine(outputFolder, $"analysis_summary_{timestamp}.json");
                JsonAnalysisExporter.ExportSummary(jsonSummaryPath, results);
                Console.WriteLine($"  ✓ Summary JSON: {Path.GetFullPath(jsonSummaryPath)}");

                // 3. Summary CSV
                var csvSummaryPath = Path.Combine(outputFolder, $"analysis_summary_{timestamp}.csv");
                CsvAnalysisExporter.ExportSummary(csvSummaryPath, results);
                Console.WriteLine($"  ✓ Summary CSV: {Path.GetFullPath(csvSummaryPath)}");

                // 4. Detailed Issues CSV
                var csvIssuesPath = Path.Combine(outputFolder, $"analysis_issues_{timestamp}.csv");
                CsvAnalysisExporter.ExportDetailedIssues(csvIssuesPath, results);
                Console.WriteLine($"  ✓ Detailed Issues CSV: {Path.GetFullPath(csvIssuesPath)}");

                // 5. Issues by Category CSV
                var csvCategoriesPath = Path.Combine(outputFolder, $"analysis_categories_{timestamp}.csv");
                CsvAnalysisExporter.ExportIssuesByCategory(csvCategoriesPath, results);
                Console.WriteLine($"  ✓ Issues by Category CSV: {Path.GetFullPath(csvCategoriesPath)}");

                // 6. Confidence Indicators CSV
                var csvConfidencePath = Path.Combine(outputFolder, $"analysis_confidence_{timestamp}.csv");
                CsvAnalysisExporter.ExportConfidenceIndicators(csvConfidencePath, results);
                Console.WriteLine($"  ✓ Confidence Indicators CSV: {Path.GetFullPath(csvConfidencePath)}");

                // Display summary
                Console.WriteLine();
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.WriteLine("ANALYSIS SUMMARY");
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.WriteLine($"Total models analyzed: {results.Count}");
                Console.WriteLine($"Successful: {results.Where(r => r.AnalysisStatus == "SUCCESS").Count()}");
                Console.WriteLine($"Failed: {results.Where(r => r.AnalysisStatus == "FAILED").Count()}");
                Console.WriteLine();
                Console.WriteLine($"Total issues found: {results.Sum(r => r.TotalIssues)}");
                Console.WriteLine($"  - Errors: {results.Sum(r => r.ErrorCount)}");
                Console.WriteLine($"  - Warnings: {results.Sum(r => r.WarningCount)}");
                Console.WriteLine($"  - Infos: {results.Sum(r => r.InfoCount)}");
                Console.WriteLine();
                Console.WriteLine($"All results saved to: {Path.GetFullPath(outputFolder)}");
                Console.WriteLine("═══════════════════════════════════════════════════════════");
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine($"[ERROR] Analysis failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Environment.Exit(1);
            }
        }

        static async Task RunQuestionBasedExperiment(string[] args)
        {
            Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║          IFC FireBot - Experiment Runner                 ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            // Initialize output writer
            IOutputWriter output = new ConsoleOutputWriter();

            // Define benchmark questions
            var questions = new List<Question>
            {
                new Question("Q1", "how many storeys does the building have?"),
                new Question("Q2", "what is the total area of all building storeys?"),
                new Question("Q3", "what is the building height?"),
                new Question("Q4", "does the building have a balcony?"),
                new Question("Q5", "does the building have an attic?"),
                new Question("Q6", "what is the maximum roof height of all building storeys?"),
                new Question("Q7", "how many building storeys are beneath ground level?"),
            };

            // Configure paths
            string modelsFolder = args.Length > 0 ? args[0] : "models";
            string outputFile = args.Length > 1 ? args[1] : "experiment_results.csv";

            // Validate models folder
            if (!Directory.Exists(modelsFolder))
            {
                output.WriteError($"Models folder not found: {modelsFolder}");
                output.WriteInfo("Creating models folder...");
                Directory.CreateDirectory(modelsFolder);
                output.WriteInfo($"Please add IFC files to: {Path.GetFullPath(modelsFolder)}");
                return;
            }

            // Acquire API key — read from environment variable or prompt the user
            string apiKey = Environment.GetEnvironmentVariable("HUGGINGFACE_TOKEN") ?? string.Empty;
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.Write("Enter your HuggingFace API key: ");
                var keyBuilder = new System.Text.StringBuilder();
                ConsoleKeyInfo ki;
                do
                {
                    ki = Console.ReadKey(intercept: true);
                    if (ki.Key == ConsoleKey.Backspace && keyBuilder.Length > 0)
                    {
                        keyBuilder.Remove(keyBuilder.Length - 1, 1);
                        Console.Write("\b \b");
                    }
                    else if (ki.Key != ConsoleKey.Enter)
                    {
                        keyBuilder.Append(ki.KeyChar);
                        Console.Write('*');
                    }
                }
                while (ki.Key != ConsoleKey.Enter);
                Console.WriteLine();
                apiKey = keyBuilder.ToString().Trim();
            }

            try
            {
                // Run experiments
                var runner = new ExperimentRunner(output);
                var results = await runner.Run(modelsFolder, questions, apiKey);

                // Export results
                output.WriteLine("");
                output.WriteInfo("Exporting results...");
                CsvExporter.Export(outputFile, results);

                // Display summary
                output.WriteLine("");
                output.WriteSuccess($"✓ Experiment complete!");
                output.WriteInfo($"  Total records: {results.Count}");
                output.WriteInfo($"  Results saved to: {Path.GetFullPath(outputFile)}");
            }
            catch (Exception ex)
            {
                output.WriteError($"Experiment failed: {ex.Message}");
                output.WriteError($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}
