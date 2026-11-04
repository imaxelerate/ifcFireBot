// Models/Helpers/ElevationHelper.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.Common;

namespace ifcFireBot.Models.Helpers
{
    public static class ElevationHelper
    {
        /// <summary>
        /// Intelligently determines ground level from IFC data
        /// Handles both relative (0 = ground) and absolute (elevation above sea level) coordinate systems
        /// </summary>
        public static double DetermineGroundLevel(
            List<IIfcBuildingStorey> allStoreys,
            List<IIfcBuildingStorey> realStoreys,
            List<IIfcBuildingStorey> basementStoreys,
            double firstFloorElevation,
            IOutputWriter output,
            bool isDebugMode = false)
        {
            var model = allStoreys.First().Model;
            bool isAbsolute = StoreyFilterHelper.IsAbsoluteElevationSystem(realStoreys);

            // Method 0: Check for context buildings (Kontext_Byggnader) - HIGHEST PRIORITY!
            var contextGroundLevel = TryGetGroundLevelFromContextBuilding(model, output, isDebugMode);
            if (contextGroundLevel.HasValue)
            {
                // **FIX: Validate and correct coordinate system mismatch**
                // If we're in an absolute system and the context ground level is negative
                // but storeys are at high positive elevations, the context building is using
                // a different coordinate reference (likely inverted Z-axis or relative coordinates)
                if (isAbsolute && contextGroundLevel.Value < 0 && realStoreys.Any())
                {
                    var minStoreyElevation = realStoreys
                        .Where(s => s.Elevation.HasValue)
                        .Min(s => (double)s.Elevation!.Value);
                    var maxStoreyElevation = realStoreys
                        .Where(s => s.Elevation.HasValue)
                        .Max(s => (double)s.Elevation!.Value);

                    // Check if storeys are at high positive elevations (>100m) but context is negative
                    if (minStoreyElevation > 100000) // > 100m in mm
                    {
                        if (isDebugMode)
                        {
                            output.WriteWarning($"   Coordinate system mismatch detected for context building:");
                            output.WriteWarning($"   Context building: {contextGroundLevel.Value / 1000:F2}m (negative)");
                            output.WriteWarning($"   Building storeys: {minStoreyElevation / 1000:F2}m - {maxStoreyElevation / 1000:F2}m (positive)");
                        }

                        // Fix: Use absolute value (most common case - inverted Z-axis)
                        var correctedGroundLevel = Math.Abs(contextGroundLevel.Value);

                        // Verify the correction makes sense (should be close to lowest storey)
                        if (Math.Abs(correctedGroundLevel - minStoreyElevation) < 10000) // Within 10m
                        {
                            if (isDebugMode)
                            {
                                output.WriteSuccess($"   Corrected ground level (absolute value): {correctedGroundLevel / 1000:F2}m");
                            }
                            return correctedGroundLevel;
                        }
                        else
                        {
                            if (isDebugMode)
                            {
                                output.WriteWarning($"   Correction failed (difference: {Math.Abs(correctedGroundLevel - minStoreyElevation) / 1000:F2}m).");
                                output.WriteWarning($"   Ignoring context building and using another method.");
                            }
                            // Fall through to next method
                        }
                    }
                    else
                    {
                        // Negative context level but low storey elevations - might be valid
                        if (isDebugMode)
                        {
                            output.WriteSuccess($"   Ground level from context building: {contextGroundLevel.Value / 1000:F2}m");
                        }
                        return contextGroundLevel.Value;
                    }
                }
                else
                {
                    if (isDebugMode)
                    {
                        output.WriteSuccess($"   Ground level from context building: {contextGroundLevel.Value / 1000:F2}m");
                    }
                    return contextGroundLevel.Value;
                }
            }

            // Method 1: Check for external elements (IsExternal property)
            var externalGroundLevel = TryGetGroundLevelFromExternalElements(model, realStoreys, output, isDebugMode);
            if (externalGroundLevel.HasValue)
            {
                if (isDebugMode)
                {
                    output.WriteSuccess($"   Ground level from external elements: {externalGroundLevel.Value / 1000:F2}m");
                }
                return externalGroundLevel.Value;
            }

            // Method 2: Check for "Mark" storey representing the ground level plane
            var markStorey = allStoreys
                .FirstOrDefault(s =>
                {
                    var name = s.Name?.ToString()?.ToLower() ?? "";
                    return name == "mark" || name == "markplan" || name == "mark-plan" || name == "mark plan";
                });

            if (markStorey != null && markStorey.Elevation.HasValue)
            {
                var markElevation = (double)markStorey.Elevation.Value;

                // Try to get the height of the Mark storey to find its top elevation
                var markHeight = GetStoreyHeight(markStorey, allStoreys, output, isDebugMode);

                if (markHeight.HasValue && markHeight.Value > 0)
                {
                    var groundLevel = markElevation + markHeight.Value;
                    if (isDebugMode)
                    {
                        output.WriteInfo($"   Found 'Mark' level:");
                        output.WriteInfo($"      Bottom elevation: {markElevation / 1000:F2}m (sea level)");
                        output.WriteInfo($"      Height: {markHeight.Value / 1000:F2}m");
                        output.WriteSuccess($"   Ground level (top of 'Mark'): {groundLevel / 1000:F2}m");
                    }
                    return groundLevel;
                }
                else
                {
                    // If we can't determine height, use the elevation itself as ground
                    if (isDebugMode)
                    {
                        output.WriteInfo($"   Found 'Mark' level without height information");
                        output.WriteInfo($"   Using 'Mark' elevation as ground level: {markElevation / 1000:F2}m");
                    }
                    return markElevation;
                }
            }

            // Method 3: Check for explicit ground reference levels (in absolute systems)
            if (isAbsolute)
            {
                var groundReferences = allStoreys
                    .Where(s => s.Elevation.HasValue)
                    .Where(s =>
                    {
                        var name = s.Name?.ToString()?.ToLower() ?? "";
                        var longName = s.LongName?.ToString()?.ToLower() ?? "";

                        return name.Contains("ground") || name.Contains("terrain") ||
                               longName.Contains("ground") || longName.Contains("terrain");
                    })
                    .ToList();

                if (groundReferences.Any())
                {
                    var groundLevel = (double)groundReferences.First().Elevation!.Value;
                    if (isDebugMode)
                    {
                        output.WriteInfo($"   Found ground reference: '{groundReferences.First().Name}' @ {groundLevel / 1000:F2}m");
                    }
                    return groundLevel;
                }
            }

            // Method 4: Check IFC Site (only for non-absolute systems)
            if (!isAbsolute)
            {
                var site = model.Instances.OfType<IIfcSite>().FirstOrDefault();
                if (site?.RefElevation.HasValue == true)
                {
                    var groundLevel = Convert.ToDouble(site.RefElevation.Value);
                    if (isDebugMode)
                    {
                        output.WriteInfo($"   Found ground level from IfcSite: {groundLevel / 1000:F2}m");
                    }
                    return groundLevel;
                }
            }

            // Method 5: Use lowest non-basement floor as ground reference (for absolute systems)
            if (isAbsolute && realStoreys.Any())
            {
                var lowestNonBasement = realStoreys
                    .Where(s => s.Elevation.HasValue)
                    .OrderBy(s => (double)s.Elevation!.Value)
                    .FirstOrDefault();

                if (lowestNonBasement != null)
                {
                    var groundLevel = (double)lowestNonBasement.Elevation!.Value;
                    if (isDebugMode)
                    {
                        output.WriteInfo($"   Using lowest non-basement floor as ground reference: '{lowestNonBasement.Name}' @ {groundLevel / 1000:F2}m");
                    }
                    return groundLevel;
                }
            }

            // Method 6: Window heuristic for absolute systems
            if (isAbsolute)
            {
                if (isDebugMode)
                {
                    output.WriteWarning($"   No ground reference found, using heuristics...");
                }

                var allWindows = model.Instances.OfType<IIfcWindow>().ToList();

                if (allWindows.Any())
                {
                    var lowestWindow = allWindows
                        .Where(w => w.ObjectPlacement != null)
                        .OrderBy(w => GetWindowElevation(w))
                        .FirstOrDefault();

                    if (lowestWindow != null)
                    {
                        var sillHeight = GetWindowElevation(lowestWindow);

                        if (isDebugMode)
                        {
                            output.WriteInfo($"   Found lowest window sill: {sillHeight / 1000:F2}m");
                        }

                        if (sillHeight < 5000) // If sill height is less than 5m, consider it plausible
                        {
                            return sillHeight;
                        }
                        else
                        {
                            if (isDebugMode)
                            {
                                output.WriteWarning($"   Window height seems unrealistic as ground level ({sillHeight / 1000:F2}m)");
                            }
                        }
                    }
                }
                else
                {
                    if (isDebugMode)
                    {
                        output.WriteWarning($"   No windows found for heuristics");
                    }
                }
            }

            // Default: relative system
            if (isDebugMode)
            {
                output.WriteInfo($"   Using standard reference: 0.00m (relativ höjd)");
            }
            return 0.0;
        }

