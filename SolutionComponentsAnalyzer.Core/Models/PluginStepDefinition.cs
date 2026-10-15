using System;
using System.Collections.Generic;
using System.Text;

namespace SolutionComponentsAnalyzer.Core.Models
{
    public class PluginStepDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Stage { get; set; } = string.Empty;
        public string Mode { get; set; } = string.Empty;
        public string Rank { get; set; } = string.Empty;
        public string FilteringAttributes { get; set; } = string.Empty;
        public string Configuration { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
    }
}
