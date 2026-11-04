// Models/Calculators/CountCalculator.cs
using System.Collections.Generic;
using System.Linq;
using System;
using Xbim.Ifc4.Interfaces;
using ifcFireBot.Models.Helpers;

namespace ifcFireBot.Models.Calculators
{
    public class CountCalculator : ICalculator
    {
        private const double BelowGroundThreshold = 0.5; // 50%

        public string Calculate(List<IIfcProduct> elements, Intent intent, IOutputWriter output)
        {
            // For building storey counting, use structured output
            if (elements.FirstOrDefault() is IIfcBuildingStorey)
            {
                return CountStoreysWithStructuredOutput(elements.Cast<IIfcBuildingStorey>().ToList(), output);
            }

            // For other entities, use structured output
            return CountGenericElements(elements, intent, output);
        }

        private string CountStoreysWithStructuredOutput(List<IIfcBuildingStorey> allStoreys, IOutputWriter output)
        {
            if (!allStoreys.Any())
                return "No building storeys found";

            var structured = new StructuredOutputWriter(output);

            // Section 1: Identified storeys
            structured.WriteSectionHeader("Identified Storey Levels");
            structured.WriteSimpleText($"Found {allStoreys.Count} IfcBuildingStorey entities:");
            structured.WriteEmptyLine();

            var storeyNames = allStoreys
                .Select(s => $"  {s.Name?.ToString() ?? "(no name)"}")
                .ToList();
            structured.WriteList(storeyNames, indent: 2);

            // Section 2: Storey classification
            structured.WriteSectionHeader("Storey Classification");

            var realStoreys = StoreyFilterHelper.FilterRealStoreys(allStoreys).ToList();
            var referenceStoreys = allStoreys.Except(realStoreys).ToList();

            if (referenceStoreys.Any())
            {
                structured.WriteSubSection("Reference Levels (excluded):");
                var referenceList = referenceStoreys.Select(s => 
                {
                    var reason = GetExclusionReason(s, allStoreys);
                    return $"  {s.Name} – {reason}";
                }).ToList();
                structured.WriteList(referenceList, indent: 2);
            }

            // Filter out underground storeys (>50% below ground)
            var (aboveGroundStoreys, undergroundStoreys) = FilterUndergroundStoreys(realStoreys, output);

            if (undergroundStoreys.Any())
            {
                structured.WriteSubSection("Underground Floors (>50% below ground, excluded):");
                var undergroundList = undergroundStoreys.Select(s => 
                {
                    var elevation = s.Elevation.HasValue ? $"{s.Elevation.Value / 1000:F2}m" : "N/A";
                    return $"  {s.Name} ({elevation})";
                }).ToList();
                structured.WriteList(undergroundList, indent: 2);
            }

            structured.WriteSubSection("Above-Ground Floors:");
            var aboveGroundNames = aboveGroundStoreys
                .Select(s => $"  {s.Name?.ToString() ?? "(no name)"}")
                .ToList();
            structured.WriteList(aboveGroundNames, indent: 2);

            // Answer section
            structured.WriteAnswer($"{aboveGroundStoreys.Count} floors");

            return $"{aboveGroundStoreys.Count} floors";
        }

        private string CountGenericElements(List<IIfcProduct> elements, Intent intent, IOutputWriter output)
        {
            var structured = new StructuredOutputWriter(output);

            // Section 1: Element count
            structured.WriteSectionHeader($"Counting {intent.IfcEntity}");
            structured.WriteSimpleText($"Total found: {elements.Count}");
            structured.WriteEmptyLine();

            // Section 2: Examples (if any)
            if (elements.Any())
            {
                structured.WriteSectionHeader("Examples of Found Elements");

                var examples = elements.Take(5).Select(e =>
                {
                    var name = e.Name?.ToString() ?? "(no name)";
                    return $"  • {name}";
                }).ToList();

                structured.WriteList(examples, indent: 0);

                if (elements.Count > 5)
                {
                    structured.WriteSimpleText($"... and {elements.Count - 5} more", indent: 2);
                    structured.WriteEmptyLine();
                }
            }

            // Answer
            structured.WriteGenericAnswer($"Total: **{elements.Count} {intent.IfcEntity}**");

            return $"{elements.Count} {intent.IfcEntity}";
        }

        private string GetExclusionReason(IIfcBuildingStorey storey, List<IIfcBuildingStorey> allStoreys)
        {
            var name = storey.Name?.ToString()?.ToLower() ?? "";

            if (name.Contains("mark") || name.Contains("ground"))
                return "ground level";
            if (name.Contains("tak") || name.Contains("roof"))
                return "roof level";

            // Check for elevation gaps
            if (storey.Elevation.HasValue && allStoreys.Any(s => s.Elevation.HasValue))
            {
                var otherElevations = allStoreys
                    .Where(s => s != storey && s.Elevation.HasValue)
                    .Select(s => (double)s.Elevation!.Value)
                    .ToList();

                if (otherElevations.Any())
                {
                    var thisElev = (double)storey.Elevation!.Value;
                    var minGap = otherElevations.Min(e => Math.Abs(e - thisElev));

                    if (minGap > 100000) // 100m gap
                        return $"deviating elevation ({minGap / 1000:F0}m gap)";
                }
            }

            return "reference level";
        }

