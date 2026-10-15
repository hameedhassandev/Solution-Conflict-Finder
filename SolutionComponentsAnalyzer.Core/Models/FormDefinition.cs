using System;
using System.Collections.Generic;
using System.Text;

namespace SolutionComponentsAnalyzer.Core.Models
{
    public class FormDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string NormalizedXml { get; set; } = string.Empty;
        public List<string> FormLibraries { get; set; } = new();
        public List<string> EventHandlers { get; set; } = new();
        public List<TabDefinition> Tabs { get; } = new();
    }

    public class TabDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public List<SectionDefinition> Sections { get; } = new();
    }

    public class SectionDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public List<ControlDefinition> Controls { get; } = new();
    }

    public class ControlDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string FieldName { get; set; } = string.Empty;
        public string ControlType { get; set; } = string.Empty;
        public bool? Visible { get; set; }
        public bool? Disabled { get; set; }
    }
}
