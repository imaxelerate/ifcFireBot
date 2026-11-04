// Models/Calculate.cs
using System.Collections.Generic;
using Xbim.Ifc4.Interfaces;
using ifcFireBot.Models.Calculators;

#nullable enable

namespace ifcFireBot.Models
{
    public class Calculate
    {
        private static readonly Dictionary<string, ICalculator> _calculators = new()
        {
            ["count"] = new CountCalculator(),
            ["exists"] = new ExistsCalculator(),
            ["list"] = new ListCalculator(),
            ["area"] = new AreaCalculator(),
            ["volume"] = new VolumeCalculator(),
            ["height"] = new HeightCalculator(),
            ["buildingheight"] = new BuildingHeightCalculator(),
            ["undergroundfloors"] = new UndergroundFloorCalculator()
        };

        /// <summary>
        /// Main entry point for calculating answers based on intent
        /// </summary>
        public static string Answer(List<IIfcProduct> elements, Intent intent, IOutputWriter output)
        {
            var questionType = intent.QuestionType.ToLower();
            
            if (_calculators.TryGetValue(questionType, out var calculator))
            {
                return calculator.Calculate(elements, intent, output);
            }

            // Fallback for unsupported question types
            return $"Question type '{questionType}' is not supported. Found {elements.Count} elements";
        }
    }
}