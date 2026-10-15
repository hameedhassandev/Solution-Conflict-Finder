using System;
using System.Collections.Generic;
using System.Text;

namespace SolutionComponentsAnalyzer.Core.Models
{
    public class PluginAssemblyDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string PublicKeyToken { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
    }
}