        /// <summary>
        /// Filters storeys into above-ground and underground based on 50% threshold
        /// </summary>
        private (List<IIfcBuildingStorey> aboveGround, List<IIfcBuildingStorey> underground) 
            FilterUndergroundStoreys(List<IIfcBuildingStorey> realStoreys, IOutputWriter output)
        {
            var aboveGround = new List<IIfcBuildingStorey>();
            var underground = new List<IIfcBuildingStorey>();

            if (!realStoreys.Any())
                return (aboveGround, underground);

            // Detect if using absolute elevation system
            bool isAbsoluteElevation = StoreyFilterHelper.IsAbsoluteElevationSystem(realStoreys);

            var basementStoreys = realStoreys.Where(s => StoreyFilterHelper.IsBasement(s, isAbsoluteElevation)).ToList();
            var aboveGroundCandidates = realStoreys.Where(s => !StoreyFilterHelper.IsBasement(s, isAbsoluteElevation)).ToList();

            var storeysWithElevation = realStoreys
                .Where(s => s.Elevation.HasValue)
                .ToList();

            if (!storeysWithElevation.Any())
            {
                // If no elevation data, return all as above ground
                return (realStoreys, underground);
            }

            var firstFloor = aboveGroundCandidates
                .Where(s => s.Elevation.HasValue)
                .OrderBy(s => (double)s.Elevation!.Value)
                .FirstOrDefault();

            if (firstFloor == null)
            {
                // All storeys are basements, classify them all as underground
                return (aboveGround, realStoreys);
            }

            var firstFloorElevation = (double)firstFloor.Elevation!.Value;

            // Determine ground level
            var groundLevel = ElevationHelper.DetermineGroundLevel(
                realStoreys,
                realStoreys,
                basementStoreys,
                firstFloorElevation,
                output,
                false);

            // Analyze each storey to determine if more than 50% is below ground
            foreach (var storey in realStoreys.Where(s => s.Elevation.HasValue))
            {
                var storeyElevation = (double)storey.Elevation!.Value;

                // Get storey height
                var storeyHeight = GetStoreyHeight(storey, realStoreys, output);

                if (!storeyHeight.HasValue || storeyHeight.Value <= 0)
                {
                    // If we can't determine height, use elevation as indicator
                    if (storeyElevation < groundLevel)
                        underground.Add(storey);
                    else
                        aboveGround.Add(storey);
                    continue;
                }

                // Calculate top elevation of storey
                var topElevation = storeyElevation + storeyHeight.Value;

                // Calculate percentage below ground
                double percentageUnderground = 0;

                if (topElevation <= groundLevel)
                {
                    // Entire floor is below ground
                    percentageUnderground = 1.0;
                }
                else if (storeyElevation >= groundLevel)
                {
                    // Entire floor is above ground
                    percentageUnderground = 0.0;
                }
                else
                {
                    // Floor spans ground level - calculate percentage below
                    var heightBelowGround = groundLevel - storeyElevation;
                    percentageUnderground = heightBelowGround / storeyHeight.Value;
                }

                // Add to appropriate list based on 50% threshold
                if (percentageUnderground > BelowGroundThreshold)
                {
                    underground.Add(storey);
                }
                else
                {
                    aboveGround.Add(storey);
                }
            }

            // Handle storeys without elevation data
            foreach (var storey in realStoreys.Where(s => !s.Elevation.HasValue))
            {
                // Default to above ground if no elevation info
                aboveGround.Add(storey);
            }

            return (aboveGround, underground);
        }

        /// <summary>
        /// Gets the height of a storey
        /// </summary>
        private double? GetStoreyHeight(IIfcBuildingStorey storey, List<IIfcBuildingStorey> allStoreys, IOutputWriter output)
        {
            // Method 1: Try to get from properties
            var heightFromProps = IfcPropertyHelper.GetHeightFromProperties(storey);
            if (heightFromProps.HasValue)
            {
                return heightFromProps.Value;
            }

            // Method 2: Calculate from elevation difference to next storey
            var heightFromElevation = StoreyFilterHelper.CalculateStoreyHeightFromNeighbor(storey, allStoreys);
            if (heightFromElevation.HasValue)
            {
                return heightFromElevation.Value;
            }

            // Method 3: Try to get from spaces
            var heightFromSpaces = StoreyFilterHelper.GetMaxHeightFromSpaces(storey);
            if (heightFromSpaces.HasValue)
            {
                return heightFromSpaces.Value;
            }

            // Method 4: Use default typical storey height
            return 2800.0; // 2.8m in mm
        }
    }
}