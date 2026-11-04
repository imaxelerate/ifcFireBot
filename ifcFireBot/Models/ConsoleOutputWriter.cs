// Models/ConsoleOutputWriter.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Xbim.Ifc4.Interfaces;

namespace ifcFireBot.Models
{
    /// <summary>
    /// Console-based implementation of IOutputWriter
    /// </summary>
    public class ConsoleOutputWriter : IOutputWriter
    {
        public void WriteInfo(string message)
        {
            Console.WriteLine(message);
        }

        public void WriteSuccess(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        public void WriteError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        public void WriteWarning(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        public void WriteDebug(string message)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        public void WriteAnswer(string message)
        {
            Console.WriteLine($"\nAnswer: {message}\n");
        }

        public void WriteBuilding(string message)
        {
            Console.WriteLine(message);
        }

        public void WriteIntentDetails(Intent intent)
        {
            Console.WriteLine("\nParsed intent:");
            Console.WriteLine($"   - IFC entity: {intent.IfcEntity}");
            Console.WriteLine($"   - Question type: {intent.QuestionType}");
            
            if (!string.IsNullOrEmpty(intent.EntityFilter))
                Console.WriteLine($"   - Filter: {intent.EntityFilter}");
            
            if (!string.IsNullOrEmpty(intent.PropertyName))
                Console.WriteLine($"   - Property: {intent.PropertyName}");
        }

        public void WriteElementDetails(IEnumerable<IIfcProduct> elements, int maxToShow = 10)
        {
            var elementList = elements.ToList();
            
            if (elementList.Count == 0)
                return;

            Console.WriteLine("\nElement details:");
            
            foreach (var el in elementList.Take(maxToShow))
            {
                var name = el.Name?.ToString() ?? "(no name)";
                var typeName = el.GetType().Name;
                Console.WriteLine($"   - {name} ({typeName})");
            }
            
            if (elementList.Count > maxToShow)
                Console.WriteLine($"   ... and {elementList.Count - maxToShow} more");
        }

        public void WritePrompt(string message)
        {
            Console.WriteLine(message);
        }

        public string ReadLine()
        {
            return Console.ReadLine() ?? string.Empty;
        }

        // NEW: Colored output method
        public void WriteColored(string message, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        // NEW: Plain output method
        public void WriteLine(string message)
        {
            Console.WriteLine(message);
        }
    }
}