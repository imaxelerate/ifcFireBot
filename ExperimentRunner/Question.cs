using System;

namespace ExperimentRunner
{
    /// <summary>
    /// Represents a benchmark question for IFC model testing
    /// </summary>
    public class Question
    {
        public string Id { get; set; }
        public string Text { get; set; }

        public Question(string id, string text)
        {
            Id = id;
            Text = text;
        }
    }
}
