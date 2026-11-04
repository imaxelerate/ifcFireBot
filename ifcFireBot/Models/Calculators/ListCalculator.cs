// Models/Calculators/ListCalculator.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xbim.Ifc4.Interfaces;
using ifcFireBot.Models.Helpers;

namespace ifcFireBot.Models.Calculators
{
    public class ListCalculator : ICalculator
    {
        public string Calculate(List<IIfcProduct> elements, Intent intent, IOutputWriter output)
        {
            if (elements.Count == 0)
            {
                var structured = new StructuredOutputWriter(output);
                structured.WriteSectionHeader("Search");
                structured.WriteSimpleText($"No {intent.IfcEntity} elements found.");
                structured.WriteEmptyLine();
                structured.WriteGenericAnswer($"No elements found.");
                return "No elements found.";
            }

            // Special handling for building storeys with elevation info
            if (elements.FirstOrDefault() is IIfcBuildingStorey)
            {
                return ListBuildingStoreys(elements.OfType<IIfcBuildingStorey>(), output);
            }

            // Default list handling
            return ListDefaultElements(elements, intent, output);
        }

        private string ListBuildingStoreys(IEnumerable<IIfcBuildingStorey> storeys, IOutputWriter output)
        {
            var structured = new StructuredOutputWriter(output);
            var realStoreys = StoreyFilterHelper.FilterRealStoreys(storeys, output).ToList();
            
            if (realStoreys.Count == 0)
            {
                structured.WriteSectionHeader("Building Storeys");
                structured.WriteSimpleText("No floors found after filtering.");
                structured.WriteEmptyLine();
                structured.WriteGenericAnswer("No floors found.");
                return "No floors found after filtering.";
            }

            // Section 1: All storeys found
            structured.WriteSectionHeader("Identified Building Storeys");
            structured.WriteSimpleText($"Found {storeys.Count()} building storeys in total.");
            structured.WriteEmptyLine();

            // Detect if using absolute elevation system
            bool isAbsoluteElevation = StoreyFilterHelper.IsAbsoluteElevationSystem(realStoreys);

            // Separate basement and above-ground floors
            var basementStoreys = realStoreys.Where(s => StoreyFilterHelper.IsBasement(s, isAbsoluteElevation)).ToList();
            var aboveGroundStoreys = realStoreys.Where(s => !StoreyFilterHelper.IsBasement(s, isAbsoluteElevation)).ToList();

            // Validate basement classification
            StoreyFilterHelper.ValidateBasementClassification(basementStoreys, aboveGroundStoreys, output);

            // Sort each group by elevation
            basementStoreys = basementStoreys
                .OrderBy(s => 
                {
                    if (!s.Elevation.HasValue) return double.MinValue;
                    var elev = (double)s.Elevation.Value;
                    if (double.IsNaN(elev) || double.IsInfinity(elev)) return double.MinValue;
                    return elev;
                })
                .ToList();

            aboveGroundStoreys = aboveGroundStoreys
                .OrderBy(s => 
                {
                    if (!s.Elevation.HasValue) return double.MinValue;
                    var elev = (double)s.Elevation.Value;
                    if (double.IsNaN(elev) || double.IsInfinity(elev)) return double.MinValue;
                    return elev;
                })
                .ToList();

            // Section 2: Storey listing
            structured.WriteSectionHeader("Storey List");

            if (basementStoreys.Any())
            {
                structured.WriteSubSection($"Basement Floors ({basementStoreys.Count}):");
                structured.WriteEmptyLine();

                var basementList = basementStoreys.Select(s =>
                {
                    var elevation = s.Elevation.HasValue ? $"{(double)s.Elevation.Value / 1000:F2}m" : "N/A";
                    return $"  🔻 {s.Name} (elevation: {elevation})";
                }).ToList();

                structured.WriteList(basementList, indent: 0);
            }

            if (aboveGroundStoreys.Any())
            {
                structured.WriteSubSection($"Floors Above Ground ({aboveGroundStoreys.Count}):");
                structured.WriteEmptyLine();

                var aboveGroundList = aboveGroundStoreys.Select(s =>
                {
                    var elevation = s.Elevation.HasValue ? $"{(double)s.Elevation.Value / 1000:F2}m" : "N/A";
                    return $"  🏢 {s.Name} (elevation: {elevation})";
                }).ToList();

                structured.WriteList(aboveGroundList, indent: 0);
            }

            // Answer
            var result = $"{realStoreys.Count} building storeys" +
                        (basementStoreys.Any() ? $" ({basementStoreys.Count} basement, {aboveGroundStoreys.Count} above ground)" : "");
            
            structured.WriteGenericAnswer($"Total: **{result}**");

            return result;
        }

        private string ListDefaultElements(List<IIfcProduct> elements, Intent intent, IOutputWriter output)
        {
            var structured = new StructuredOutputWriter(output);

            // Section 1: Element identification
            structured.WriteSectionHeader($"Found {intent.IfcEntity} Elements");
            structured.WriteSimpleText($"Total count: {elements.Count}");
            structured.WriteEmptyLine();

            // Section 2: Element listing
            structured.WriteSectionHeader("Element List");

            var elementList = elements.Take(20).Select(e =>
            {
                var name = e.Name?.ToString() ?? "(no name)";
                var id = e.GlobalId.ToString() ?? "N/A";
                return $"  • {name} (ID: {id})";
            }).ToList();

            structured.WriteList(elementList, indent: 0);

            if (elements.Count > 20)
            {
                structured.WriteSimpleText($"... and {elements.Count - 20} more elements", indent: 2);
                structured.WriteEmptyLine();
            }

            // Answer
            structured.WriteGenericAnswer($"Total: **{elements.Count} {intent.IfcEntity}**");

            return $"{elements.Count} elements listed";
        }
    }
}