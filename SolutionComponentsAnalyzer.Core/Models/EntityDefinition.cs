using System;
using System.Collections.Generic;
using System.Text;

namespace SolutionComponentsAnalyzer.Core.Models
{
    public class EntityDefinition
    {
        public string LogicalName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public List<FieldDefinition> Fields { get; } = new();
        public List<FormDefinition> Forms { get; } = new();
        public List<ViewDefinition> Views { get; } = new();
    }
}
