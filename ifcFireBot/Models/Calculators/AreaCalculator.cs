// Models/Calculators/AreaCalculator.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Xbim.Ifc4.Interfaces;
using ifcFireBot.Models.Helpers;

namespace ifcFireBot.Models.Calculators
{
    public class AreaCalculator : ICalculator
    {
        public string Calculate(List<IIfcProduct> elements, Intent intent, IOutputWriter output)
        {
            if (elements.Count == 0)
                return "No elements to calculate area for.";

            return elements.FirstOrDefault() switch
            {
                IIfcSpace => CalculateSpaceArea(elements.OfType<IIfcSpace>(), output, intent),
                IIfcBuildingStorey => CalculateBuildingStoreyArea(elements.OfType<IIfcBuildingStorey>(), output, intent),
                IIfcSlab => CalculateSlabArea(elements.OfType<IIfcSlab>(), output),
                _ => $"Area calculation not supported for {elements.First().GetType().Name}"
            };
        }

        private string CalculateSpaceArea(IEnumerable<IIfcSpace> spaces, IOutputWriter output, Intent intent)
        {
            var spaceList = spaces.ToList();
            if (spaceList.Count == 0)
                return "No spaces to calculate area for.";

            var structured = new StructuredOutputWriter(output);

            double totalArea = 0;
            int countedFromProperties = 0;
            int countedFromGeometry = 0;
            var details = new List<string>();

            // Section 1: Data sources
            structured.WriteSectionHeader("Identified Spaces and Data Sources");
            structured.WriteSimpleText($"Found {spaceList.Count} IIfcSpace entities.");
            structured.WriteEmptyLine();

            foreach (var space in spaceList)
            {
                var area = IfcPropertyHelper.GetAreaFromProperties(space);
                
                if (area.HasValue)
                {
                    totalArea += area.Value;
                    countedFromProperties++;
                    details.Add($"{space.Name}: {area.Value:F2} m² (from properties)");
                }
                else
                {
                    // Method 2: Calculate from geometry
                    var geometryArea = GeometryHelper.CalculateAreaFromGeometry(space);
                    
                    if (geometryArea > 0)
                    {
                        totalArea += geometryArea;
                        countedFromGeometry++;
                        details.Add($"{space.Name}: {geometryArea:F2} m² (from geometry)");
                    }
                    else
                    {
                        details.Add($"{space.Name}: No area data available");
                    }
                }
            }

            // Section 2: Detailed breakdown
            structured.WriteSectionHeader("Area Calculation per Space");
            
            if (details.Any())
            {
                structured.WriteList(details, indent: 2);
            }

            structured.WriteSubSection($"Data Sources:");
            structured.WriteSimpleText($"From properties: {countedFromProperties} spaces");
            structured.WriteSimpleText($"From geometry: {countedFromGeometry} spaces");
            structured.WriteEmptyLine();

            // Answer section
            if (countedFromProperties > 0 || countedFromGeometry > 0)
            {
                structured.WriteAnswer($"{totalArea:F2} m²");

                // Section 3: Analysis
                structured.WriteSectionHeader("Analysis and Assessment");
                
                if (totalArea > 1250)
                {
                    output.WriteWarning($"⚠️  Total area exceeds 1250 m² (fire safety limit)");
                }
                else
                {
                    output.WriteSuccess($"✓ Total area is below 1250 m²");
                }

                return $"{totalArea:F2} m²";
            }
            else
            {
                output.WriteLine("");
                output.WriteError("❌ Could not find or calculate area information.");
                output.WriteWarning("\n💡 Tip: IFC file lacks both area properties and interpretable geometry.");
                return "No area data available";
            }
        }

        private string CalculateBuildingStoreyArea(IEnumerable<IIfcBuildingStorey> storeys, IOutputWriter output, Intent intent)
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

