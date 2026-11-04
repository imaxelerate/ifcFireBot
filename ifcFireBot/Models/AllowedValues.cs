#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xbim.Ifc4.Interfaces;

namespace ifcFireBot.Models
{
    public static class AllowedValues
    {
        public static readonly HashSet<string> IfcEntities = new(StringComparer.OrdinalIgnoreCase)
        {
            "IfcZone",
            "IfcBuilding",
            "IfcSite",
            "IfcProject",
            "IfcBuildingStorey",
            "IfcSpace",
            "IfcWall",
            "IfcCurtainWall",
            "IfcDoor",
            "IfcWindow",
            "IfcSlab",
            "IfcRoof",
            "IfcCovering",
            "IfcRailing",
            "IfcStair",
            "IfcStairFlight",
            "IfcRamp",
            "IfcRampFlight",
            "IfcShadingDevice",
            "IfcChimney",
            "IfcOpeningElement",
            "IfcBuildingElementProxy",
            "IfcVirtualElement",
            "IfcGrid",
            "IfcAnnotation",
            "IfcFurniture",
            "IfcFurnishingElement",
            "IfcSystemFurnitureElement",
            "IfcTransportElement",
            "IfcMember",
            "IfcColumn",
            "unknown"
        };


        public static readonly HashSet<string> QuestionTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Count",
            "List",
            "Property",
            "Volume",
            "Area",
            "Height",
            "Width",
            "Length",
            "Location",
            "Material",
            "Filter",
            "Exists",
            "unknown",
            "BuildingHeight",
            "UndergroundFloors"
        };

        public static string GetClosestMatch(string value, HashSet<string> allowedValues)
        {
            // Exact match (case-insensitive)
            var exactMatch = allowedValues.FirstOrDefault(v => 
                v.Equals(value, StringComparison.OrdinalIgnoreCase));
            
            if (exactMatch != null)
                return exactMatch;

            // Partial match
            var partialMatch = allowedValues.FirstOrDefault(v => 
                v.Contains(value, StringComparison.OrdinalIgnoreCase) ||
                value.Contains(v, StringComparison.OrdinalIgnoreCase));
            
            return partialMatch ?? "unknown";
        }
        
        /// <summary>
        /// Maps common Swedish building terms to IFC entity types
        /// </summary>
        public static readonly Dictionary<string, string> SwedishTerms = new(StringComparer.OrdinalIgnoreCase)
        {
            // Spaces and rooms
            { "rum", "IfcSpace" },
            { "utrymme", "IfcSpace" },
            { "kök", "IfcSpace" },
            { "sovrum", "IfcSpace" },
            { "badrum", "IfcSpace" },
            
            // Building storeys
            { "våning", "IfcBuildingStorey" },
            { "plan", "IfcBuildingStorey" },
            { "entréplan", "IfcBuildingStorey" },
            { "vindplan", "IfcBuildingStorey" },
            { "vind", "IfcBuildingStorey" },
            
            // Common patterns
            { "plan 1", "IfcBuildingStorey" },
            { "plan 2", "IfcBuildingStorey" },
            { "våning 1", "IfcBuildingStorey" },
            { "våning 2", "IfcBuildingStorey" }
        };
        
        /// <summary>
        /// Maps common building terms (Swedish + English) to IFC entity types
        /// </summary>
        public static readonly Dictionary<string, string> SwedishToIfcMapping = new(StringComparer.OrdinalIgnoreCase)
        {
            // SWEDISH TERMS
            { "vägg", "IfcWall" },
            { "väggar", "IfcWall" },
            { "dörr", "IfcDoor" },
            { "dörrar", "IfcDoor" },
            { "fönster", "IfcWindow" },
            { "golv", "IfcSlab" },
            { "bjälklag", "IfcSlab" },
            { "tak", "IfcRoof" },
            { "rum", "IfcSpace" },
            { "utrymme", "IfcSpace" },
            { "vindplan", "IfcBuildingStorey" },
            { "vind", "IfcBuildingStorey" },
            { "våning", "IfcBuildingStorey" },
            { "plan", "IfcBuildingStorey" },
            { "trappa", "IfcStair" },
            { "balk", "IfcMember" },
            { "pelare", "IfcColumn" },
            { "balkong", "IfcSpace" },
            { "altan", "IfcSpace" },
            { "terrass", "IfcSpace" },
            { "uteplats", "IfcSpace" },
            { "hiss", "IfcTransportElement" },
            { "möbel", "IfcFurniture" },
            { "möbler", "IfcFurniture" },
            { "räcke", "IfcRailing" },
            { "skydd", "IfcShadingDevice" },
            { "källare", "IfcBuildingStorey" },
            { "entréplan", "IfcBuildingStorey" },
            { "våningsplan", "IfcBuildingStorey" },
            
            // ENGLISH TERMS
            { "wall", "IfcWall" },
            { "walls", "IfcWall" },
            { "door", "IfcDoor" },
            { "doors", "IfcDoor" },
            { "window", "IfcWindow" },
            { "windows", "IfcWindow" },
            { "floor", "IfcSlab" },
            { "slab", "IfcSlab" },
            { "slabs", "IfcSlab" },
            { "roof", "IfcRoof" },
            { "room", "IfcSpace" },
            { "rooms", "IfcSpace" },
            { "space", "IfcSpace" },
            { "spaces", "IfcSpace" },
            { "attic", "IfcBuildingStorey" },
            { "storey", "IfcBuildingStorey" },
            { "storeys", "IfcBuildingStorey" },
            { "floors", "IfcBuildingStorey" },
            { "level", "IfcBuildingStorey" },
            { "stair", "IfcStair" },
            { "stairs", "IfcStair" },
            { "beam", "IfcMember" },
            { "column", "IfcColumn" },
            { "balcony", "IfcSpace" },
            { "terrace", "IfcSpace" },
            { "patio", "IfcSpace" },
            { "elevator", "IfcTransportElement" },
            { "lift", "IfcTransportElement" },
            { "furniture", "IfcFurniture" },
            { "railing", "IfcRailing" },
            { "shading", "IfcShadingDevice" },
            { "basement", "IfcBuildingStorey" },
            { "entrance", "IfcBuildingStorey" }
        };
        
