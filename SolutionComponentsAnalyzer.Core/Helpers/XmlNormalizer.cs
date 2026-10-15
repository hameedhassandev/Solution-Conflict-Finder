using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace SolutionComponentsAnalyzer.Core.Helpers
{
    public static class XmlNormalizer
    {
        public static string Normalize(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml)) return string.Empty;

            try
            {
                var doc = XDocument.Parse(xml, LoadOptions.None);

                foreach (var comment in doc.DescendantNodes().OfType<XComment>().ToList())
                    comment.Remove();

                foreach (var element in doc.Descendants().ToList())
                {
                    var sorted = element.Attributes()
                        .OrderBy(a => a.Name.ToString(), StringComparer.Ordinal)
                        .ToList();
                    element.ReplaceAttributes(sorted);
                }

                return doc.ToString(SaveOptions.DisableFormatting);
            }
            catch
            {
                return xml.Trim();   // not XML (e.g. JSON clientdata) -> plain compare
            }
        }
    }
}
