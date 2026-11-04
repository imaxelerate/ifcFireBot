// Models/Calculators/HeightCalculator.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xbim.Ifc4.Interfaces;
using ifcFireBot.Models.Helpers;

namespace ifcFireBot.Models.Calculators
{
    public class HeightCalculator : ICalculator
    {
        private const double AtticHeightThreshold = 1900.0; // 1.9m in mm

        public string Calculate(List<IIfcProduct> elements, Intent intent, IOutputWriter output)
        {
            if (elements.Count == 0)
                return "No elements to calculate height for.";

            return elements.FirstOrDefault() switch
            {
                IIfcSpace => CalculateSpaceHeight(elements.OfType<IIfcSpace>(), intent, output),
                IIfcBuildingStorey => CalculateStoreyHeight(elements.OfType<IIfcBuildingStorey>(), intent, output),
                _ => $"Height calculation not supported for {elements.First().GetType().Name}"
            };
        }

        private string CalculateSpaceHeight(IEnumerable<IIfcSpace> spaces, Intent intent, IOutputWriter output)
        {
            var spaceList = spaces.ToList();

            if (spaceList.Count == 0)
                return "No spaces found.";

            var structured = new StructuredOutputWriter(output);

            double? maxHeight = null;
            double? minHeight = null;
            var heightDetails = new List<(string name, double height)>();

            // Section 1: Space identification
            structured.WriteSectionHeader("Identified Spaces");
            structured.WriteSimpleText($"Analyzing {spaceList.Count} spaces on {intent.EntityFilter}");
            structured.WriteEmptyLine();

            foreach (var space in spaceList)
            {
                var height = IfcPropertyHelper.GetHeightFromProperties(space);
                
                if (!height.HasValue)
                {
                    var geometryHeight = GeometryHelper.GetSpaceHeight(space);
                    if (geometryHeight > 0)
                    {
                        height = geometryHeight * 1000.0;
                    }
                }
                
                if (height.HasValue && !double.IsNaN(height.Value) && !double.IsInfinity(height.Value) && height.Value >= 0)
                {
                    heightDetails.Add((space.Name?.ToString() ?? "Unnamed", height.Value));
                    
                    if (!maxHeight.HasValue || height.Value > maxHeight.Value)
                        maxHeight = height.Value;
                    
                    if (!minHeight.HasValue || height.Value < minHeight.Value)
                        minHeight = height.Value;
                }
            }

            // Fallback to storey height if no space heights found
            if (!maxHeight.HasValue && spaceList.Any())
            {
                var firstSpace = spaceList.First();
                var storey = StoreyFilterHelper.GetParentStorey(firstSpace);
                
                if (storey != null)
                {
                    var storeyHeight = IfcPropertyHelper.GetHeightFromProperties(storey);
                    if (storeyHeight.HasValue)
                    {
                        maxHeight = storeyHeight.Value;
                        minHeight = storeyHeight.Value;
                    }
                    else
                    {
                        var calcHeight = StoreyFilterHelper.CalculateStoreyHeightFromNeighbor(storey);
                        if (calcHeight.HasValue)
                        {
                            maxHeight = calcHeight.Value;
                            minHeight = calcHeight.Value;
                        }
                    }
                }
            }

            if (!maxHeight.HasValue)
            {
                return "Could not find height information for these spaces.\n\n💡 Tip: Check that the IFC file contains height properties.";
            }

            // Section 2: Height analysis
            structured.WriteSectionHeader("Height Analysis");
            
            structured.WriteSubSection("Measurements:");
            structured.WriteSimpleText($"Maximum ceiling height: {maxHeight.Value / 1000:F2}m");
            
            if (minHeight.HasValue && minHeight.Value != maxHeight.Value)
            {
                structured.WriteSimpleText($"Minimum ceiling height: {minHeight.Value / 1000:F2}m");
            }
            structured.WriteEmptyLine();

            if (heightDetails.Any())
            {
                structured.WriteSubSection("Per Space:");
                var detailList = heightDetails.Select(d => $"{d.name}: {d.height / 1000:F2}m").ToList();
                structured.WriteList(detailList, indent: 2);
            }

            // Answer section
            if (minHeight.HasValue && minHeight.Value != maxHeight.Value)
            {
                structured.WriteAnswer($"{minHeight.Value / 1000:F2}m - {maxHeight.Value / 1000:F2}m ceiling height");
            }
            else
            {
                structured.WriteAnswer($"{maxHeight.Value / 1000:F2}m ceiling height");
            }

            // Section 3: Assessment
            structured.WriteSectionHeader("Assessment");
            
            if (maxHeight.Value < AtticHeightThreshold)
            {
                output.WriteWarning($"⚠️  Ceiling height is below 1.9m - may limit use according to building codes");
            }
            else
            {
                output.WriteSuccess($"✓ Ceiling height meets minimum requirements (>1.9m)");
            }

            return $"{maxHeight.Value / 1000:F2}m";
        }