        /// <summary>
        /// Question type keywords (Swedish + English) for parsing user questions
        /// </summary>
        public static readonly Dictionary<string, string> QuestionTypeKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            // SWEDISH - Count questions
            { "hur många", "Count" },
            { "antal", "Count" },
            { "räkna", "Count" },
            
            // ENGLISH - Count questions
            { "how many", "Count" },
            { "number of", "Count" },
            { "count", "Count" },
            
            // SWEDISH - List questions
            { "lista", "List" },
            { "visa", "List" },
            { "vilka", "List" },
            
            // ENGLISH - List questions
            { "list", "List" },
            { "show", "List" },
            { "which", "List" },
            { "what are", "List" },
            
            // SWEDISH - Area questions
            { "area", "Area" },
            { "yta", "Area" },
            { "stor", "Area" },
            { "storlek", "Area" },
            { "surface", "Area" },
            { "size", "Area" },
            { "how big", "Area" },
            
            // SWEDISH - Volume questions
            { "volym", "Volume" },
            
            // ENGLISH - Volume questions
            { "volume", "Volume" },
            
            // SWEDISH - Height questions
            { "höjd", "Height" },
            { "hög", "Height" },
            { "takhöjd", "Height" },
            { "rumshöjd", "Height" },
            { "maximal höjd", "Height" },
            { "max höjd", "Height" },
            { "minimal höjd", "Height" },
            { "min höjd", "Height" },
            { "takfri höjd", "Height" },
            
            // ENGLISH - Height questions
            { "height", "Height" },
            { "ceiling height", "Height" },
            { "clear height", "Height" },
            { "maximum height", "Height" },
            { "room height", "Height" },
            { "how high", "Height" },
            
            // Material questions (removed duplicate "material")
            { "material", "Material" },
            { "gjord av", "Material" },
            { "made of", "Material" },
            
            // Property questions
            { "egenskaper", "Property" },
            { "properties", "Property" },
            { "property", "Property" },
            
            // Location questions
            { "var", "Location" },
            { "var finns", "Location" },
            { "plats", "Location" },
            { "placering", "Location" },
            
            // ENGLISH - Location questions
            { "where", "Location" },
            { "location", "Location" },
            { "placed", "Location" },
            
            // SWEDISH - Exists questions
            { "finns det", "Exists" },
            { "finns", "Exists" },
            { "har", "Exists" },
            { "innehåller", "Exists" },
            
            // ENGLISH - Exists questions
            { "does", "Exists" },
            { "is there", "Exists" },
            { "have", "Exists" },
            { "has", "Exists" },
            { "contains", "Exists" },
            { "exist", "Exists" },

            // SWEDISH - Building height questions
            { "byggnadshöjd", "BuildingHeight" },
            { "höjd från mark", "BuildingHeight" },
            { "höjd från marknivå", "BuildingHeight" },
            { "höjd från bottenplatta", "BuildingHeight" },
            { "första våningen höjd", "BuildingHeight" },
            { "markhöjd", "BuildingHeight" },
            
            // ENGLISH - Building height questions
            { "building height", "BuildingHeight" },
            { "height from ground", "BuildingHeight" },
            { "height from grade", "BuildingHeight" },
            { "height from base", "BuildingHeight" },
            { "first floor height", "BuildingHeight" },
            { "ground height", "BuildingHeight" },

            // SWEDISH - Underground floors questions
            { "plan under mark", "UndergroundFloors" },
            { "antal plan under mark", "UndergroundFloors" },
            { "källarplan", "UndergroundFloors" },
            { "antal källarplan", "UndergroundFloors" },
            { "källare", "UndergroundFloors" },
            { "under marknivå", "UndergroundFloors" },
            { "under mark", "UndergroundFloors" },
            
