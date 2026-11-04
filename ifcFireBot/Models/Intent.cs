#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifcFireBot.Models
{
    /// <summary>
    /// Represents the parsed intent from a user's question about an IFC model.
    /// Used both for YAML deserialization and throughout the application.
    /// </summary>
    public class Intent
    {
        /// <summary>
        /// The IFC entity type (e.g., "IfcWall", "IfcDoor")
        /// </summary>
        public string IfcEntity { get; set; } = string.Empty;
        
        /// <summary>
        /// The type of operation (e.g., "Count", "List", "Area")
        /// </summary>
        public string QuestionType { get; set; } = string.Empty;
        
        /// <summary>
        /// Optional filter for specific entity names (e.g., "vindplan", "våning 2")
        /// </summary>
        public string? EntityFilter { get; set; }
        
        /// <summary>
        /// Optional property name being queried (e.g., "area", "höjd")
        /// </summary>
        public string? PropertyName { get; set; }
        
        /// <summary>
        /// Optional specific entity name (e.g., "Wall_1", "Door_2")
        /// </summary>
        public string? EntityName { get; set; } // Added to provide specific entity reference
    }
}
