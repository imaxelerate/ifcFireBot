using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ifcFireBot;
using ifcFireBot.Models;
using ifcFireAnalyzer;
using Xbim.Ifc;

namespace ExperimentRunner
{
    /// <summary>
    /// Runs benchmark experiments across multiple IFC models and questions
    /// </summary>
    public class ExperimentRunner
    {
        private readonly IOutputWriter _output;

        public ExperimentRunner(IOutputWriter output)
        {
            _output = output;
        }

        /// <summary>
        /// Run experiments on all IFC models in the specified folder
        /// </summary>
        public async Task<List<ResultRecord>> Run(string modelsFolder, List<Question> questions, string apiKey)
        {
            var results = new List<ResultRecord>();
            var files = Directory.GetFiles(modelsFolder, "*.ifc");

            if (!files.Any())
            {
                _output.WriteError($"No IFC files found in folder: {modelsFolder}");
                return results;
            }

            _output.WriteInfo($"Found {files.Length} IFC model(s) to process");
            _output.WriteLine("");

            foreach (var file in files)
            {
                var modelName = Path.GetFileName(file);
                _output.WriteColored($"\n{new string('═', 60)}", ConsoleColor.Cyan);
                _output.WriteColored($"Processing: {modelName}", ConsoleColor.Cyan);
                _output.WriteColored($"{new string('═', 60)}\n", ConsoleColor.Cyan);

                try
                {
                    var modelResults = await ProcessModel(file, modelName, questions, apiKey);
                    results.AddRange(modelResults);
                }
                catch (Exception ex)
                {
                    _output.WriteError($"Failed to process {modelName}: {ex.Message}");
                    
                    // Add error records for all questions in this model
                    foreach (var question in questions)
                    {
                        results.Add(new ResultRecord
                        {
                            ModelName = modelName,
                            QuestionId = question.Id,
                            Question = question.Text,
                            Answer = $"ERROR: {ex.Message}",
                            ConfidenceLevel = "N/A"
                        });
                    }
                }
            }

            return results;
        }

        private async Task<List<ResultRecord>> ProcessModel(string filePath, string modelName, List<Question> questions, string apiKey)
        {
            var results = new List<ResultRecord>();

            using (var model = IfcStore.Open(filePath))
            {
                _output.WriteInfo($"Model loaded: {model.Instances.Count()} instances");

                foreach (var question in questions)
                {
                    _output.WriteLine($"\n  Q{question.Id}: {question.Text}");

                    try
                    {
                        var record = await ProcessQuestion(filePath, modelName, model, question, apiKey);
                        results.Add(record);

                        _output.WriteSuccess($"    → {record.Answer}");
                        _output.WriteInfo($"    Confidence: {record.ConfidenceLevel} ({record.TotalIssues} issue(s))");

                        if (record.DetectedIssueCategories.Any())
                        {
                            _output.WriteInfo($"    Issue Categories: {string.Join(", ", record.DetectedIssueCategories)}");
                        }

                        // Add delay between questions to avoid rate limiting (3 seconds)
                        await Task.Delay(3000);
                    }
                    catch (Exception ex)
                    {
                        _output.WriteError($"    Error: {ex.Message}");

                        results.Add(new ResultRecord
                        {
                            ModelName = modelName,
                            QuestionId = question.Id,
                            Question = question.Text,
                            Answer = $"ERROR: {ex.Message}",
                            ConfidenceLevel = "N/A"
                        });

                        // Add longer delay after errors to allow service to recover (10 seconds)
                        _output.WriteWarning($"    Waiting 10 seconds before continuing...");
                        await Task.Delay(10000);
                    }
                }
            }

            return results;
        }

