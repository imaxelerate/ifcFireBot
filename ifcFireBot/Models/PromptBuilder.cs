#nullable enable
using System;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Core;
using System.Linq;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;

namespace ifcFireBot.Models
{
    public class PromptBuilder
    {
        private readonly IDeserializer _deserializer;
        private readonly IOutputWriter _output;

        public PromptBuilder(IOutputWriter output)
        {
            _output = output;
            _deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
        }

        public static string PrePrompt(string question)
        {
            // This overload is for when we don't have the model
            // Still use hints but without model context

            var entityHint = AllowedValues.GetIfcEntityHint(question);
            var questionTypeHint = AllowedValues.GetQuestionTypeHint(question);

            var hints = "";
            if (entityHint != "unknown")
                hints += $"TIPS: Baserat på svenska termer, frågan verkar handla om '{entityHint}'.\n";
            if (questionTypeHint != "unknown")
                hints += $"TIPS: Baserat på nyckelord, frågan verkar vara av typ '{questionTypeHint}'.\n";

            var allowedIfcEntities = new[]
            {
                "IfcWall", "IfcDoor", "IfcWindow", "IfcSlab",
                "IfcBeam", "IfcColumn", "IfcStair", "IfcRoof",
                "IfcSpace", "IfcBuildingStorey"
            };

            var allowedQuestionTypes = new[]
            {
                "Count", "List", "Property", "Volume",
                "Area", "Height", "BuildingHeight", "UndergroundFloors", "Location", "Material", "Exists"
            };

            string preProcessedPrompt =
                "Du är en expert på IFC-modeller och Building Information Modeling (BIM).\n\n" +

                (string.IsNullOrEmpty(hints) ? "" : hints + "\n") +

                "TILLÅTNA IFC-ENTITETER:\n" +
                string.Join(", ", allowedIfcEntities) + "\n\n" +

                "TILLÅTNA FRÅGETYPER:\n" +
                string.Join(", ", allowedQuestionTypes) + "\n\n" +

                "EXEMPEL:\n" +
                GetFewShotExamples() + "\n\n" +

                "VIKTIGT:\n" +
                "- Använd ENDAST värden från de tillåtna listorna\n" +
                "- Om inget specifikt namn nämns, använd 'none' för entityFilter och propertyName\n" +
                "- Svara ENDAST med YAML-format, ingen annan text\n\n" +

                $"FRÅGA: '{question}'";

            return preProcessedPrompt;
        }

        private string GetModelContext(IfcStore model)
        {
            var walls = model.Instances.OfType<IIfcWall>().Count();
            var doors = model.Instances.OfType<IIfcDoor>().Count();
            var windows = model.Instances.OfType<IIfcWindow>().Count();
            var slabs = model.Instances.OfType<IIfcSlab>().Count();
            var spaces = model.Instances.OfType<IIfcSpace>().Count();
            var storeys = model.Instances.OfType<IIfcBuildingStorey>().Count();

            return $"Modellen innehåller:\n" +
                   $"- {walls} väggar (IfcWall)\n" +
                   $"- {doors} dörrar (IfcDoor)\n" +
                   $"- {windows} fönster (IfcWindow)\n" +
                   $"- {slabs} bjälklag/golv (IfcSlab)\n" +
                   $"- {spaces} rum/utrymmen (IfcSpace)\n" +
                   $"- {storeys} våningar (IfcBuildingStorey)";
        }

