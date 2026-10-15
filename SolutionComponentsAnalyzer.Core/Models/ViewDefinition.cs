using System;
using System.Collections.Generic;
using System.Text;

namespace SolutionComponentsAnalyzer.Core.Models
{
    public class ViewDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string FetchXml { get; set; } = string.Empty;
        public string LayoutXml { get; set; } = string.Empty;
        public List<string> Columns { get; } = new();
    }
}
