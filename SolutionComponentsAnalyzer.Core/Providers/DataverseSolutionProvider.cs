using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using SolutionComponentsAnalyzer.Core.Comparers;
using SolutionComponentsAnalyzer.Core.Helpers;
using SolutionComponentsAnalyzer.Core.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace SolutionComponentsAnalyzer.Core.Providers
{
    public class DataverseSolutionProvider : ISolutionProvider
    {
        private readonly IOrganizationService _service;
        private HashSet<int> IgnoredComponentTypes { get; } = new HashSet<int>(ComponentTypes.Ignored);
        public DataverseSolutionProvider(IOrganizationService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public SolutionDefinition GetSolution(string uniqueName)
        {
            var solution = GetSolutionRecord(uniqueName)
                ?? throw new InvalidOperationException("Solution '" + uniqueName + "' was not found.");

            var result = new SolutionDefinition
            {
                UniqueName = solution.GetAttributeValue<string>("uniquename") ?? string.Empty,
                FriendlyName = solution.GetAttributeValue<string>("friendlyname") ?? string.Empty,
                Version = solution.GetAttributeValue<string>("version") ?? string.Empty
            };

            LoadComponents(solution.Id, result);
            return result;
        }

        // ------------------------------------------------------------------
        // Querying
        // ------------------------------------------------------------------
        private Entity GetSolutionRecord(string uniqueName)
        {
            var query = new QueryExpression("solution")
            {
                ColumnSet = new ColumnSet("solutionid", "uniquename", "friendlyname", "version")
            };
            query.Criteria.AddCondition("uniquename", ConditionOperator.Equal, uniqueName);
            return _service.RetrieveMultiple(query).Entities.FirstOrDefault();
        }

        private List<Entity> GetSolutionComponents(Guid solutionId)
        {
            var query = new QueryExpression("solutioncomponent")
            {
                ColumnSet = new ColumnSet("objectid", "componenttype", "rootsolutioncomponentid", "rootcomponentbehavior")
            };
            query.Criteria.AddCondition("solutionid", ConditionOperator.Equal, solutionId);
            return RetrieveAll(query);
        }

        // RetrieveMultiple returns max 5000 rows: page it.
        private List<Entity> RetrieveAll(QueryExpression query)
        {
            var result = new List<Entity>();
            query.PageInfo = new PagingInfo { PageNumber = 1, Count = 5000, PagingCookie = null };

            while (true)
            {
                var page = _service.RetrieveMultiple(query);
                result.AddRange(page.Entities);
                if (!page.MoreRecords) break;

                query.PageInfo.PageNumber++;
                query.PageInfo.PagingCookie = page.PagingCookie;
            }

            return result;
        }

        // ------------------------------------------------------------------
        // Components
        // ------------------------------------------------------------------
        private void LoadComponents(Guid solutionId, SolutionDefinition solution)
        {
            var components = GetSolutionComponents(solutionId);

            // attributes that were added to the solution one by one
            var attributeIds = new HashSet<Guid>(
                components
                    .Where(x => GetComponentType(x) == ComponentTypes.Attribute)
                    .Select(GetObjectId)
                    .Where(x => x != Guid.Empty));

            foreach (var component in components)
            {
                var type = GetComponentType(component);
                var objectId = GetObjectId(component);

                if (objectId == Guid.Empty)
                    continue;

                switch (type)
                {
                    case ComponentTypes.Entity:
                        // rootcomponentbehavior: 0 = include all subcomponents, 1 = do not include, 2 = shell only
                        var behavior = component.GetAttributeValue<OptionSetValue>("rootcomponentbehavior")?.Value ?? 0;
                        LoadEntity(objectId, attributeIds, behavior == 0, solution);
                        break;

                    case ComponentTypes.SystemForm:
                        LoadSystemForm(objectId, solution); 
                        break;
                    case ComponentTypes.SavedQuery: 
                        LoadSavedQuery(objectId, solution); 
                        break;
                    case ComponentTypes.WebResource: 
                        LoadWebResource(objectId, solution); 
                        break;
                    case ComponentTypes.PluginAssembly: 
                        LoadPluginAssembly(objectId, solution); 
                        break;
                    case ComponentTypes.PluginStep: 
                        LoadPluginStep(objectId, solution); 
                        break;
                    case ComponentTypes.Workflow: 
                        LoadWorkflow(objectId, solution); 
                        break;
                    case ComponentTypes.GlobalOptionSet: 
                        LoadOptionSet(objectId, solution); 
                        break;

                    case ComponentTypes.Attribute:
                        // loaded from EntityMetadata in LoadEntity
                        break;

                    default:
                        LoadOtherComponent(type, objectId, solution);
                        break;
                }
            }
        }

        private void LoadEntity(Guid metadataId, HashSet<Guid> solutionAttributeIds, bool includeAllCustomAttributes, SolutionDefinition solution)
        {
            var request = new RetrieveEntityRequest
            {
                MetadataId = metadataId,
                EntityFilters = EntityFilters.Entity | EntityFilters.Attributes,
                RetrieveAsIfPublished = false
            };

            var metadata = ((RetrieveEntityResponse)_service.Execute(request)).EntityMetadata;

            if (metadata == null || string.IsNullOrWhiteSpace(metadata.LogicalName))
                return;

            var entity = GetOrCreateEntity(solution, metadata.LogicalName);
            if (string.IsNullOrEmpty(entity.DisplayName))
                entity.DisplayName = GetLocalizedLabel(metadata.DisplayName);

            foreach (var attribute in metadata.Attributes ?? Array.Empty<AttributeMetadata>())
            {
                if (attribute == null || string.IsNullOrWhiteSpace(attribute.LogicalName))
                    continue;

                // virtual companion columns (xxxname, xxxyominame...) are not real fields
                if (!string.IsNullOrEmpty(attribute.AttributeOf))
                    continue;

                var listedInSolution = attribute.MetadataId.HasValue && solutionAttributeIds.Contains(attribute.MetadataId.Value);

                // When the entity is added with "include all subcomponents", Dataverse does NOT list
                // each attribute as a solutioncomponent row - so the old filter dropped every field.
                var included = listedInSolution || (includeAllCustomAttributes && attribute.IsCustomAttribute == true);
                if (!included)
                    continue;

                if (entity.Fields.Any(x => x.LogicalName.Equals(attribute.LogicalName, StringComparison.OrdinalIgnoreCase)))
                    continue;

                entity.Fields.Add(new FieldDefinition
                {
                    LogicalName = attribute.LogicalName,
                    SchemaName = attribute.SchemaName ?? string.Empty,
                    DisplayName = GetLocalizedLabel(attribute.DisplayName),
                    AttributeType = attribute.AttributeTypeName?.Value ?? attribute.AttributeType?.ToString() ?? string.Empty,
                    RequiredLevel = attribute.RequiredLevel?.Value.ToString() ?? string.Empty,
                    Properties = ReadProperties(attribute)
                });
            }
        }

        // Type-specific settings (min/max, precision, max length, formats, options, lookup targets...).
        // Stored as key/value pairs so the comparer can diff ANY of them without per-property code.
        private static Dictionary<string, string> ReadProperties(AttributeMetadata attribute)
        {
            var p = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // InvariantCulture so 1.5 vs 1,5 is never reported as a conflict
            void Add(string key, object value)
            {
                if (value == null)
                {
                    p[key] = string.Empty;
                    return;
                }

                var formattable = value as IFormattable;
                p[key] = formattable != null
                    ? formattable.ToString(null, CultureInfo.InvariantCulture)
                    : (value.ToString() ?? string.Empty);
            }

            void AddOptions(OptionSetMetadata set)
            {
                foreach (var option in set?.Options ?? new OptionMetadataCollection())
                    p["Option[" + option.Value + "]"] = GetLocalizedLabel(option.Label);
            }

            // common to every field
            Add("IsAuditEnabled", attribute.IsAuditEnabled?.Value);
            Add("IsSecured", attribute.IsSecured);
            Add("Description", GetLocalizedLabel(attribute.Description));

            switch (attribute)
            {
                case IntegerAttributeMetadata i:
                    Add("MinValue", i.MinValue); Add("MaxValue", i.MaxValue); Add("Format", i.Format);
                    break;

                case BigIntAttributeMetadata b:
                    Add("MinValue", b.MinValue); Add("MaxValue", b.MaxValue);
                    break;

                case DecimalAttributeMetadata d:
                    Add("MinValue", d.MinValue); Add("MaxValue", d.MaxValue); Add("Precision", d.Precision);
                    break;

                case DoubleAttributeMetadata db:
                    Add("MinValue", db.MinValue); Add("MaxValue", db.MaxValue); Add("Precision", db.Precision);
                    break;

                case MoneyAttributeMetadata m:
                    Add("MinValue", m.MinValue); Add("MaxValue", m.MaxValue);
                    Add("Precision", m.Precision); Add("PrecisionSource", m.PrecisionSource);
                    break;

                case StringAttributeMetadata s:
                    Add("MaxLength", s.MaxLength); Add("Format", s.FormatName?.Value);
                    break;

                case MemoAttributeMetadata memo:
                    Add("MaxLength", memo.MaxLength); Add("Format", memo.Format);
                    break;

                case DateTimeAttributeMetadata dt:
                    Add("Format", dt.Format); Add("DateTimeBehavior", dt.DateTimeBehavior?.Value);
                    break;

                case BooleanAttributeMetadata bo:
                    Add("DefaultValue", bo.DefaultValue);
                    Add("TrueLabel", GetLocalizedLabel(bo.OptionSet?.TrueOption?.Label));
                    Add("FalseLabel", GetLocalizedLabel(bo.OptionSet?.FalseOption?.Label));
                    break;

                case PicklistAttributeMetadata pick:
                    Add("DefaultFormValue", pick.DefaultFormValue);
                    Add("OptionSetName", pick.OptionSet?.Name);
                    AddOptions(pick.OptionSet);
                    break;

                case MultiSelectPicklistAttributeMetadata multi:
                    Add("OptionSetName", multi.OptionSet?.Name);
                    AddOptions(multi.OptionSet);
                    break;

                case StatusAttributeMetadata status:
                    AddOptions(status.OptionSet);
                    break;

                case StateAttributeMetadata state:
                    AddOptions(state.OptionSet);
                    break;

                case LookupAttributeMetadata l:
                    Add("Targets", string.Join(",", (l.Targets ?? Array.Empty<string>()).OrderBy(x => x)));
                    break;
            }

            return p;
        }

        private void LoadSystemForm(Guid formId, SolutionDefinition solution)
        {
            var form = _service.Retrieve("systemform", formId,
                new ColumnSet("formid", "name", "objecttypecode", "formxml", "type"));

            var formXml = form.GetAttributeValue<string>("formxml");
            if (string.IsNullOrWhiteSpace(formXml))
                return;

            var entity = GetOrCreateEntity(solution, form.GetAttributeValue<string>("objecttypecode"));
            var definition = ParseFormXml(formXml, form.Id, form.GetAttributeValue<string>("name"));

            if (!entity.Forms.Any(x => x.Id.Equals(definition.Id, StringComparison.OrdinalIgnoreCase)))
                entity.Forms.Add(definition);
        }

        private void LoadSavedQuery(Guid viewId, SolutionDefinition solution)
        {
            var view = _service.Retrieve("savedquery", viewId,
                new ColumnSet("savedqueryid", "name", "returnedtypecode", "fetchxml", "layoutxml"));

            var entity = GetOrCreateEntity(solution, view.GetAttributeValue<string>("returnedtypecode"));

            var definition = new ViewDefinition
            {
                Id = view.Id.ToString("D"),
                Name = view.GetAttributeValue<string>("name") ?? string.Empty,
                FetchXml = view.GetAttributeValue<string>("fetchxml") ?? string.Empty,
                LayoutXml = view.GetAttributeValue<string>("layoutxml") ?? string.Empty
            };

            ParseViewColumns(definition);

            if (!entity.Views.Any(x => x.Id.Equals(definition.Id, StringComparison.OrdinalIgnoreCase)))
                entity.Views.Add(definition);
        }

        private void LoadWebResource(Guid webResourceId, SolutionDefinition solution)
        {
            var resource = _service.Retrieve("webresource", webResourceId,
                new ColumnSet("webresourceid", "name", "webresourcetype", "content"));

            var bytes = DecodeContent(resource.GetAttributeValue<string>("content"));
            var name = resource.GetAttributeValue<string>("name") ?? string.Empty;
            var isText = IsTextResource(name);

            string text = null;
            if (isText)
            {
                // Normalize BOM + line endings so a CRLF/LF difference is not reported as a conflict.
                text = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF').Replace("\r\n", "\n");
                bytes = Encoding.UTF8.GetBytes(text);
            }

            solution.WebResources.Add(new WebResourceDefinition
            {
                Name = name,
                Type = resource.GetAttributeValue<OptionSetValue>("webresourcetype")?.Value.ToString() ?? string.Empty,
                Hash = ComputeSha256(bytes),
                IsText = isText,
                Content = text
            });
        }

        private void LoadPluginAssembly(Guid assemblyId, SolutionDefinition solution)
        {
            var assembly = _service.Retrieve("pluginassembly", assemblyId,
                new ColumnSet("name", "version", "publickeytoken", "content"));

            var bytes = DecodeContent(assembly.GetAttributeValue<string>("content"));

            solution.PluginAssemblies.Add(new PluginAssemblyDefinition
            {
                Name = assembly.GetAttributeValue<string>("name") ?? string.Empty,
                Version = assembly.GetAttributeValue<string>("version") ?? string.Empty,
                PublicKeyToken = assembly.GetAttributeValue<string>("publickeytoken") ?? string.Empty,
                Hash = ComputeSha256(bytes)
            });
        }

        private void LoadPluginStep(Guid stepId, SolutionDefinition solution)
        {
            var step = _service.Retrieve("sdkmessageprocessingstep", stepId,
                new ColumnSet("name", "stage", "mode", "rank", "filteringattributes", "configuration", "statecode"));

            solution.PluginSteps.Add(new PluginStepDefinition
            {
                Id = step.Id.ToString("D"),
                Name = step.GetAttributeValue<string>("name") ?? string.Empty,
                Stage = step.GetAttributeValue<OptionSetValue>("stage")?.Value.ToString() ?? string.Empty,
                Mode = step.GetAttributeValue<OptionSetValue>("mode")?.Value.ToString() ?? string.Empty,
                Rank = step.GetAttributeValue<int>("rank").ToString(),
                FilteringAttributes = step.GetAttributeValue<string>("filteringattributes") ?? string.Empty,
                Configuration = step.GetAttributeValue<string>("configuration") ?? string.Empty,
                State = step.GetAttributeValue<OptionSetValue>("statecode")?.Value.ToString() ?? string.Empty
            });
        }

        private void LoadWorkflow(Guid workflowId, SolutionDefinition solution)
        {
            var workflow = _service.Retrieve("workflow", workflowId,
                new ColumnSet("workflowid", "name", "category", "statecode", "primaryentity", "xaml", "clientdata", "uniquename"));

            var category = workflow.GetAttributeValue<OptionSetValue>("category")?.Value.ToString() ?? string.Empty;
            var name = workflow.GetAttributeValue<string>("name") ?? string.Empty;
            var primaryEntity = workflow.GetAttributeValue<string>("primaryentity") ?? string.Empty;
            var xaml = workflow.GetAttributeValue<string>("xaml") ?? string.Empty;
            var clientData = workflow.GetAttributeValue<string>("clientdata") ?? string.Empty;
            var state = workflow.GetAttributeValue<OptionSetValue>("statecode")?.Value.ToString() ?? string.Empty;

            // Category: 0 workflow, 1 dialog, 2 business rule, 3 action, 4 BPF, 5 modern flow
            if (category == "4")
            {
                var bpf = new BpfDefinition
                {
                    Id = workflow.Id.ToString("D"),
                    Name = name,
                    PrimaryEntity = primaryEntity,
                    // stage/step details live in xaml + clientdata
                    Definition = xaml + "\n" + clientData
                };

                LoadBpfStages(workflowId, bpf);
                solution.BusinessProcessFlows.Add(bpf);
                return;
            }

            solution.Workflows.Add(new WorkflowDefinition
            {
                Id = workflow.Id.ToString("D"),
                Name = name,
                Entity = primaryEntity,
                Category = category,
                State = state,
                // business rules / classic workflows use xaml, modern flows use clientdata
                Definition = !string.IsNullOrWhiteSpace(xaml) ? xaml : clientData
            });
        }

        private void LoadBpfStages(Guid workflowId, BpfDefinition bpf)
        {
            var query = new QueryExpression("processstage")
            {
                ColumnSet = new ColumnSet("processstageid", "stagename")
            };
            query.Criteria.AddCondition("processid", ConditionOperator.Equal, workflowId);

            foreach (var stage in RetrieveAll(query).OrderBy(x => x.GetAttributeValue<string>("stagename")))
            {
                bpf.Stages.Add(new BpfStageDefinition
                {
                    Name = stage.GetAttributeValue<string>("stagename") ?? string.Empty,
                    Steps = new List<string>()   // step-level differences are caught by BpfDefinition.Definition
                });
            }
        }

        private void LoadOptionSet(Guid metadataId, SolutionDefinition solution)
        {
            try
            {
                var response = (RetrieveOptionSetResponse)_service.Execute(
                    new RetrieveOptionSetRequest { MetadataId = metadataId });

                var metadata = response.OptionSetMetadata as OptionSetMetadata;
                if (metadata == null)
                {
                    LoadOtherComponent(ComponentTypes.GlobalOptionSet, metadataId, solution); 
                    return;
                }

                var definition = new OptionSetDefinition
                {
                    Id = metadataId.ToString("D"),
                    Name = metadata.Name ?? string.Empty,
                    DisplayName = GetLocalizedLabel(metadata.DisplayName)
                };

                foreach (var option in metadata.Options)
                    definition.Options["Option[" + option.Value + "]"] = GetLocalizedLabel(option.Label);

                solution.OptionSets.Add(definition);
            }
            catch (Exception)
            {
                //review manually"
                LoadOtherComponent(ComponentTypes.GlobalOptionSet, metadataId, solution);
            }
        }
        // Other component types: high-level info only
        private void LoadOtherComponent(int type, Guid objectId, SolutionDefinition solution)
        {
            if (IgnoredComponentTypes.Contains(type))
                return;

            var item = new GenericComponentDefinition
            {
                ComponentTypeCode = type,
                ComponentTypeName = "Component type " + type,
                ObjectId = objectId.ToString("D")
            };

            KnownType known;
            if (KnownTypes.All.TryGetValue(type, out known))
            {
                item.ComponentTypeName = known.TypeName;

                try
                {
                    var columns = new List<string>(known.FingerprintColumns);
                    if (!columns.Contains(known.NameColumn))
                        columns.Add(known.NameColumn);

                    var record = _service.Retrieve(known.Table, objectId, new ColumnSet(columns.ToArray()));

                    item.Name = record.GetAttributeValue<string>(known.NameColumn) ?? string.Empty;

                    if (known.FingerprintColumns.Length > 0)
                        item.Fingerprint = ComputeFingerprint(record, known.FingerprintColumns);
                }
                catch (Exception)
                {
                    // Unreadable (permissions, table not available...): still reported, as "exists in both".
                    item.Name = "(could not be read)";
                    item.Fingerprint = string.Empty;
                }
            }

            solution.OtherComponents.Add(item);
        }

        private static string ComputeFingerprint(Entity record, string[] columns)
        {
            var builder = new StringBuilder();

            foreach (var column in columns)
            {
                object value;
                record.Attributes.TryGetValue(column, out value);
                builder.Append(column).Append('=').Append(NormalizeValue(value)).Append('\u001f');
            }

            return ComputeSha256(Encoding.UTF8.GetBytes(builder.ToString()));
        }

        private static string NormalizeValue(object value)
        {
            if (value == null)
                return string.Empty;

            var text = value as string;
            if (text != null)
            {
                var trimmed = text.Trim();
                return trimmed.StartsWith("<", StringComparison.Ordinal)
                    ? XmlNormalizer.Normalize(trimmed)
                    : trimmed.Replace("\r\n", "\n");
            }

            var option = value as OptionSetValue;
            if (option != null)
                return option.Value.ToString(CultureInfo.InvariantCulture);

            var reference = value as EntityReference;
            if (reference != null)
                return reference.Id.ToString("D");

            var formattable = value as IFormattable;
            if (formattable != null)
                return formattable.ToString(null, CultureInfo.InvariantCulture);

            return value.ToString();
        }

        // ------------------------------------------------------------------
        // Parsing
        // ------------------------------------------------------------------
        private static FormDefinition ParseFormXml(string formXml, Guid formId, string name)
        {
            var document = XDocument.Parse(formXml);      // no PreserveWhitespace: whitespace is not a conflict
            var form = document.Root;

            var definition = new FormDefinition
            {
                Id = formId.ToString("D"),
                Name = name ?? string.Empty,
                NormalizedXml = XmlNormalizer.Normalize(formXml)
            };

            if (form == null)
                return definition;

            foreach (var tab in form.Descendants().Where(x => IsNamed(x, "tab")))
            {
                var tabDefinition = new TabDefinition { Name = GetAttribute(tab, "name"), Label = GetLabel(tab) };

                foreach (var section in tab.Descendants().Where(x => IsNamed(x, "section")))
                {
                    var sectionDefinition = new SectionDefinition { Name = GetAttribute(section, "name"), Label = GetLabel(section) };
                    ReadControls(section, sectionDefinition);
                    tabDefinition.Sections.Add(sectionDefinition);
                }

                definition.Tabs.Add(tabDefinition);
            }

            // header and footer controls are outside any <tab>
            AddPseudoTab(definition, form, "header");
            AddPseudoTab(definition, form, "footer");

            // JS libraries + event handlers: the #1 source of form conflicts
            foreach (var lib in form.Descendants().Where(x => IsNamed(x, "Library")))
                definition.FormLibraries.Add(GetAttribute(lib, "name"));

            foreach (var ev in form.Descendants().Where(x => IsNamed(x, "event")))
            {
                foreach (var handler in ev.Descendants().Where(x => IsNamed(x, "Handler")))
                {
                    definition.EventHandlers.Add(string.Join("|",
                        GetAttribute(ev, "name"),
                        GetAttribute(ev, "attribute"),
                        GetAttribute(handler, "libraryName"),
                        GetAttribute(handler, "functionName"),
                        "enabled=" + GetAttribute(handler, "enabled"),
                        "params=" + GetAttribute(handler, "parameters")));
                }
            }

            return definition;
        }

        private static void AddPseudoTab(FormDefinition definition, XElement form, string elementName)
        {
            var element = form.Elements().FirstOrDefault(x => IsNamed(x, elementName));
            if (element == null) return;

            var section = new SectionDefinition { Name = "[" + elementName + "]", Label = elementName };
            ReadControls(element, section);

            var tab = new TabDefinition { Name = "[" + elementName + "]", Label = elementName };
            tab.Sections.Add(section);
            definition.Tabs.Add(tab);
        }

        private static void ReadControls(XElement scope, SectionDefinition section)
        {
            foreach (var control in scope.Descendants().Where(x => IsNamed(x, "control")))
            {
                // In FormXml "visible" normally sits on the parent <cell>, not on <control>
                var cell = control.Ancestors().FirstOrDefault(x => IsNamed(x, "cell"));
                var visible = GetAttribute(control, "visible");
                if (string.IsNullOrEmpty(visible))
                    visible = GetAttribute(cell, "visible");

                section.Controls.Add(new ControlDefinition
                {
                    Id = GetAttribute(control, "id"),
                    FieldName = GetAttribute(control, "datafieldname"),
                    ControlType = GetAttribute(control, "classid"),
                    Visible = ParseNullableBool(visible),
                    Disabled = ParseNullableBool(GetAttribute(control, "disabled"))
                });
            }
        }

        private static void ParseViewColumns(ViewDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(definition.LayoutXml))
                return;

            try
            {
                var document = XDocument.Parse(definition.LayoutXml);
                foreach (var cell in document.Descendants().Where(x => IsNamed(x, "cell")))
                {
                    var name = GetAttribute(cell, "name");
                    if (!string.IsNullOrWhiteSpace(name))
                        definition.Columns.Add(name);
                }
            }
            catch
            {
                // raw LayoutXml is still compared
            }
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------
        private static EntityDefinition GetOrCreateEntity(SolutionDefinition solution, string logicalName)
        {
            // dashboards use objecttypecode "none"; don't crash on empty values
            if (string.IsNullOrWhiteSpace(logicalName))
                logicalName = "none";

            var existing = solution.Entities.FirstOrDefault(x =>
                x.LogicalName.Equals(logicalName, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
                return existing;

            var entity = new EntityDefinition { LogicalName = logicalName };
            solution.Entities.Add(entity);
            return entity;
        }

        private static int GetComponentType(Entity component)
            => component.GetAttributeValue<OptionSetValue>("componenttype")?.Value ?? 0;

        private static Guid GetObjectId(Entity component)
            => component.GetAttributeValue<Guid>("objectid");

        private static string GetLocalizedLabel(Label label)
            => label?.UserLocalizedLabel?.Label
               ?? label?.LocalizedLabels?.FirstOrDefault()?.Label
               ?? string.Empty;

        private static bool IsNamed(XElement element, string localName)
            => element.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase);

        private static string GetAttribute(XElement element, string name)
            => element?.Attribute(name)?.Value ?? string.Empty;

        private static string GetLabel(XElement element)
        {
            var label = element.Elements().FirstOrDefault(x => IsNamed(x, "labels"))?
                .Descendants().FirstOrDefault(x => IsNamed(x, "label"));
            return label?.Attribute("description")?.Value ?? string.Empty;
        }

        private static bool? ParseNullableBool(string value)
            => bool.TryParse(value, out var parsed) ? (bool?)parsed : null;

        // Dataverse always returns web resource / assembly content as base64.
        // The old "fallback to UTF8" could silently hash the wrong thing.
        private static byte[] DecodeContent(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return Array.Empty<byte>();

            return Convert.FromBase64String(content);
        }

        private static bool IsTextResource(string fileName)
        {
            var ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
            return ext == ".js" || ext == ".css" || ext == ".html" || ext == ".htm"
                || ext == ".xml" || ext == ".json" || ext == ".txt" || ext == ".svg"
                || ext == ".resx" || ext == ".xsl" || ext == ".xslt";
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
            }
        }
    }

}
