using SolutionComponentsAnalyzer.Core.Comparers;
using SolutionComponentsAnalyzer.Core.Models;
using SolutionComponentsAnalyzer.Core.Providers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SolutionComponentsAnalyzer.Core.Services
{
    public class SolutionComparisonService
    {
        /// <summary>
        /// A (sourceA) = current/target solution: receives the import and holds your customizations.
        /// B (sourceB) = incoming solution: the one that will be imported and overwrite A.
        /// </summary>
        public ComparisonResult Compare(SolutionDefinition sourceA, SolutionDefinition sourceB, string sourceAName, string sourceBName)
        {
            if (sourceA == null) throw new ArgumentNullException(nameof(sourceA));
            if (sourceB == null) throw new ArgumentNullException(nameof(sourceB));

            return new ComparisonResult
            {
                SourceA = sourceAName,
                SourceB = sourceBName,
                Target = sourceA,
                Source = sourceB,
                Differences = ComponentComparers
                    .CompareSolutions(sourceA, sourceB, sourceAName, sourceBName)
                    .OrderByDescending(x => x.Type == DifferenceType.Conflict)   // conflicts first
                    .ThenBy(x => x.ComponentType)
                    .ThenBy(x => x.EntityName)
                    .ThenBy(x => x.ComponentName)
                    .ToList()
            };
        }

        /// <summary>Loads both solutions through the providers, then compares them.</summary>
        public ComparisonResult Compare(
            ISolutionProvider providerA, string solutionA, string sourceAName,
            ISolutionProvider providerB, string solutionB, string sourceBName)
        {
            if (providerA == null) throw new ArgumentNullException(nameof(providerA));
            if (providerB == null) throw new ArgumentNullException(nameof(providerB));

            return Compare(
                providerA.GetSolution(solutionA),
                providerB.GetSolution(solutionB),
                sourceAName,
                sourceBName);
        }
    }

}
