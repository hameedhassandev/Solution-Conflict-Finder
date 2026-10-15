using System;
using System.Collections.Generic;
using System.Text;

namespace SolutionComponentsAnalyzer.Core.Models
{
    /// <summary>
    /// A solution component we only know at a high level (report, site map, app, environment variable...).
    /// </summary>
    public class GenericComponentDefinition
    {
        public int ComponentTypeCode { get; set; }
        public string ComponentTypeName { get; set; } = string.Empty;
        public string ObjectId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        //Hash of the component's content. Empty = content cannot be compared
        public string Fingerprint { get; set; } = string.Empty;
    }
}
