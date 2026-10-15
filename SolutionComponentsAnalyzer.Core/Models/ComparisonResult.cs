using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SolutionComponentsAnalyzer.Core.Models
{
    public class ComparisonResult
    {
        public string SourceA { get; set; } = string.Empty;   // name of current/target side
        public string SourceB { get; set; } = string.Empty;   // name of incoming/source side

        public SolutionDefinition Target { get; set; } = new();
        public SolutionDefinition Source { get; set; } = new();
        public List<Difference> Differences { get; set; } = new();

        public IEnumerable<Difference> Conflicts => Differences.Where(x => x.Type == DifferenceType.Conflict);
        public IEnumerable<Difference> NeedsReview => Differences.Where(x => x.Type == DifferenceType.ExistsInBoth);
        public IEnumerable<Difference> WillBeAdded => Differences.Where(x => x.Type == DifferenceType.OnlyInSource);
        public IEnumerable<Difference> UntouchedInTarget => Differences.Where(x => x.Type == DifferenceType.OnlyInTarget);
    }
}
