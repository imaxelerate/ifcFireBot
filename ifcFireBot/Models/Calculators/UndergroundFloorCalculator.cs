// Models/Calculators/UndergroundFloorCalculator.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Xbim.Ifc4.Interfaces;
using ifcFireBot.Models.Helpers;

#nullable enable

namespace ifcFireBot.Models.Calculators
{
    /// <summary>
    /// Calculates the number of floors below ground level
    /// A floor counts as "below ground" if more than 50% is below ground level
    /// </summary>
    public class UndergroundFloorCalculator : ICalculator
    {
        private const double BelowGroundThreshold = 0.5; // 50%

        public string Calculate(List<IIfcProduct> elements, Intent intent, IOutputWriter output)
        {
            if (elements.FirstOrDefault() is IIfcBuildingStorey storey)
            {
                var model = storey.Model;
                var allStoreys = model.Instances.OfType<IIfcBuildingStorey>().ToList();
                return CalculateUndergroundFloors(allStoreys, output);
            }
            else if (elements.FirstOrDefault() is IIfcBuilding building)
            {
                var storeys = building.IsDecomposedBy
                    .SelectMany(rel => rel.RelatedObjects)
                    .OfType<IIfcBuildingStorey>()
                    .ToList();
                return CalculateUndergroundFloors(storeys, output);
            }
            else
            {
                var model = elements.FirstOrDefault()?.Model;
                if (model != null)
                {
                    var allStoreys = model.Instances.OfType<IIfcBuildingStorey>().ToList();
                    return CalculateUndergroundFloors(allStoreys, output);
                }
            }

            return "Could not find building storeys to calculate underground floors.";
        }

        private string CalculateUndergroundFloors(List<IIfcBuildingStorey> allStoreys, IOutputWriter output)
        {
            if (!allStoreys.Any())
                return "No building storeys found in the model.";

            var structured = new StructuredOutputWriter(output);
            var realStoreys = StoreyFilterHelper.FilterRealStoreys(allStoreys, output).ToList();

            if (!realStoreys.Any())
                return "No real building storeys found (only reference levels).";

            // Detect if using absolute elevation system
            bool isAbsoluteElevation = StoreyFilterHelper.IsAbsoluteElevationSystem(realStoreys);

            var aboveGroundStoreys = realStoreys.Where(s => !StoreyFilterHelper.IsBasement(s, isAbsoluteElevation)).ToList();
            var basementStoreys = realStoreys.Where(s => StoreyFilterHelper.IsBasement(s, isAbsoluteElevation)).ToList();

            var storeysWithElevation = realStoreys
                .Where(s => s.Elevation.HasValue)
                .ToList();

            if (!storeysWithElevation.Any())
                return "Could not find elevation data for building storeys.";

            var firstFloor = aboveGroundStoreys
                .Where(s => s.Elevation.HasValue)
                .OrderBy(s => (double)s.Elevation!.Value)
                .FirstOrDefault();

            if (firstFloor == null)
            {
                return FormatUndergroundFloorOutput(structured, basementStoreys, basementStoreys, null, null);
            }

            var firstFloorElevation = (double)firstFloor.Elevation!.Value;

            // Determine ground level
            var groundLevel = ElevationHelper.DetermineGroundLevel(
                allStoreys,
                realStoreys,
                basementStoreys,
                firstFloorElevation,
                output,
                false);

            // Section 1: Ground level determination
            structured.WriteSectionHeader("Ground Level Analysis");
            structured.WriteSimpleText($"Determined ground level: {groundLevel / 1000:F2}m");
            structured.WriteSimpleText($"First floor above ground: {firstFloor.Name} ({firstFloorElevation / 1000:F2}m)");
            structured.WriteEmptyLine();

            // Analyze each storey to determine if more than 50% is below ground
            var undergroundFloors = new List<(IIfcBuildingStorey storey, double percentageUnderground, double height)>();
            var allFloorData = new List<(IIfcBuildingStorey storey, double percentageUnderground, double height)>();

            foreach (var storey in realStoreys.Where(s => s.Elevation.HasValue))
            {
                var storeyElevation = (double)storey.Elevation!.Value;

                // Get storey height
                var storeyHeight = GetStoreyHeight(storey, realStoreys, output);

                if (!storeyHeight.HasValue || storeyHeight.Value <= 0)
                {
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

                // Add to all floor data
                allFloorData.Add((storey, percentageUnderground, storeyHeight.Value));

                // Add to underground list if >50%
                if (percentageUnderground > BelowGroundThreshold)
                {
                    undergroundFloors.Add((storey, percentageUnderground, storeyHeight.Value));
                }
            }

            return FormatUndergroundFloorOutput(
                structured,
                undergroundFloors.Select(f => f.storey).ToList(),
                basementStoreys,
                groundLevel,
                allFloorData);
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

        private string FormatUndergroundFloorOutput(
            StructuredOutputWriter structured,
            List<IIfcBuildingStorey> undergroundFloors,
            List<IIfcBuildingStorey> basementStoreys,
            double? groundLevel,
            List<(IIfcBuildingStorey storey, double percentageUnderground, double height)>? allFloorData)
        {
            // Section 2: Underground floor classification
            structured.WriteSectionHeader("Underground Floor Classification");
            structured.WriteSimpleText("A floor is classified as underground if >50% of the floor is below ground level");
            structured.WriteEmptyLine();

            if (undergroundFloors.Any())
            {
                structured.WriteSubSection($"Underground floors: {undergroundFloors.Count}");
                structured.WriteEmptyLine();

                var orderedUnderground = undergroundFloors
                    .OrderBy(s => s.Elevation.HasValue ? (double)s.Elevation.Value : double.MaxValue)
                    .ToList();

                if (allFloorData != null)
                {
                    var undergroundData = allFloorData
                        .Where(f => f.percentageUnderground > 0.5)
                        .OrderBy(f => (double)f.storey.Elevation!.Value)
                        .ToList();

                    var floorDetails = undergroundData.Select(floor =>
                    {
                        var storey = floor.storey;
                        var elevation = (double)storey.Elevation!.Value;
                        var topElevation = elevation + floor.height;
                        var icon = floor.percentageUnderground >= 1.0 ? "■" : "▤";
                        return $"  {icon} {storey.Name}\n" +
                               $"     Elevation: {elevation / 1000:F2}m - {topElevation / 1000:F2}m\n" +
                               $"     Below ground: {floor.percentageUnderground * 100:F0}%";
                    }).ToList();

                    structured.WriteList(floorDetails, indent: 0);
                }
                else
                {
                    var floorList = orderedUnderground.Select(storey =>
                    {
                        var elevation = storey.Elevation.HasValue ? (double)storey.Elevation.Value : 0;
                        return $"  • {storey.Name} (elevation: {elevation / 1000:F2}m)";
                    }).ToList();

                    structured.WriteList(floorList, indent: 0);
                }
            }
            else
            {
                structured.WriteSimpleText("No floors identified as underground.");
                structured.WriteEmptyLine();
            }

            // Additional information
            if (basementStoreys.Any() && basementStoreys.Count != undergroundFloors.Count)
            {
                structured.WriteSubSection("Note:");
                structured.WriteSimpleText($"{basementStoreys.Count} floors were initially identified as basements based on name/properties,");
                structured.WriteSimpleText($"but only {undergroundFloors.Count} of these have >50% below ground level.");
                structured.WriteEmptyLine();
            }

            // Answer
            structured.WriteAnswer($"{undergroundFloors.Count} underground floors");

            return $"{undergroundFloors.Count} underground floors";
        }
    }
}