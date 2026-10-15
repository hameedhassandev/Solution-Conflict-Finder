using System;
using System.Collections.Generic;
using System.Text;

namespace SolutionComponentsAnalyzer.Core.Helpers
{
    public static class ComponentTypes
    {
        public const int Entity = 1;
        public const int Attribute = 2;
        public const int GlobalOptionSet = 9;
        public const int SavedQuery = 26;
        public const int Workflow = 29;
        public const int SystemForm = 60;
        public const int WebResource = 61;
        public const int PluginAssembly = 91;
        public const int PluginStep = 92;

        //Sub-components that are always part of a parent, so they are not reported on their own
        public static readonly HashSet<int> Ignored = new HashSet<int>
        {
            2, 10, 14, 16, 21, 22,// attribute / relationship / entity key / privilege / role privilege / display string
            3, 4, 5, 6, 7, 8, 11, 12, 13, 17, 18, 23,// relationship / label / index internals
            32, 33, 34,// report entity / category / visibility
            45, 47, // duplicate rule condition, attribute map
            151, 153, 155, 162,// routing / SLA / convert / mobile profile items
            25, 201, 202, 203, 204, 205, 206, 207// organization + SDK message metadata (system)
        };
    }
}
