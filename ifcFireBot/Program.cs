// Program.cs (Refactored)
using ifcFireBot.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using ifcFireAnalyzer;  // Add this line
using System.IO;

#nullable enable

namespace ifcFireBot
{
    internal enum AppMode { Analyze, QA }
    internal enum NavigationResult { ReturnToMenu, ExitProgram }

    internal class Program
    {
        static async Task Main(string[] args)
        {
            IOutputWriter output = new ConsoleOutputWriter();
            try
            {
                await RunApplication(output);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nUnexpected error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Console.ResetColor();
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
            }
        }

        static async Task RunApplication(IOutputWriter output)
        {
            ShowBanner(output);
            string? cachedApiKey = null;

            while (true)
            {
                var mode = AskForMode(output);

                if (mode == null)
                {
                    output.WriteInfo("\nGoodbye!");
                    return;
                }

                NavigationResult result;

                if (mode == AppMode.Analyze)
                {
                    result = await RunAnalyzeLoop(output);
                }
                else
                {
                    var (ifcPath, pathNav) = AskForIfcFilePath(output);
                    if (ifcPath == null) { result = pathNav; }
                    else
                    {
                        var (apiKey, keyNav) = AskForApiKey(output, cachedApiKey);
                        if (apiKey == null) { result = keyNav; }
                        else
                        {
                            cachedApiKey = apiKey;
                            result = await RunQAMode(ifcPath, apiKey, output);
                        }
                    }
                }

                if (result == NavigationResult.ExitProgram)
                {
                    output.WriteInfo("\nGoodbye!");
                    return;
                }

                // NavigationResult.ReturnToMenu — redisplay the banner and loop
                ShowBanner(output);
            }
        }

        static void ShowBanner(IOutputWriter output)
        {
            output.WriteColored("\n╔══════════════════════════════════════════════╗", ConsoleColor.Cyan);
            output.WriteColored("║          Welcome to ifcFireBot               ║", ConsoleColor.Cyan);
            output.WriteColored("╚══════════════════════════════════════════════╝", ConsoleColor.Cyan);
            output.WriteLine("");
            output.WriteInfo("Please select a mode:");
            output.WriteLine("");
            output.WriteColored("  [1]    Analyze Mode  — scan IFC file for data quality issues (no API key needed)", ConsoleColor.White);
            output.WriteColored("  [2]    Q&A Mode      — ask questions about your IFC model using AI", ConsoleColor.White);
            output.WriteColored("  [exit] Exit          — close the program  (also works at any prompt)", ConsoleColor.White);
            output.WriteLine("");
        }

        static AppMode? AskForMode(IOutputWriter output)
        {
            while (true)
            {
                output.WritePrompt("Enter 1, 2, or 'exit': ");
                string input = output.ReadLine().Trim().ToLower();
                if (input == "1") return AppMode.Analyze;
                if (input == "2") return AppMode.QA;
                if (input == "exit") return null;
                output.WriteWarning("Invalid choice. Please enter 1, 2, or 'exit'.");
            }
        }

        static (string? path, NavigationResult nav) AskForIfcFilePath(IOutputWriter output)
        {
            output.WriteInfo("  (type 'return' for main menu  |  'exit' to close the program)");
            while (true)
            {
                output.WritePrompt("\nEnter the full path to your IFC file: ");
                string raw = output.ReadLine().Trim();

                if (raw.ToLower() == "return") return (null, NavigationResult.ReturnToMenu);
                if (raw.ToLower() == "exit")   return (null, NavigationResult.ExitProgram);

                string path = raw.Trim('"');

                if (!path.EndsWith(".ifc", StringComparison.OrdinalIgnoreCase))
                {
                    output.WriteWarning("Path must point to a file with a .ifc extension. Please try again.");
                    continue;
                }

                if (!File.Exists(path))
                {
                    output.WriteWarning($"File not found: '{path}'. Please check the path and try again.");
                    continue;
                }

                return (path, NavigationResult.ReturnToMenu);
            }
        }

        static (string? key, NavigationResult nav) AskForApiKey(IOutputWriter output, string? cachedKey = null)
        {
            output.WriteLine("");
            if (cachedKey != null)
            {
                output.WriteInfo("API key already entered this session — press Enter to reuse it.");
                output.WriteInfo("  (type a new key to replace it  |  'return' or 'exit' to leave this prompt)");
            }
            else
            {
                output.WriteInfo("Your API key will be used this session only — it is never saved to disk.");
                output.WriteInfo("  (type 'return' or 'exit' then press Enter to leave this prompt)");
            }

            while (true)
            {
                output.WritePrompt("Enter your HuggingFace API key: ");

                var key = new System.Text.StringBuilder();
                ConsoleKeyInfo keyInfo;
                do
                {
                    keyInfo = Console.ReadKey(intercept: true);
                    if (keyInfo.Key == ConsoleKey.Backspace)
                    {
                        if (key.Length > 0)
                        {
                            key.Remove(key.Length - 1, 1);
                            Console.Write("\b \b");
                        }
                    }
                    else if (keyInfo.Key != ConsoleKey.Enter)
                    {
                        key.Append(keyInfo.KeyChar);
                        Console.Write('*');
                    }
                }
                while (keyInfo.Key != ConsoleKey.Enter);
                Console.WriteLine();

                string result = key.ToString().Trim();

                if (result.ToLower() == "return") return (null, NavigationResult.ReturnToMenu);
                if (result.ToLower() == "exit")   return (null, NavigationResult.ExitProgram);

                if (string.IsNullOrEmpty(result) && cachedKey != null)
                    return (cachedKey, NavigationResult.ReturnToMenu);

                if (!string.IsNullOrEmpty(result))
                    return (result, NavigationResult.ReturnToMenu);

                output.WriteWarning("API key cannot be empty. Please try again.");
            }
        }

