using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.Common;

namespace ifcFireAnalyzer;

public class IFCAnalyzer
{
    // Dictionary mapping entity names to their retrieval functions
    private Dictionary<string, Func<IfcStore, IEnumerable<IPersistEntity>>> _entityMap = null!;
    
    // Dictionary mapping entity types to their specific validation functions
    private Dictionary<Type, Action<IPersistEntity, List<Issue>>> _specificChecks = null!;
    
    // Dictionary mapping entity types to relevant issue categories
    private Dictionary<string, List<string>> _relevanceMap = null!;

    public AnalysisResult Analyze(string path, string? targetEntityType = null)
    {
        var result = new AnalysisResult { TargetEntityType = targetEntityType };

        using var model = IfcStore.Open(path);
        
        InitializeEntityMappings(model);

        if (string.IsNullOrEmpty(targetEntityType))
        {
            // General analysis: only issues
            PerformGeneralAnalysis(model, result.Issues);
        }
        else
        {
            // Targeted analysis: issues + confidence indicators
            PerformTargetedAnalysis(model, targetEntityType, result);
        }

        return result;
    }

    private void InitializeEntityMappings(IfcStore model)
    {
        // Entity retrieval mappings
        _entityMap = new Dictionary<string, Func<IfcStore, IEnumerable<IPersistEntity>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["IfcBuildingStorey"] = m => m.Instances.OfType<IIfcBuildingStorey>(),
            ["IfcWall"] = m => m.Instances.OfType<IIfcWall>(),
            ["IfcSlab"] = m => m.Instances.OfType<IIfcSlab>(),
            ["IfcDoor"] = m => m.Instances.OfType<IIfcDoor>(),
            ["IfcWindow"] = m => m.Instances.OfType<IIfcWindow>(),
            ["IfcStair"] = m => m.Instances.OfType<IIfcStair>(),
            ["IfcSpace"] = m => m.Instances.OfType<IIfcSpace>(),
            ["IfcBeam"] = m => m.Instances.OfType<IIfcBeam>(),
            ["IfcColumn"] = m => m.Instances.OfType<IIfcColumn>(),
            ["IfcRoof"] = m => m.Instances.OfType<IIfcRoof>(),
            ["IfcBuildingElementProxy"] = m => m.Instances.OfType<IIfcBuildingElementProxy>(),
            ["IfcCovering"] = m => m.Instances.OfType<IIfcCovering>(),
            ["IfcRailing"] = m => m.Instances.OfType<IIfcRailing>(),
            ["IfcFurnishingElement"] = m => m.Instances.OfType<IIfcFurnishingElement>(),
        };

        // Entity-specific validation mappings
        _specificChecks = new Dictionary<Type, Action<IPersistEntity, List<Issue>>>
        {
            [typeof(IIfcBuildingStorey)] = (entity, issues) => CheckStoreySpecific((IIfcBuildingStorey)entity, issues),
            [typeof(IIfcWall)] = (entity, issues) => CheckWallSpecific((IIfcWall)entity, issues),
            [typeof(IIfcDoor)] = (entity, issues) => CheckDoorSpecific((IIfcDoor)entity, issues),
            [typeof(IIfcWindow)] = (entity, issues) => CheckWindowSpecific((IIfcWindow)entity, issues),
            [typeof(IIfcSpace)] = (entity, issues) => CheckSpaceSpecific((IIfcSpace)entity, issues),
            [typeof(IIfcSlab)] = (entity, issues) => CheckSlabSpecific((IIfcSlab)entity, issues),
        };

