using System.ComponentModel.Composition;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;

namespace SolutionComponentsAnalyzer.XrmToolBox
{
    [Export(typeof(IXrmToolBoxPlugin)),
     ExportMetadata("Name", "Solution Conflict Finder"),
     ExportMetadata("Description", "Compare solutions across environments and review conflicts before you import."),
     ExportMetadata("BackgroundColor", "Gray"),
     ExportMetadata("PrimaryFontColor", "Black"),
     ExportMetadata("SecondaryFontColor", "DimGray"),
     ExportMetadata("SmallImageBase64", PluginIcons.Small),
     ExportMetadata("BigImageBase64", PluginIcons.Big)]
    public class Plugin : PluginBase
    {
        public override IXrmToolBoxPluginControl GetControl()
        {
            return new SolutionConflictFinderControl();
        }
    }
}
