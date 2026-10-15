using System;
using System.Collections.Generic;
using System.Text;

namespace SolutionComponentsAnalyzer.Core.Models
{
    public class BpfDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string PrimaryEntity { get; set; } = string.Empty;
        public string Definition { get; set; } = string.Empty;
        public List<BpfStageDefinition> Stages { get; } = new();
    }

    public class BpfStageDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Order { get; set; }
        public List<string> Steps { get; set; } = new();
    }

}
