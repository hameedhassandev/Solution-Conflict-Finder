using SolutionComponentsAnalyzer.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SolutionComponentsAnalyzer.Core.Providers
{
    public interface ISolutionProvider
    {
        SolutionDefinition GetSolution(string uniqueName);
    }
}