        /// <summary>
        /// Tries to find the base plate/slab elevation as alternative reference point
        /// </summary>
        public static double? TryFindBasePlateHeight(IIfcBuildingStorey firstFloor, IOutputWriter output, bool isDebugMode = false)
        {
            try
            {
                var model = firstFloor.Model;

                // Method 1: Look for slabs marked as "BASESLAB" or "FLOOR"
                var slabs = firstFloor.ContainsElements
                    .SelectMany(rel => rel.RelatedElements)
                    .OfType<IIfcSlab>()
                    .Where(s => s.PredefinedType == Xbim.Ifc4.Interfaces.IfcSlabTypeEnum.BASESLAB ||
                               s.PredefinedType == Xbim.Ifc4.Interfaces.IfcSlabTypeEnum.FLOOR)
                    .ToList();

                if (!slabs.Any())
                {
                    slabs = firstFloor.ContainsElements
                        .SelectMany(rel => rel.RelatedElements)
                        .OfType<IIfcSlab>()
                        .Where(s =>
                        {
                            var name = s.Name?.ToString()?.ToLower() ?? "";
                            return name.Contains("botten") || name.Contains("grund") ||
                                   name.Contains("base") || name.Contains("foundation");
                        })
                        .ToList();
                }

                if (slabs.Any())
                {
                    var firstSlab = slabs.First();

                    if (firstSlab.ObjectPlacement is IIfcLocalPlacement localPlacement)
                    {
                        var elevation = ExtractElevationFromPlacement(localPlacement);
                        if (elevation.HasValue)
                        {
                            if (isDebugMode)
                            {
                                output.WriteSuccess($"   Found base slab: {firstSlab.Name} @ {elevation.Value / 1000:F2}m");
                            }
                            return elevation.Value;
                        }
                    }
                }

                // Method 2: Use the first floor's elevation minus typical floor thickness
                if (firstFloor.Elevation.HasValue)
                {
                    var floorThickness = 200.0; // Assume 200mm typical floor thickness
                    return (double)firstFloor.Elevation.Value - floorThickness;
                }

                return null;
            }
            catch (Exception ex)
            {
                if (isDebugMode)
                {
                    output.WriteWarning($"   Could not find base slab: {ex.Message}");
                }
                return null;
            }
        }

