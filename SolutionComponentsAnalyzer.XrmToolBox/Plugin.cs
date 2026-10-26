using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;

namespace SolutionComponentsAnalyzer.XrmToolBox
{
    [Export(typeof(IXrmToolBoxPlugin)),
     ExportMetadata("Name", "Solution Conflict Finder"),
     ExportMetadata("Description", "Compare solutions across environments and review conflicts before you import."),
     ExportMetadata("BackgroundColor", "White"),
     ExportMetadata("PrimaryFontColor", "Black"),
     ExportMetadata("SecondaryFontColor", "DimGray"),
     ExportMetadata("SmallImageBase64", PluginIcons.Small),
     ExportMetadata("BigImageBase64", PluginIcons.Big)]
    public class Plugin : PluginBase
    {
        public Plugin()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveFromPluginFolder;
        }

        public override IXrmToolBoxPluginControl GetControl()
        {
            return new SolutionConflictFinderControl();
        }

        // loads SolutionComponentsAnalyzer.Core.dll from the sub-folder named like this plugin
        private static Assembly ResolveFromPluginFolder(object sender, ResolveEventArgs args)
        {
            var current = Assembly.GetExecutingAssembly();
            var name = args.Name.Split(',')[0];

            if (!current.GetReferencedAssemblies().Any(a => a.Name == name))
                return null;

            var folder = Path.Combine(
                Path.GetDirectoryName(current.Location),
                Path.GetFileNameWithoutExtension(current.Location));

            var path = Path.Combine(folder, name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        }
    }
}
