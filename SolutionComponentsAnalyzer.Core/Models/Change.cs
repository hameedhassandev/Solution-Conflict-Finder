using System;
using System.Collections.Generic;
using System.Text;

namespace SolutionComponentsAnalyzer.Core.Models
{
    public class Change
    {
        public Change(string property, string details)
        {
            Property = property;
            Details = details;
        }

        public string Property { get; private set; }
        public string Details { get; private set; }
    }
}
