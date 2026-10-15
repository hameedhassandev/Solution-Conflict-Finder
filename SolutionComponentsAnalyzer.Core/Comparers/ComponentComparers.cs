using SolutionComponentsAnalyzer.Core.Helpers;
using SolutionComponentsAnalyzer.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SolutionComponentsAnalyzer.Core.Comparers
{
    public static class ComponentComparers
    {
        public static IEnumerable<Difference> CompareSolutions(SolutionDefinition a, SolutionDefinition b, string aName, string bName)
        {
            var entityNames = a.Entities.Select(x => x.LogicalName)
                .Union(b.Entities.Select(x => x.LogicalName), StringComparer.OrdinalIgnoreCase);

            var entityDiffs = entityNames.SelectMany(name =>
            {
                var ea = a.Entities.FirstOrDefault(x => x.LogicalName.Equals(name, StringComparison.OrdinalIgnoreCase));
                var eb = b.Entities.FirstOrDefault(x => x.LogicalName.Equals(name, StringComparison.OrdinalIgnoreCase));

                return CompareFields(ea, eb, aName, bName)
                    .Concat(CompareForms(ea, eb, aName, bName))
                    .Concat(CompareViews(ea, eb, aName, bName));
            });

            return entityDiffs
                .Concat(CompareWebResources(a.WebResources, b.WebResources, aName, bName))
                .Concat(CompareAssemblies(a.PluginAssemblies, b.PluginAssemblies, aName, bName))
                .Concat(ComparePluginSteps(a.PluginSteps, b.PluginSteps, aName, bName))
                .Concat(CompareWorkflows(a.Workflows, b.Workflows, aName, bName))
                .Concat(CompareBpfs(a.BusinessProcessFlows, b.BusinessProcessFlows, aName, bName))
                .Concat(CompareOptionSets(a.OptionSets, b.OptionSets, aName, bName))
                .Concat(CompareOtherComponents(a.OtherComponents, b.OtherComponents, aName, bName));
        }

        private static IEnumerable<Difference> CompareKeyed<T>(
            string componentType,
            IEnumerable<T> a,
            IEnumerable<T> b,
            Func<T, string> key,
            Func<T, string> name,
            Func<T, string> entity,
            Func<T, T, List<Change>> compare,
            string aName,
            string bName)
        {
            var aMap = ToMap(a, key);
            var bMap = ToMap(b, key);

            foreach (var pair in aMap)
            {
                var x = pair.Value;

                if (!bMap.TryGetValue(pair.Key, out var y))
                {
                    yield return Diff(componentType, name(x), entity(x), DifferenceType.OnlyInTarget,
                        $"{componentType} exists only in {aName}. Import will not touch it.");
                    continue;
                }

                foreach (var change in compare(x, y))
                {
                    yield return Diff(componentType, name(x), entity(x), DifferenceType.Conflict,
                        $"{change.Details}  [{aName} -> {bName}: import will overwrite {aName}]", change.Property);
                }
            }

            foreach (var pair in bMap)
            {
                if (!aMap.ContainsKey(pair.Key))
                {
                    var y = pair.Value;
                    yield return Diff(componentType, name(y), entity(y), DifferenceType.OnlyInSource,
                        $"{componentType} exists only in {bName}. Import will add it to {aName}.");
                }
            }
        }

        // Fields
        public static IEnumerable<Difference> CompareFields(EntityDefinition a, EntityDefinition b, string aName, string bName)
        {
            var entityName = a?.LogicalName ?? b?.LogicalName;

            return CompareKeyed("Field", a?.Fields, b?.Fields,
                x => x.LogicalName, x => x.LogicalName, _ => entityName,
                (x, y) =>
                {
                    var list = new List<Change>();
                    Check(list, "DisplayName", x.DisplayName, y.DisplayName, StringComparison.Ordinal);
                    Check(list, "AttributeType", x.AttributeType, y.AttributeType);
                    Check(list, "RequiredLevel", x.RequiredLevel, y.RequiredLevel);
                    Check(list, "SchemaName", x.SchemaName, y.SchemaName);
                    DictionaryDiff(list, x.Properties, y.Properties);   // MinValue, MaxValue, Precision, MaxLength, Options...
                    return list;
                }, aName, bName);
        }

        // Forms: a form is replaced as a WHOLE on import, so every difference is a conflict
        public static IEnumerable<Difference> CompareForms(EntityDefinition a, EntityDefinition b, string aName, string bName)
        {
            var entityName = a?.LogicalName ?? b?.LogicalName;

            return CompareKeyed("Form", a?.Forms, b?.Forms,
                x => string.IsNullOrWhiteSpace(x.Id) ? x.Name : x.Id, x => x.Name, _ => entityName,
                (x, y) =>
                {
                    var list = new List<Change>();
                    var xc = ControlMap(x);
                    var yc = ControlMap(y);

                    foreach (var k in xc.Keys.Except(yc.Keys, StringComparer.OrdinalIgnoreCase))
                        list.Add(new Change("Control", $"Control '{k}' exists only in target form -> it will be REMOVED from the form."));

                    foreach (var k in yc.Keys.Except(xc.Keys, StringComparer.OrdinalIgnoreCase))
                        list.Add(new Change("Control", $"Control '{k}' exists only in source form -> it will be ADDED to the form."));

                    foreach (var k in xc.Keys.Intersect(yc.Keys, StringComparer.OrdinalIgnoreCase))
                    {
                        var cx = xc[k];
                        var cy = yc[k];

                        if (cx.Visible.HasValue && cy.Visible.HasValue && cx.Visible != cy.Visible)
                            list.Add(new Change("Visible", $"Control '{k}': Visible {cx.Visible} -> {cy.Visible}"));

                        if (cx.Disabled.HasValue && cy.Disabled.HasValue && cx.Disabled != cy.Disabled)
                            list.Add(new Change("Disabled", $"Control '{k}': Disabled {cx.Disabled} -> {cy.Disabled}"));
                    }

                    SetDiff(list, "FormLibrary", x.FormLibraries, y.FormLibraries);
                    SetDiff(list, "EventHandler", x.EventHandlers, y.EventHandlers);

                    // Anything else (labels, tab/section layout, ordering, properties...)
                    if (list.Count == 0 &&
                        !string.IsNullOrEmpty(x.NormalizedXml) &&
                        !string.Equals(x.NormalizedXml, y.NormalizedXml, StringComparison.Ordinal))
                    {
                        list.Add(new Change("FormXml", "Form XML differs (layout, labels, ordering or other properties)."));
                    }

                    return list;
                }, aName, bName);
        }

        // Views
        public static IEnumerable<Difference> CompareViews(EntityDefinition a, EntityDefinition b, string aName, string bName)
        {
            var entityName = a?.LogicalName ?? b?.LogicalName;

            return CompareKeyed("View", a?.Views, b?.Views,
                x => string.IsNullOrWhiteSpace(x.Id) ? x.Name : x.Id, x => x.Name, _ => entityName,
                (x, y) =>
                {
                    var list = new List<Change>();
                    SetDiff(list, "Column", x.Columns, y.Columns);

                    Check(list, "Name", x.Name, y.Name, StringComparison.Ordinal);

                    if (!XmlEquivalent(x.FetchXml, y.FetchXml))
                        list.Add(new Change("FetchXML", "FetchXML (filters/sorting/joins) changed."));

                    if (!XmlEquivalent(x.LayoutXml, y.LayoutXml))
                        list.Add(new Change("LayoutXML", "LayoutXML (column order/width) changed."));

                    return list;
                }, aName, bName);
        }

        // Web resources
        public static IEnumerable<Difference> CompareWebResources(IEnumerable<WebResourceDefinition> a, IEnumerable<WebResourceDefinition> b, string aName, string bName)
        {
            return CompareKeyed("Web Resource", a, b,
                x => x.Name, x => x.Name, _ => null,
                (x, y) =>
                {
                    var list = new List<Change>();
                    if (!string.Equals(x.Hash, y.Hash, StringComparison.OrdinalIgnoreCase))
                        list.Add(new Change("Content", "Web resource content changed."));
                    return list;
                }, aName, bName);
        }

        // Plugin Assembly
        public static IEnumerable<Difference> CompareAssemblies(IEnumerable<PluginAssemblyDefinition> a, IEnumerable<PluginAssemblyDefinition> b, string aName, string bName)
        {
            return CompareKeyed("Plugin Assembly", a, b,
                x => x.Name, x => x.Name, _ => null,
                (x, y) =>
                {
                    var list = new List<Change>();
                    Check(list, "Version", x.Version, y.Version);
                    Check(list, "PublicKeyToken", x.PublicKeyToken, y.PublicKeyToken);
                    if (!string.Equals(x.Hash, y.Hash, StringComparison.OrdinalIgnoreCase))
                        list.Add(new Change("Content", "Assembly binary changed."));
                    return list;
                }, aName, bName);
        }

        // Plugin Step
        public static IEnumerable<Difference> ComparePluginSteps(IEnumerable<PluginStepDefinition> a, IEnumerable<PluginStepDefinition> b, string aName, string bName)
        {
            return CompareKeyed("Plugin Step", a, b,
                x => x.Id, x => x.Name, _ => null,
                (x, y) =>
                {
                    var list = new List<Change>();
                    Check(list, "Stage", x.Stage, y.Stage);
                    Check(list, "Mode", x.Mode, y.Mode);
                    Check(list, "Rank", x.Rank, y.Rank);
                    Check(list, "FilteringAttributes", x.FilteringAttributes, y.FilteringAttributes);
                    Check(list, "Configuration", x.Configuration, y.Configuration, StringComparison.Ordinal);
                    Check(list, "State", x.State, y.State);
                    return list;
                }, aName, bName);
        }

        // Workflow
        public static IEnumerable<Difference> CompareWorkflows(IEnumerable<WorkflowDefinition> a, IEnumerable<WorkflowDefinition> b, string aName, string bName)
        {
            return CompareKeyed("Workflow", a, b,
                x => StableName(x.Id, x.Name), x => x.Name, x => x.Entity,
                (x, y) =>
                {
                    var list = new List<Change>();
                    Check(list, "Name", x.Name, y.Name, StringComparison.Ordinal);
                    Check(list, "Category", x.Category, y.Category);
                    Check(list, "State", x.State, y.State);
                    if (!XmlEquivalent(x.Definition, y.Definition))
                        list.Add(new Change("Definition", "Workflow/flow definition changed."));
                    return list;
                }, aName, bName);
        }

        // BPF
        public static IEnumerable<Difference> CompareBpfs(IEnumerable<BpfDefinition> a, IEnumerable<BpfDefinition> b, string aName, string bName)
        {
            return CompareKeyed("BPF", a, b,
                x => StableName(x.Id, x.Name), x => x.Name, x => x.PrimaryEntity,
                (x, y) =>
                {
                    var list = new List<Change>();

                    var xs = x.Stages ?? new List<BpfStageDefinition>();
                    var ys = y.Stages ?? new List<BpfStageDefinition>();

                    SetDiff(list, "Stage", xs.Select(s => s.Name), ys.Select(s => s.Name));

                    foreach (var sx in xs)
                    {
                        var sy = ys.FirstOrDefault(s => s.Name.Equals(sx.Name, StringComparison.OrdinalIgnoreCase));
                        if (sy != null)
                            SetDiff(list, $"Step ({sx.Name})", sx.Steps, sy.Steps);
                    }

                    if (!XmlEquivalent(x.Definition, y.Definition))
                        list.Add(new Change("Definition", "BPF definition (stages/steps/conditions) changed."));

                    return list;
                }, aName, bName);
        }

        // Global Option set
        public static IEnumerable<Difference> CompareOptionSets(IEnumerable<OptionSetDefinition> a, IEnumerable<OptionSetDefinition> b, string aName, string bName)
        {
            string GetKey(OptionSetDefinition x) => x.Name;
            string GetName(OptionSetDefinition x) => x.Name;
            string GetEntity(OptionSetDefinition x) => string.Empty;

            List<Change> FindChanges(OptionSetDefinition x, OptionSetDefinition y)
            {
                var changes = new List<Change>();

                Check(changes, "DisplayName", x.DisplayName, y.DisplayName, StringComparison.Ordinal);
                DictionaryDiff(changes, x.Options, y.Options);   // Option[3]: 'Open' -> 'Opened', or a missing option

                return changes;
            }

            return CompareKeyed(
                componentType: "Global Option Set",
                a: a,
                b: b,
                key: GetKey,
                name: GetName,
                entity: GetEntity,
                compare: FindChanges,
                aName: aName,
                bName: bName);
        }

        // Everything else (reports, site maps, apps, env variables, roles...): high-level only
        public static IEnumerable<Difference> CompareOtherComponents(IEnumerable<GenericComponentDefinition> a, IEnumerable<GenericComponentDefinition> b, string aName, string bName)
        {
            var aMap = ToMap(a, OtherKey);
            var bMap = ToMap(b, OtherKey);

            foreach (var pair in aMap)
            {
                var x = pair.Value;

                if (!bMap.TryGetValue(pair.Key, out var y))
                {
                    yield return Diff(x.ComponentTypeName, OtherName(x), null, DifferenceType.OnlyInTarget,
                        x.ComponentTypeName + " exists only in " + aName + ". Import will not touch it.");
                    continue;
                }

                if (!string.IsNullOrEmpty(x.Fingerprint) && !string.IsNullOrEmpty(y.Fingerprint))
                {
                    if (!string.Equals(x.Fingerprint, y.Fingerprint, StringComparison.OrdinalIgnoreCase))
                    {
                        yield return Diff(x.ComponentTypeName, OtherName(x), null, DifferenceType.Conflict,
                            "Content differs (high-level only, no details). Import will overwrite " + aName + "'s version.");
                    }
                }
                else
                {
                    yield return Diff(x.ComponentTypeName, OtherName(x), null, DifferenceType.ExistsInBoth,
                        "Exists in both; details are not compared for this component type. Import will overwrite " + aName + " - review manually.");
                }
            }

            foreach (var pair in bMap)
            {
                if (!aMap.ContainsKey(pair.Key))
                {
                    var y = pair.Value;
                    yield return Diff(y.ComponentTypeName, OtherName(y), null, DifferenceType.OnlyInSource,
                        y.ComponentTypeName + " exists only in " + bName + ". Import will add it to " + aName + ".");
                }
            }
        }

        private static string OtherKey(GenericComponentDefinition x)
        {
            return x.ComponentTypeCode + ":" + x.ObjectId;
        }

        private static string OtherName(GenericComponentDefinition x)
        {
            return string.IsNullOrWhiteSpace(x.Name) ? x.ObjectId : x.Name;
        }

        private static Dictionary<string, ControlDefinition> ControlMap(FormDefinition form)
        {
            return form.Tabs
                .SelectMany(t => t.Sections)
                .SelectMany(s => s.Controls)
                .Select(c => new { Key = !string.IsNullOrWhiteSpace(c.FieldName) ? c.FieldName : c.Id, Control = c })
                .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Control, StringComparer.OrdinalIgnoreCase);
        }

        private static Dictionary<string, T> ToMap<T>(IEnumerable<T> items, Func<T, string> key)
        {
            var map = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items ?? Enumerable.Empty<T>())
            {
                var k = key(item);
                if (!string.IsNullOrWhiteSpace(k))
                    if (!map.ContainsKey(k)) map.Add(k, item);   // no duplicate-key exceptions (TryAdd does not exist on net48)
            }
            return map;
        }

        private static void Check(List<Change> list, string property, string x, string y,
            StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        {
            if (!string.Equals(x ?? string.Empty, y ?? string.Empty, comparison))
                list.Add(new Change(property, $"{property}: '{x}' -> '{y}'"));
        }

        private static void DictionaryDiff(List<Change> list, IDictionary<string, string> a, IDictionary<string, string> b)
        {
            if (a == null) a = new Dictionary<string, string>();
            if (b == null) b = new Dictionary<string, string>();

            foreach (var key in a.Keys.Union(b.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            {
                a.TryGetValue(key, out var va);
                b.TryGetValue(key, out var vb);

                if (string.Equals(va, vb, StringComparison.Ordinal))
                    continue;

                var left = va ?? "(not set)";
                var right = vb ?? "(not set)";
                list.Add(new Change(key, key + ": '" + left + "' -> '" + right + "'"));
            }
        }

        private static void SetDiff(List<Change> list, string property, IEnumerable<string> a, IEnumerable<string> b)
        {
            var sa = new HashSet<string>(a ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var sb = new HashSet<string>(b ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);

            foreach (var item in sa.Where(i => !sb.Contains(i)))
                list.Add(new Change(property, $"{property} '{item}' exists only in target -> it will be REMOVED."));

            foreach (var item in sb.Where(i => !sa.Contains(i)))
                list.Add(new Change(property, $"{property} '{item}' exists only in source -> it will be ADDED."));
        }

        private static string StableName(string id, string name)
            => string.IsNullOrWhiteSpace(id) ? name : id;

        private static bool XmlEquivalent(string x, string y)
        {
            if (string.IsNullOrWhiteSpace(x) && string.IsNullOrWhiteSpace(y)) return true;
            if (string.IsNullOrWhiteSpace(x) || string.IsNullOrWhiteSpace(y)) return false;
            return string.Equals(XmlNormalizer.Normalize(x), XmlNormalizer.Normalize(y), StringComparison.Ordinal);
        }

        private static Difference Diff(string componentType, string componentName, string entityName, DifferenceType type, string details, string property = null)
        {
            return new Difference
            {
                ComponentType = componentType,
                ComponentName = componentName,
                EntityName = entityName,
                Type = type,
                Property = property,
                Details = details
            };
        }
    }

}


