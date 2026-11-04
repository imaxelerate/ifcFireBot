// Models/Helpers/StoreyFilterHelper.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Xbim.Ifc4.Interfaces;

#nullable enable

namespace ifcFireBot.Models.Helpers
{
    public static class StoreyFilterHelper
    {
        /// <summary>
        /// Filters out non-storey elements using multiple heuristics
        /// </summary>
        public static IEnumerable<IIfcBuildingStorey> FilterRealStoreys(IEnumerable<IIfcBuildingStorey> storeys, IOutputWriter? output = null, bool isDebugMode = false)
        {
            var storeyList = storeys.ToList();
            var results = new List<(IIfcBuildingStorey storey, bool isReal, string reason)>();

            foreach (var storey in storeyList)
            {
                var name = storey.Name?.ToString() ?? "";
                var longName = storey.LongName?.ToString() ?? "";
                
                // Test 1: Name pattern detection
                if (HasReferenceNamePattern(name, longName))
                {
                    results.Add((storey, false, $"Name indicates reference level: '{name}'"));
                    continue;
                }

                // Test 2: Check for physical building elements
                if (!HasBuildingElements(storey))
                {
                    results.Add((storey, false, $"No building elements: '{name}'"));
                    continue;
                }

                // Passed all tests - it's a real storey
                results.Add((storey, true, ""));
            }

            // Test 3: Elevation clustering analysis
            var realCandidates = results.Where(r => r.isReal).Select(r => r.storey).ToList();
            if (realCandidates.Count > 1)
            {
                try
                {
                    var outliers = DetectElevationOutliers(realCandidates, output, isDebugMode);
                    foreach (var outlier in outliers)
                    {
                        var index = results.FindIndex(r => r.storey == outlier);
                        if (index >= 0)
                        {
                            var current = results[index];
                            var elevationStr = outlier.Elevation.HasValue 
                                ? $"{outlier.Elevation.Value:F2}m" 
                                : "N/A";
                            results[index] = (current.storey, false, $"Deviating elevation: '{outlier.Name}' ({elevationStr})");
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (isDebugMode && output != null)
                    {
                        output.WriteWarning($"   Could not analyze storey elevations: {ex.Message}");
                    }
                }
            }

            // Log filtering results
            if (isDebugMode && output != null)
            {
                foreach (var result in results.Where(r => !r.isReal))
                {
                    output.WriteInfo($"   Excluding: {result.reason}");
                }
            }

            return results.Where(r => r.isReal).Select(r => r.storey);
        }

        /// <summary>
        /// Checks if name matches reference level patterns
        /// </summary>
        public static bool HasReferenceNamePattern(string name, string longName)
        {
            var excludePatterns = new[]
            {
                "sea level", "sealevel", "havsnivå", "referensnivå", "reference level",
                "reference", "site level", "ground reference", "datum", "survey point",
                "project base", "mark", "terrain", "site", "baseline",
                "uppställningar", "takfot", "nockhöjd", "marknivå", "grund",
                "foundation", "footing", "ridge", "eave", "parapet", "takplan",
                "0.000", "±0.00", "level 0", "survey", "benchmark"
            };

            var lowerName = name.ToLower();
            var lowerLongName = longName.ToLower();

            return excludePatterns.Any(pattern => 
                lowerName.Contains(pattern) || lowerLongName.Contains(pattern));
        }

        /// <summary>
        /// Checks if storey contains actual building elements
        /// </summary>
        public static bool HasBuildingElements(IIfcBuildingStorey storey)
        {
            var containedElements = storey.ContainsElements
                .SelectMany(rel => rel.RelatedElements)
                .ToList();

            if (!containedElements.Any())
            {
                containedElements = storey.IsDecomposedBy
                    .SelectMany(rel => rel.RelatedObjects)
                    .OfType<IIfcProduct>()
                    .ToList();
            }

            var significantElementCount = containedElements.Count(e =>
                e is IIfcWall ||
                e is IIfcSlab ||
                e is IIfcColumn ||
                e is IIfcBeam ||
                e is IIfcDoor ||
                e is IIfcWindow ||
                e is IIfcStair ||
                e is IIfcRoof ||
                e is IIfcCurtainWall ||
                e is IIfcRailing ||
                e is IIfcMember ||
                e is IIfcPlate);

            return significantElementCount > 0;
        }

        /// <summary>
        /// Gets the count of building elements in a storey
        /// </summary>
        public static int GetBuildingElementCount(IIfcBuildingStorey storey)
        {
            var containedElements = storey.ContainsElements
                .SelectMany(rel => rel.RelatedElements)
                .ToList();

            if (!containedElements.Any())
            {
                containedElements = storey.IsDecomposedBy
                    .SelectMany(rel => rel.RelatedObjects)
                    .OfType<IIfcProduct>()
                    .ToList();
            }

            return containedElements.Count(e =>
                e is IIfcWall ||
                e is IIfcSlab ||
                e is IIfcColumn ||
                e is IIfcBeam ||
                e is IIfcDoor ||
                e is IIfcWindow ||
                e is IIfcStair ||
                e is IIfcRoof ||
                e is IIfcCurtainWall ||
                e is IIfcRailing ||
                e is IIfcMember ||
                e is IIfcPlate);
        }

        /// <summary>
        /// Detects elevation outliers using clustering analysis
        /// </summary>
        public static List<IIfcBuildingStorey> DetectElevationOutliers(List<IIfcBuildingStorey> storeys, IOutputWriter? output = null, bool isDebugMode = false)
        {
            if (storeys.Count < 3)
                return new List<IIfcBuildingStorey>();

            try
            {
                var elevationData = storeys
                    .Where(s => s.Elevation.HasValue)
                    .Select(s => new { 
                        Storey = s, 
                        Elevation = (double)s.Elevation!.Value 
                    })
                    .Where(x => !double.IsNaN(x.Elevation) && !double.IsInfinity(x.Elevation))
                    .Select(x => new { 
                        x.Storey, 
                        Elevation = Math.Round(x.Elevation, 2) 
                    })
                    .OrderBy(x => x.Elevation)
                    .ToList();

                if (elevationData.Count < 3)
                    return new List<IIfcBuildingStorey>();

                if (isDebugMode && output != null)
                {
                    output.WriteInfo($"   Elevation analysis: {elevationData.Count} floors with levels from {elevationData.First().Elevation / 1000:F2}m to {elevationData.Last().Elevation / 1000:F2}m");
                }

                var spacings = new List<double>();
                for (int i = 1; i < elevationData.Count; i++)
                {
                    var spacing = elevationData[i].Elevation - elevationData[i - 1].Elevation;
                    if (spacing > 0)
                    {
                        spacings.Add(spacing);
                    }
                }

                if (spacings.Count < 2)
                    return new List<IIfcBuildingStorey>();

                spacings.Sort();
                var medianSpacing = spacings.Count % 2 == 0
                    ? (spacings[spacings.Count / 2 - 1] + spacings[spacings.Count / 2]) / 2
                    : spacings[spacings.Count / 2];

                if (isDebugMode && output != null)
                {
                    output.WriteInfo($"   Typical storey height: {medianSpacing / 1000:F2}m");
                }

                var outliers = new List<IIfcBuildingStorey>();
                const double maxRealisticGap = 50000; // 50 meters in mm

                for (int i = 0; i < elevationData.Count; i++)
                {
                    var curr = elevationData[i];
                    var gapToPrev = i > 0 ? curr.Elevation - elevationData[i - 1].Elevation : double.MaxValue;
                    var gapToNext = i < elevationData.Count - 1 ? elevationData[i + 1].Elevation - curr.Elevation : double.MaxValue;
                    
                    var minGap = Math.Min(gapToPrev, gapToNext);
                    
                    if (minGap > maxRealisticGap)
                    {
                        var elementCount = GetBuildingElementCount(curr.Storey);
                        
                        if (elementCount < 100)
                        {
                            if (isDebugMode && output != null)
                            {
                                output.WriteInfo($"   '{curr.Storey.Name}' isolated with {minGap / 1000:F2}m gap (only {elementCount} elements)");
                            }
                            outliers.Add(curr.Storey);
                            continue;
                        }
                    }
                }

                for (int i = 1; i < elevationData.Count; i++)
                {
                    var spacing = elevationData[i].Elevation - elevationData[i - 1].Elevation;

                    if (spacing < 500)  // Less than 0.5m
                    {
                        var prev = elevationData[i - 1].Storey;
                        var curr = elevationData[i].Storey;
                        
                        var prevCount = GetBuildingElementCount(prev);
                        var currCount = GetBuildingElementCount(curr);
                        
                        if (prevCount < currCount)
                            outliers.Add(prev);
                        else
                            outliers.Add(curr);
                    }
                    else if (Math.Abs(spacing - medianSpacing) > medianSpacing * 3.0)
                    {
                        var curr = elevationData[i].Storey;
                        var elementCount = GetBuildingElementCount(curr);
                        if (elementCount < 10)
                        {
                            outliers.Add(curr);
                        }
                    }
                }

                return outliers.Distinct().ToList();
            }
            catch (Exception ex)
            {
                if (isDebugMode && output != null)
                {
                    output.WriteWarning($"   Could not analyze storey elevations: {ex.Message}");
                }
                return new List<IIfcBuildingStorey>();
            }
        }

        /// <summary>
        /// Checks if a storey is a basement/below-ground level
        /// </summary>
        public static bool IsBasement(IIfcBuildingStorey storey, bool isAbsoluteElevation = false, IOutputWriter? output = null, bool isDebugMode = false)
        {
            var name = storey.Name?.ToString() ?? "";
            var longName = storey.LongName?.ToString() ?? "";
            
            var basementPatterns = new[]
            {
                "källare", "källarplan", "basement", "underground", 
                "garage", "parking", "p-hus", "källarvåning",
                "-1", "-2", "-3", "-4", "-5",
                "u1", "u2", "u3", "b1", "b2", "b3",
                "plan 0", "plan 00"
            };
            
            var lowerName = name.ToLower();
            var lowerLongName = longName.ToLower();
            
            if (basementPatterns.Any(pattern => 
                lowerName.Contains(pattern) || lowerLongName.Contains(pattern)))
            {
                if (isDebugMode && output != null)
                {
                    output.WriteInfo($"   '{name}' identified as basement based on name");
                }
                return true;
            }
            
            if (storey.Elevation.HasValue && !isAbsoluteElevation)
            {
                var elevation = (double)storey.Elevation!.Value;
                
                if (elevation <= 0)
                {
                    if (isDebugMode && output != null)
                    {
                        output.WriteInfo($"   '{name}' identified as basement based on negative/zero elevation");
                    }
                    return true;
                }
            }
            
            if (isAbsoluteElevation && storey.Elevation.HasValue)
            {
                var windows = storey.ContainsElements
                    .SelectMany(rel => rel.RelatedElements)
                    .OfType<IIfcWindow>()
                    .Count();
                
                var walls = storey.ContainsElements
                    .SelectMany(rel => rel.RelatedElements)
                    .OfType<IIfcWall>()
                    .Count();
                
                var spaces = storey.ContainsElements
                    .SelectMany(rel => rel.RelatedElements)
                    .OfType<IIfcSpace>()
                    .Count();
                
                if ((walls > 5 || spaces > 2) && windows == 0)
                {
                    if (isDebugMode && output != null)
                    {
                        output.WriteInfo($"   '{name}' identified as basement based on lack of windows ({walls} walls, {spaces} spaces, 0 windows)");
                    }
                    return true;
                }
            }
            
            return false;
        }

        /// <summary>
        /// Validates that basement classifications are consistent with elevation data
        /// </summary>
        public static void ValidateBasementClassification(
            List<IIfcBuildingStorey> basements, 
            List<IIfcBuildingStorey> aboveGround,
            IOutputWriter? output = null)
        {
            if (!basements.Any() || !aboveGround.Any())
                return;

            var basementsWithElevation = basements.Where(b => b.Elevation.HasValue).ToList();
            var aboveGroundWithElevation = aboveGround.Where(a => a.Elevation.HasValue).ToList();

            if (!basementsWithElevation.Any() || !aboveGroundWithElevation.Any())
                return;

            var maxBasementElevation = basementsWithElevation.Max(b => (double)b.Elevation!.Value);
            var minAboveGroundElevation = aboveGroundWithElevation.Min(a => (double)a.Elevation!.Value);

            var highestBasement = basementsWithElevation.First(b => (double)b.Elevation!.Value == maxBasementElevation);
            var lowestAboveGround = aboveGroundWithElevation.First(a => (double)a.Elevation!.Value == minAboveGroundElevation);

            if (maxBasementElevation >= minAboveGroundElevation)
            {
                // Note: This validation output is intentionally always shown as it indicates data issues
                if (output != null)
                {
                    output.WriteWarning($"\n   WARNING: Elevation conflict detected!");
                    output.WriteWarning($"   Highest basement floor: '{highestBasement.Name}' @ {maxBasementElevation:F2}mm");
                    output.WriteWarning($"   Lowest floor above ground: '{lowestAboveGround.Name}' @ {minAboveGroundElevation:F2}mm");
                    output.WriteWarning($"   This indicates incorrect elevation data or naming in the IFC file");
                    output.WriteWarning($"   Check if '{highestBasement.Name}' is really a basement\n");
                }
            }
        }

        /// <summary>
        /// Detects if the model uses absolute elevation (meters above sea level)
        /// </summary>
        public static bool IsAbsoluteElevationSystem(List<IIfcBuildingStorey> realStoreys)
        {
            var allElevations = realStoreys
                .Where(s => s.Elevation.HasValue)
                .Select(s => (double)s.Elevation!.Value)
                .ToList();

            return allElevations.Any() && allElevations.Min() > 50000; // 50m in mm
        }

        /// <summary>
        /// Gets the parent building storey of a space
        /// </summary>
        public static IIfcBuildingStorey? GetParentStorey(IIfcSpace space)
        {
            var storey = space.Decomposes
                ?.FirstOrDefault()?.RelatingObject as IIfcBuildingStorey;
            
            if (storey != null)
                return storey;
            
            foreach (var rel in space.IsDefinedBy)
            {
                if (rel.RelatingPropertyDefinition is IIfcPropertySet pset && pset.Name == "BIP")
                {
                    foreach (var prop in pset.HasProperties.OfType<IIfcPropertySingleValue>())
                    {
                        if (prop.Name.ToString() == "StoreyName")
                        {
                            var storeyName = prop.NominalValue?.Value?.ToString();
                            if (!string.IsNullOrEmpty(storeyName))
                            {
                                var model = space.Model;
                                return model.Instances.OfType<IIfcBuildingStorey>()
                                    .FirstOrDefault(s => s.Name?.ToString() == storeyName);
                            }
                        }
                    }
                }
            }
            
            return null;
        }

        /// <summary>
        /// Calculates height from neighboring storey
        /// </summary>
        public static double? CalculateStoreyHeightFromNeighbor(IIfcBuildingStorey storey, IOutputWriter? output = null, bool isDebugMode = false)
        {
            if (!storey.Elevation.HasValue)
                return null;
            
            var model = storey.Model;
            var currentElevation = (double)storey.Elevation!.Value;
            
            var allStoreys = model.Instances.OfType<IIfcBuildingStorey>()
                .Where(s => s.Elevation.HasValue && s != storey)
                .Where(s => !HasReferenceNamePattern(s.Name?.ToString() ?? "", s.LongName?.ToString() ?? ""))
                .Select(s => new { Storey = s, Elevation = (double)s.Elevation!.Value })
                .OrderBy(s => s.Elevation)
                .ToList();
            
            if (!allStoreys.Any())
                return null;
            
            var isTopStorey = allStoreys.All(s => s.Elevation <= currentElevation);
            
            if (isTopStorey)
            {
                if (isDebugMode && output != null)
                {
                    output.WriteInfo($"   {storey.Name} is the top floor");
                }
                
                var storeysBelow = allStoreys
                    .Where(s => s.Elevation < currentElevation)
                    .OrderByDescending(s => s.Elevation)
                    .ToList();
                
                foreach (var storeyBelow in storeysBelow)
                {
                    var heightDiff = currentElevation - storeyBelow.Elevation;
                    
                    if (heightDiff < 100)
                    {
                        if (isDebugMode && output != null)
                        {
                            output.WriteInfo($"   '{storeyBelow.Storey.Name} is too close ({heightDiff}mm) - skipping");
                        }
                        continue;
                    }
                    
                    if (isDebugMode && output != null)
                    {
                        output.WriteInfo($"   Distance to {storeyBelow.Storey.Name}: {heightDiff / 1000:F2}m");
                    }
                    
                    if (heightDiff >= 1000 && heightDiff <= 5000)
                    {
                        return heightDiff;
                    }
                    else
                    {
                        if (isDebugMode && output != null)
                        {
                            output.WriteInfo($"   Height seems unrealistic ({heightDiff / 1000:F2}m)");
                        }
                    }
                }
                
                if (isDebugMode && output != null)
                {
                    output.WriteInfo($"   No suitable floor below {storey.Name} found");
                }
                return null;
            }
            else
            {
                var validNeighbors = allStoreys
                    .Where(s => Math.Abs(s.Elevation - currentElevation) >= 100)
                    .OrderBy(s => Math.Abs(s.Elevation - currentElevation))
                    .ToList();
                
                if (!validNeighbors.Any())
                {
                    if (isDebugMode && output != null)
                    {
                        output.WriteInfo($"   No valid neighboring floors found");
                    }
                    return null;
                }
                
                var nearestStorey = validNeighbors.First();
                var heightDiff = Math.Abs(currentElevation - nearestStorey.Elevation);
                
                return heightDiff;
            }
        }

        /// <summary>
        /// Calculates height from neighboring storey (with explicit storey list)
        /// </summary>
        public static double? CalculateStoreyHeightFromNeighbor(IIfcBuildingStorey storey, List<IIfcBuildingStorey> allStoreys)
        {
            if (!storey.Elevation.HasValue)
                return null;
            
            var currentElevation = (double)storey.Elevation!.Value;
            
            var storeysFiltered = allStoreys
                .Where(s => s.Elevation.HasValue && s != storey)
                .Where(s => !HasReferenceNamePattern(s.Name?.ToString() ?? "", s.LongName?.ToString() ?? ""))
                .Select(s => new { Storey = s, Elevation = (double)s.Elevation!.Value })
                .OrderBy(s => s.Elevation)
                .ToList();
            
            if (!storeysFiltered.Any())
                return null;
            
            // Find the next storey above
            var storeyAbove = storeysFiltered
                .Where(s => s.Elevation > currentElevation)
                .OrderBy(s => s.Elevation)
                .FirstOrDefault();
            
            if (storeyAbove != null)
            {
                var heightDiff = storeyAbove.Elevation - currentElevation;
                if (heightDiff >= 100 && heightDiff <= 10000) // Between 0.1m and 10m
                {
                    return heightDiff;
                }
            }
            
            // If no storey above, or height is unrealistic, try storey below
            var storeyBelow = storeysFiltered
                .Where(s => s.Elevation < currentElevation)
                .OrderByDescending(s => s.Elevation)
                .FirstOrDefault();
            
            if (storeyBelow != null)
            {
                var heightDiff = currentElevation - storeyBelow.Elevation;
                if (heightDiff >= 100 && heightDiff <= 10000) // Between 0.1m and 10m
                {
                    return heightDiff;
                }
            }
            
            return null;
        }

        /// <summary>
        /// Gets maximum height from spaces contained in a storey
        /// </summary>
        public static double? GetMaxHeightFromSpaces(IIfcBuildingStorey storey, IOutputWriter? output = null, bool isDebugMode = false)
        {
            var containedSpaces = storey.ContainsElements
                .SelectMany(rel => rel.RelatedElements)
                .OfType<IIfcSpace>()
                .ToList();

            if (!containedSpaces.Any())
            {
                var storeyName = storey.Name?.ToString();
                if (!string.IsNullOrEmpty(storeyName))
                {
                    var model = storey.Model;
                    containedSpaces = model.Instances.OfType<IIfcSpace>()
                        .Where(space =>
                        {
                            foreach (var rel in space.IsDefinedBy)
                            {
                                if (rel.RelatingPropertyDefinition is IIfcPropertySet pset && pset.Name == "BIP")
                                {
                                    foreach (var prop in pset.HasProperties.OfType<IIfcPropertySingleValue>())
                                    {
                                        if (prop.Name.ToString() == "StoreyName")
                                        {
                                            var value = prop.NominalValue?.Value?.ToString();
                                            return value != null && value.Equals(storeyName, StringComparison.OrdinalIgnoreCase);
                                        }
                                    }
                                }
                            }
                            return false;
                        })
                        .ToList();
                }
            }

            if (!containedSpaces.Any())
            {
                if (isDebugMode && output != null)
                {
                    output.WriteInfo($"   No spaces found in {storey.Name}");
                }
                return null;
            }

            if (isDebugMode && output != null)
            {
                output.WriteInfo($"   Checking height from {containedSpaces.Count} spaces in {storey.Name}");
            }

            double? maxHeight = null;
            
            foreach (var space in containedSpaces)
            {
                // First try to get from properties
                var height = IfcPropertyHelper.GetHeightFromProperties(space);
                
                // If not found in properties, try geometry calculation
                if (!height.HasValue)
                {
                    var geometryHeight = GeometryHelper.GetSpaceHeight(space);
                    if (geometryHeight > 0)
                    {
                        height = geometryHeight * 1000.0; // Convert m to mm for consistency
                        if (isDebugMode && output != null)
                        {
                            output.WriteSuccess($"      ✓ {space.Name}: Height from geometry = {geometryHeight:F2}m");
                        }
                    }
                }
                
                if (height.HasValue)
                {
                    if (!maxHeight.HasValue || height.Value > maxHeight.Value)
                        maxHeight = height.Value;
                }
            }

            return maxHeight;
        }
    }
}