        private static string GetFewShotExamples()
        {
            return
                "Exempel 1:\n" +
                "Fråga: 'Hur många dörrar finns det?'\n" +
                "```yaml\n" +
                "ifcEntity: IfcDoor\n" +
                "questionType: Count\n" +
                "entityFilter: none\n" +
                "propertyName: none\n" +
                "```\n\n" +

                "Exempel 2:\n" +
                "Fråga: 'Vilken area har rummen?'\n" +
                "```yaml\n" +
                "ifcEntity: IfcSpace\n" +
                "questionType: Area\n" +
                "entityFilter: none\n" +
                "propertyName: none\n" +
                "```\n\n" +

                "Exempel 3:\n" +
                "Fråga: 'Hur stor är vindplan?'\n" +
                "```yaml\n" +
                "ifcEntity: IfcSpace\n" +
                "questionType: Area\n" +
                "entityFilter: vindplan\n" +
                "propertyName: none\n" +
                "```\n\n" +

                "Exempel 4:\n" +
                "Fråga: 'Lista alla väggar på våning 2'\n" +
                "```yaml\n" +
                "ifcEntity: IfcWall\n" +
                "questionType: List\n" +
                "entityFilter: våning 2\n" +
                "propertyName: none\n" +
                "```\n\n" +

                "Exempel 5:\n" +
                "Fråga: 'Finns det en balkong?'\n" +
                "```yaml\n" +
                "ifcEntity: IfcSpace\n" +
                "questionType: Exists\n" +
                "entityFilter: balkong\n" +
                "propertyName: none\n" +
                "```\n\n" +

                "Exempel 6:\n" +
                "Fråga: 'Har byggnaden en hiss?'\n" +
                "```yaml\n" +
                "ifcEntity: IfcTransportElement\n" +
                "questionType: Exists\n" +
                "entityFilter: hiss\n" +
                "propertyName: none\n" +
                "```\n\n" +

                // ✨ NEW: BuildingHeight examples
                "Exempel 7:\n" +
                "Fråga: 'Byggnadshöjd från mark?'\n" +
                "```yaml\n" +
                "ifcEntity: IfcBuildingStorey\n" +
                "questionType: BuildingHeight\n" +
                "entityFilter: none\n" +
                "propertyName: none\n" +
                "```\n\n" +

                "Exempel 8:\n" +
                "Fråga: 'Höjd från mark till första våningen?'\n" +
                "```yaml\n" +
                "ifcEntity: IfcBuildingStorey\n" +
                "questionType: BuildingHeight\n" +
                "entityFilter: none\n" +
                "propertyName: none\n" +
                "```\n\n" +

                "Exempel 9:\n" +
                "Fråga: 'Vilken är takhöjden på vindsplan?'\n" +
                "```yaml\n" +
                "ifcEntity: IfcSpace\n" +
                "questionType: Height\n" +
                "entityFilter: vindsplan\n" +
                "propertyName: none\n" +
                "```\n\n" +

                "Exempel 10:\n" +
                "Fråga: 'Antal plan under mark?'\n" +
                "```yaml\n" +
                "ifcEntity: IfcBuildingStorey\n" +
                "questionType: UndergroundFloors\n" +
                "entityFilter: none\n" +
                "propertyName: none\n" +
                "```\n\n" +

                "Exempel 11:\n" +
                "Fråga: 'Hur många källarplan finns det?'\n" +
                "```yaml\n" +
                "ifcEntity: IfcBuildingStorey\n" +
                "questionType: UndergroundFloors\n" +
                "entityFilter: none\n" +
                "propertyName: none\n" +
                "```\n\n" +

                "VIKTIGT - Skillnad mellan Height och BuildingHeight:\n" +
                "- Använd 'Height' för takhöjd/rumshöjd i specifika utrymmen eller våningar\n" +
                "- Använd 'BuildingHeight' för byggnadens höjd från marknivå/bottenplatta till första våning\n" +
                "- Använd 'UndergroundFloors' för att räkna antal plan under mark/källarplan (>50% under marknivå)";
        }

        public ParseResult<Intent> ParseResponseWithDetails(string aiResponse)
        {
            if (string.IsNullOrWhiteSpace(aiResponse))
            {
                return ParseResult<Intent>.Failure("AI-svaret var tomt eller saknas.");
            }

            try
            {
                var yaml = ExtractYamlContent(aiResponse);

                if (string.IsNullOrWhiteSpace(yaml))
                {
                    var fallbackResult = TryFallbackParsing(aiResponse);
                    if (fallbackResult != null)
                    {
                        return ParseResult<Intent>.Success(
                            fallbackResult,
                            "Använder fallback-parsning (gammalt format)."
                        );
                    }

                    return ParseResult<Intent>.Failure("Inget YAML-innehåll hittades i svaret.");
                }

                // Deserialize directly to Intent
                var response = _deserializer.Deserialize<Intent>(yaml);

                var validationError = ValidateResponse(response);
                if (validationError != null)
                {
                    return ParseResult<Intent>.Failure(validationError);
                }

                return ParseResult<Intent>.Success(response);
            }
            catch (YamlException ex)
            {
                return ParseResult<Intent>.Failure(
                    $"YAML-parsningsfel: {ex.Message}. Kontrollera att formatet är korrekt."
                );
            }
            catch (Exception ex)
            {
                return ParseResult<Intent>.Failure(
                    $"Oväntat fel vid parsning: {ex.Message}"
                );
            }
        }

        public Intent? ParseResponse(string aiResponse)
        {
            var result = ParseResponseWithDetails(aiResponse);

            if (!result.IsSuccess)
            {
                _output.WriteWarning($"Varning vid parsning: {result.ErrorMessage}");
            }

            return result.Data;
        }