        private string CalculateStoreyHeight(IEnumerable<IIfcBuildingStorey> storeys, Intent intent, IOutputWriter output)
        {
            var storeyList = storeys.ToList();

            if (storeyList.Count == 0)
                return "No building storeys found.";

            var structured = new StructuredOutputWriter(output);

            // Filter to only real building storeys (exclude reference levels)
            var realStoreys = StoreyFilterHelper.FilterRealStoreys(storeyList).ToList();

            if (!realStoreys.Any())
                return "No real building storeys found (only reference levels).";

            // Determine if using absolute elevation system
            bool isAbsoluteElevation = StoreyFilterHelper.IsAbsoluteElevationSystem(realStoreys);

            // Filter to only above-ground storeys (including attic)
            var aboveGroundStoreys = realStoreys
                .Where(s => !StoreyFilterHelper.IsBasement(s, isAbsoluteElevation))
                .ToList();

            if (!aboveGroundStoreys.Any())
                return "No building storeys above ground found.";

            // Section 1: Storey identification
            structured.WriteSectionHeader("Storey Identification");
            structured.WriteSimpleText($"Found {aboveGroundStoreys.Count} building storeys above ground (including attic if present).");
            structured.WriteEmptyLine();

            var heights = new List<(string name, double height, double elevation)>();

            foreach (var storey in aboveGroundStoreys)
            {
                var height = IfcPropertyHelper.GetHeightFromProperties(storey);

                if (!height.HasValue)
                {
                    height = StoreyFilterHelper.CalculateStoreyHeightFromNeighbor(storey);
                }

                if (height.HasValue)
                {
                    var elevation = storey.Elevation.HasValue ? (double)storey.Elevation.Value : 0;
                    heights.Add((storey.Name?.ToString() ?? "Unnamed", height.Value, elevation));
                }
            }

            if (!heights.Any())
                return "Could not find height information for these building storeys.";

            // Order by elevation (lowest to highest) for logical ordering
            heights = heights.OrderBy(h => h.elevation).ToList();

            // Section 2: Storey Heights - list all heights
            structured.WriteSectionHeader("Storey Heights (Ceiling/Roof Heights)");

            var heightList = heights.Select(h => $"{h.name}: {h.height / 1000:F2}m").ToList();
            structured.WriteList(heightList, indent: 2);
            structured.WriteEmptyLine();

            // Answer section - list all storey heights with names
            if (heights.Count == 1)
            {
                structured.WriteAnswer($"{heights[0].name}: {heights[0].height / 1000:F2}m");
                return $"{heights[0].name}: {heights[0].height / 1000:F2}m";
            }
            else
            {
                // Build answer with all storey heights (ordered by elevation)
                var allHeights = string.Join(", ", heights.Select(h => $"{h.name}: {h.height / 1000:F2}m"));
                structured.WriteAnswer($"{allHeights}");

                // Return format: include storey names for traceability
                return allHeights;
            }
        }
    }
}