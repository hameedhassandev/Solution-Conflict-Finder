using System;
using System.Collections.Generic;
using System.Text;

namespace SolutionComponentsAnalyzer.Core.Helpers
{
    public class KnownType
    {
        public KnownType(string typeName, string table, string nameColumn, params string[] fingerprintColumns)
        {
            TypeName = typeName;
            Table = table;
            NameColumn = nameColumn;
            FingerprintColumns = fingerprintColumns;
        }

        public string TypeName { get; private set; }
        public string Table { get; private set; }
        public string NameColumn { get; private set; }
        public string[] FingerprintColumns { get; private set; }
    }

    public static class KnownTypes
    {
        // Component types handled generically: name + (optional) content fingerprint.
        public static readonly Dictionary<int, KnownType> All = new Dictionary<int, KnownType>
        {
            { 20,  new KnownType("Security Role",              "role",                          "name") },
            { 31,  new KnownType("Report",                     "report",                        "name", "bodytext") },
            { 36,  new KnownType("Email Template",             "template",                      "title", "subject", "body") },
            { 44,  new KnownType("Duplicate Detection Rule",   "duplicaterule",                 "name", "baseentityname", "matchingentityname") },
            { 59,  new KnownType("Chart",                      "savedqueryvisualization",       "name", "datadescription", "presentationdescription") },
            { 62,  new KnownType("Site Map",                   "sitemap",                       "sitemapname", "sitemapxml") },
            { 80,  new KnownType("Model-driven App",           "appmodule",                     "name") },
            { 90,  new KnownType("Plugin Type",                "plugintype",                    "typename") },
            { 93,  new KnownType("Plugin Step Image",          "sdkmessageprocessingstepimage", "name", "imagetype", "attributes", "entityalias", "messagepropertyname") },
            { 300, new KnownType("Canvas App",                 "canvasapp",                     "displayname") },
            { 380, new KnownType("Environment Variable",       "environmentvariabledefinition", "schemaname", "defaultvalue", "type") },
            { 381, new KnownType("Environment Variable Value", "environmentvariablevalue",      "schemaname", "value") },
            { 63,  new KnownType("Connection Role",        "connectionrole",       "name") },
            { 66,  new KnownType("Custom Control",         "customcontrol",        "name", "manifest") },
            { 70,  new KnownType("Field Security Profile", "fieldsecurityprofile", "name") },
            { 95,  new KnownType("Service Endpoint",       "serviceendpoint",      "name", "contract", "messageformat", "path") },
            { 150, new KnownType("Routing Rule",           "routingrule",          "name") },
            { 152, new KnownType("SLA",                    "sla",                  "name") },
            { 154, new KnownType("Convert Rule",           "convertrule",          "name") },
            { 161, new KnownType("Mobile Offline Profile", "mobileofflineprofile", "name") },
            { 165, new KnownType("Similarity Rule",        "similarityrule",       "name") },
            { 208, new KnownType("Import Map",             "importmap",            "name") },
            { 371, new KnownType("Connector",              "connector",            "name") },
            { 372, new KnownType("Connector",              "connector",            "name") },
        };
    }
}
