// Models/Calculators/BuildingHeightCalculator.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Xbim.Ifc4.Interfaces;
using ifcFireBot.Models.Helpers;

namespace ifcFireBot.Models.Calculators
{
    public class BuildingHeightCalculator : ICalculator
    {
        public string Calculate(List<IIfcProduct> elements, Intent intent, IOutputWriter output)
        {
            if (elements.FirstOrDefault() is IIfcBuildingStorey storey)
            {
                var model = storey.Model;
                var allStoreys = model.Instances.OfType<IIfcBuildingStorey>().ToList();
                return CalculateBuildingHeightFromStoreys(allStoreys, output);
            }
            else if (elements.FirstOrDefault() is IIfcBuilding building)
            {
                var storeys = building.IsDecomposedBy
                    .SelectMany(rel => rel.RelatedObjects)
                    .OfType<IIfcBuildingStorey>()
                    .ToList();
                return CalculateBuildingHeightFromStoreys(storeys, output);
            }
            else
            {
                var model = elements.FirstOrDefault()?.Model;
                if (model != null)
                {
                    var allStoreys = model.Instances.OfType<IIfcBuildingStorey>().ToList();
                    return CalculateBuildingHeightFromStoreys(allStoreys, output);
                }
            }
            
            return "Could not find building storeys to calculate building height.";
        }

        private string CalculateBuildingHeightFromStoreys(List<IIfcBuildingStorey> allStoreys, IOutputWriter output)
        {
            if (!allStoreys.Any())
                return "No building storeys found in the model.";

            var structured = new StructuredOutputWriter(output);
            var realStoreys = StoreyFilterHelper.FilterRealStoreys(allStoreys).ToList();

            if (!realStoreys.Any())
                return "No real building storeys found (only reference levels).";

            bool isAbsoluteElevation = StoreyFilterHelper.IsAbsoluteElevationSystem(realStoreys);

            // Section 1: Identified storeys
            structured.WriteSectionHeader("Storey Identification");
            structured.WriteSimpleText($"Found {realStoreys.Count} real building storeys.");
            structured.WriteEmptyLine();

            var aboveGroundStoreys = realStoreys.Where(s => !StoreyFilterHelper.IsBasement(s, isAbsoluteElevation)).ToList();
            var basementStoreys = realStoreys.Where(s => StoreyFilterHelper.IsBasement(s, isAbsoluteElevation)).ToList();

            if (!aboveGroundStoreys.Any())
                return "No floors above ground found.";

            var storeysWithElevation = aboveGroundStoreys
                .Where(s => s.Elevation.HasValue)
                .OrderBy(s => (double)s.Elevation.Value)
                .ToList();

            if (!storeysWithElevation.Any())
                return "Could not find elevation data for floors above ground.";

            var firstFloor = storeysWithElevation.First();
            var firstFloorElevation = (double)firstFloor.Elevation!.Value;

            var highestFloor = storeysWithElevation.Last();
            var highestFloorElevation = (double)highestFloor.Elevation!.Value;

            // Find the actual top of the building (roof or top of highest floor)
            double buildingTop = highestFloorElevation;
            string topSource = "highest floor slab";

            // Method 1: Try to find roof elements
            var model = highestFloor.Model;
            var roofElements = model.Instances.OfType<IIfcRoof>().ToList();

            if (roofElements.Any())
            {
                double? maxRoofElevation = null;
                foreach (var roof in roofElements)
                {
                    var roofElevation = ElevationHelper.GetElementTopElevation(roof);
                    if (roofElevation.HasValue && (!maxRoofElevation.HasValue || roofElevation.Value > maxRoofElevation.Value))
                    {
                        maxRoofElevation = roofElevation.Value;
                    }
                }

                if (maxRoofElevation.HasValue && maxRoofElevation.Value > buildingTop)
                {
                    buildingTop = maxRoofElevation.Value;
                    topSource = "roof element";
                }
            }

            // Method 2: Calculate top of highest floor using storey height
            var highestFloorHeight = ElevationHelper.GetStoreyHeight(highestFloor, allStoreys, output, false);
            if (highestFloorHeight.HasValue)
            {
                var topOfHighestFloor = highestFloorElevation + highestFloorHeight.Value;
                if (topOfHighestFloor > buildingTop)
                {
                    buildingTop = topOfHighestFloor;
                    topSource = "top of highest floor";
                }
            }

            // Section 2: Height calculation
            structured.WriteSectionHeader("Building Height Calculation");

            structured.WriteSubSection("First Floor (Reference):");
            structured.WriteSimpleText($"Name: {firstFloor.Name}");
            structured.WriteSimpleText($"Elevation: {firstFloorElevation / 1000:F2}m");
            structured.WriteEmptyLine();

            structured.WriteSubSection("Highest Floor:");
            structured.WriteSimpleText($"Name: {highestFloor.Name}");
            structured.WriteSimpleText($"Elevation (floor slab): {highestFloorElevation / 1000:F2}m");
            if (highestFloorHeight.HasValue)
            {
                structured.WriteSimpleText($"Floor height: {highestFloorHeight.Value / 1000:F2}m");
            }
            structured.WriteEmptyLine();

            structured.WriteSubSection("Building Top:");
            structured.WriteSimpleText($"Elevation: {buildingTop / 1000:F2}m");
            structured.WriteSimpleText($"Source: {topSource}");
            structured.WriteEmptyLine();

            double groundLevel;
            string groundSource;

            if (!isAbsoluteElevation)
            {
                var basePlateHeight = ElevationHelper.TryFindBasePlateHeight(firstFloor, output);
    
                if (basePlateHeight.HasValue)
                {
                    groundLevel = basePlateHeight.Value;
                    groundSource = "base slab";
                }
                else
                {
                    groundLevel = ElevationHelper.DetermineGroundLevel(allStoreys, realStoreys, basementStoreys, firstFloorElevation, output);
                    groundSource = groundLevel > 0 ? "detected ground level" : "ground (0.00m)";
                }
            }
            else
            {
                groundLevel = ElevationHelper.DetermineGroundLevel(allStoreys, realStoreys, basementStoreys, firstFloorElevation, output);
                groundSource = groundLevel > 0 ? "detected ground level" : "ground (0.00m)";
            }

            structured.WriteSubSection("Ground Reference:");
            if (groundLevel > 0)
            {
                structured.WriteSimpleText($"Ground level ({groundSource}): {groundLevel / 1000:F2}m");
            }
            else
            {
                structured.WriteSimpleText($"Ground level: 0.00m (standard reference)");
            }
            structured.WriteEmptyLine();

            var buildingHeight = buildingTop - groundLevel;

            // Answer section
            structured.WriteAnswer($"{buildingHeight / 1000:F2}m building height (ground to building top)");

            // Section 3: Additional information
            if (basementStoreys.Any())
            {
                structured.WriteSectionHeader("Additional Information");
                
                output.WriteWarning($"Note: {basementStoreys.Count} basement floors below first floor");

                var lowestBasement = basementStoreys
                    .Where(b => b.Elevation.HasValue)
                    .OrderBy(b => (double)b.Elevation!.Value)
                    .FirstOrDefault();

                if (lowestBasement != null)
                {
                    var basementElevation = (double)lowestBasement.Elevation!.Value;
                    var depthBelowGround = groundLevel - basementElevation;
                    
                    structured.WriteSimpleText($"Lowest basement floor: {lowestBasement.Name} ({basementElevation / 1000:F2}m)");
                    structured.WriteSimpleText($"Depth below ground: {depthBelowGround / 1000:F2}m");
                }
            }

            return $"{buildingHeight / 1000:F2}m";
        }
    }
}