            // Section 1: Identified storeys
            structured.WriteSectionHeader("Identified Floors");
            structured.WriteSimpleText($"Found {aboveGroundStoreys.Count} building storeys above ground (including attic if present).");
            structured.WriteEmptyLine();

            var areas = new List<(string name, double area)>();
            var dataSources = new Dictionary<string, int>
            {
                ["From properties"] = 0,
                ["From slabs"] = 0,
                ["From spaces"] = 0
            };

            foreach (var storey in aboveGroundStoreys)
            {
                var area = IfcPropertyHelper.GetAreaFromProperties(storey);

                if (area.HasValue)
                {
                    areas.Add((storey.Name?.ToString() ?? "Unnamed", area.Value));
                    dataSources["From properties"]++;
                }
                else
                {
                    // Method 2: Calculate from slabs
                    var slabArea = IfcPropertyHelper.CalculateAreaFromSlabs(storey);

                    if (slabArea > 0)
                    {
                        areas.Add((storey.Name?.ToString() ?? "Unnamed", slabArea));
                        dataSources["From slabs"]++;
                    }
                    else
                    {
                        // Method 3: Calculate from contained spaces
                        var spaceArea = IfcPropertyHelper.CalculateAreaFromSpaces(storey);

                        if (spaceArea > 0)
                        {
                            areas.Add((storey.Name?.ToString() ?? "Unnamed", spaceArea));
                            dataSources["From spaces"]++;
                        }
                    }
                }
            }

            if (!areas.Any())
                return "Could not find area information for these building storeys.";

            // Section 2: Area breakdown per floor
            structured.WriteSectionHeader("Area Calculation per Floor");

            var areaList = areas.Select(a => $"{a.name}: {a.area:F2} m²").ToList();
            structured.WriteList(areaList, indent: 2);

            structured.WriteSubSection("Data Sources:");
            foreach (var source in dataSources.Where(s => s.Value > 0))
            {
                structured.WriteSimpleText($"{source.Key}: {source.Value} floors");
            }
            structured.WriteEmptyLine();

            // Answer section - list all storey areas with names
            if (areas.Count == 1)
            {
                structured.WriteAnswer($"{areas[0].name}: {areas[0].area:F2} m²");
                return $"{areas[0].name}: {areas[0].area:F2} m²";
            }
            else
            {
                // Build answer with all storey areas including names
                var allAreas = string.Join(", ", areas.Select(a => $"{a.name}: {a.area:F2} m²"));
                structured.WriteAnswer($"{allAreas}");

                // Return format: include storey names with areas for CSV
                return allAreas;
            }
        }

        private string CalculateSlabArea(IEnumerable<IIfcSlab> slabs, IOutputWriter output)
        {
            var slabList = slabs.ToList();
            var structured = new StructuredOutputWriter(output);

            double totalArea = 0;
            int counted = 0;
            var details = new List<string>();

            structured.WriteSectionHeader("Slab Analysis");
            structured.WriteSimpleText($"Analyzing {slabList.Count} slabs.");
            structured.WriteEmptyLine();

            foreach (var slab in slabList)
            {
                var area = IfcPropertyHelper.GetAreaFromProperties(slab);
                
                if (area.HasValue)
                {
                    totalArea += area.Value;
                    counted++;
                    details.Add($"{slab.Name}: {area.Value:F2} m²");
                }
                else
                {
                    var geometryArea = GeometryHelper.CalculateAreaFromGeometry(slab);
                    if (geometryArea > 0)
                    {
                        totalArea += geometryArea;
                        counted++;
                        details.Add($"{slab.Name}: {geometryArea:F2} m² (from geometry)");
                    }
                }
            }

            structured.WriteSectionHeader("Results per Slab");
            structured.WriteList(details, indent: 2);

            if (counted > 0)
            {
                structured.WriteAnswer($"{totalArea:F2} m² (based on {counted} slabs)");
                return $"{totalArea:F2} m²";
            }
            else
            {
                return "Could not find area information for these slabs.";
            }
        }
    }
}