        private async Task<ResultRecord> ProcessQuestion(string filePath, string modelName, IfcStore model, Question question, string apiKey)
        {
            // Step 1: Parse the question using the existing AI system
            var intent = await GetIntentFromQuestion(question.Text, model, apiKey);

            // Step 2: Run analysis on the target entity type
            AnalysisResult? analysisResult = null;
            if (!string.IsNullOrEmpty(intent.IfcEntity))
            {
                analysisResult = RunAnalysis(filePath, intent.IfcEntity);
            }

            // Step 3: Get relevant IFC data
            var fetcher = new FetchIfcEnteties();
            var elements = fetcher.FetchByIntent(model, intent);
            elements = fetcher.ApplyEntityFilter(elements, intent, model);

            // Step 4: Calculate the answer
            var answer = Calculate.Answer(elements, intent, _output);

            // Step 5: Create result record
            var record = new ResultRecord
            {
                ModelName = modelName,
                QuestionId = question.Id,
                Question = question.Text,
                Answer = answer,
                TargetEntityType = intent.IfcEntity,
                EntityCount = elements.Count
            };

            // Step 6: Extract confidence indicators from analysis result
            if (analysisResult != null)
            {
                record.ConfidenceLevel = analysisResult.ConfidenceLevel;
                record.TotalIssues = analysisResult.Issues.Count;
                record.ErrorCount = analysisResult.Issues.Count(i => i.Severity == "ERROR");
                record.WarningCount = analysisResult.Issues.Count(i => i.Severity == "WARNING");

                // Extract detected issue categories
                record.DetectedIssueCategories = analysisResult.Issues
                    .Where(i => !string.IsNullOrEmpty(i.Category))
                    .Select(i => i.Category)
                    .Distinct()
                    .OrderBy(c => c)
                    .ToList();

                // Extract specific confidence metrics from Metadata (not Data)
                var geometryIndicator = analysisResult.ConfidenceIndicators
                    .FirstOrDefault(c => c.Category == "Geometry Completeness");
                if (geometryIndicator != null && geometryIndicator.Metadata.TryGetValue("missing_geometry", out var missingGeom))
                {
                    record.GeometryMissing = Convert.ToInt32(missingGeom);
                }

                var propertyIndicator = analysisResult.ConfidenceIndicators
                    .FirstOrDefault(c => c.Category == "Property Completeness");
                if (propertyIndicator != null && propertyIndicator.Metadata.TryGetValue("missing_properties", out var missingProps))
                {
                    record.PropertiesMissing = Convert.ToInt32(missingProps);
                }

                var namingIndicator = analysisResult.ConfidenceIndicators
                    .FirstOrDefault(c => c.Category == "Naming Completeness");
                if (namingIndicator != null && namingIndicator.Metadata.TryGetValue("unnamed", out var unnamed))
                {
                    record.UnnamedEntities = Convert.ToInt32(unnamed);
                }
            }
            else
            {
                record.ConfidenceLevel = "N/A";
            }

            return record;
        }

        private async Task<Intent> GetIntentFromQuestion(string question, IfcStore model, string apiKey)
        {
            var ai = new Ai();
            var stringBuilder = new PromptBuilder(_output);
            var api = ai.InitAiConnection(apiKey);

            var prompt = PromptBuilder.PrePrompt(question);
            var completionRequest = ai.Chat(prompt, useStructuredOutput: true);
            
            // Retry logic with exponential backoff
            int maxRetries = 5;
            int delayMs = 5000; // Start with 5 seconds

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var aiResponse = await ai.StreamChat(api, completionRequest, showOutput: false);

                    if (string.IsNullOrWhiteSpace(aiResponse))
                    {
                        throw new InvalidOperationException("Received empty response from AI");
                    }

                    var parseResult = stringBuilder.ParseResponseWithDetails(aiResponse);

                    if (!parseResult.IsSuccess)
                    {
                        throw new InvalidOperationException($"Could not interpret AI response: {parseResult.ErrorMessage}");
                    }

                    return parseResult.Data!;
                }
                catch (InvalidOperationException ex) when (attempt < maxRetries)
                {
                    // Check if the error is retryable
                    bool isRetryable = ex.Message.Contains("rate limit") ||
                                     ex.Message.Contains("Network error") ||
                                     ex.Message.Contains("Connection error") ||
                                     ex.Message.Contains("timed out") ||
                                     ex.Message.Contains("parse API response") ||
                                     ex.Message.Contains("service error") ||
                                     ex.Message.Contains("Ingen sådan värd");

                    if (isRetryable)
                    {
                        _output.WriteWarning($"    Attempt {attempt}/{maxRetries} failed: {ex.Message}");
                        _output.WriteWarning($"    Retrying in {delayMs}ms...");
                        await Task.Delay(delayMs);
                        delayMs = Math.Min(delayMs * 2, 30000); // Exponential backoff: 5s, 10s, 20s, 30s (capped at 30s)
                    }
                    else
                    {
                        // Non-retryable error, throw immediately
                        throw;
                    }
                }
            }
            
            throw new InvalidOperationException($"Failed to get intent after {maxRetries} attempts");
        }

        private AnalysisResult? RunAnalysis(string ifcFilePath, string entityType)
        {
            try
            {
                var analyzer = new IFCAnalyzer();
                return analyzer.Analyze(ifcFilePath, entityType);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
