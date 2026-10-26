using McTools.Xrm.Connection;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using SolutionComponentsAnalyzer.Core.Models;
using SolutionComponentsAnalyzer.Core.Providers;
using SolutionComponentsAnalyzer.Core.Services;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using XrmToolBox.Extensibility;
using Label = System.Windows.Forms.Label;

namespace SolutionComponentsAnalyzer.XrmToolBox
{
    /// <summary>
    /// Same design as the desktop app. The difference: XrmToolBox makes the connections.
    ///   Current environment  = the tool's normal XrmToolBox connection (Service)
    ///   Incoming environment = a second connection added with "Connect other environment..."
    ///                          (if you do not add one, both sides use the current environment)
    /// </summary>
    public sealed class SolutionConflictFinderControl : MultipleConnectionsPluginControlBase
    {
        private readonly ConnectedEnvironmentPanel _current = new ConnectedEnvironmentPanel("1. Current environment  (receives the import)", null);

        private readonly ConnectedEnvironmentPanel _incoming = new ConnectedEnvironmentPanel("2. Incoming environment  (solution to import)", "Connect other environment...");

        private readonly ResultsView _results = new ResultsView();
        private readonly Label _status = new Label();

        // null = no second environment was added: the incoming side uses the current environment
        private IOrganizationService _incomingService;
        private string _incomingName;

        public SolutionConflictFinderControl()
        {
            BuildUi();
            Load += (s, e) => OnControlLoad();
        }

        // -------------------------- UI --------------------------
        private void BuildUi()
        {
            Dock = DockStyle.Fill;
            Font = new Font("Segoe UI", 9F);

            var environments = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 160,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(6, 6, 6, 0)
            };
            environments.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            environments.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            _current.Dock = DockStyle.Fill;
            _incoming.Dock = DockStyle.Fill;
            environments.Controls.Add(_current, 0, 0);
            environments.Controls.Add(_incoming, 1, 0);

            _current.ReloadClicked += (s, e) => ReloadEverything();
            _incoming.ReloadClicked += (s, e) => ReloadEverything();
            _incoming.ActionClicked += (s, e) => AddAdditionalOrganization();   // XrmToolBox connection dialog

            _results.CompareRequested += (s, e) => ExecuteMethod(CompareSolutions);
            _results.StatusMessage += SetStatus;

            _status.Dock = DockStyle.Bottom;
            _status.Height = 26;
            _status.Padding = new Padding(8, 5, 8, 5);
            _status.Text = "Ready";

            // Dock order matters in WinForms: the Fill control is added first, edges after it
            Controls.Add(_results);
            Controls.Add(environments);
            Controls.Add(_status);
        }

        // ------------------------------- connections ------------------------------- 
        private void OnControlLoad()
        {
            RefreshCurrentLabel();
            RefreshIncomingConnection();

            if (Service != null)
                ExecuteMethod(LoadAllSolutions);
        }

        //Called by XrmToolBox when the main connection is made or changed
        public override void UpdateConnection(IOrganizationService newService, ConnectionDetail detail, string actionName, object parameter)
        {
            base.UpdateConnection(newService, detail, actionName, parameter);

            RefreshCurrentLabel();
            RefreshIncomingConnection();
            LoadAllSolutions();
        }

        // Called by XrmToolBox when a second environment is added or removed.
        // If this line does not compile with your XrmToolBox package version, just delete the method:
        // the "Reload solutions" button does the same job.
        protected override void ConnectionDetailsUpdated(NotifyCollectionChangedEventArgs e)
        {
            RefreshIncomingConnection();
            LoadAllSolutions();
        }

        private void RefreshCurrentLabel()
        {
            if (ConnectionDetail != null)
                _current.SetEnvironment(ConnectionDetail.ConnectionName, true);
            else
                _current.SetEnvironment("Not connected - use the Connect button of XrmToolBox", false);
        }

        private void RefreshIncomingConnection()
        {
            var detail = AdditionalConnectionDetails != null ? AdditionalConnectionDetails.LastOrDefault() : null;

            if (detail == null)
            {
                _incomingService = null;
                _incomingName = null;
                _incoming.SetEnvironment("Same as current (press 'Connect other environment...' to change)", false);
                return;
            }

            _incomingService = detail.GetCrmServiceClient();
            _incomingName = detail.ConnectionName;
            _incoming.SetEnvironment(detail.ConnectionName, true);
        }

        private void ReloadEverything()
        {
            RefreshCurrentLabel();
            RefreshIncomingConnection();
            ExecuteMethod(LoadAllSolutions);
        }