        // Relevance filtering: maps entity types to their relevant issue categories
        _relevanceMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["IfcBuildingStorey"] = new() { "Structure", "Naming", "Properties", "Geometry" }, 
            ["IfcWall"] = new() { "Structure", "Geometry", "Properties", "Naming" },
            ["IfcSlab"] = new() { "Structure", "Geometry", "Properties", "Naming" },
            ["IfcDoor"] = new() { "Structure", "Geometry", "Properties", "Naming" },
            ["IfcWindow"] = new() { "Structure", "Geometry", "Properties", "Naming" },
            ["IfcStair"] = new() { "Semantic", "Geometry", "Structure", "Properties" },
            ["IfcSpace"] = new() { "Properties", "Structure", "Geometry", "Naming" },
            ["IfcBeam"] = new() { "Structure", "Geometry", "Properties", "Naming" },
            ["IfcColumn"] = new() { "Structure", "Geometry", "Properties", "Naming" },
            ["IfcRoof"] = new() { "Structure", "Geometry", "Properties", "Naming" },
            ["IfcBuildingElementProxy"] = new() { "Semantic", "Structure", "Geometry" },
            ["IfcCovering"] = new() { "Structure", "Geometry", "Properties", "Naming" },
            ["IfcRailing"] = new() { "Structure", "Geometry", "Properties" },
            ["IfcFurnishingElement"] = new() { "Semantic", "Properties", "Naming" },
        };
    }

    private void PerformGeneralAnalysis(IfcStore model, List<Issue> issues)
    {
        try
        {
            var storeys = model.Instances.OfType<IIfcBuildingStorey>().ToList();
            var stairs = model.Instances.OfType<IIfcStair>().ToList();
            var spaces = model.Instances.OfType<IIfcSpace>().ToList();
            var proxies = model.Instances.OfType<IIfcBuildingElementProxy>().ToList();

            // Basic checks
            if (!storeys.Any())
                issues.Add(new Issue("ERROR", "No IfcBuildingStorey entities found", "Structure"));

            if (!stairs.Any())
                issues.Add(new Issue("WARNING", "No IfcStair entities detected", "Structure"));

            if (proxies.Count > 5)
                issues.Add(new Issue("WARNING", $"High number of proxy elements ({proxies.Count})", "Semantic"));

            CheckNamingConsistency(storeys, issues);

            // Extra analyses - each wrapped in try-catch
            SafeExecute(() => CheckMissingPropertySets(storeys, issues), issues, "property set check");
            SafeExecute(() => CheckStoreyHeightIssues(storeys, issues), issues, "storey height check");
            SafeExecute(() => CheckGeometryPresence(model, issues), issues, "geometry check");
        }
        catch (Exception ex)
        {
            issues.Add(new Issue(
                "ERROR",
                $"General analysis failed: {ex.Message}",
                "Analysis"
            ));
        }
    }

    private void SafeExecute(Action action, List<Issue> issues, string operationName)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            issues.Add(new Issue(
                "WARNING",
                $"Failed during {operationName}: {ex.Message}",
                "Analysis"
            ));
        }
    }

    private void PerformTargetedAnalysis(IfcStore model, string targetEntityType, AnalysisResult result)
    {
        var normalizedName = NormalizeEntityTypeName(targetEntityType);

        if (!_entityMap.TryGetValue(normalizedName, out var getter))
        {
            result.Issues.Add(new Issue("ERROR", $"Unsupported entity type: {targetEntityType}. Use one of: {string.Join(", ", _entityMap.Keys)}", "Analysis"));
            return;
        }

        var targetEntities = getter(model).ToList();
        result.EntityCount = targetEntities.Count;

        if (!targetEntities.Any())
        {
            result.Issues.Add(new Issue("ERROR", $"No {normalizedName} entities found in the model", "Structure"));
            return;
        }

        result.Issues.Add(new Issue("INFO", $"Found {targetEntities.Count} {normalizedName} entities", "Structure"));

        // Collect confidence indicators
        CollectConfidenceIndicators(targetEntities, normalizedName, result.ConfidenceIndicators);

        // Special collection-level checks for building storeys
        if (normalizedName.Equals("IfcBuildingStorey", StringComparison.OrdinalIgnoreCase))
        {
            var storeys = targetEntities.OfType<IIfcBuildingStorey>().ToList();
            CheckNamingConsistency(storeys, result.Issues);
        }

        // Add semantic checks for the model (check proxy usage)
        CheckSemanticIssues(model, normalizedName, result.Issues);

        // Perform generic and entity-specific checks (validation issues)
        var tempIssues = new List<Issue>();
        foreach (var entity in targetEntities)
        {
            // Generic checks
            CheckEntityGeometry(entity, tempIssues);
            CheckEntityProperties(entity, tempIssues);
            CheckEntityNaming(entity, tempIssues);
            CheckEntityClassification(entity, tempIssues);

            // Entity-specific validations using the mapping
            var entityType = entity.GetType().GetInterfaces()
                .FirstOrDefault(i => i.Name.StartsWith("IIfc") && _specificChecks.ContainsKey(i));

            if (entityType != null && _specificChecks.TryGetValue(entityType, out var specificCheck))
            {
                specificCheck(entity, tempIssues);
            }
        }

        var filteredIssues = ApplyRelevanceFiltering(tempIssues, normalizedName);
        result.Issues.AddRange(filteredIssues);
    }

    private List<Issue> ApplyRelevanceFiltering(List<Issue> issues, string entityType)
    {
        // If no relevance map exists for this entity type, return all issues
        if (!_relevanceMap.TryGetValue(entityType, out var relevantCategories))
        {
            return issues;
        }

        // Filter issues by relevant categories, but always include INFO and ERROR severity
        // and issues without categories (like Analysis category)
        return issues
            .Where(i => 
                i.Severity == "INFO" || 
                i.Severity == "ERROR" ||
                i.Category == "Analysis" ||
                relevantCategories.Contains(i.Category))
            .ToList();
    }

    private string NormalizeEntityTypeName(string entityType)
    {
        // Remove "IIfc" or "I" prefix if present, keep "Ifc"
        if (entityType.StartsWith("IIfc", StringComparison.OrdinalIgnoreCase))
            return entityType[1..]; // Remove first 'I'
        
        if (entityType.StartsWith("Ifc", StringComparison.OrdinalIgnoreCase))
            return entityType;
        
        return "Ifc" + entityType;
    }

    // Generic validation methods
    private void CheckEntityGeometry(IPersistEntity entity, List<Issue> issues)
    {
        if (entity is IIfcProduct product)
        {
            if (product.Representation == null)
            {
                issues.Add(new Issue(
                    "WARNING",
                    $"{GetEntityTypeName(entity)} '{GetEntityName(product)}' has no geometric representation",
                    "Geometry",
                    GetEntityGlobalId(product)
                ));
            }
        }
    }

    private void CheckEntityProperties(IPersistEntity entity, List<Issue> issues)
    {
        if (entity is IIfcObject obj)
        {
            var propertySets = obj.IsDefinedBy
                .OfType<IIfcRelDefinesByProperties>()
                .Select(r => r.RelatingPropertyDefinition)
                .OfType<IIfcPropertySet>()
                .ToList();

            if (!propertySets.Any())
            {
                issues.Add(new Issue(
                    "WARNING",
                    $"{GetEntityTypeName(entity)} '{GetEntityName(obj)}' has no property sets",
                    "Properties",
                    GetEntityGlobalId(obj)
                ));
            }
        }
    }

    private void CheckEntityNaming(IPersistEntity entity, List<Issue> issues)
    {
        if (entity is IIfcRoot root)
        {
            if (string.IsNullOrWhiteSpace(root.Name?.ToString()))
            {
                issues.Add(new Issue(
                    "WARNING",
                    $"{GetEntityTypeName(entity)} with GlobalId '{root.GlobalId}' has no name",
                    "Naming",
                    root.GlobalId.ToString()
                ));
            }
        }
    }

    private void CheckEntityClassification(IPersistEntity entity, List<Issue> issues)
    {
        if (entity is IIfcObject obj)
        {
            var classifications = obj.HasAssociations
                .OfType<IIfcRelAssociatesClassification>()
                .ToList();

            if (!classifications.Any())
            {
                issues.Add(new Issue(
                    "INFO",
                    $"{GetEntityTypeName(entity)} '{GetEntityName(obj)}' has no classification",
                    "Properties",
                    GetEntityGlobalId(obj)
                ));
            }
        }
    }

    // Entity-specific validation methods
    private void CheckStoreySpecific(IIfcBuildingStorey storey, List<Issue> issues)
    {
        if (!storey.Elevation.HasValue)
        {
            issues.Add(new Issue(
                "ERROR",
                $"Building storey '{storey.Name}' is missing elevation value",
                "Structure",
                storey.GlobalId.ToString()
            ));
        }
    }

    private void CheckWallSpecific(IIfcWall wall, List<Issue> issues)
    {
        // Check if wall has type
        if (wall.IsTypedBy == null || !wall.IsTypedBy.Any())
        {
            issues.Add(new Issue(
                "WARNING",
                $"Wall '{wall.Name}' has no type definition",
                "Structure",
                wall.GlobalId.ToString()
            ));
        }
    }

    private void CheckDoorSpecific(IIfcDoor door, List<Issue> issues)
    {
        // Check if door fills an opening
        if (door.FillsVoids == null || !door.FillsVoids.Any())
        {
            issues.Add(new Issue(
                "WARNING",
                $"Door '{door.Name}' is not associated with an opening",
                "Structure",
                door.GlobalId.ToString()
            ));
        }
    }

    private void CheckWindowSpecific(IIfcWindow window, List<Issue> issues)
    {
        if (window.FillsVoids == null || !window.FillsVoids.Any())
        {
            issues.Add(new Issue(
                "WARNING",
                $"Window '{window.Name}' is not associated with an opening",
                "Structure",
                window.GlobalId.ToString()
            ));
        }
    }

    private void CheckSpaceSpecific(IIfcSpace space, List<Issue> issues)
    {
        var boundaries = space.BoundedBy?.ToList();
        if (boundaries == null || !boundaries.Any())
        {
            issues.Add(new Issue(
                "WARNING",
                $"Space '{space.Name}' has no space boundaries defined",
                "Structure",
                space.GlobalId.ToString()
            ));
        }
    }

    private void CheckSlabSpecific(IIfcSlab slab, List<Issue> issues)
    {
        if (slab.IsTypedBy == null || !slab.IsTypedBy.Any())
        {
            issues.Add(new Issue(
                "WARNING",
                $"Slab '{slab.Name}' has no type definition",
                "Structure",
                slab.GlobalId.ToString()
            ));
        }
    }

    // Helper methods
    private string GetEntityTypeName(IPersistEntity entity)
    {
        var interfaceName = entity.GetType().GetInterfaces()
            .FirstOrDefault(i => i.Name.StartsWith("IIfc"))?.Name;
        
        return interfaceName?[1..] ?? entity.GetType().Name; // Remove leading 'I'
    }

    private string GetEntityName(IIfcRoot entity)
    {
        return entity.Name?.ToString() ?? "[Unnamed]";
    }

    private string GetEntityGlobalId(IIfcRoot entity)
    {
        return entity.GlobalId.ToString();
    }

    // General analysis methods
    private void CheckNamingConsistency(List<IIfcBuildingStorey> storeys, List<Issue> issues)
    {
        var patterns = storeys
            .Select(s => s.Name?.ToString())
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(name => ExtractNamingPattern(name!))
            .Distinct()
            .ToList();

        if (patterns.Count > 3)
        {
            issues.Add(new Issue(
                "WARNING",
                $"High variability in storey naming conventions ({patterns.Count} different patterns detected)",
                "Naming"
            ));
        }
    }

    private string ExtractNamingPattern(string name)
    {
        // Remove numbers and trim to get the base pattern
        // "Plan 1" -> "Plan"
        // "Floor 2" -> "Floor"
        // "Level-03" -> "Level-"
        return System.Text.RegularExpressions.Regex.Replace(name, @"\d+", "#").Trim();
    }

    private void CheckMissingPropertySets(List<IIfcBuildingStorey> storeys, List<Issue> issues)
    {
        foreach (var storey in storeys)
        {
            var propertySets = storey.IsDefinedBy
                .OfType<IIfcRelDefinesByProperties>()
                .Select(r => r.RelatingPropertyDefinition)
                .OfType<IIfcPropertySet>()
                .ToList();

            var hasCommonPset = propertySets.Any(ps => 
                ps.Name?.ToString().Contains("Pset_BuildingStoreyCommon") == true);

            if (!hasCommonPset)
            {
                issues.Add(new Issue(
                    "WARNING",
                    $"Storey '{storey.Name}' is missing Pset_BuildingStoreyCommon",
                    "Properties",
                    storey.GlobalId.ToString()
                ));
            }
        }
    }

    private void CheckStoreyHeightIssues(List<IIfcBuildingStorey> storeys, List<Issue> issues)
    {
        // Filter storeys with valid elevation values
        var validStoreys = storeys
            .Where(s => s.Elevation.HasValue && 
                        !double.IsNaN(s.Elevation.Value) && 
                        !double.IsInfinity(s.Elevation.Value))
            .ToList();

        if (validStoreys.Count == 0)
        {
            if (storeys.Any())
            {
                issues.Add(new Issue(
                    "ERROR",
                    $"All {storeys.Count} storey(s) have missing or invalid elevation values",
                    "Structure"
                ));
            }
            return;
        }

        // Sort manually with safe comparison
        validStoreys.Sort((a, b) => 
        {
            try
            {
                var aVal = (double)a.Elevation!.Value;
                var bVal = (double)b.Elevation!.Value;
                
                // Extra validation
                if (double.IsNaN(aVal) || double.IsInfinity(aVal)) return 1;
                if (double.IsNaN(bVal) || double.IsInfinity(bVal)) return -1;
                
                return aVal.CompareTo(bVal);
            }
            catch
            {
                return 0; // Treat as equal if comparison fails
            }
        });

        if (validStoreys.Count != storeys.Count)
        {
            var missing = storeys.Count - validStoreys.Count;
            issues.Add(new Issue(
                "ERROR",
                $"{missing} storey(s) missing or have invalid elevation values",
                "Structure"
            ));
        }

        // Check for unusual height differences
        for (int i = 1; i < validStoreys.Count; i++)
        {
            try
            {
                var heightDiff = (double)validStoreys[i].Elevation!.Value - 
                               (double)validStoreys[i - 1].Elevation!.Value;

                if (heightDiff < 2000 || heightDiff > 6000) // Assuming mm units
                {
                    issues.Add(new Issue(
                        "WARNING",
                        $"Unusual storey height ({heightDiff:F0}mm) between '{validStoreys[i - 1].Name}' and '{validStoreys[i].Name}'",
                        "Structure",
                        validStoreys[i].GlobalId.ToString()
                    ));
                }
            }
            catch (Exception ex)
            {
                // Skip this comparison if it fails
                issues.Add(new Issue(
                    "WARNING",
                    $"Could not calculate height difference between storeys: {ex.Message}",
                    "Structure"
                ));
            }
        }
    }

    private void CheckGeometryPresence(IfcStore model, List<Issue> issues)
    {
        try
        {
            var buildingElements = model.Instances
                .OfType<IIfcBuildingElement>()
                .ToList();

            var elementsWithoutGeometry = buildingElements
                .Where(e => e.Representation == null)
                .ToList();

            if (elementsWithoutGeometry.Any())
            {
                var percentage = (elementsWithoutGeometry.Count * 100.0) / buildingElements.Count;
                issues.Add(new Issue(
                    "WARNING",
                    $"{elementsWithoutGeometry.Count} building elements ({percentage:F1}%) have no geometric representation",
                    "Geometry"
                ));

                // Report specific element types missing geometry - with error handling
                try
                {
                    var allTypesWithoutGeometry = elementsWithoutGeometry
                        .GroupBy(e => e.GetType().Name ?? "Unknown")
                        .Where(g => g.Key != null)  // Extra safety
                        .OrderByDescending(g => g.Count())
                        .ToList();  // Materialize before iterating

                    var topTypes = allTypesWithoutGeometry.Take(3).ToList();
                    
                    foreach (var group in topTypes)
                    { 
                        issues.Add(new Issue(
                            "INFO",
                            $"  - {group.Count()} {group.Key} elements without geometry",
                            "Geometry"
                        ));
                    }

                    // Add summary for remaining types
                    if (allTypesWithoutGeometry.Count > 3)
                    {
                        var remainingCount = allTypesWithoutGeometry.Count - 3;
                        var remainingElements = allTypesWithoutGeometry.Skip(3).Sum(g => g.Count());
                        issues.Add(new Issue(
                            "INFO",
                            $"  - ...and {remainingCount} other type(s) with {remainingElements} element(s) without geometry",
                            "Geometry"
                        ));
                    }
                }
                catch (Exception ex)
                {
                    // Don't fail the entire analysis if grouping fails
                    issues.Add(new Issue(
                        "WARNING",
                        $"Could not categorize elements without geometry: {ex.Message}",
                        "Geometry"
                    ));
                }
            }
        }
        catch (Exception ex)
        {
            issues.Add(new Issue(
                "ERROR",
                $"Failed to check geometry presence: {ex.Message}",
                "Analysis"
            ));
        }
    }

    private void CollectConfidenceIndicators(List<IPersistEntity> entities, string entityType, List<ConfidenceIndicator> indicators)
    {
        // Data completeness indicators
        if (entities.OfType<IIfcProduct>().Any())
        {
            var products = entities.OfType<IIfcProduct>().ToList();
            var withoutGeometry = products.Count(p => p.Representation == null);
            
            if (withoutGeometry > 0)
            {
                indicators.Add(new ConfidenceIndicator(
                    "Geometry Completeness",
                    $"{withoutGeometry} of {products.Count} entities lack geometric representation",
                    new() { ["missing_geometry"] = withoutGeometry, ["total"] = products.Count }
                ));
            }
        }

        // Property completeness
        if (entities.OfType<IIfcObject>().Any())
        {
            var objects = entities.OfType<IIfcObject>().ToList();
            var withoutProperties = objects.Count(o => !o.IsDefinedBy
                .OfType<IIfcRelDefinesByProperties>()
                .Any());
            
            if (withoutProperties > 0)
            {
                indicators.Add(new ConfidenceIndicator(
                    "Property Completeness",
                    $"{withoutProperties} of {objects.Count} entities have no property sets",
                    new() { ["missing_properties"] = withoutProperties, ["total"] = objects.Count }
                ));
            }
        }

        // Naming consistency
        if (entities.OfType<IIfcRoot>().Any())
        {
            var roots = entities.OfType<IIfcRoot>().ToList();
            var unnamed = roots.Count(r => string.IsNullOrWhiteSpace(r.Name?.ToString()));
            
            if (unnamed > 0)
            {
                indicators.Add(new ConfidenceIndicator(
                    "Naming Completeness",
                    $"{unnamed} of {roots.Count} entities are unnamed",
                    new() { ["unnamed"] = unnamed, ["total"] = roots.Count }
                ));
            }
        }

        // Entity-specific indicators
        if (entityType.Equals("IfcBuildingStorey", StringComparison.OrdinalIgnoreCase))
        {
            CollectStoreyConfidenceIndicators(entities.OfType<IIfcBuildingStorey>().ToList(), indicators);
        }
        else if (entityType.Equals("IfcDoor", StringComparison.OrdinalIgnoreCase))
        {
            CollectDoorConfidenceIndicators(entities.OfType<IIfcDoor>().ToList(), indicators);
        }
        else if (entityType.Equals("IfcWindow", StringComparison.OrdinalIgnoreCase))
        {
            CollectWindowConfidenceIndicators(entities.OfType<IIfcWindow>().ToList(), indicators);
        }
        else if (entityType.Equals("IfcSpace", StringComparison.OrdinalIgnoreCase))
        {
            CollectSpaceConfidenceIndicators(entities.OfType<IIfcSpace>().ToList(), indicators);
        }
    }

    private void CollectStoreyConfidenceIndicators(List<IIfcBuildingStorey> storeys, List<ConfidenceIndicator> indicators)
    {
        var withoutElevation = storeys.Count(s => !s.Elevation.HasValue);
        if (withoutElevation > 0)
        {
            indicators.Add(new ConfidenceIndicator(
                "Elevation Data",
                $"{withoutElevation} storeys excluded due to missing elevation values",
                new() { ["excluded"] = withoutElevation, ["total"] = storeys.Count }
            ));
        }

        var patterns = storeys
            .Select(s => s.Name?.ToString())
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(name => ExtractNamingPattern(name!))
            .Distinct()
            .Count();
        
        if (patterns > 3)
        {
            indicators.Add(new ConfidenceIndicator(
                "Naming Variability",
                $"{patterns} different naming patterns detected across storeys",
                new() { ["pattern_count"] = patterns }
            ));
        }
    }

    private void CollectDoorConfidenceIndicators(List<IIfcDoor> doors, List<ConfidenceIndicator> indicators)
    {
        var withoutOpenings = doors.Count(d => d.FillsVoids == null || !d.FillsVoids.Any());
        if (withoutOpenings > 0)
        {
            indicators.Add(new ConfidenceIndicator(
                "Opening Associations",
                $"{withoutOpenings} doors not associated with wall openings",
                new() { ["unassociated"] = withoutOpenings, ["total"] = doors.Count }
            ));
        }
    }

    private void CollectWindowConfidenceIndicators(List<IIfcWindow> windows, List<ConfidenceIndicator> indicators)
    {
        var withoutOpenings = windows.Count(w => w.FillsVoids == null || !w.FillsVoids.Any());
        if (withoutOpenings > 0)
        {
            indicators.Add(new ConfidenceIndicator(
                "Opening Associations",
                $"{withoutOpenings} windows not associated with wall openings",
                new() { ["unassociated"] = withoutOpenings, ["total"] = windows.Count }
            ));
        }
    }

    private void CollectSpaceConfidenceIndicators(List<IIfcSpace> spaces, List<ConfidenceIndicator> indicators)
    {
        var withoutBoundaries = spaces.Count(s => s.BoundedBy == null || !s.BoundedBy.Any());
        if (withoutBoundaries > 0)
        {
            indicators.Add(new ConfidenceIndicator(
                "Space Boundaries",
                $"{withoutBoundaries} spaces have no boundary definitions",
                new() { ["missing_boundaries"] = withoutBoundaries, ["total"] = spaces.Count }
            ));
        }
    }

    private void CheckSemanticIssues(IfcStore model, string targetEntityType, List<Issue> issues)
    {
        try
        {
            // Check for excessive proxy element usage (semantic issue)
            var proxies = model.Instances.OfType<IIfcBuildingElementProxy>().ToList();
            
            if (proxies.Count > 5)
            {
                issues.Add(new Issue(
                    "WARNING",
                    $"High number of proxy elements ({proxies.Count}) detected - may indicate incorrect entity classification",
                    "Semantic"
                ));
            }

            // Check if target entity should exist but doesn't (e.g., stairs in multi-storey building)
            if (targetEntityType.Equals("IfcStair", StringComparison.OrdinalIgnoreCase))
            {
                var storeys = model.Instances.OfType<IIfcBuildingStorey>().ToList();
                var stairs = model.Instances.OfType<IIfcStair>().ToList();
                
                if (storeys.Count > 1 && !stairs.Any())
                {
                    issues.Add(new Issue(
                        "WARNING",
                        $"Multi-storey building ({storeys.Count} storeys) has no IfcStair elements - stairs may be misclassified",
                        "Semantic"
                    ));
                }
            }
        }
        catch (Exception ex)
        {
            issues.Add(new Issue(
                "WARNING",
                $"Failed during semantic check: {ex.Message}",
                "Analysis"
            ));
        }
    }
}