using System;
using System.Collections.Generic;
using System.Linq;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;

#nullable enable

namespace ifcFireBot.Models
{
    public class FetchIfcEnteties
    {
        // Fetches entities by IFC class name (e.g., "IfcWall", "IfcDoor")
        public List<IIfcProduct> FetchByClassName(IfcStore model, string className)
        {
            return model.Instances.OfType<IIfcProduct>()
                .Where(e => e.GetType().Name.Equals(className, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }


        // Fetches entities based on parsed intent
        public List<IIfcProduct> FetchByIntent(IfcStore model, Intent intent)
        {
            return FetchByClassName(model, intent.IfcEntity);
        }

        /// <summary>
        /// Fetches entities by intent with name/filter matching.
        /// Uses exact match first, then falls back to contains match (without splitting the filter).
        /// </summary>
        public List<IIfcProduct> FetchByIntentWithFilter(IfcStore model, Intent intent)
        {
            if (string.IsNullOrEmpty(intent.EntityFilter))
                return new List<IIfcProduct>();

            // First try exact match (case-insensitive)
            var exactMatch = model.Instances.OfType<IIfcProduct>()
                .Where(e => e.GetType().Name.Equals(intent.IfcEntity, StringComparison.OrdinalIgnoreCase) &&
                            e.Name != null &&
                            e.Name.Value.ToString().Equals(intent.EntityFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();
            
            if (exactMatch.Count > 0)
                return exactMatch;

            // Then try contains match with full filter string (not split by spaces)
            var elements = model.Instances.OfType<IIfcProduct>()
                .Where(e => e.GetType().Name.Equals(intent.IfcEntity, StringComparison.OrdinalIgnoreCase) &&
                            e.Name != null &&
                            e.Name.Value.ToString().IndexOf(intent.EntityFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
            
            // Fallback: Check LongName for spatial structure elements
            if (elements.Count == 0)
            {
                var spatialTypes = new[] { "IfcSpace", "IfcBuildingStorey", "IfcBuilding", "IfcSite", "IfcZone" };
                
                if (spatialTypes.Any(t => t.Equals(intent.IfcEntity, StringComparison.OrdinalIgnoreCase)))
                {
                    elements = model.Instances.OfType<IIfcSpatialStructureElement>()
                        .Where(e => e.GetType().Name.Equals(intent.IfcEntity, StringComparison.OrdinalIgnoreCase) &&
                                   ((e.Name != null && e.Name.Value.ToString().IndexOf(intent.EntityFilter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                    (e.LongName != null && e.LongName.Value.ToString().IndexOf(intent.EntityFilter, StringComparison.OrdinalIgnoreCase) >= 0)))
                        .Cast<IIfcProduct>()
                        .ToList();
                }
            }
            
            return elements;
        }

        // Generic method to fetch entities by interface type
        public List<T> FetchByType<T>(IfcStore model) where T : class, IIfcProduct
        {
            return model.Instances.OfType<T>().ToList();
        }

        /// <summary>
        /// Applies entity filter with special logic for storey detection when dealing with IfcSpace queries.
        /// </summary>
        public List<IIfcProduct> ApplyEntityFilter(List<IIfcProduct> elements, Intent intent, IfcStore model)
        {
            // Normalize "none" to empty string
            var normalizedFilter = intent.EntityFilter?.Trim();
            if (string.Equals(normalizedFilter, "none", StringComparison.OrdinalIgnoreCase) || 
                string.IsNullOrWhiteSpace(normalizedFilter))
            {
                normalizedFilter = string.Empty;
            }

            if (string.IsNullOrEmpty(normalizedFilter))
            {
                // No filter specified - check if we should interpret generically
                // For volume questions about "plan", interpret as all building storeys
                if (intent.IfcEntity == "IfcSpace" && intent.QuestionType == "Volume")
                {
                    Console.WriteLine($"Interpreting as question about all building storeys");
                    var allStoreys = model.Instances.OfType<IIfcBuildingStorey>().Cast<IIfcProduct>().ToList();
                    return allStoreys;
                }
                
                return elements;
            }

            // Special case: If AI returned IfcSpace with a storey name filter,
            // check if user is actually asking about a storey, not individual spaces
            if (intent.IfcEntity == "IfcSpace")
            {
                // Don't try to match generic terms like "plan", "våning", "storey" as specific storey names
                var genericStoreyTerms = new[] { "plan", "våning", "storey", "floor", "level" };
                var isGenericTerm = genericStoreyTerms.Any(term => 
                    normalizedFilter.Equals(term, StringComparison.OrdinalIgnoreCase));

                if (!isGenericTerm)
                {
                    var matchingStorey = TryFindStoreyByFilter(normalizedFilter, model);
                    if (matchingStorey != null)
                    {
                        // User is asking about a specific storey - switch to storey-based calculation
                        Console.WriteLine($"Interpreting as question about storey: '{matchingStorey.Name}'");
                        return new List<IIfcProduct> { matchingStorey };
                    }
                    else
                    {
                        // No matching storey, filter spaces by name as originally intended
                        var filteredElements = FetchByIntentWithFilter(model, intent);
                        Console.WriteLine($"After filtering: {filteredElements.Count} elements with '{normalizedFilter}' in the name");
                        return filteredElements;
                    }
                }
                else
                {
                    // Generic term detected - get all storeys in the building
                    Console.WriteLine($"Interpreting as question about all building storeys");
                    var allStoreys = model.Instances.OfType<IIfcBuildingStorey>().Cast<IIfcProduct>().ToList();
                    return allStoreys;
                }
            }
            else
            {
                var filteredElements = FetchByIntentWithFilter(model, intent);
                Console.WriteLine($"After filtering: {filteredElements.Count} elements with '{normalizedFilter}' in the name");
                return filteredElements;
            }
        }

        /// <summary>
        /// Tries to find a building storey that matches the filter text.
        /// Returns the storey if found, null otherwise.
        /// </summary>
        private IIfcBuildingStorey? TryFindStoreyByFilter(string filterText, IfcStore model)
        {
            var normalizedFilter = filterText.ToLower().Trim();
            
            var matchingStorey = model.Instances.OfType<IIfcBuildingStorey>()
                .FirstOrDefault(s => 
                    s.Name?.ToString()?.Contains(normalizedFilter, StringComparison.OrdinalIgnoreCase) == true);
            
            return matchingStorey;
        }
    }
}