        static async Task<NavigationResult> RunAnalyzeLoop(IOutputWriter output)
        {
            while (true)
            {
                var (ifcPath, nav) = AskForIfcFilePath(output);
                if (ifcPath == null)
                    return nav;

                try
                {
                    RunFullModelAnalysis(ifcPath, output);
                }
                catch (Exception ex)
                {
                    output.WriteError($"Analysis failed: {ex.Message}");
                    output.WriteError(ex.StackTrace ?? string.Empty);
                }
                finally
                {
                    // Force Xbim/Esent native resources to fully release
                    // before the next IfcStore.Open call
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }

                output.WriteLine("");
                output.WriteColored("  [y]      Analyze another file", ConsoleColor.White);
                output.WriteColored("  [return] Return to main menu", ConsoleColor.White);
                output.WriteColored("  [exit]   Exit the program", ConsoleColor.White);

                while (true)
                {
                    output.WritePrompt("\nEnter y, 'return', or 'exit': ");
                    string input = output.ReadLine().Trim().ToLower();

                    if (input == "exit")   return NavigationResult.ExitProgram;
                    if (input == "return") return NavigationResult.ReturnToMenu;
                    if (input == "y")      break;

                    output.WriteWarning("Invalid choice. Please enter y, 'return', or 'exit'.");
                }
            }
        }

        static async Task<NavigationResult> RunQAMode(string ifcFilePath, string apiKey, IOutputWriter output)
        {
            bool isDebugMode = false;

            output.WriteInfo($"\nLoading IFC model '{ifcFilePath}'.....");

            using (var model = IfcStore.Open(ifcFilePath))
            {
                output.WriteInfo($"IFC model '{ifcFilePath}' opened successfully.");
                output.WriteInfo($"Model contains {model.Instances.Count()} instances of different types.");
                output.WriteLine("");
                output.WriteInfo("Commands: 'return' — main menu  |  'exit' — quit program  |  'debug' — toggle debug mode");
                output.WriteInfo("Enter your question about the IFC model:");

                while (true)
                {
                    string question = output.ReadLine();

                    if (string.IsNullOrWhiteSpace(question))
                        continue;

                    if (question.ToLower() == "exit")
                        return NavigationResult.ExitProgram;

                    if (question.ToLower() == "return")
                        return NavigationResult.ReturnToMenu;

                    if (question.ToLower() == "debug")
                    {
                        isDebugMode = !isDebugMode;
                        output.WriteInfo($"\nDebug mode: {(isDebugMode ? "ON" : "OFF")}");
                        output.WriteInfo("Commands: 'return' — main menu  |  'exit' — quit program  |  'debug' — toggle debug mode");
                        output.WriteInfo("Enter your question about the IFC model:\n");
                        continue;
                    }

                    try
                    {
                        var intent = await GetIntentFromLLM(question, model, output, isDebugMode, apiKey);

                        if (isDebugMode)
                        {
                            output.WriteIntentDetails(intent);
                        }

                        AnalysisResult? analysisResult = null;
                        if (!string.IsNullOrEmpty(intent.IfcEntity))
                        {
                            analysisResult = RunProblemDetectionSilently(ifcFilePath, intent.IfcEntity, isDebugMode);
                        }

                        var elements = GetRelevantDataFromIFC(intent, model, output, isDebugMode);

                        if (isDebugMode)
                        {
                            output.WriteElementDetails(elements);
                        }

                        var fetcher = new FetchIfcEnteties();
                        elements = fetcher.ApplyEntityFilter(elements, intent, model);

                        var answer = Calculate.Answer(elements, intent, output);

                        output.WriteAnswer(answer);

                        if (analysisResult != null && analysisResult.ConfidenceIndicators.Any())
                        {
                            DisplayConfidenceSection(analysisResult, output);
                        }

                        output.WritePrompt("\n---");
                        output.WriteInfo("Commands: 'return' — main menu  |  'exit' — quit program  |  'debug' — toggle debug mode");
                        output.WriteInfo("Ask another question, or enter a command:");
                    }
                    catch (Exception ex)
                    {
                        output.WriteError($"Error: {ex.Message}\n");
                    }
                }
            }
        }