        private Intent? TryFallbackParsing(string aiResponse)
        {
            try
            {
                var entityMatch = Regex.Match(aiResponse, @"IFC?Entity:\s*([^,\n]+)", RegexOptions.IgnoreCase);
                var questionMatch = Regex.Match(aiResponse, @"QuestionType:\s*([^,\n]+)", RegexOptions.IgnoreCase);

                if (entityMatch.Success && questionMatch.Success)
                {
                    return new Intent
                    {
                        IfcEntity = entityMatch.Groups[1].Value.Trim(),
                        QuestionType = questionMatch.Groups[1].Value.Trim()
                    };
                }
            }
            catch
            {
                // Fallback parsing failed, return null
            }

            return null;
        }

        private string? ValidateResponse(Intent response)
        {
            if (response == null)
            {
                return "Parsningen resulterade i ett null-objekt.";
            }

            if (string.IsNullOrWhiteSpace(response.IfcEntity))
            {
                return "IfcEntity saknas eller är tom i svaret.";
            }

            if (string.IsNullOrWhiteSpace(response.QuestionType))
            {
                return "QuestionType saknas eller är tom i svaret.";
            }

            // Normalize 'none' values to null
            if (response.EntityFilter?.Equals("none", StringComparison.OrdinalIgnoreCase) == true)
            {
                response.EntityFilter = null;
            }

            if (response.PropertyName?.Equals("none", StringComparison.OrdinalIgnoreCase) == true)
            {
                response.PropertyName = null;
            }

            // Validate and normalize IfcEntity
            if (!AllowedValues.IfcEntities.Contains(response.IfcEntity))
            {
                var closestMatch = AllowedValues.GetClosestMatch(response.IfcEntity, AllowedValues.IfcEntities);
                Console.WriteLine($"Ogiltigt IFC-entitetsvärde '{response.IfcEntity}'. Använder närmaste matchning: '{closestMatch}'");
                response.IfcEntity = closestMatch;
            }

            // Validate and normalize QuestionType
            if (!AllowedValues.QuestionTypes.Contains(response.QuestionType))
            {
                var closestMatch = AllowedValues.GetClosestMatch(response.QuestionType, AllowedValues.QuestionTypes);
                Console.WriteLine($"Ogiltigt frågetypsvärde '{response.QuestionType}'. Använder närmaste matchning: '{closestMatch}'");
                response.QuestionType = closestMatch;
            }

            if (response.IfcEntity.Equals("unknown", StringComparison.OrdinalIgnoreCase) &&
                response.QuestionType.Equals("unknown", StringComparison.OrdinalIgnoreCase))
            {
                return "LLM kunde inte identifiera entitet eller frågetyp.";
            }

            return null;
        }

        public string ExtractIfcEntity(string aiResponse)
        {
            var response = ParseResponse(aiResponse);
            return response?.IfcEntity ?? string.Empty;
        }

        public string ExtractQuestionType(string aiResponse)
        {
            var response = ParseResponse(aiResponse);
            return response?.QuestionType ?? string.Empty;
        }

        private string? ExtractYamlContent(string aiResponse)
        {
            // Extract YAML content between ```yaml and ```
            var yamlMatch = Regex.Match(aiResponse, @"```yaml\s*(.*?)```", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (yamlMatch.Success)
            {
                return yamlMatch.Groups[1].Value.Trim();
            }
            return null;
        }

        public string CreateValidationPrompt(string question, Intent intentResponse, IfcStore model)
        {
            // Simple implementation: combine question, intent, and model context for validation
            var modelContext = GetModelContext(model);
            var intentDetails = $"Entity: {intentResponse.IfcEntity}, Filter: {intentResponse.EntityFilter}, Property: {intentResponse.PropertyName}";
            return $"Validate the following user question and intent:\nQuestion: {question}\nIntent: {intentDetails}\nModel Context: {modelContext}";
        }
    }

    public class ParseResult<T>
    {
        public bool IsSuccess { get; }
        public T? Data { get; }
        public string? ErrorMessage { get; }
        public string? WarningMessage { get; }

        private ParseResult(bool isSuccess, T? data, string? errorMessage, string? warningMessage = null)
        {
            IsSuccess = isSuccess;
            Data = data;
            ErrorMessage = errorMessage;
            WarningMessage = warningMessage;
        }

        public static ParseResult<T> Success(T data, string? warning = null)
        {
            return new ParseResult<T>(true, data, null, warning);
        }

        public static ParseResult<T> Failure(string errorMessage)
        {
            return new ParseResult<T>(false, default, errorMessage);
        }
    }
}