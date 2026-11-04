// Models/Calculators/ExistsCalculator.cs
using System.Collections.Generic;
using System.Linq;
using Xbim.Ifc4.Interfaces;

namespace ifcFireBot.Models.Calculators
{
    public class ExistsCalculator : ICalculator
    {
        public string Calculate(List<IIfcProduct> elements, Intent intent, IOutputWriter output)
        {
            var structured = new StructuredOutputWriter(output);

            // Section 1: Search results
            structured.WriteSectionHeader($"Search for {intent.IfcEntity}");
            
            if (!string.IsNullOrEmpty(intent.EntityFilter))
            {
                structured.WriteSimpleText($"Filter criteria: '{intent.EntityFilter}'");
                structured.WriteEmptyLine();
            }

            structured.WriteSimpleText($"Number found: {elements.Count}");
            structured.WriteEmptyLine();

            if (elements.Count > 0)
            {
                // Section 2: Found elements
                structured.WriteSectionHeader("Found Elements");
                
                structured.WriteSubSection("Examples of found elements:");
                structured.WriteEmptyLine();

                var examples = elements.Take(5).Select(e => 
                {
                    var name = e.Name?.ToString() ?? "(no name)";
                    var id = e.GlobalId;
                    return $"  - {name} (ID: {id})";
                }).ToList();
                
                structured.WriteList(examples, indent: 0);

                if (elements.Count > 5)
                {
                    structured.WriteSimpleText($"... and {elements.Count - 5} more", indent: 2);
                    structured.WriteEmptyLine();
                }

                // Answer
                structured.WriteGenericAnswer($"**Yes**, there are {elements.Count} {intent.IfcEntity}" +
                    (elements.Count == 1 ? "" : "s") + " in the building.");

                return $"Yes, {elements.Count} found";
            }
            else
            {
                // No elements found
                structured.WriteSectionHeader("No Match");
                
                structured.WriteSimpleText($"No {intent.IfcEntity}s found in the model.");
                
                if (!string.IsNullOrEmpty(intent.EntityFilter))
                {
                    structured.WriteSimpleText($"The filter '{intent.EntityFilter}' matched no elements.");
                }
                
                structured.WriteEmptyLine();

                structured.WriteSubSection("Possible reasons:");
                structured.WriteEmptyLine();
                
                var suggestions = new List<string>
                {
                    $"The model contains no {intent.IfcEntity}s",
                    "The element may have a different name in the model",
                    "The element may be classified as a different IFC type"
                };
                
                structured.WriteBulletList(suggestions, indent: 2);
                structured.WriteEmptyLine();

                // Answer
                structured.WriteGenericAnswer($"**No**, no {intent.IfcEntity}s" +
                    (string.IsNullOrEmpty(intent.EntityFilter) ? "" : $" matching '{intent.EntityFilter}'") +
                    " found in the building.");

                return $"No, none found";
            }
        }
    }
}