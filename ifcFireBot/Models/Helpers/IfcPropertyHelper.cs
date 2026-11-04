// Models/Helpers/IfcPropertyHelper.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Xbim.Ifc4.Interfaces;

namespace ifcFireBot.Models.Helpers
{
    public static class IfcPropertyHelper
    {
        /// <summary>
        /// Gets height/clearance from IFC property sets
        /// </summary>
        public static double? GetHeightFromProperties(IIfcObject ifcObject)
        {
            var heightPropertyNames = new[]
            {
                "Height", "ClearHeight", "NominalHeight", 
                "HeadHeight", "CeilingHeight", "InternalHeight",
                "Höjd", "Takhöjd", "Rumshöjd" // Swedish
            };

            // Check property sets
            foreach (var relDefines in ifcObject.IsDefinedBy)
            {
                if (relDefines.RelatingPropertyDefinition is IIfcPropertySet propertySet)
                {
                    foreach (var property in propertySet.HasProperties.OfType<IIfcPropertySingleValue>())
                    {
                        var propName = property.Name.ToString();

                        if (heightPropertyNames.Any(name => propName.Contains(name, StringComparison.OrdinalIgnoreCase)))
                        {
                            if (property.NominalValue != null)
                            {
                                try
                                {
                                    return Convert.ToDouble(property.NominalValue.Value);
                                }
                                catch
                                {
                                    // Continue to next property
                                }
                            }
                        }
                    }
                }

                // Check quantity sets
                if (relDefines.RelatingPropertyDefinition is IIfcElementQuantity quantitySet)
                {
                    foreach (var quantity in quantitySet.Quantities.OfType<IIfcQuantityLength>())
                    {
                        if (heightPropertyNames.Any(name => quantity.Name.ToString().Contains(name, StringComparison.OrdinalIgnoreCase)))
                        {
                            return quantity.LengthValue;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Gets area from IFC property sets
        /// </summary>
        public static double? GetAreaFromProperties(IIfcObject ifcObject)
        {
            var areaPropertyNames = new[]
            {
                "GrossFloorArea", "NetFloorArea", "Area",
                "GrossArea", "NetArea", "TotalArea",
                "Bruttoarea", "Nettoarea", "Yta" // Swedish
            };

            // Check property sets
            foreach (var relDefines in ifcObject.IsDefinedBy)
            {
                if (relDefines.RelatingPropertyDefinition is IIfcPropertySet propertySet)
                {
                    foreach (var property in propertySet.HasProperties.OfType<IIfcPropertySingleValue>())
                    {
                        var propName = property.Name.ToString();

                        if (areaPropertyNames.Any(name => propName.Contains(name, StringComparison.OrdinalIgnoreCase)))
                        {
                            if (property.NominalValue != null)
                            {
                                try
                                {
                                    return Convert.ToDouble(property.NominalValue.Value);
                                }
                                catch
                                {
                                    // Continue to next property
                                }
                            }
                        }
                    }
                }

                // Check quantity sets
                if (relDefines.RelatingPropertyDefinition is IIfcElementQuantity quantitySet)
                {
                    foreach (var quantity in quantitySet.Quantities.OfType<IIfcQuantityArea>())
                    {
                        if (areaPropertyNames.Any(name => quantity.Name.ToString().Contains(name, StringComparison.OrdinalIgnoreCase)))
                        {
                            return quantity.AreaValue;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Calculates area from slabs contained in a building storey
        /// </summary>
        public static double CalculateAreaFromSlabs(IIfcBuildingStorey storey)
        {
            double totalArea = 0;

            // Get all slabs in this storey
            var containedElements = storey.IsDecomposedBy
                .SelectMany(rel => rel.RelatedObjects)
                .OfType<IIfcSlab>();

            // Alternative: use spatial containment
            if (!containedElements.Any())
            {
                containedElements = storey.ContainsElements
                    .SelectMany(rel => rel.RelatedElements)
                    .OfType<IIfcSlab>();
            }

            foreach (var slab in containedElements)
            {
                var area = GetAreaFromProperties(slab);
                if (area.HasValue)
                {
                    totalArea += area.Value;
                }
                else
                {
                    // Fallback to geometry calculation (same as CalculateSlabArea in AreaCalculator)
                    var geometryArea = GeometryHelper.CalculateAreaFromGeometry(slab);
                    if (geometryArea > 0)
                    {
                        totalArea += geometryArea;
                    }
                }
            }

            return totalArea;
        }

        /// <summary>
        /// Calculates area from spaces contained in a building storey
        /// </summary>
        public static double CalculateAreaFromSpaces(IIfcBuildingStorey storey)
        {
            double totalArea = 0;
            
            // Get storey name for matching
            var storeyName = storey.Name?.ToString();
            if (string.IsNullOrEmpty(storeyName))
                return 0;

            // Method 1: Try spatial containment (standard IFC approach)
            var containedSpaces = storey.ContainsElements
                .SelectMany(rel => rel.RelatedElements)
                .OfType<IIfcSpace>()
                .ToList();

            // Method 2: If no spaces found via containment, search by StoreyName property 
            if (!containedSpaces.Any())
            {
                // Get all spaces in the model
                var model = storey.Model;
                containedSpaces = model.Instances.OfType<IIfcSpace>()
                    .Where(space =>
                    {
                        // Check StoreyName property in BIP PropertySet
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

            // Calculate area from found spaces
            foreach (var space in containedSpaces)
            {
                // Method 1: Try to get area from properties
                var area = GetAreaFromProperties(space);
                if (area.HasValue)
                {
                    totalArea += area.Value;
                }
                else
                {
                    // Method 2: Calculate from geometry
                    var geometryArea = GeometryHelper.CalculateAreaFromGeometry(space);
                    if (geometryArea > 0)
                    {
                        totalArea += geometryArea;
                    }
                }
            }

            return totalArea;
        }

        /// <summary>
        /// Debug method to print all properties and quantities for elements
        /// </summary>
        public static void PrintAllPropertiesAndQuantities(IEnumerable<IIfcProduct> elements)
        {
            Console.WriteLine("\n🔍 DEBUG: Inspecting element properties...");
            Console.WriteLine("========================================");
            
            foreach (var el in elements)
            {
                Console.WriteLine($"\n📦 Element: {el.Name} ({el.GetType().Name})");
                Console.WriteLine($"   GlobalId: {el.GlobalId}");
                
                if (el is IIfcSpatialStructureElement spatial && spatial.LongName != null)
                {
                    Console.WriteLine($"   LongName: {spatial.LongName}");
                }
                
                if (el is IIfcBuildingStorey storey && storey.Elevation.HasValue)
                {
                    Console.WriteLine($"   Elevation: {storey.Elevation.Value:F2}");
                }

                // Check property sets
                foreach (var rel in el.IsDefinedBy)
                {
                    if (rel.RelatingPropertyDefinition is IIfcPropertySet propertySet)
                    {
                        Console.WriteLine($"\n   📋 PropertySet: {propertySet.Name}");
                        foreach (var prop in propertySet.HasProperties.OfType<IIfcPropertySingleValue>())
                        {
                            var value = prop.NominalValue?.Value?.ToString() ?? "null";
                            Console.WriteLine($"      • {prop.Name}: {value}");
                        }
                    }
                    
                    // Check quantity sets
                    if (rel.RelatingPropertyDefinition is IIfcElementQuantity quantitySet)
                    {
                        Console.WriteLine($"\n   📐 QuantitySet: {quantitySet.Name}");
                        foreach (var qty in quantitySet.Quantities)
                        {
                            if (qty is IIfcQuantityArea areaQty)
                            {
                                Console.WriteLine($"      • {qty.Name}: {areaQty.AreaValue:F2} m² ⭐");
                            }
                            else if (qty is IIfcQuantityLength lengthQty)
                            {
                                Console.WriteLine($"      • {qty.Name}: {lengthQty.LengthValue:F2} m");
                            }
                            else if (qty is IIfcQuantityVolume volumeQty)
                            {
                                Console.WriteLine($"      • {qty.Name}: {volumeQty.VolumeValue:F2} m³");
                            }
                            else
                            {
                                Console.WriteLine($"      • {qty.Name} ({qty.GetType().Name})");
                            }
                        }
                    }
                }
                
                Console.WriteLine();
            }
            
            Console.WriteLine("========================================\n");
        }

        public static double? GetVolumeFromProperties(IIfcProduct element)
        {
            // Similar to GetAreaFromProperties but looking for volume properties
            var volumeQuantity = element.IsDefinedBy
                .OfType<IIfcRelDefinesByProperties>()
                .SelectMany(r => r.RelatingPropertyDefinition is IIfcElementQuantity eq
                    ? eq.Quantities
                    : Enumerable.Empty<IIfcPhysicalQuantity>())
                .OfType<IIfcQuantityVolume>()
                .FirstOrDefault();

            if (volumeQuantity != null)
            {
                var rawValue = volumeQuantity.VolumeValue;
                var convertedValue = rawValue / 1000.0;
                Console.WriteLine($"   DEBUG Volume: Raw={rawValue:F2} dm³, Converted={convertedValue:F2} m³");
                return convertedValue;
            } 

            // IFC stores volume in dm³ (liters), convert to m³
            // 1 m³ = 1000 dm³ (liters)
            return null;
        }

        public static double CalculateVolumeFromSpaces(IIfcBuildingStorey storey, List<IIfcBuildingStorey> allStoreys, IOutputWriter output, bool isDebugMode = false)
        {
            double totalVolume = 0;
            
            // Get storey name for matching
            var storeyName = storey.Name?.ToString();
            if (string.IsNullOrEmpty(storeyName))
                return 0;

            // Method 1: Try spatial decomposition (standard IFC approach)
            var containedSpaces = storey.IsDecomposedBy
                .SelectMany(r => r.RelatedObjects)
                .OfType<IIfcSpace>()
                .ToList();

            // Method 2: Try spatial containment relationship
            if (!containedSpaces.Any())
            {
                containedSpaces = storey.ContainsElements
                    .SelectMany(rel => rel.RelatedElements)
                    .OfType<IIfcSpace>()
                    .ToList();
            }

            // Method 3: If no spaces found via relationships, search by StoreyName property
            if (!containedSpaces.Any())
            {
                var model = storey.Model;
                containedSpaces = model.Instances.OfType<IIfcSpace>()
                    .Where(space =>
                    {
                        // Check StoreyName property in property sets
                        foreach (var rel in space.IsDefinedBy)
                        {
                            if (rel.RelatingPropertyDefinition is IIfcPropertySet pset)
                            {
                                foreach (var prop in pset.HasProperties.OfType<IIfcPropertySingleValue>())
                                {
                                    if (prop.Name.ToString().Equals("StoreyName", StringComparison.OrdinalIgnoreCase))
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

            // Method 4: If still no spaces, try calculating from area × height
            if (!containedSpaces.Any())
            {
                // Try to get area from slabs or storey properties
                var area = CalculateAreaFromSlabs(storey);
                if (area == 0)
                {
                    area = GetAreaFromProperties(storey) ?? 0;
                }

                // Try to get height from storey properties or elevation difference
                var height = GetHeightFromProperties(storey);
                if (!height.HasValue)
                {
                    height = ElevationHelper.GetStoreyHeight(storey, allStoreys, output, isDebugMode);
                }

                if (area > 0 && height.HasValue && height.Value > 0)
                {
                    return area * height.Value;
                }

                return 0; // No spaces and no fallback data available
            }

            // Calculate volume from found spaces
            foreach (var space in containedSpaces)
            {
                // First try to get volume from properties
                var volume = GetVolumeFromProperties(space);
                if (volume.HasValue)
                {
                    totalVolume += volume.Value;
                }
                else
                {
                    // Fallback to geometry-based calculation
                    var geometryVolume = GeometryHelper.CalculateVolumeFromGeometry(space);
                    if (geometryVolume > 0)
                    {
                        totalVolume += geometryVolume;
                    }
                }
            }

            return totalVolume;
        }
    }
}