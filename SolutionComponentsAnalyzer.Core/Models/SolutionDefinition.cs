using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SolutionComponentsAnalyzer.Core.Models
{
    public class SolutionDefinition
    {
        public string UniqueName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string FriendlyName { get; set; } = string.Empty;
        public List<EntityDefinition> Entities { get; } = new();
        public List<WebResourceDefinition> WebResources { get; } = new();
        public List<PluginAssemblyDefinition> PluginAssemblies { get; } = new();
        public List<PluginStepDefinition> PluginSteps { get; set; } = new();
        public List<WorkflowDefinition> Workflows { get; } = new();
        public List<BpfDefinition> BusinessProcessFlows { get; } = new();
        public List<OptionSetDefinition> OptionSets { get; set; } = new();
        public List<GenericComponentDefinition> OtherComponents { get; set; } = new();
    }
}
