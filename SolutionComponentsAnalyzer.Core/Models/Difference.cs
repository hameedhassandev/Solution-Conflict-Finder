using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SolutionComponentsAnalyzer.Core.Models
{
    public enum DifferenceType
    {
        OnlyInTarget,
        OnlyInSource,
        Conflict,
        ExistsInBoth
    }


    public class Difference
    {
        public string ComponentType { get; set; } = string.Empty;
        public string ComponentName { get; set; } = string.Empty;
        public string EntityName { get; set; } = string.Empty;
        public DifferenceType Type { get; set; }
        public string? Path { get; set; }
        public string? Property { get; set; }
        public string? SourceAValue { get; set; }
        public string? SourceBValue { get; set; }
        public string Details { get; set; } = string.Empty;
    }
}