        // Updated method to handle AnalysisResult
        static void RunFullModelAnalysis(string ifcFilePath, IOutputWriter output)
        {
            try
            {
                output.WriteLine("");
                output.WriteColored("╔═══════════════════════════════════════════════════════════╗", ConsoleColor.Cyan);
                output.WriteColored("║         Starting Full IFC Model Analysis                  ║", ConsoleColor.Cyan);
                output.WriteColored("╚═══════════════════════════════════════════════════════════╝", ConsoleColor.Cyan);
                output.WriteLine("");
                
                output.WriteInfo("Analyzing model for general and entity-specific issues...");
                output.WriteLine("");
                
                var analyzer = new IFCAnalyzer();
                
                // Run general analysis (no specific entity type)
                var result = analyzer.Analyze(ifcFilePath, targetEntityType: null);
                var issues = result.Issues; // Extract issues from result
                
                if (issues.Any())
                {
                    // Group issues by severity
                    var errorIssues = issues.Where(i => i.Severity == "ERROR").ToList();
                    var warningIssues = issues.Where(i => i.Severity == "WARNING").ToList();
                    var infoIssues = issues.Where(i => i.Severity == "INFO").ToList();
                    
                    // Display summary
                    output.WriteInfo("SUMMARY:");
                    output.WriteInfo($"   Total issues: {issues.Count}");
                    
                    if (errorIssues.Any())
                    {
                        output.WriteError($"   Critical errors: {errorIssues.Count}");
                    }
                    
                    if (warningIssues.Any())
                    {
                        output.WriteWarning($"   Warnings: {warningIssues.Count}");
                    }
                    
                    if (infoIssues.Any())
                    {
                        output.WriteInfo($"   Information: {infoIssues.Count}");
                    }
                    
                    output.WriteLine("");
                    output.WriteLine(new string('─', 60));
                    output.WriteLine("");
                    
                    // Print full report
                    ReportGenerator.Print(result);
                }
                else
                {
                    output.WriteSuccess("No issues found - model appears to be well-structured!");
                }
                
                output.WriteLine("");
                output.WriteLine(new string('═', 60));
            }
            catch (Exception ex)
            {
                output.WriteError($"\nError during analysis: {ex.Message}");
                output.WriteError($"Stack trace: {ex.StackTrace}");
            }
        }

        // NEW: Silent problem detection that returns AnalysisResult
        static AnalysisResult? RunProblemDetectionSilently(string ifcFilePath, string entityType, bool isDebugMode)
        {
            try
            {
                var analyzer = new IFCAnalyzer();
                return analyzer.Analyze(ifcFilePath, entityType);
            }
            catch (Exception)
            {
                // Silently fail - don't interrupt the main flow
                return null;
            }
        }

        // NEW: Display confidence section at the end
        static void DisplayConfidenceSection(AnalysisResult result, IOutputWriter output)
        {
            output.WriteLine("");
            output.WriteLine(new string('-', 50));
            output.WriteLine("3. Model Confidence Indicators");
            output.WriteLine(new string('-', 50));
            output.WriteLine("");
            output.WriteLine("The following model issues may affect reliability:");
            output.WriteLine("");

            foreach (var indicator in result.ConfidenceIndicators)
            {
                output.WriteColored($"• {indicator.Category}", ConsoleColor.Yellow);
                output.WriteLine($"  {indicator.Description}");
                output.WriteLine("");
            }

            output.WriteLine($"System confidence: **{result.ConfidenceLevel}**");
        }

        static async Task<Intent> GetIntentFromLLM(string question, IfcStore model, IOutputWriter output, bool isDebugMode, string apiKey)
        {
            var ai = new Ai();
            var stringBuilder = new PromptBuilder(output);
            var api = ai.InitAiConnection(apiKey);

            var prompt = PromptBuilder.PrePrompt(question);
            var completionRequest = ai.Chat(prompt, useStructuredOutput: true);
            var aiResponse = await ai.StreamChat(api, completionRequest, isDebugMode);

            var parseResult = stringBuilder.ParseResponseWithDetails(aiResponse);

            if (!parseResult.IsSuccess)
            {
                output.WriteError($"Error parsing AI response: {parseResult.ErrorMessage}");
                throw new InvalidOperationException($"Could not interpret AI response: {parseResult.ErrorMessage}");
            }

            return parseResult.Data!;
        }

        static List<IIfcProduct> GetRelevantDataFromIFC(Intent intent, IfcStore model, IOutputWriter output, bool isDebugMode)
        {
            var fetcher = new FetchIfcEnteties();
            var elements = fetcher.FetchByIntent(model, intent);
            
            // Only show in debug mode
            if (isDebugMode)
            {
                output.WriteDebug($"Found {elements.Count} elements of type {intent.IfcEntity}");
            }
            
            return elements;
        }
    }
}