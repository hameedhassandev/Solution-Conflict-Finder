using System;
using System.Collections.Generic;
using System.Text;

namespace SolutionComponentsAnalyzer.Core.Models
{
    public class WebResourceDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public string? Content { get; set; }
        public bool IsText { get; set; }
    }

}