            // ENGLISH - Underground floors questions
            { "basement floors", "UndergroundFloors" },
            { "underground floors", "UndergroundFloors" },
            { "floors below ground", "UndergroundFloors" },
            { "below ground", "UndergroundFloors" },
            { "basement", "UndergroundFloors" },
            { "underground", "UndergroundFloors" }
        };

        /// <summary>
        /// Analyzes the question and suggests likely IFC entity type
        /// </summary>
        public static string GetIfcEntityHint(string question)
        {
            var lowerQuestion = question.ToLower();
            
            foreach (var mapping in SwedishToIfcMapping)
            {
                if (lowerQuestion.Contains(mapping.Key))
                {
                    return mapping.Value;
                }
            }
            
            return "unknown";
        }

        /// <summary>
        /// Analyzes the question and suggests likely question type with priority rules
        /// </summary>
        public static string GetQuestionTypeHint(string question)
        {
            var lowerQuestion = question.ToLower();
            
            // Priority 1: UndergroundFloors-specific patterns (MUST check FIRST)
            var undergroundFloorPatterns = new[] 
            {
                "plan under mark", "antal plan under mark", "källarplan", 
                "antal källarplan", "under marknivå", "basement floors",
                "underground floors", "floors below ground", "below ground"
            };
            
            foreach (var pattern in undergroundFloorPatterns)
            {
                if (lowerQuestion.Contains(pattern))
                {
                    return "UndergroundFloors";
                }
            }
            
            // Priority 2: BuildingHeight-specific patterns (MUST check BEFORE general height)
            var buildingHeightPatterns = new[] 
            {
                "byggnadshöjd", "höjd från mark", "höjd från marknivå", 
                "höjd från bottenplatta", "markhöjd", "höjd över mark",
                "building height from ground", "height from ground", "height from grade",
                "building height"
            };
            
            foreach (var pattern in buildingHeightPatterns)
            {
                if (lowerQuestion.Contains(pattern))
                {
                    return "BuildingHeight";
                }
            }
            
            // Priority 3: Height-specific patterns (storey/space heights)
            var heightPatterns = new[] 
            {
                "maximal höjd", "max höjd", "minimal höjd", "min höjd",
                "takhöjd", "rumshöjd", "hur hög", "höjd på", "ceiling height",
                "clear height", "maximum height", "våningshöjd", "room height",
                "how high"
            };
            
            foreach (var pattern in heightPatterns)
            {
                if (lowerQuestion.Contains(pattern))
                {
                    return "Height";
                }
            }
            
            // Priority 4: Check other specific keywords
            foreach (var keyword in QuestionTypeKeywords)
            {
                if (lowerQuestion.Contains(keyword.Key))
                {
                    return keyword.Value;
                }
            }
            
            return "unknown";
        }

        static void DebugStoreyProperties(IIfcBuildingStorey storey)
        {
            Console.WriteLine($"\n Debug: {storey.Name} properties:");
            
            foreach (var relDefines in storey.IsDefinedBy)
            {
                if (relDefines.RelatingPropertyDefinition is IIfcPropertySet pset)
                {
                    Console.WriteLine($"  PropertySet: {pset.Name}");
                    foreach (var prop in pset.HasProperties.OfType<IIfcPropertySingleValue>())
                    {
                        Console.WriteLine($"    - {prop.Name}: {prop.NominalValue?.Value}");
                    }
                }
                
                if (relDefines.RelatingPropertyDefinition is IIfcElementQuantity qset)
                {
                    Console.WriteLine($"  QuantitySet: {qset.Name}");
                    foreach (var qty in qset.Quantities)
                    {
                        Console.WriteLine($"    - {qty.Name}: {qty}");
                    }
                }
            }
        }

        /// <summary>
        /// Determines if question is asking about spaces within a specific storey
        /// </summary>
        public static (bool isSpaceQuery, string? storeyFilter) ParseSpaceStoreyQuery(string question)
        {
            var lowerQuestion = question.ToLower();
            
            // Patterns indicating spaces on a specific floor (Swedish + English)
            var storeyPatterns = new Dictionary<string, string>
            {
                // Swedish patterns
                { "vindsplan", "Vindsplan" },
                { "vind", "Vindsplan" },
                { "plan 1", "Plan 1" },
                { "plan 2", "Plan 2" },
                { "plan 3", "Plan 3" },
                { "plan 4", "Plan 4" },
                { "källare", "Källare" },
                { "entréplan", "Entréplan" },
                
                // English patterns
                { "attic", "Vindsplan" },
                { "floor 1", "Plan 1" },
                { "floor 2", "Plan 2" },
                { "floor 3", "Plan 3" },
                { "floor 4", "Plan 4" },
                { "basement", "Källare" },
                { "ground floor", "Entréplan" },
                { "entrance level", "Entréplan" }
            };
            
            foreach (var pattern in storeyPatterns)
            {
                if (lowerQuestion.Contains(pattern.Key))
                {
                    return (true, pattern.Value);
                }
            }
            
            return (false, null);
        }
    }
}