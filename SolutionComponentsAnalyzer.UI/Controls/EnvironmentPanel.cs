using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using SolutionComponentsAnalyzer.UI.Connection;
using SolutionComponentsAnalyzer.UI.Infrastructure;
using SolutionComponentsAnalyzer.UI.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Label = System.Windows.Forms.Label;

namespace SolutionComponentsAnalyzer.UI.Controls
{
    /// <summary>
    /// One environment: URL + user name + password, a Connect button, then a list of its solutions.
    /// </summary>
    public class EnvironmentPanel : UserControl
    {
        private readonly string _settingsKey;

        private readonly TextBox _url = new TextBox();
        private readonly TextBox _user = new TextBox();
        private readonly TextBox _password = new TextBox();
        private readonly Button _connectButton = new Button();
        private readonly Label _status = new Label();
        private readonly ComboBox _solutions = new ComboBox();
        private readonly ToolTip _toolTip = new ToolTip();

        private IOrganizationService _service;
        private string _environmentLabel = string.Empty;
        private string _environmentKey = string.Empty;

        public EnvironmentPanel(string title, string settingsKey)
        {
            _settingsKey = settingsKey;

            BuildUi(title);

            // remembered from the last run (the password is never saved)
            _url.Text = UserSettings.Get(settingsKey + ".url");
            _user.Text = UserSettings.Get(settingsKey + ".user");

        }

        // ------------------------------------------------------------------ public API
        public bool IsConnected
        {
            get { return _service != null; }
        }

        public IOrganizationService Service
        {
            get { return _service; }
        }

        /// <summary>The solution chosen in the list, or null if nothing valid is chosen.</summary>
        public SolutionChoice SelectedSolution
        {
            get { return _solutions.SelectedItem as SolutionChoice; }
        }

        /// <summary>Short display name, e.g. crm.contoso.com/OrgName</summary>
        public string EnvironmentLabel
        {
            get { return _environmentLabel; }
        }

        /// <summary>Identifies URL + user, so the form can tell if both sides are the same environment.</summary>
        public string EnvironmentKey
        {
            get { return _environmentKey; }
        }

        /// <summary>Locks the inputs while a long operation is running.</summary>
        public void SetBusy(bool busy)
        {
            _url.Enabled = !busy;
            _user.Enabled = !busy;
            _password.Enabled = !busy;
            _connectButton.Enabled = !busy;
            _solutions.Enabled = !busy && _service != null;
        }

        // ------------------------------------------------------------------ UI
        private void BuildUi(string title)
        {
            var group = new GroupBox
            {
                Text = title,
                Dock = DockStyle.Fill,
                Padding = new Padding(10, 6, 10, 10),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (var i = 0; i < 5; i++)
                table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _password.UseSystemPasswordChar = true;
            _password.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    _connectButton.PerformClick();
                }
            };

            _toolTip.SetToolTip(_url, "Organization URL, e.g. https://crm.contoso.com/OrgName");
            _toolTip.SetToolTip(_user, "User name, e.g. DOMAIN\\user");

            AddRow(table, 0, "URL", _url);
            AddRow(table, 1, "User name", _user);
            AddRow(table, 2, "Password", _password);

            _connectButton.Text = "Connect";
            _connectButton.AutoSize = true;
            _connectButton.MinimumSize = new Size(90, 27);
            _connectButton.Click += OnConnectClick;

            _status.AutoSize = true;
            _status.Text = "Not connected";
            _status.ForeColor = SystemColors.GrayText;
            _status.Margin = new Padding(10, 7, 0, 0);

            var connectRow = new FlowLayoutPanel
            {
                AutoSize = true,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Padding(0, 3, 0, 3)
            };
            connectRow.Controls.Add(_connectButton);
            connectRow.Controls.Add(_status);
            table.Controls.Add(connectRow, 1, 3);

            // typing filters the list (prefix match on the unique name)
            _solutions.DropDownStyle = ComboBoxStyle.DropDown;
            _solutions.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            _solutions.AutoCompleteSource = AutoCompleteSource.ListItems;
            _solutions.Enabled = false;
            AddRow(table, 4, "Solution", _solutions);

            group.Controls.Add(table);
            Controls.Add(group);
        }

        private static void AddRow(TableLayoutPanel table, int row, string caption, Control input)
        {
            var label = new Label
            {
                Text = caption,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 6, 10, 6)
            };

            input.Dock = DockStyle.Fill;
            input.Margin = new Padding(0, 3, 0, 3);

            table.Controls.Add(label, 0, row);
            table.Controls.Add(input, 1, row);
        }

        private void SetStatus(string text, Color color)
        {
            _status.Text = text;
            _status.ForeColor = color;
        }

        // ------------------------------------------------------------------ connecting
        private async void OnConnectClick(object sender, EventArgs e)
        {
            var url = _url.Text.Trim();
            var user = _user.Text.Trim();
            var password = _password.Text;

            if (url.Length == 0 || user.Length == 0 || password.Length == 0)
            {
                SetStatus("Enter URL, user name and password.", Color.Firebrick);
                return;
            }

            SetBusy(true);
            SetStatus("Connecting...", SystemColors.ControlText);

            try
            {
                // everything that talks to the server runs on a background thread, so the window never freezes
                var result = await Task.Run(() =>
                {
                    var service = DataverseConnectionFactory.CreateService(url, user, password);
                    DataverseConnectionFactory.TestConnection(service);

                    return new ConnectResult
                    {
                        Service = service,
                        Solutions = QuerySolutions(service)
                    };
                });

                DisposeService();
                _service = result.Service;
                _environmentLabel = DataverseConnectionFactory.GetEnvironmentLabel(url);
                _environmentKey = (DataverseConnectionFactory.GetBaseUrl(url) + "|" + user).ToLowerInvariant();

                _solutions.Items.Clear();
                _solutions.Items.AddRange(result.Solutions.Cast<object>().ToArray());
                _solutions.SelectedIndex = -1;
                _solutions.Text = string.Empty;

                UserSettings.Set(_settingsKey + ".url", url);
                UserSettings.Set(_settingsKey + ".user", user);

                SetStatus("Connected - " + result.Solutions.Count + " solution(s)", Color.SeaGreen);
            }
            catch (Exception ex)
            {
                SetStatus("Connection failed", Color.Firebrick);
                MessageBox.Show(
                    FindForm(),
                    ErrorFormatter.GetMessage(ex),
                    "Could not connect to " + url,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
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

        private void DisposeService()
        {
            var disposable = _service as IDisposable;
            _service = null;

            if (disposable == null)
                return;

            try
            {
                disposable.Dispose();
            }
            catch (Exception)
            {
                // closing an already broken channel can throw; nothing to do about it
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                DisposeService();

            base.Dispose(disposing);
        }

        private sealed class ConnectResult
        {
            public IOrganizationService Service { get; set; }
            public List<SolutionChoice> Solutions { get; set; }
        }
    }
}
