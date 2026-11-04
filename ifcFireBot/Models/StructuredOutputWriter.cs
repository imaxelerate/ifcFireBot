// Models/StructuredOutputWriter.cs
using System;
using System.Collections.Generic;
using System.Text;

namespace ifcFireBot.Models
{
    /// <summary>
    /// Structured output formatter for detailed analysis responses
    /// </summary>
    public class StructuredOutputWriter
    {
        private readonly IOutputWriter _output;
        private int _sectionNumber = 0;
        private const int SeparatorLength = 50;

        public StructuredOutputWriter(IOutputWriter output)
        {
            _output = output;
        }

        public void WriteQuestion(string question)
        {
            _output.WriteLine($"Question: {question}");
            _output.WriteLine("");
            _sectionNumber = 0; // Reset section counter
        }

        public void WriteSectionHeader(string title)
        {
            _sectionNumber++;
            _output.WriteLine(new string('-', SeparatorLength));
            _output.WriteLine($"{_sectionNumber}. {title}");
            _output.WriteLine(new string('-', SeparatorLength));
            _output.WriteLine("");
        }

        public void WriteSubSection(string title)
        {
            _output.WriteColored(title, ConsoleColor.Cyan);
        }

        public void WriteList(IEnumerable<string> items, int indent = 2)
        {
            foreach (var item in items)
            {
                _output.WriteLine($"{new string(' ', indent)}{item}");
            }
            _output.WriteLine("");
        }

        public void WriteBulletList(IEnumerable<string> items, int indent = 0)
        {
            foreach (var item in items)
            {
                _output.WriteLine($"{new string(' ', indent)}• {item}");
            }
        }

        /// <summary>
        /// Write an answer with building-specific format
        /// </summary>
        public void WriteAnswer(string answer)
        {
            _output.WriteLine("");
            _output.WriteLine(new string('-', SeparatorLength));
            _output.WriteLine("Answer");
            _output.WriteLine(new string('-', SeparatorLength));
            _output.WriteLine("");
            _output.WriteLine($"The building has **{answer}**.");
            _output.WriteLine("");
        }

        /// <summary>
        /// Write a generic answer (for yes/no, exists, etc.)
        /// </summary>
        public void WriteGenericAnswer(string answer)
        {
            _output.WriteLine("");
            _output.WriteLine(new string('-', SeparatorLength));
            _output.WriteLine("Answer");
            _output.WriteLine(new string('-', SeparatorLength));
            _output.WriteLine("");
            _output.WriteLine(answer);
            _output.WriteLine("");
        }

        public void WriteConfidenceSection(int entityCount, string confidenceLevel, List<(string Category, string Description)> indicators)
        {
            _output.WriteLine("");
            _output.WriteLine(new string('-', SeparatorLength));
            _output.WriteLine($"{_sectionNumber + 1}. Model Confidence Indicators");
            _output.WriteLine(new string('-', SeparatorLength));
            _output.WriteLine("");
            _output.WriteLine("The following model issues may affect reliability:");
            _output.WriteLine("");

            foreach (var indicator in indicators)
            {
                _output.WriteColored($"• {indicator.Category}", ConsoleColor.Yellow);
                _output.WriteLine($"  {indicator.Description}");
                _output.WriteLine("");
            }

            _output.WriteLine($"System confidence: **{confidenceLevel}**");
        }

        public void WriteSimpleText(string text, int indent = 2)
        {
            _output.WriteLine($"{new string(' ', indent)}{text}");
        }

        public void WriteEmptyLine()
        {
            _output.WriteLine("");
        }
    }
}