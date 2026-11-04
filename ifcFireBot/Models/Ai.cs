using Microsoft.Isam.Esent.Interop;
using Betalgo.Ranul.OpenAI;
using Betalgo.Ranul.OpenAI.Managers;
using Betalgo.Ranul.OpenAI.ObjectModels;
using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;



namespace ifcFireBot.Models
{
    public class Ai
    {
		public async Task Main() 
		{
			var ai = new Ai();
			// Get user prompt
			Console.Write("Enter your prompt: ");
			string userPrompt = Console.ReadLine() ?? string.Empty;

			


		}
		public OpenAIService InitAiConnection(string apiKey)
		{
			var accessToken = apiKey;
			var baseDomain = "https://router.huggingface.co/v1";

			var api = new OpenAIService(new OpenAIOptions()
			{
				ApiKey = accessToken,
				BaseDomain = baseDomain
			});

			return api;
		}

		public ChatCompletionCreateRequest Chat(string message, bool useStructuredOutput = true) 
		{
			var systemPrompt = @"Du är en expert på IFC-modeller och Building Information Modeling (BIM).

			VIKTIGA REGLER FÖR ENTITETS- OCH FRÅGETYPSVAL:

			1. **BuildingHeight (Byggnadshöjd)** - Använd när frågan handlar om:
			   - Totala höjden från mark till taket
			   - ""building height"", ""byggnadshöjd""
			   - Höjd från mark till byggnadens högsta punkt

			   Exempel:
			   ✓ ""what is the building height?"" → BuildingHeight
			   ✓ ""hur hög är byggnaden?"" → BuildingHeight

			2. **Height (Höjd)** - För våningshöjder och rumshöjder:

			   a) Använd **IfcBuildingStorey** när frågan handlar om:
				  - Våningshöjd mellan golv och tak/tak
				  - ""maximal våningshöjd"", ""maximum roof height of storeys""
				  - ""höjd mellan våningar""
				  - Våningsavstånd

				  Exempel:
				  ✓ ""maximum roof height of all building storeys"" → Height + IfcBuildingStorey
				  ✓ ""maximal våningshöjd"" → Height + IfcBuildingStorey

			   b) Använd **IfcSpace** när frågan handlar om:
				  - Takhöjd, rumshöjd i specifika utrymmen
				  - ""maximal takhöjd"", ""takhöjd på vindsplan""
				  - Höjd INOM ett specifikt rum/utrymme (ceiling height)

				  Exempel:
				  ✓ ""maximal höjd på vindsplan"" → Height + IfcSpace + entityFilter: vindsplan
				  ✓ ""takhöjd vind"" → Height + IfcSpace + entityFilter: vind
				  ✓ ""rumshöjd plan 2"" → Height + IfcSpace + entityFilter: plan 2

			3. **Area** - Använd IfcSpace för utrymmen, IfcBuildingStorey för hela våningar

			4. **Count** - ""hur många"", ""antal""

			5. **List** - ""lista"", ""vilka"", ""visa""

			6. **Exists** - ""finns det"", ""har""

			YAML-format (svara ENDAST med detta):
						";

			var completionRequest = new ChatCompletionCreateRequest
			{
				Model = "meta-llama/Meta-Llama-3-8B-Instruct",
				Messages = new List<ChatMessage> 
				{ 
					ChatMessage.FromSystem(systemPrompt),
					ChatMessage.FromUser(message)
				},
				Temperature = 0.1f, // Lower temperature = more deterministic
                MaxTokens = 150,  // Slightly increased for better reasoning
				TopP = 0.9f,	//Nucleus Sampling
				FrequencyPenalty = 0.0f,
				PresencePenalty = 0.0f,
				Stream = true
			};

			return completionRequest;
		}

		public async Task<string> StreamChat(OpenAIService api, ChatCompletionCreateRequest completionRequest, bool showOutput = true) 
		{
			var responseBuilder = new StringBuilder();

			try
			{
				await foreach (var completion in api.ChatCompletion.CreateCompletionAsStream(completionRequest))
				{
					if (completion.Successful)
					{
						var content = completion.Choices.FirstOrDefault()?.Message.Content;
						if (!string.IsNullOrEmpty(content))
						{
							// Only show streaming in real-time if showOutput is true (debug mode)
							if (showOutput)
							{
								Console.Write(content);
							}
							responseBuilder.Append(content);  // Always accumulate the response
						}
					}
					else
					{
						// Extract error information from the API response
						var errorMsg = completion.Error?.Message ?? "Unknown API error";
						var errorCode = completion.Error?.Code ?? "N/A";
						var errorType = completion.Error?.Type ?? "unknown";

						// Log the error if output is enabled
						if (showOutput)
						{
							Console.WriteLine($"\nAPI Error: {errorMsg} (Code: {errorCode}, Type: {errorType})");
						}

						// Throw exception to properly handle the error upstream
						throw new InvalidOperationException(
							$"OpenAI API error: {errorMsg} (Code: {errorCode}, Type: {errorType})"
						);
					}
				}
			}
			catch (System.Text.Json.JsonException jsonEx)
			{
				// Catch JSON deserialization errors from the SDK
				// This often happens when HuggingFace returns error responses in a different format
				// than what the OpenAI SDK expects
				if (showOutput)
				{
					Console.WriteLine($"\nJSON Deserialization Error: {jsonEx.Message}");
				}

				// Check if this is likely a rate limiting error based on the error path
				// HuggingFace returns errors with different JSON structure than OpenAI
				if (jsonEx.Message.Contains("$.error") || jsonEx.Message.Contains("Error"))
				{
					throw new InvalidOperationException(
						$"API rate limit or service error detected. The API returned an error response that couldn't be parsed. Please wait a moment and try again."
					);
				}

				throw new InvalidOperationException(
					$"Failed to parse API response. Error: {jsonEx.Message}"
				);
			}
			catch (System.Net.Http.HttpRequestException httpEx)
			{
				// Catch network errors (DNS, connection issues, etc.)
				if (showOutput)
				{
					Console.WriteLine($"\nNetwork Error: {httpEx.Message}");
				}
				throw new InvalidOperationException(
					$"Network error connecting to API: {httpEx.Message}"
				);
			}
			catch (System.Net.Sockets.SocketException sockEx)
			{
				// Catch socket errors (connection refused, host not found, etc.)
				if (showOutput)
				{
					Console.WriteLine($"\nConnection Error: {sockEx.Message}");
				}
				throw new InvalidOperationException(
					$"Connection error: {sockEx.Message}"
				);
			}
			catch (TaskCanceledException tcEx)
			{
				// Catch timeout errors
				if (showOutput)
				{
					Console.WriteLine($"\nTimeout Error: {tcEx.Message}");
				}
				throw new InvalidOperationException(
					$"Request timed out: {tcEx.Message}"
				);
			}
			catch (Exception ex) when (ex is not InvalidOperationException)
			{
				// Catch any other unexpected errors
				if (showOutput)
				{
					Console.WriteLine($"\nUnexpected Error: {ex.GetType().Name} - {ex.Message}");
				}
				throw new InvalidOperationException(
					$"Unexpected error during API call: {ex.GetType().Name} - {ex.Message}",
					ex
				);
			}

			if (showOutput)
			{
				Console.WriteLine();  // Add newline after streaming completes (only in debug mode)
			}
			
			// Validate that we got a response
			var response = responseBuilder.ToString();
			if (string.IsNullOrWhiteSpace(response))
			{
				throw new InvalidOperationException("Received empty response from OpenAI API");
			}
			
			return response;
		}
        

    }
}
