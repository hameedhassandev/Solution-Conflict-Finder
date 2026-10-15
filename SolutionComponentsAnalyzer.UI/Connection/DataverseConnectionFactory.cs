using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using System;
using System.Net;
using System.ServiceModel.Description;

namespace SolutionComponentsAnalyzer.UI.Connection
{
    /// <summary>
    /// Same approach as your Test console app: URL + user name + password through OrganizationServiceProxy
    /// (on-premises / IFD style authentication).
    /// </summary>
    public static class DataverseConnectionFactory
    {
        public static IOrganizationService CreateService(string organizationUrl, string username, string password)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            var credentials = new ClientCredentials();
            credentials.UserName.UserName = username;
            credentials.UserName.Password = password;

            var proxy = new OrganizationServiceProxy(
                new Uri(GetOrganizationServiceUrl(organizationUrl)),
                null,
                credentials,
                null);

            // reading big solutions (web resources, assemblies) can take longer than the 2 minute default
            proxy.Timeout = TimeSpan.FromMinutes(10);
            proxy.EnableProxyTypes();

            return proxy;
        }

        /// <summary>Makes a real call so wrong credentials / URLs fail at "Connect", not later.</summary>
        public static void TestConnection(IOrganizationService service)
        {
            service.Execute(new WhoAmIRequest());
        }

        /// <summary>https://host/Org  (accepts input with or without scheme or /XRMServices/... suffix)</summary>
        public static string GetBaseUrl(string organizationUrl)
        {
            var url = (organizationUrl ?? string.Empty).Trim();

            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }

            var index = url.IndexOf("/XRMServices", StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
                url = url.Substring(0, index);

            return url.TrimEnd('/');
        }

        public static string GetOrganizationServiceUrl(string organizationUrl)
        {
            return GetBaseUrl(organizationUrl) + "/XRMServices/2011/Organization.svc";
        }

        /// <summary>Short name shown in the UI and in the result messages (host, plus org name if present).</summary>
        public static string GetEnvironmentLabel(string organizationUrl)
        {
            try
            {
                var uri = new Uri(GetBaseUrl(organizationUrl));
                var path = uri.AbsolutePath.Trim('/');
                return path.Length == 0 ? uri.Host : uri.Host + "/" + path;
            }
            catch (UriFormatException)
            {
                return organizationUrl;
            }
        }
    }
}
