using System;
using System.Collections.Generic;
using System.Text;

namespace SolutionComponentsAnalyzer.Core.Models
{
    public class OptionSetDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;  
        public string DisplayName { get; set; } = string.Empty;

        // "Option[3]" -> label
        public Dictionary<string, string> Options { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
