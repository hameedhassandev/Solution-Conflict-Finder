using System;
using System.Collections.Generic;
using System.Text;

namespace SolutionComponentsAnalyzer.Core.Models
{
    public class FieldDefinition
    {
        public string LogicalName { get; set; } = string.Empty;
        public string SchemaName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string AttributeType { get; set; } = string.Empty;
        public string RequiredLevel { get; set; } = string.Empty;
        public Dictionary<string, string> Properties { get; set; }
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
