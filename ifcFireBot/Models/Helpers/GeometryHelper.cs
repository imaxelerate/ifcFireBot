// Models/Helpers/GeometryHelper.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;

#nullable enable

namespace ifcFireBot.Models.Helpers
{
    public static class GeometryHelper
    {
        /// <summary>
        /// Calculates area from geometric representation of spaces
        /// </summary>
        public static double CalculateAreaFromGeometry(IIfcSpace space)
        {
            try
            {
                var representation = space.Representation;
                if (representation == null)
                    return 0;

                double totalArea = 0;

                foreach (var rep in representation.Representations)
                {
                    var repType = rep.RepresentationIdentifier?.ToString()?.ToLower();
                    
                    if (repType == "footprint" || repType == "body")
                    {
                        foreach (var item in rep.Items)
                        {
                            var area = CalculateAreaFromGeometricItem(item);
                            if (area > 0)
                            {
                                totalArea += area;
                                break;
                            }
                        }
                    }
                }

                // Convert from mm² to m²
                return totalArea / 1_000_000.0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   Fel vid geometriberäkning för {space.Name}: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Calculates area from geometric representation of products (slabs, walls, etc.)
        /// </summary>
        public static double CalculateAreaFromGeometry(IIfcProduct product)
        {
            try
            {
                var representation = product.Representation;
                if (representation == null)
                    return 0;

                double totalArea = 0;

                foreach (var rep in representation.Representations)
                {
                    var repType = rep.RepresentationIdentifier?.ToString()?.ToLower();
                    
                    if (repType == "footprint" || repType == "body")
                    {
                        foreach (var item in rep.Items)
                        {
                            var area = CalculateAreaFromGeometricItem(item);
                            if (area > 0)
                            {
                                totalArea += area;
                                break;
                            }
                        }
                    }
                }

                // Convert from mm² to m²
                return totalArea / 1_000_000.0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   Fel vid geometriberäkning för {product.Name}: {ex.Message}");
                return 0;
            }
        }

        private static double CalculateAreaFromGeometricItem(IIfcRepresentationItem item)
        {
            try
            {
                switch (item)
                {
                    case IIfcExtrudedAreaSolid extrusion:
                        return CalculateProfileArea(extrusion.SweptArea);

                    case IIfcFacetedBrep brep:
                        return CalculateBrepBottomArea(brep);

                    case IIfcBooleanResult boolean:
                        return CalculateBooleanResultArea(boolean);

                    case IIfcGeometricCurveSet curveSet:
                        return CalculateCurveSetArea(curveSet);

                    default:
                        return 0;
                }
            }
            catch
            {
                return 0;
            }
        }

        private static double CalculateProfileArea(IIfcProfileDef profile)
        {
            try
            {
                switch (profile)
                {
                    case IIfcRectangleProfileDef rectangle:
                        return rectangle.XDim * rectangle.YDim;

                    case IIfcCircleProfileDef circle:
                        return Math.PI * Math.Pow(circle.Radius, 2);

                    case IIfcArbitraryProfileDefWithVoids arbitraryWithVoids:
                        var outerArea = CalculateClosedCurveArea(arbitraryWithVoids.OuterCurve);
                        var voidArea = 0.0;
                        foreach (var innerCurve in arbitraryWithVoids.InnerCurves)
                        {
                            voidArea += CalculateClosedCurveArea(innerCurve);
                        }
                        return outerArea - voidArea;

                    case IIfcArbitraryClosedProfileDef arbitrary:
                        return CalculateClosedCurveArea(arbitrary.OuterCurve);

                    default:
                        return 0;
                }
            }
            catch
            {
                return 0;
            }
        }

        private static double CalculateClosedCurveArea(IIfcCurve curve)
        {
            try
            {
                var points = new List<(double x, double y)>();

                switch (curve)
                {
                    case IIfcPolyline polyline:
                        foreach (var point in polyline.Points)
                        {
                            var coords = point.Coordinates;
                            if (coords.Count >= 2)
                            {
                                points.Add(((double)coords[0].Value, (double)coords[1].Value));
                            }
                        }
                        break;

                    case IIfcCompositeCurve composite:
                        foreach (var segment in composite.Segments)
                        {
                            if (segment.ParentCurve is IIfcPolyline poly)
                            {
                                foreach (var point in poly.Points)
                                {
                                    var coords = point.Coordinates;
                                    if (coords.Count >= 2)
                                    {
                                        points.Add(((double)coords[0].Value, (double)coords[1].Value));
                                    }
                                }
                            }
                        }
                        break;
                }

                if (points.Count >= 3)
                {
                    return CalculatePolygonArea(points);
                }

                return 0;
            }
            catch
            {
                return 0;
            }
        }

        private static double CalculatePolygonArea(List<(double x, double y)> points)
        {
            if (points.Count < 3)
                return 0;

            double area = 0;
            int n = points.Count;

            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                area += points[i].x * points[j].y;
                area -= points[j].x * points[i].y;
            }

            return Math.Abs(area) / 2.0;
        }

        private static double CalculateBrepBottomArea(IIfcFacetedBrep brep)
        {
            try
            {
                double minZ = double.MaxValue;
                IIfcFace? bottomFace = null;

                foreach (var face in brep.Outer.CfsFaces)
                {
                    var bounds = face.Bounds.FirstOrDefault();
                    if (bounds?.Bound is IIfcPolyLoop loop)
                    {
                        var avgZ = loop.Polygon.Average(p => p.Coordinates.Count > 2 ? p.Coordinates[2] : 0);
                        if (avgZ < minZ)
                        {
                            minZ = avgZ;
                            bottomFace = face;
                        }
                    }
                }

                if (bottomFace != null)
                {
                    var bounds = bottomFace.Bounds.FirstOrDefault();
                    if (bounds?.Bound is IIfcPolyLoop loop)
                    {
                        var points = loop.Polygon
                            .Where(p => p.Coordinates.Count >= 2)
                            .Select(p => ((double)p.Coordinates[0].Value, (double)p.Coordinates[1].Value))
                            .ToList();

                        return CalculatePolygonArea(points);
                    }
                }

                return 0;
            }
            catch
            {
                return 0;
            }
        }

        private static double CalculateBooleanResultArea(IIfcBooleanResult boolean)
        {
            try
            {
                if (boolean.FirstOperand is IIfcExtrudedAreaSolid extrusion)
                {
                    return CalculateProfileArea(extrusion.SweptArea);
                }

                return 0;
            }
            catch
            {
                return 0;
            }
        }

        private static double CalculateCurveSetArea(IIfcGeometricCurveSet curveSet)
        {
            try
            {
                var points = new List<(double x, double y)>();

                foreach (var element in curveSet.Elements)
                {
                    if (element is IIfcPolyline polyline)
                    {
                        foreach (var point in polyline.Points)
                        {
                            var coords = point.Coordinates;
                            if (coords.Count >= 2)
                            {
                                points.Add((coords[0], coords[1]));
                            }
                        }
                    }
                }

                if (points.Count >= 3)
                {
                    return CalculatePolygonArea(points);
                }

                return 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Calculates volume from geometric representation of spaces
        /// </summary>
        public static double CalculateVolumeFromGeometry(IIfcSpace space)
        {
            try
            {
                var area = CalculateAreaFromGeometry(space);
                var height = GetSpaceHeight(space);

                if (area > 0 && height > 0)
                {
                    return area * height;
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   Fel vid volymberäkning för {space.Name}: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Calculates volume from geometric representation of products (walls, slabs, beams, etc.)
        /// </summary>
        public static double CalculateVolumeFromProduct(IIfcProduct product)
        {
            try
            {
                var representation = product.Representation;
                if (representation == null)
                    return 0;

                foreach (var rep in representation.Representations)
                {
                    var repType = rep.RepresentationIdentifier?.ToString()?.ToLower();
                    
                    // Look for body representation
                    if (repType == "body")
                    {
                        foreach (var item in rep.Items)
                        {
                            var volume = CalculateVolumeFromGeometricItem(item);
                            if (volume > 0)
                            {
                                // Convert from mm³ to m³
                                return volume / 1_000_000_000.0;
                            }
                        }
                    }
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   Fel vid volymberäkning för {product.Name}: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Calculates volume from a geometric representation item
        /// </summary>
        private static double CalculateVolumeFromGeometricItem(IIfcRepresentationItem item)
        {
            try
            {
                switch (item)
                {
                    case IIfcExtrudedAreaSolid extrusion:
                        // Volume = Profile Area × Extrusion Depth
                        var profileArea = CalculateProfileArea(extrusion.SweptArea);
                        var depth = extrusion.Depth;
                        return profileArea * depth;

                    case IIfcBooleanResult boolean:
                        // Try to get volume from the first operand
                        if (boolean.FirstOperand is IIfcExtrudedAreaSolid boolExtrusion)
                        {
                            var boolProfileArea = CalculateProfileArea(boolExtrusion.SweptArea);
                            var boolDepth = boolExtrusion.Depth;
                            return boolProfileArea * boolDepth;
                        }
                        return 0;

                    default:
                        return 0;
                }
            }
            catch
            {
                return 0;
            }
        }

        public static double GetSpaceHeight(IIfcSpace space)
        {
            try
            {
                // Method 1: Try to get height from properties first (most reliable)
                var propertyHeight = IfcPropertyHelper.GetHeightFromProperties(space);
                if (propertyHeight.HasValue)
                {
                    return propertyHeight.Value; // Already in meters (IFC quantities are in SI units)
                }

                // Method 2: Try to extract from geometry representation
                var representation = space.Representation;
                if (representation != null)
                {
                    foreach (var rep in representation.Representations)
                    {
                        foreach (var item in rep.Items)
                        {
                            var geometricHeight = ExtractHeightFromGeometricItem(item);
                            if (geometricHeight > 0)
                            {
                                return geometricHeight / 1000.0; // Convert mm to m (geometry is in mm)
                            }
                        }
                    }
                }

                // Method 3: Fallback - try to get from parent storey
                var storey = StoreyFilterHelper.GetParentStorey(space);
                if (storey != null)
                {
                    var storeyHeight = IfcPropertyHelper.GetHeightFromProperties(storey);
                    if (storeyHeight.HasValue)
                    {
                        return storeyHeight.Value; // Already in meters (IFC quantities are in SI units)
                    }
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   Fel vid höjdberäkning för {space.Name}: {ex.Message}");
                return 0;
            }
        }

        private static double ExtractHeightFromGeometricItem(IIfcRepresentationItem item)
        {
            try
            {
                switch (item)
                {
                    case IIfcExtrudedAreaSolid extrusion:
                        // Extrusion depth represents the height
                        return extrusion.Depth;

                    case IIfcFacetedBrep brep:
                        // Calculate height from bounding box (max Z - min Z)
                        return CalculateBrepHeight(brep);

                    default:
                        return 0;
                }
            }
            catch
            {
                return 0;
            }
        }

        private static double CalculateBrepHeight(IIfcFacetedBrep brep)
        {
            try
            {
                double minZ = double.MaxValue;
                double maxZ = double.MinValue;

                foreach (var face in brep.Outer.CfsFaces)
                {
                    var bounds = face.Bounds.FirstOrDefault();
                    if (bounds?.Bound is IIfcPolyLoop loop)
                    {
                        foreach (var point in loop.Polygon)
                        {
                            if (point.Coordinates.Count > 2)
                            {
                                var z = point.Coordinates[2];
                                if (z < minZ) minZ = z;
                                if (z > maxZ) maxZ = z;
                            }
                        }
                    }
                }

                if (minZ != double.MaxValue && maxZ != double.MinValue)
                {
                    return maxZ - minZ;
                }

                return 0;
            }
            catch
            {
                return 0;
            }
        }

        public static List<IIfcProduct> ApplyEntityFilter(List<IIfcProduct> elements, Intent intent, IfcStore model)
        {
            // Normalize "none" to empty string
            var normalizedFilter = intent.EntityFilter?.Trim();
            if (string.Equals(normalizedFilter, "none", StringComparison.OrdinalIgnoreCase))
            {
                normalizedFilter = string.Empty;
            }

            if (string.IsNullOrEmpty(normalizedFilter))
            {
                return elements;
            }

            // Special case: If AI returned IfcSpace with a storey name filter,
            // check if user is actually asking about a storey, not individual spaces
            if (intent.IfcEntity == "IfcSpace")
            {
                // Don't try to match generic terms like "plan", "våning", "storey" as specific storey names
                var genericStoreyTerms = new[] { "plan", "våning", "storey", "floor", "level" };
                var isGenericTerm = genericStoreyTerms.Any(term => 
                    normalizedFilter.Equals(term, StringComparison.OrdinalIgnoreCase));

                if (!isGenericTerm)
                {
                    var matchingStorey = TryFindStoreyByFilter(normalizedFilter, model);
                    if (matchingStorey != null)
                    {
                        // User is asking about a specific storey - switch to storey-based calculation
                        Console.WriteLine($"Tolkar som fråga om våningsplan: '{matchingStorey.Name}'");
                        return new List<IIfcProduct> { matchingStorey };
                    }
                    else
                    {
                        // No matching storey, filter spaces by name as originally intended
                        var filteredElements = FetchByIntentWithFilterString(model, intent.IfcEntity, normalizedFilter);
                        Console.WriteLine($"Efter filtrering: {filteredElements.Count} element med '{normalizedFilter}' i namnet");
                        return filteredElements;
                    }
                }
                else
                {
                    // Generic term detected - get all storeys in the building
                    Console.WriteLine($"Tolkar som fråga om alla våningsplan i byggnaden");
                    var allStoreys = model.Instances.OfType<IIfcBuildingStorey>().Cast<IIfcProduct>().ToList();
                    return allStoreys;
                }
            }
            else
            {
                var filteredElements = FetchByIntentWithFilterString(model, intent.IfcEntity, normalizedFilter);
                Console.WriteLine($"Efter filtrering: {filteredElements.Count} element med '{normalizedFilter}' i namnet");
                return filteredElements;
            }
        }

        /// <summary>
        /// Tries to find a building storey that matches the given filter string
        /// </summary>
        private static IIfcBuildingStorey? TryFindStoreyByFilter(string filter, IfcStore model)
        {
            try
            {
                var allStoreys = model.Instances.OfType<IIfcBuildingStorey>();
                
                // Try exact match first
                var exactMatch = allStoreys.FirstOrDefault(s => 
                    s.Name?.ToString().Equals(filter, StringComparison.OrdinalIgnoreCase) ?? false);
                
                if (exactMatch != null)
                    return exactMatch;
                
                // Try partial match
                var partialMatch = allStoreys.FirstOrDefault(s => 
                    s.Name?.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);
                
                return partialMatch;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Fetches IFC products based on intent with entity filter applied
        /// </summary>
        private static List<IIfcProduct> FetchByIntentWithFilter(IfcStore model, Intent intent)
        {
            var results = new List<IIfcProduct>();
            
            try
            {
                // Get all instances of the requested entity type
                IEnumerable<IIfcProduct> products;
                
                switch (intent.IfcEntity)
                {
                    case "IfcSpace":
                        products = model.Instances.OfType<IIfcSpace>().Cast<IIfcProduct>();
                        break;
                    case "IfcBuildingStorey":
                        products = model.Instances.OfType<IIfcBuildingStorey>().Cast<IIfcProduct>();
                        break;
                    case "IfcWall":
                        products = model.Instances.OfType<IIfcWall>().Cast<IIfcProduct>();
                        break;
                    case "IfcSlab":
                        products = model.Instances.OfType<IIfcSlab>().Cast<IIfcProduct>();
                        break;
                    default:
                        // Try to get all products if entity type is not specifically handled
                        products = model.Instances.OfType<IIfcProduct>();
                        break;
                }
                
                // Apply filter if specified
                if (!string.IsNullOrEmpty(intent.EntityFilter))
                {
                    products = products.Where(p => 
                        p.Name?.ToString().Contains(intent.EntityFilter, StringComparison.OrdinalIgnoreCase) ?? false);
                }
                
                results.AddRange(products);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   Fel vid hämtning av element: {ex.Message}");
            }
            
            return results;
        }

        // Add helper method to avoid creating new Intent objects
        private static List<IIfcProduct> FetchByIntentWithFilterString(IfcStore model, string ifcEntity, string filter)
        {
            var tempIntent = new Intent { IfcEntity = ifcEntity, EntityFilter = filter };
            return FetchByIntentWithFilter(model, tempIntent);
        }
    }
}