        // ------------------------------ solutions ------------------------------ 
        private void LoadAllSolutions()
        {
            if (Service == null)
                return;

            var currentService = Service;
            var incomingService = _incomingService ?? Service;
            var sameEnvironment = ReferenceEquals(incomingService, currentService);

            SetBusy(true);

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Loading solutions...",
                Work = (worker, args) =>
                {
                    var current = QuerySolutions(currentService);
                    var incoming = sameEnvironment ? current : QuerySolutions(incomingService);

                    args.Result = new List<SolutionChoice>[] { current, incoming };
                },
                PostWorkCallBack = args =>
                {
                    SetBusy(false);

                    if (args.Error != null)
                    {
                        ShowErrorDialog(args.Error, "Loading solutions");
                        return;
                    }

                    var lists = (List<SolutionChoice>[])args.Result;
                    _current.SetSolutions(lists[0]);
                    _incoming.SetSolutions(lists[1]);

                    SetStatus(lists[0].Count + " solution(s) in current, " + lists[1].Count + " in incoming.");
                }
            });
        }

        private static List<SolutionChoice> QuerySolutions(IOrganizationService service)
        {
            var query = new QueryExpression("solution")
            {
                ColumnSet = new ColumnSet("solutionid", "uniquename", "friendlyname", "version", "createdon")
            };
            query.Criteria.AddCondition("isvisible", ConditionOperator.Equal, true);
            query.AddOrder("createdon", OrderType.Descending);

            return service.RetrieveMultiple(query).Entities
                .Select(x => new SolutionChoice
                {
                    Id = x.Id,
                    UniqueName = x.GetAttributeValue<string>("uniquename") ?? string.Empty,
                    FriendlyName = x.GetAttributeValue<string>("friendlyname") ?? string.Empty,
                    Version = x.GetAttributeValue<string>("version") ?? string.Empty
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.UniqueName))
                .ToList();
        }

        // ------------------------------ compare ------------------------------ 
        private void CompareSolutions()
        {
            var currentSolution = _current.SelectedSolution;
            var incomingSolution = _incoming.SelectedSolution;

            if (currentSolution == null || incomingSolution == null)
            {
                MessageBox.Show(this, "Choose a solution from the list in both boxes.", "Solution Conflict Finder", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var currentService = Service;
            var incomingService = _incomingService ?? Service;
            var sameEnvironment = ReferenceEquals(incomingService, currentService);

            if (sameEnvironment && string.Equals(currentSolution.UniqueName, incomingSolution.UniqueName, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this,
                    "Both sides are the same solution in the same environment. Choose two different solutions, or add another environment.",
                    "Solution Conflict Finder", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var currentLabel = ConnectionDetail != null ? ConnectionDetail.ConnectionName : "Current";
            var incomingLabel = _incomingName ?? currentLabel;

            // these names appear in the result messages, so include the solution too
            var currentName = currentLabel + " [" + currentSolution.UniqueName + "]";
            var incomingName = incomingLabel + " [" + incomingSolution.UniqueName + "]";

            var started = DateTime.Now;
            SetBusy(true);

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Reading both solutions (large solutions can take several minutes)...",
                Work = (worker, args) =>
                {
                    var currentProvider = new DataverseSolutionProvider(currentService);
                    var incomingProvider = new DataverseSolutionProvider(incomingService);

                    SolutionDefinition current;
                    SolutionDefinition incoming;

                    if (sameEnvironment)
                    {
                        // one connection object must not be used by two threads at once
                        current = currentProvider.GetSolution(currentSolution.UniqueName);
                        incoming = incomingProvider.GetSolution(incomingSolution.UniqueName);
                    }
                    else
                    {
                        // two different connections: read both environments at the same time
                        var readCurrent = Task.Run(() => currentProvider.GetSolution(currentSolution.UniqueName));
                        var readIncoming = Task.Run(() => incomingProvider.GetSolution(incomingSolution.UniqueName));

                        try
                        {
                            Task.WaitAll(readCurrent, readIncoming);
                        }
                        catch (AggregateException ex)
                        {
                            throw ex.Flatten().InnerExceptions.First();   // show the real error, not "One or more errors occurred"
                        }

                        current = readCurrent.Result;
                        incoming = readIncoming.Result;
                    }

                    args.Result = new SolutionComparisonService().Compare(current, incoming, currentName, incomingName);
                },
                PostWorkCallBack = args =>
                {
                    SetBusy(false);

                    if (args.Error != null)
                    {
                        ShowErrorDialog(args.Error, "Comparing solutions");
                        return;
                    }

                    var result = args.Result as ComparisonResult;
                    if (result == null)
                    {
                        SetStatus("No comparison result.");
                        return;
                    }

                    _results.ShowResult(result);
                    SetStatus("Done in " + (DateTime.Now - started).ToString(@"mm\:ss") + " - " + result.Differences.Count + " difference(s).");
                }
            });
        }

        // -------------------------------- helpers -------------------------------- 
        private void SetBusy(bool busy)
        {
            _current.SetBusy(busy);
            _incoming.SetBusy(busy);
            _results.SetBusy(busy);
        }

        private void SetStatus(string text)
        {
            _status.Text = text;
        }
    }
}