        /// <summary>
        /// Extracts elevation from IFC placement data
        /// </summary>
        public static double? ExtractElevationFromPlacement(IIfcLocalPlacement placement)
        {
            try
            {
                if (placement.RelativePlacement is IIfcAxis2Placement3D axis3D)
                {
                    var location = axis3D.Location;
                    if (location.Coordinates.Count >= 3)
                    {
                        return (double)location.Coordinates[2].Value; // Z coordinate
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the elevation of a window from its placement
        /// </summary>
        public static double GetWindowElevation(IIfcWindow window)
        {
            try
            {
                if (window.ObjectPlacement is IIfcLocalPlacement localPlacement)
                {
                    var elevation = ExtractElevationFromPlacement(localPlacement);
                    if (elevation.HasValue)
                    {
                        return elevation.Value;
                    }
                }

                return double.MaxValue;
            }
            catch
            {
                return double.MaxValue;
            }
        }

        /// <summary>
        /// Tries to determine the height of a building storey by examining its geometry and relationships
        /// </summary>
        public static double? GetStoreyHeight(IIfcBuildingStorey storey, List<IIfcBuildingStorey> allStoreys, IOutputWriter output, bool isDebugMode = false)
        {
            try
            {
                // Method 1: Check Pset_BuildingStoreyCommon for NominalHeight
                foreach (var rel in storey.IsDefinedBy)
                {
                    if (rel.RelatingPropertyDefinition is IIfcPropertySet pset)
                    {
                        if (pset.Name?.ToString() == "Pset_BuildingStoreyCommon")
                        {
                            var heightProp = pset.HasProperties
                                .OfType<IIfcPropertySingleValue>()
                                .FirstOrDefault(p => p.Name.ToString() == "NominalHeight");

                            if (heightProp?.NominalValue?.Value != null)
                            {
                                if (isDebugMode)
                                {
                                    output.WriteDebug($"      Found storey height in Pset_BuildingStoreyCommon");
                                }
                                return Convert.ToDouble(heightProp.NominalValue.Value);
                            }
                        }

                        // Also check for any property with "height" or "höjd" in name
                        var anyHeightProp = pset.HasProperties
                            .OfType<IIfcPropertySingleValue>()
                            .FirstOrDefault(p =>
                            {
                                var propName = p.Name.ToString()?.ToLower() ?? "";
                                return propName.Contains("height") || propName.Contains("höjd");
                            });

                        if (anyHeightProp?.NominalValue?.Value != null)
                        {
                            if (isDebugMode)
                            {
                                output.WriteDebug($"      Found height in property: '{anyHeightProp.Name}'");
                            }
                            return Convert.ToDouble(anyHeightProp.NominalValue.Value);
                        }
                    }
                }

                // Method 2: Calculate from difference to next storey with HIGHER elevation
                if (!storey.Elevation.HasValue)
                    return null;

                var currentElevation = (double)storey.Elevation.Value;

                var storeysWithElevation = allStoreys
                    .Where(s => s.Elevation.HasValue)
                    .OrderBy(s => (double)s.Elevation!.Value)
                    .ToList();

                // Find the next storey with a HIGHER elevation (not the same)
                var nextStorey = storeysWithElevation
                    .FirstOrDefault(s => (double)s.Elevation!.Value > currentElevation);

                if (nextStorey != null)
                {
                    var nextElevation = (double)nextStorey.Elevation!.Value;
                    var height = nextElevation - currentElevation;

                    if (isDebugMode)
                    {
                        output.WriteDebug($"      Calculated height from next storey ('{nextStorey.Name}' @ {nextElevation / 1000:F2}m)");
                    }
                    return height;
                }

                // Method 3: Try to calculate from bounding box of elements
                var heightFromBoundingBox = GetStoreyHeightFromBoundingBox(storey, output, isDebugMode);
                if (heightFromBoundingBox.HasValue)
                {
                    return heightFromBoundingBox;
                }

                return null;
            }
            catch (Exception ex)
            {
                if (isDebugMode)
                {
                    output.WriteWarning($"      Could not determine storey height: {ex.Message}");
                }
                return null;
            }
        }

        /// <summary>
        /// Attempts to calculate storey height from the bounding box of its elements
        /// </summary>
        private static double? GetStoreyHeightFromBoundingBox(IIfcBuildingStorey storey, IOutputWriter output, bool isDebugMode = false)
        {
            try
            {
                var elements = storey.ContainsElements
                    .SelectMany(rel => rel.RelatedElements)
                    .Where(e => e.Representation != null)
                    .ToList();

                if (!elements.Any())
                    return null;

                double? minZ = null;
                double? maxZ = null;

                foreach (var element in elements)
                {
                    var bbox = GetBoundingBox(element);
                    if (bbox.HasValue)
                    {
                        if (!minZ.HasValue || bbox.Value.minZ < minZ.Value)
                            minZ = bbox.Value.minZ;
                        if (!maxZ.HasValue || bbox.Value.maxZ > maxZ.Value)
                            maxZ = bbox.Value.maxZ;
                    }
                }

                if (minZ.HasValue && maxZ.HasValue)
                {
                    var height = maxZ.Value - minZ.Value;
                    if (isDebugMode)
                    {
                        output.WriteDebug($"      Calculated height from bounding box: {height / 1000:F2}m");
                    }
                    return height;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the bounding box of an IFC product
        /// </summary>
        private static (double minZ, double maxZ)? GetBoundingBox(IIfcProduct product)
        {
            try
            {
                if (product.Representation == null)
                    return null;

                double? minZ = null;
                double? maxZ = null;

                foreach (var rep in product.Representation.Representations)
                {
                    foreach (var item in rep.Items)
                    {
                        var bbox = GetBoundingBoxFromItem(item);
                        if (bbox.HasValue)
                        {
                            if (!minZ.HasValue || bbox.Value.minZ < minZ.Value)
                                minZ = bbox.Value.minZ;
                            if (!maxZ.HasValue || bbox.Value.maxZ > maxZ.Value)
                                maxZ = bbox.Value.maxZ;
                        }
                    }
                }

                if (minZ.HasValue && maxZ.HasValue)
                    return (minZ.Value, maxZ.Value);

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Recursively gets bounding box from a representation item
        /// </summary>
        private static (double minZ, double maxZ)? GetBoundingBoxFromItem(IIfcRepresentationItem item)
        {
            try
            {
                // Try to get bounding box if it's explicitly defined
                if (item is IIfcBoundingBox bbox)
                {
                    var corner = bbox.Corner;
                    var z = corner.Coordinates.Count >= 3 ? (double)corner.Coordinates[2].Value : 0;
                    var height = (double)bbox.ZDim;
                    return (z, z + height);
                }

                // For other geometric items, we'd need more complex geometry processing
                // This is a simplified approach
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Tries to get ground level from context buildings marked with Pset_BuildingElementProxyCommon
        /// These are reference/context buildings that represent the surrounding environment
        /// </summary>
        private static double? TryGetGroundLevelFromContextBuilding(IModel model, IOutputWriter output, bool isDebugMode = false)
        {
            try
            {
                if (isDebugMode)
                {
                    output.WriteDebug("   Looking for context buildings...");
                }

                // Find all BuildingElementProxy or similar elements that might be context buildings
                var contextElements = model.Instances
                    .OfType<IIfcBuildingElementProxy>()
                    .ToList();

                if (!contextElements.Any())
                {
                    // Also check for general products with the property
                    contextElements = model.Instances
                        .OfType<IIfcProduct>()
                        .Where(p => p is IIfcBuildingElementProxy)
                        .Cast<IIfcBuildingElementProxy>()
                        .ToList();
                }

                if (isDebugMode)
                {
                    output.WriteDebug($"   Found {contextElements.Count} potential context elements");
                }

                foreach (var element in contextElements)
                {
                    // Check for Pset_BuildingElementProxyCommon property set
                    foreach (var relDefines in element.IsDefinedBy)
                    {
                        if (relDefines.RelatingPropertyDefinition is IIfcPropertySet pset)
                        {
                            var psetName = pset.Name?.ToString() ?? "";

                            if (psetName.Contains("BuildingElementProxyCommon", StringComparison.OrdinalIgnoreCase) ||
                                psetName.Contains("Common", StringComparison.OrdinalIgnoreCase))
                            {
                                if (isDebugMode)
                                {
                                    output.WriteDebug($"      Checking PropertySet: {psetName}");
                                }

                                foreach (var property in pset.HasProperties.OfType<IIfcPropertySingleValue>())
                                {
                                    var propName = property.Name.ToString() ?? "";
                                    var propValue = property.NominalValue?.Value?.ToString() ?? "";

                                    if (isDebugMode)
                                    {
                                        output.WriteDebug($"         • {propName}: {propValue}");
                                    }

                                    // Check if this is a context building
                                    if ((propName.Equals("Reference", StringComparison.OrdinalIgnoreCase) ||
                                         propName.Equals("Referens", StringComparison.OrdinalIgnoreCase)) &&
                                        (propValue.Contains("Kontext", StringComparison.OrdinalIgnoreCase) ||
                                         propValue.Contains("Context", StringComparison.OrdinalIgnoreCase)))
                                    {
                                        if (isDebugMode)
                                        {
                                            output.WriteInfo($"   Found context building: {element.Name} (Reference: {propValue})");
                                        }

                                        // Try to get elevation from this context building
                                        var elevation = GetElevationFromElement(element, output, isDebugMode);
                                        if (elevation.HasValue)
                                        {
                                            if (isDebugMode)
                                            {
                                                output.WriteInfo($"      • Elevation: {elevation.Value / 1000:F2}m");
                                            }

                                            // If it's a slab or similar, get its top surface
                                            var height = IfcPropertyHelper.GetHeightFromProperties(element);
                                            if (height.HasValue)
                                            {
                                                var topElevation = elevation.Value + height.Value;
                                                if (isDebugMode)
                                                {
                                                    output.WriteInfo($"      • Height: {height.Value / 1000:F2}m");
                                                    output.WriteInfo($"      • Top elevation: {topElevation / 1000:F2}m");
                                                }
                                                return topElevation;
                                            }

                                            return elevation.Value;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (isDebugMode)
                {
                    output.WriteDebug("   No context building with ground level found");
                }
                return null;
            }
            catch (Exception ex)
            {
                if (isDebugMode)
                {
                    output.WriteWarning($"   Error searching for context buildings: {ex.Message}");
                }
                return null;
            }
        }

        /// <summary>
        /// Gets elevation from an IFC element's placement
        /// </summary>
        private static double? GetElevationFromElement(IIfcProduct element, IOutputWriter output, bool isDebugMode = false)
        {
            try
            {
                // Method 1: Check ObjectPlacement
                if (element.ObjectPlacement is IIfcLocalPlacement localPlacement)
                {
                    var elevation = ExtractElevationFromPlacement(localPlacement);
                    if (elevation.HasValue)
                    {
                        return elevation.Value;
                    }
                }

                // Method 2: For slabs, check representation
                if (element is IIfcSlab slab)
                {
                    // Get the lowest Z coordinate from geometry
                    var representation = slab.Representation;
                    if (representation != null)
                    {
                        foreach (var rep in representation.Representations)
                        {
                            if (rep.RepresentationIdentifier?.ToString()?.ToLower() == "body")
                            {
                                // Try to extract Z coordinate from geometric representation
                                // This is simplified - you might need more robust geometry parsing
                                foreach (var item in rep.Items)
                                {
                                    if (item is IIfcExtrudedAreaSolid extruded)
                                    {
                                        var position = extruded.Position;
                                        if (position.Location.Coordinates.Count >= 3)
                                        {
                                            return (double)position.Location.Coordinates[2].Value;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // Method 3: Check if contained in a storey
                var containingStorey = element.Decomposes?.FirstOrDefault()?.RelatingObject as IIfcBuildingStorey;
                if (containingStorey?.Elevation.HasValue == true)
                {
                    if (isDebugMode)
                    {
                        output.WriteDebug($"      Using storey's elevation: {containingStorey.Name}");
                    }
                    return (double)containingStorey.Elevation.Value;
                }

                return null;
            }
            catch (Exception ex)
            {
                if (isDebugMode)
                {
                    output.WriteDebug($"      Could not extract elevation: {ex.Message}");
                }
                return null;
            }
        }

        /// <summary>
        /// Tries to get ground level from external building elements (walls, slabs, etc.)
        /// External elements with IsExternal=true are likely at or near ground level
        /// </summary>
        private static double? TryGetGroundLevelFromExternalElements(IModel model, List<IIfcBuildingStorey> realStoreys, IOutputWriter output, bool isDebugMode = false)
        {
            try
            {
                if (isDebugMode)
                {
                    output.WriteDebug("   Looking for external building elements...");
                }

                // Identify the lowest storeys (basement + first 2 floors) to look for ground level indicators
                var lowestStoreys = realStoreys
                    .Where(s => s.Elevation.HasValue)
                    .OrderBy(s => (double)s.Elevation!.Value)
                    .Take(3) // Only consider the 3 lowest floors
                    .ToList();

                if (!lowestStoreys.Any())
                {
                    if (isDebugMode)
                    {
                        output.WriteDebug("   Could not identify lowest building storeys");
                    }
                    return null;
                }

                var lowestStoreyElevations = lowestStoreys.Select(s => (double)s.Elevation!.Value).ToHashSet();
                if (isDebugMode)
                {
                    output.WriteDebug($"   Searching external elements from the {lowestStoreys.Count} lowest floors:");
                    foreach (var s in lowestStoreys)
                    {
                        output.WriteDebug($"      • {s.Name} @ {(double)s.Elevation!.Value / 1000:F2}m");
                    }
                }

                var externalElements = new List<(IIfcProduct element, double elevation, string type)>();

                // Check external walls (only from lowest storeys)
                var walls = model.Instances.OfType<IIfcWall>().ToList();
                if (isDebugMode)
                {
                    output.WriteDebug($"   Checking {walls.Count} walls...");
                }

                foreach (var wall in walls)
                {
                    if (IsElementExternal(wall, "Pset_WallCommon", output, isDebugMode))
                    {
                        // Check if wall belongs to one of the lowest storeys
                        var containingStorey = wall.Decomposes?.FirstOrDefault()?.RelatingObject as IIfcBuildingStorey;
                        if (containingStorey == null)
                        {
                            containingStorey = wall.IsContainedIn as IIfcBuildingStorey;
                        }

                        if (containingStorey?.Elevation.HasValue == true)
                        {
                            var storeyElev = (double)containingStorey.Elevation.Value;
                            if (!lowestStoreyElevations.Contains(storeyElev))
                            {
                                continue; // Skip walls from upper floors
                            }
                        }

                        var elevation = GetBottomElevationFromElement(wall, output, isDebugMode);
                        if (elevation.HasValue)
                        {
                            externalElements.Add((wall, elevation.Value, "wall"));
                            if (isDebugMode)
                            {
                                output.WriteDebug($"      • External wall: {wall.Name} @ {elevation.Value / 1000:F2}m (from {containingStorey?.Name})");
                            }
                        }
                    }
                }

                // Check external slabs (only from lowest storeys, and exclude roofs from upper floors)
                var slabs = model.Instances.OfType<IIfcSlab>().ToList();
                if (isDebugMode)
                {
                    output.WriteDebug($"   Checking {slabs.Count} slabs...");
                }

                foreach (var slab in slabs)
                {
                    if (IsElementExternal(slab, "Pset_SlabCommon", output, isDebugMode))
                    {
                        // Check if slab belongs to one of the lowest storeys
                        var containingStorey = slab.Decomposes?.FirstOrDefault()?.RelatingObject as IIfcBuildingStorey;
                        if (containingStorey == null)
                        {
                            containingStorey = slab.IsContainedIn as IIfcBuildingStorey;
                        }

                        if (containingStorey?.Elevation.HasValue == true)
                        {
                            var storeyElev = (double)containingStorey.Elevation.Value;
                            if (!lowestStoreyElevations.Contains(storeyElev))
                            {
                                if (isDebugMode)
                                {
                                    output.WriteDebug($"      Skipping slab from upper floor: {slab.Name} ({containingStorey.Name})");
                                }
                                continue; // Skip slabs from upper floors (they're roofs)
                            }
                        }
                        else
                        {
                            // Slab has no containing storey - likely a roof with absolute placement
                            if (isDebugMode)
                            {
                                output.WriteDebug($"      Skipping slab without storey reference (likely roof): {slab.Name}");
                            }
                            continue;
                        }

                        var elevation = GetBottomElevationFromElement(slab, output, isDebugMode);
                        if (elevation.HasValue)
                        {
                            var height = IfcPropertyHelper.GetHeightFromProperties(slab);
                            var topElevation = height.HasValue ? elevation.Value + height.Value : elevation.Value;

                            externalElements.Add((slab, topElevation, "slab"));
                            if (isDebugMode)
                            {
                                output.WriteDebug($"      • External slab: {slab.Name} @ {topElevation / 1000:F2}m (top, from {containingStorey?.Name})");
                            }
                        }
                    }
                }

                // Check external doors (only from lowest storeys)
                var doors = model.Instances.OfType<IIfcDoor>().ToList();
                if (isDebugMode)
                {
                    output.WriteDebug($"   Checking {doors.Count} doors...");
                }

                foreach (var door in doors)
                {
                    if (IsElementExternal(door, "Pset_DoorCommon", output, isDebugMode))
                    {
                        // Check if door belongs to one of the lowest storeys
                        var containingStorey = door.Decomposes?.FirstOrDefault()?.RelatingObject as IIfcBuildingStorey;
                        if (containingStorey == null)
                        {
                            containingStorey = door.IsContainedIn as IIfcBuildingStorey;
                        }

                        if (containingStorey?.Elevation.HasValue == true)
                        {
                            var storeyElev = (double)containingStorey.Elevation.Value;
                            if (!lowestStoreyElevations.Contains(storeyElev))
                            {
                                continue; // Skip doors from upper floors
                            }
                        }

                        var elevation = GetBottomElevationFromElement(door, output, isDebugMode);
                        if (elevation.HasValue)
                        {
                            externalElements.Add((door, elevation.Value, "door"));
                            if (isDebugMode)
                            {
                                output.WriteDebug($"      • External door: {door.Name} @ {elevation.Value / 1000:F2}m (from {containingStorey?.Name})");
                            }
                        }
                    }
                }

                if (!externalElements.Any())
                {
                    if (isDebugMode)
                    {
                        output.WriteDebug("   No external elements with IsExternal=true found");
                    }
                    return null;
                }

                if (isDebugMode)
                {
                    output.WriteInfo($"   Found {externalElements.Count} external elements");
                }

                // Get the lowest elevation from external elements
                // This is likely the ground level (or close to it)
                var lowestExternalElements = externalElements
                    .OrderBy(e => e.elevation)
                    .Take(10) // Look at the 10 lowest elements
                    .ToList();

                // Calculate median of the lowest elements to avoid outliers
                var lowestElevations = lowestExternalElements.Select(e => e.elevation).ToList();
                lowestElevations.Sort();

                double relativeGroundLevel;
                if (lowestElevations.Count == 1)
                {
                    relativeGroundLevel = lowestElevations[0];
                }
                else if (lowestElevations.Count % 2 == 0)
                {
                    relativeGroundLevel = (lowestElevations[lowestElevations.Count / 2 - 1] +
                                           lowestElevations[lowestElevations.Count / 2]) / 2.0;
                }
                else
                {
                    relativeGroundLevel = lowestElevations[lowestElevations.Count / 2];
                }

                if (isDebugMode)
                {
                    output.WriteInfo($"   External elements at ground level:");
                    foreach (var elem in lowestExternalElements.Take(5))
                    {
                        output.WriteInfo($"      • {elem.type}: {elem.element.Name} @ {elem.elevation / 1000:F2}m");
                    }

                    output.WriteInfo($"   Calculated ground level (median of lowest elements): {relativeGroundLevel / 1000:F2}m");
                }

                // **CRITICAL FIX: Detect coordinate system mismatch**
                // Use the filtered realStoreys instead of querying all storeys
                if (realStoreys.Any())
                {
                    var minStoreyElevation = realStoreys
                        .Where(s => s.Elevation.HasValue)
                        .Min(s => (double)s.Elevation!.Value);
                    var maxStoreyElevation = realStoreys
                        .Where(s => s.Elevation.HasValue)
                        .Max(s => (double)s.Elevation!.Value);

                    // DEBUG OUTPUT
                    if (isDebugMode)
                    {
                        output.WriteDebug($"   DEBUG: minStoreyElevation = {minStoreyElevation}mm ({minStoreyElevation / 1000:F2}m)");
                        output.WriteDebug($"   DEBUG: relativeGroundLevel = {relativeGroundLevel}mm ({relativeGroundLevel / 1000:F2}m)");
                        output.WriteDebug($"   DEBUG: abs(relativeGroundLevel) = {Math.Abs(relativeGroundLevel)}mm");
                        output.WriteDebug($"   DEBUG: Check 1: {minStoreyElevation} > 50000 = {minStoreyElevation > 50000}");
                        output.WriteDebug($"   DEBUG: Check 2: {Math.Abs(relativeGroundLevel)} < 10000 = {Math.Abs(relativeGroundLevel) < 10000}");
                    }

                    // Check if there's a coordinate system mismatch
                    // Storeys are at high absolute elevations (>50m) but external elements are near zero or negative
                    // This means external elements use relative coordinates
                    if (minStoreyElevation > 50000 && Math.Abs(relativeGroundLevel) < 10000) // 50m and 10m in mm
                    {
                        if (isDebugMode)
                        {
                            output.WriteWarning($"      Coordinate system mismatch detected:");
                            output.WriteWarning($"      Building storeys: {minStoreyElevation / 1000:F2}m - {maxStoreyElevation / 1000:F2}m (absolute above sea level)");
                            output.WriteWarning($"      External elements: {relativeGroundLevel / 1000:F2}m (relative to building origin)");
                        }

                        // Convert relative ground level to absolute
                        // The relative ground level is offset from the lowest storey
                        var absoluteGroundLevel = minStoreyElevation + relativeGroundLevel;

                        if (isDebugMode)
                        {
                            output.WriteInfo($"    Converting to absolute height:");
                            output.WriteInfo($"      Relative ground level: {relativeGroundLevel / 1000:F2}m");
                            output.WriteInfo($"      Lowest floor: {minStoreyElevation / 1000:F2}m");
                            output.WriteSuccess($"      Absolute ground level: {absoluteGroundLevel / 1000:F2}m");
                        }

                        return absoluteGroundLevel;
                    }
                    else
                    {
                        if (isDebugMode)
                        {
                            output.WriteDebug($"   DEBUG: Mismatch check failed - no conversion needed");
                        }
                    }
                }

                return relativeGroundLevel;
            }
            catch (Exception ex)
            {
                if (isDebugMode)
                {
                    output.WriteWarning($"   Error searching for external elements: {ex.Message}");
                }
                return null;
            }
        }

        /// <summary>
        /// Checks if an element has IsExternal=true in its property set
        /// </summary>
        private static bool IsElementExternal(IIfcProduct element, string psetName, IOutputWriter output, bool isDebugMode = false)
        {
            try
            {
                foreach (var relDefines in element.IsDefinedBy)
                {
                    if (relDefines.RelatingPropertyDefinition is IIfcPropertySet pset)
                    {
                        var currentPsetName = pset.Name?.ToString() ?? "";

                        // Check if this is the right property set (e.g., Pset_WallCommon, Pset_SlabCommon)
                        if (currentPsetName.Equals(psetName, StringComparison.OrdinalIgnoreCase))
                        {
                            foreach (var property in pset.HasProperties.OfType<IIfcPropertySingleValue>())
                            {
                                var propName = property.Name.ToString() ?? "";

                                if (propName.Equals("IsExternal", StringComparison.OrdinalIgnoreCase))
                                {
                                    var value = property.NominalValue?.Value;

                                    // Check for boolean true
                                    if (value is bool boolValue && boolValue)
                                    {
                                        return true;
                                    }

                                    // Check for string representations of true
                                    if (value is string strValue)
                                    {
                                        return strValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                                               strValue.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                                               strValue.Equals("ja", StringComparison.OrdinalIgnoreCase) ||
                                               strValue.Equals("1", StringComparison.OrdinalIgnoreCase);
                                    }

                                    // Check for numeric 1
                                    if (value != null)
                                    {
                                        try
                                        {
                                            return Convert.ToInt32(value) == 1;
                                        }
                                        catch { }
                                    }
                                }
                            }
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                if (isDebugMode)
                {
                    output.WriteDebug($"      Error checking IsExternal for {element.Name}: {ex.Message}");
                }
                return false;
            }
        }

        /// <summary>
        /// Gets the bottom elevation of an element from its placement
        /// For walls/doors: returns the base elevation
        /// For slabs: returns the bottom surface elevation
        /// </summary>
        private static double? GetBottomElevationFromElement(IIfcProduct element, IOutputWriter output, bool isDebugMode = false)
        {
            try
            {
                // Method 1: Direct placement
                if (element.ObjectPlacement is IIfcLocalPlacement localPlacement)
                {
                    var elevation = ExtractElevationFromPlacement(localPlacement);
                    if (elevation.HasValue)
                    {
                        // Check if this is a relative placement by looking at PlacementRelTo
                        if (localPlacement.PlacementRelTo != null)
                        {
                            // This is a relative placement - need to add parent's elevation
                            var containingStorey = element.Decomposes?.FirstOrDefault()?.RelatingObject as IIfcBuildingStorey;
                            if (containingStorey == null)
                            {
                                containingStorey = element.IsContainedIn as IIfcBuildingStorey;
                            }

                            if (containingStorey?.Elevation.HasValue == true)
                            {
                                var storeyElevation = (double)containingStorey.Elevation.Value;
                                var absoluteElevation = storeyElevation + elevation.Value;
                                if (isDebugMode)
                                {
                                    output.WriteDebug($"      {element.Name}: Relative elevation {elevation.Value / 1000:F2}m + storey {storeyElevation / 1000:F2}m = {absoluteElevation / 1000:F2}m");
                                }
                                return absoluteElevation;
                            }
                        }

                        // Absolute placement
                        return elevation.Value;
                    }
                }

                // Method 2: Get from containing storey (fallback only)
                var containingStorey2 = element.Decomposes?.FirstOrDefault()?.RelatingObject as IIfcBuildingStorey;
                if (containingStorey2 == null)
                {
                    containingStorey2 = element.IsContainedIn as IIfcBuildingStorey;
                }

                if (containingStorey2?.Elevation.HasValue == true)
                {
                    return (double)containingStorey2.Elevation.Value;
                }

                return null;
            }
            catch (Exception ex)
            {
                if (isDebugMode)
                {
                    output.WriteDebug($"      Could not extract elevation from {element.Name}: {ex.Message}");
                }
                return null;
            }
        }

        /// <summary>
        /// Gets the top elevation of an element (including its height)
        /// Useful for finding roof tops, building tops, etc.
        /// </summary>
        public static double? GetElementTopElevation(IIfcProduct element, bool isDebugMode = false)
        {
            try
            {
                // Get the bottom elevation of the element
                double? bottomElevation = null;

                if (element.ObjectPlacement is IIfcLocalPlacement localPlacement)
                {
                    var elevation = ExtractElevationFromPlacement(localPlacement);
                    if (elevation.HasValue)
                    {
                        bottomElevation = elevation.Value;

                        // Handle relative placement
                        if (localPlacement.PlacementRelTo != null)
                        {
                            var containingStorey = element.Decomposes?.FirstOrDefault()?.RelatingObject as IIfcBuildingStorey;
                            if (containingStorey == null)
                            {
                                containingStorey = element.IsContainedIn as IIfcBuildingStorey;
                            }

                            if (containingStorey?.Elevation.HasValue == true)
                            {
                                var storeyElevation = (double)containingStorey.Elevation.Value;
                                bottomElevation = storeyElevation + elevation.Value;
                            }
                        }
                    }
                }

                // If we couldn't get bottom elevation from placement, try from containing storey
                if (!bottomElevation.HasValue)
                {
                    var containingStorey = element.Decomposes?.FirstOrDefault()?.RelatingObject as IIfcBuildingStorey;
                    if (containingStorey == null)
                    {
                        containingStorey = element.IsContainedIn as IIfcBuildingStorey;
                    }

                    if (containingStorey?.Elevation.HasValue == true)
                    {
                        bottomElevation = (double)containingStorey.Elevation.Value;
                    }
                }

                if (!bottomElevation.HasValue)
                    return null;

                // Now get the height of the element from its bounding box
                var bbox = GetBoundingBox(element);
                if (bbox.HasValue)
                {
                    var height = bbox.Value.maxZ - bbox.Value.minZ;
                    return bottomElevation.Value + height;
                }

                // Fallback: return bottom elevation if we can't determine height
                return bottomElevation;
            }
            catch
            {
                return null;
            }
        }
    }
}