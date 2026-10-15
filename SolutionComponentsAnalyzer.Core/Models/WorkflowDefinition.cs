using System;
using System.Collections.Generic;
using System.Text;

namespace SolutionComponentsAnalyzer.Core.Models
{
    public class WorkflowDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Entity { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Definition { get; set; } = string.Empty;
    }
}
