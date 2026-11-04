// Models/IOutputWriter.cs
using System;
using System.Collections.Generic;
using Xbim.Ifc4.Interfaces;

namespace ifcFireBot.Models
{
    /// <summary>
    /// Interface for output operations, allowing for testability and flexibility
    /// </summary>
    public interface IOutputWriter
    {
        void WriteInfo(string message);
        void WriteSuccess(string message);
        void WriteError(string message);
        void WriteWarning(string message);
        void WriteDebug(string message);
        void WriteAnswer(string message);
        void WriteBuilding(string message);
        void WriteIntentDetails(Intent intent);
        void WriteElementDetails(IEnumerable<IIfcProduct> elements, int maxToShow = 10);
        void WritePrompt(string message);
        string ReadLine(); 
        
        // NEW: Add colored output support
        void WriteColored(string message, ConsoleColor color);
        void WriteLine(string message); // NEW: Plain output without formatting
    }
}