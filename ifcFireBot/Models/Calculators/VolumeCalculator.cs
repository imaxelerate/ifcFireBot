// Models/Calculators/VolumeCalculator.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Xbim.Ifc4.Interfaces;
using ifcFireBot.Models.Helpers;

namespace ifcFireBot.Models.Calculators
{
    public class VolumeCalculator : ICalculator
    {
        public string Calculate(List<IIfcProduct> elements, Intent intent, IOutputWriter output)
        {
            if (elements.Count == 0)
                return "No elements to calculate volume for.";

            return elements.FirstOrDefault() switch
            {
                IIfcSpace => CalculateSpaceVolume(elements.OfType<IIfcSpace>(), output),
                IIfcBuildingStorey => CalculateBuildingStoreyVolume(elements.OfType<IIfcBuildingStorey>(), output),
                IIfcWall => CalculateWallVolume(elements.OfType<IIfcWall>(), output),
                IIfcSlab => CalculateSlabVolume(elements.OfType<IIfcSlab>(), output),
                _ => $"Volume calculation not supported for {elements.First().GetType().Name}"
            };
        }

        private string CalculateSpaceVolume(IEnumerable<IIfcSpace> spaces, IOutputWriter output)
        {
            var spaceList = spaces.ToList();
            if (spaceList.Count == 0)
                return "No spaces to calculate volume for.";

            var structured = new StructuredOutputWriter(output);

            // Section 1: Identified spaces
            structured.WriteSectionHeader("Identified Spaces and Data Sources");
            structured.WriteSimpleText($"Found {spaceList.Count} IIfcSpace entities.");
            structured.WriteEmptyLine();

            double totalVolume = 0;
            int countedFromProperties = 0;
            int countedFromGeometry = 0;
            var details = new List<string>();

            foreach (var space in spaceList)
            {
                // Method 1: Try to get volume from properties
                var volume = IfcPropertyHelper.GetVolumeFromProperties(space);
                
                if (volume.HasValue)
                {
                    totalVolume += volume.Value;
                    countedFromProperties++;
                    details.Add($"{space.Name}: {volume.Value:F2} m³ (from properties)");
                }
                else
                {
                    // Method 2: Calculate from geometry (area * height)
                    var geometryVolume = GeometryHelper.CalculateVolumeFromGeometry(space);
                    
                    if (geometryVolume > 0)
                    {
                        totalVolume += geometryVolume;
                        countedFromGeometry++;
                        details.Add($"{space.Name}: {geometryVolume:F2} m³ (from geometry)");
                    }
                    else
                    {
                        details.Add($"{space.Name}: No volume data available");
                    }
                }
            }

            // Section 2: Volume breakdown
            structured.WriteSectionHeader("Volume Calculation per Space");
            
            if (details.Any())
            {
                structured.WriteList(details, indent: 2);
            }

            structured.WriteSubSection("Data Sources:");
            structured.WriteSimpleText($"From properties: {countedFromProperties} spaces");
            structured.WriteSimpleText($"From geometry: {countedFromGeometry} spaces");
            structured.WriteEmptyLine();

            // Answer
            if (countedFromProperties > 0 || countedFromGeometry > 0)
            {
                structured.WriteAnswer($"{totalVolume:F2} m³");
                return $"{totalVolume:F2} m³";
            }
            else
            {
                structured.WriteGenericAnswer("No volume data could be calculated for any space.");
                return "0 m³";
            }
        }

        private string CalculateBuildingStoreyVolume(IEnumerable<IIfcBuildingStorey> storeys, IOutputWriter output)
        {
            var storeyList = storeys.ToList();
            if (!storeyList.Any())
                return "No building storeys found.";

            var structured = new StructuredOutputWriter(output);

            // Section 1: Storey identification
            structured.WriteSectionHeader("Identified Building Storeys");
            structured.WriteSimpleText($"Analyzing {storeyList.Count} building storeys.");
            structured.WriteEmptyLine();

            double totalVolume = 0;
            var details = new List<string>();

            // Section 2: Volume calculation
            structured.WriteSectionHeader("Volume Calculation per Storey");

            foreach (var storey in storeyList)
            {
                var spaces = storey.IsDecomposedBy
                    .SelectMany(rel => rel.RelatedObjects)
                    .OfType<IIfcSpace>()
                    .ToList();

                double storeyVolume = 0;
                foreach (var space in spaces)
                {
                    var volume = IfcPropertyHelper.GetVolumeFromProperties(space);
                    if (volume.HasValue)
                    {
                        storeyVolume += volume.Value;
                    }
                    else
                    {
                        var geometryVolume = GeometryHelper.CalculateVolumeFromGeometry(space);
                        storeyVolume += geometryVolume;
                    }
                }

                totalVolume += storeyVolume;
                details.Add($"{storey.Name}: {storeyVolume:F2} m³ ({spaces.Count} spaces)");
            }

            structured.WriteList(details, indent: 2);

            // Answer
            structured.WriteAnswer($"{totalVolume:F2} m³");
            return $"{totalVolume:F2} m³";
        }

        private string CalculateWallVolume(IEnumerable<IIfcWall> walls, IOutputWriter output)
        {
            var wallList = walls.ToList();
            if (!wallList.Any())
                return "No walls found.";

            var structured = new StructuredOutputWriter(output);

            // Section 1: Wall identification
            structured.WriteSectionHeader("Identified Walls");
            structured.WriteSimpleText($"Found {wallList.Count} walls.");
            structured.WriteEmptyLine();

            double totalVolume = 0;
            int calculatedCount = 0;

            // Section 2: Volume calculation
            structured.WriteSectionHeader("Volume Calculation");

            foreach (var wall in wallList)
            {
                var volume = GeometryHelper.CalculateVolumeFromProduct(wall);
                if (volume > 0)
                {
                    totalVolume += volume;
                    calculatedCount++;
                }
            }

            structured.WriteSimpleText($"Calculated volume for {calculatedCount} of {wallList.Count} walls.");
            structured.WriteEmptyLine();

            // Answer
            if (totalVolume > 0)
            {
                structured.WriteGenericAnswer($"Total volume: **{totalVolume:F2} m³**");
                return $"{totalVolume:F2} m³";
            }
            else
            {
                structured.WriteGenericAnswer("Could not calculate volume from available data.");
                return "0 m³";
            }
        }

        private string CalculateSlabVolume(IEnumerable<IIfcSlab> slabs, IOutputWriter output)
        {
            var slabList = slabs.ToList();
            if (!slabList.Any())
                return "No slabs found.";

            var structured = new StructuredOutputWriter(output);

            // Section 1: Slab identification
            structured.WriteSectionHeader("Identified Slabs");
            structured.WriteSimpleText($"Found {slabList.Count} slabs.");
            structured.WriteEmptyLine();

            double totalVolume = 0;
            int calculatedCount = 0;

            // Section 2: Volume calculation
            structured.WriteSectionHeader("Volume Calculation");

            foreach (var slab in slabList)
            {
                var volume = GeometryHelper.CalculateVolumeFromProduct(slab);
                if (volume > 0)
                {
                    totalVolume += volume;
                    calculatedCount++;
                }
            }

            structured.WriteSimpleText($"Calculated volume for {calculatedCount} of {slabList.Count} slabs.");
            structured.WriteEmptyLine();

            // Answer
            if (totalVolume > 0)
            {
                structured.WriteGenericAnswer($"Total volume: **{totalVolume:F2} m³**");
                return $"{totalVolume:F2} m³";
            }
            else
            {
                structured.WriteGenericAnswer("Could not calculate volume from available data.");
                return "0 m³";
            }
        }
    }
}