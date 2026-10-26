using System;

namespace SolutionComponentsAnalyzer.XrmToolBox
{
    internal sealed class SolutionChoice
    {
        public Guid Id { get; set; }
        public string UniqueName { get; set; } = string.Empty;
        public string FriendlyName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(Version) ? UniqueName : UniqueName + "  |  v" + Version;
        }
    }
}
