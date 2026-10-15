using SolutionComponentsAnalyzer.Core.Models;
using SolutionComponentsAnalyzer.Core.Providers;
using SolutionComponentsAnalyzer.Core.Services;
using SolutionComponentsAnalyzer.UI.Controls;
using SolutionComponentsAnalyzer.UI.Infrastructure;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SolutionComponentsAnalyzer.UI.Forms
{
    public class ComparerForm : Form
    {
        private const string ResultConflict = "Conflict";
        private const string ResultReview = "Review";
        private const string ResultAdded = "Will be added";
        private const string ResultOnlyCurrent = "Only in current";

        private const string Legend =
            "Conflict          : the component exists in BOTH environments and differs. Importing the incoming solution will OVERWRITE the current one." + "\r\n" +
            "Review            : exists in both, but this component type cannot be compared in detail. Check it manually." + "\r\n" +
            "Will be added     : exists only in the incoming solution. The import adds it to the current environment." + "\r\n" +
            "Only in current   : exists only in the current environment. The import does not touch it.";

        private readonly EnvironmentPanel _current = new EnvironmentPanel("1. Current environment  (receives the import)", "current");
        private readonly EnvironmentPanel _incoming = new EnvironmentPanel("2. Incoming environment  (solution to import)", "incoming");

        private readonly Button _compareButton = new Button();
        private readonly Button _exportButton = new Button();
        private readonly TextBox _filterBox = new TextBox();
        private readonly Label _summary = new Label();
        private readonly TabControl _tabs = new TabControl();
        private readonly TextBox _details = new TextBox();
        private readonly StatusStrip _statusStrip = new StatusStrip();
        private readonly ToolStripStatusLabel _statusLabel = new ToolStripStatusLabel();
        private readonly ToolStripProgressBar _progress = new ToolStripProgressBar();

        private readonly List<ResultTab> _resultTabs = new List<ResultTab>();

        private ComparisonResult _lastResult;
        private bool _busy;

        public ComparerForm()
        {
            BuildUi();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Only ask when the user is closing the window, not when Windows is shutting down
            if (e.CloseReason == CloseReason.UserClosing)
            {
                var message = _busy
                    ? "A comparison is still running. Are you sure you want to close the application?"
                    : "Are you sure you want to close the application?";

                var answer = MessageBox.Show(
                    this,
                    message,
                    "Solution Conflict Finder",
                    MessageBoxButtons.YesNo,
                    _busy ? MessageBoxIcon.Warning : MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2);   // "No" is the default, so Enter doesn't close by accident

                if (answer != DialogResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
            }

            base.OnFormClosing(e);
        }

        // ------------------------------UI------------------------------------ 
        private void BuildUi()
        {
            Text = "Solution Conflict Finder";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1300, 820);
            MinimumSize = new Size(1000, 650);
            Font = new Font("Segoe UI", 9F);

            // --- the two environments, side by side
            var environments = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 215,
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

            // --- toolbar
            _compareButton.Text = "Compare";
            _compareButton.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            _compareButton.Size = new Size(110, 32);
            _compareButton.Click += OnCompareClick;

            _exportButton.Text = "Export CSV...";
            _exportButton.Size = new Size(110, 32);
            _exportButton.Enabled = false;
            _exportButton.Click += (s, e) => ExportCsv();

            var filterLabel = new Label
            {
                Text = "Filter:",
                AutoSize = true,
                Margin = new Padding(18, 9, 4, 0)
            };

            _filterBox.Width = 240;
            _filterBox.Margin = new Padding(0, 6, 0, 0);
            _filterBox.TextChanged += (s, e) => ApplyFilter();

            _summary.AutoSize = true;
            _summary.Margin = new Padding(24, 9, 0, 0);
            _summary.Font = new Font("Segoe UI", 9.5F);
            _summary.Text = "Connect both environments, choose a solution in each, then press Compare.";

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 46,
                Padding = new Padding(10, 6, 6, 4),
                WrapContents = false
            };
            toolbar.Controls.Add(_compareButton);
            toolbar.Controls.Add(_exportButton);
            toolbar.Controls.Add(filterLabel);
            toolbar.Controls.Add(_filterBox);
            toolbar.Controls.Add(_summary);

            // --- result tabs (one table per kind of difference)
            AddResultTab("Conflicts", d => d.Type == DifferenceType.Conflict);
            AddResultTab("Review", d => d.Type == DifferenceType.ExistsInBoth);
            AddResultTab("Will be added", d => d.Type == DifferenceType.OnlyInSource);
            AddResultTab("Only in current", d => d.Type == DifferenceType.OnlyInTarget);
            AddResultTab("All", d => true);

            _tabs.Dock = DockStyle.Fill;
            _tabs.SelectedIndexChanged += (s, e) => ShowSelectedDetails();

            // --- details of the selected row
            _details.Dock = DockStyle.Bottom;
            _details.Height = 100;
            _details.Multiline = true;
            _details.ReadOnly = true;
            _details.ScrollBars = ScrollBars.Vertical;
            _details.BackColor = SystemColors.Window;
            _details.Font = new Font("Consolas", 9F);
            _details.Text = Legend;

            // --- status bar
            _progress.Style = ProgressBarStyle.Marquee;
            _progress.MarqueeAnimationSpeed = 30;
            _progress.Visible = false;
            _statusLabel.Spring = true;
            _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            _statusLabel.Text = "Ready";
            _statusStrip.Items.Add(_statusLabel);
            _statusStrip.Items.Add(_progress);

            Controls.Add(_tabs);
            Controls.Add(_details);
            Controls.Add(toolbar);
            Controls.Add(environments);
            Controls.Add(_statusStrip);

            Controls.Add(new Label
            {
                Text = "Created by Hameed Hassan",
                Dock = DockStyle.Bottom,
                Height = 22,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = SystemColors.GrayText
            });
        }

        private void AddResultTab(string title, Func<Difference, bool> match)
        {
            var grid = CreateGrid();

            var page = new TabPage(title);
            page.Controls.Add(grid);
            _tabs.TabPages.Add(page);

            _resultTabs.Add(new ResultTab
            {
                Title = title,
                Match = match,
                Page = page,
                Grid = grid
            });
        }

        private DataGridView CreateGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoGenerateColumns = false,
                RowHeadersVisible = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.LightGray,
                BorderStyle = BorderStyle.None,
                // only measure the rows on screen, so thousands of rows stay fast
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCells,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
            };

            grid.Columns.Add(CreateColumn("Result", "Result", 110, false));
            grid.Columns.Add(CreateColumn("Type", "Component type", 140, false));
            grid.Columns.Add(CreateColumn("Entity", "Entity", 130, false));
            grid.Columns.Add(CreateColumn("Component", "Component", 230, false));
            grid.Columns.Add(CreateColumn("Property", "Property", 120, false));
            grid.Columns.Add(CreateColumn("Details", "Details", 300, true));

            grid.CellFormatting += OnGridCellFormatting;
            grid.SelectionChanged += OnGridSelectionChanged;

            return grid;
        }

        private static DataGridViewTextBoxColumn CreateColumn(string name, string header, int width, bool fill)
        {
            var column = new DataGridViewTextBoxColumn
            {
                Name = name,
                DataPropertyName = name,
                HeaderText = header,
                SortMode = DataGridViewColumnSortMode.Automatic
            };

            if (fill)
            {
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                column.MinimumWidth = width;
                column.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            }
            else
            {
                column.Width = width;
            }

            return column;
        }

        // ---------------------------compare--------------------------------------- 
        private async void OnCompareClick(object sender, EventArgs e)
        {
            await CompareAsync();
        }

        private async Task CompareAsync()
        {
            if (_busy)
                return;

            if (!_current.IsConnected || !_incoming.IsConnected)
            {
                Warn("Connect to both environments first (press Connect in each box).");
                return;
            }

            var currentSolution = _current.SelectedSolution;
            var incomingSolution = _incoming.SelectedSolution;

            if (currentSolution == null || incomingSolution == null)
            {
                Warn("Choose a solution from the list in both environments.");
                return;
            }

            var sameEnvironment = _current.EnvironmentKey == _incoming.EnvironmentKey;
            if (sameEnvironment &&
                string.Equals(currentSolution.UniqueName, incomingSolution.UniqueName, StringComparison.OrdinalIgnoreCase))
            {
                Warn("Both sides are the same solution in the same environment. Choose two different solutions or environments.");
                return;
            }

            // these names appear in the result messages, so include the solution too
            var currentName = _current.EnvironmentLabel + " [" + currentSolution.UniqueName + "]";
            var incomingName = _incoming.EnvironmentLabel + " [" + incomingSolution.UniqueName + "]";

            var currentProvider = new DataverseSolutionProvider(_current.Service);
            var incomingProvider = new DataverseSolutionProvider(_incoming.Service);

            SetBusy(true, "Reading both solutions (large solutions can take several minutes)...");
            var watch = Stopwatch.StartNew();

            try
            {
                // each environment has its own connection, so both are read at the same time
                var readCurrent = Task.Run(() => currentProvider.GetSolution(currentSolution.UniqueName));
                var readIncoming = Task.Run(() => incomingProvider.GetSolution(incomingSolution.UniqueName));
                await Task.WhenAll(readCurrent, readIncoming);

                SetStatus("Comparing...");
                var current = readCurrent.Result;
                var incoming = readIncoming.Result;

                var result = await Task.Run(() =>
                    new SolutionComparisonService().Compare(current, incoming, currentName, incomingName));

                ShowResult(result);

                watch.Stop();
                SetBusy(false, "Done in " + watch.Elapsed.ToString(@"mm\:ss") + " - " + result.Differences.Count + " difference(s).");
            }
            catch (Exception ex)
            {
                SetBusy(false, "Comparison failed.");
                MessageBox.Show(
                    this,
                    ErrorFormatter.GetMessage(ex) + Environment.NewLine + Environment.NewLine +
                    "If this keeps failing, press Connect again to create a fresh connection.",
                    "Comparison failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void SetBusy(bool busy, string message)
        {
            _busy = busy;

            _current.SetBusy(busy);
            _incoming.SetBusy(busy);
            _compareButton.Enabled = !busy;
            _exportButton.Enabled = !busy && _lastResult != null;
            _progress.Visible = busy;
            Cursor = busy ? Cursors.AppStarting : Cursors.Default;

            SetStatus(message);
        }

        private void SetStatus(string text)
        {
            _statusLabel.Text = text;
        }

        private void Warn(string message)
        {
            MessageBox.Show(this, message, "Solution Conflict Finder", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ---------------------------results--------------------------------------- 
        private void ShowResult(ComparisonResult result)
        {
            _lastResult = result;

            foreach (var tab in _resultTabs)
            {
                tab.Table = BuildTable(result.Differences.Where(tab.Match));
                tab.Grid.DataSource = tab.Table.DefaultView;
                tab.Page.Text = tab.Title + " (" + tab.Table.Rows.Count + ")";
            }

            ApplyFilter();

            var conflicts = result.Conflicts.Count();
            _summary.ForeColor = conflicts > 0 ? Color.Firebrick : Color.SeaGreen;
            _summary.Text =
                "Conflicts: " + conflicts +
                "     Review: " + result.NeedsReview.Count() +
                "     Will be added: " + result.WillBeAdded.Count() +
                "     Only in current: " + result.UntouchedInTarget.Count();

            // open the most important tab that has something in it
            var first = _resultTabs.FirstOrDefault(t => t.Table.Rows.Count > 0 && t.Title != "All");
            _tabs.SelectedTab = first != null ? first.Page : _resultTabs[0].Page;

            ShowSelectedDetails();
        }

        private static DataTable BuildTable(IEnumerable<Difference> differences)
        {
            var table = new DataTable();
            table.Columns.Add("Result", typeof(string));
            table.Columns.Add("Type", typeof(string));
            table.Columns.Add("Entity", typeof(string));
            table.Columns.Add("Component", typeof(string));
            table.Columns.Add("Property", typeof(string));
            table.Columns.Add("Details", typeof(string));

            foreach (var d in differences)
            {
                table.Rows.Add(
                    ResultLabel(d.Type),
                    d.ComponentType ?? string.Empty,
                    d.EntityName ?? string.Empty,
                    d.ComponentName ?? string.Empty,
                    d.Property ?? string.Empty,
                    d.Details ?? string.Empty);
            }

            return table;
        }

        private static string ResultLabel(DifferenceType type)
        {
            switch (type)
            {
                case DifferenceType.Conflict: return ResultConflict;
                case DifferenceType.ExistsInBoth: return ResultReview;
                case DifferenceType.OnlyInSource: return ResultAdded;
                default: return ResultOnlyCurrent;
            }
        }

        private static Color ResultColor(string label)
        {
            switch (label)
            {
                case ResultConflict: return Color.MistyRose;
                case ResultReview: return Color.LightGoldenrodYellow;
                case ResultAdded: return Color.Honeydew;
                default: return Color.White;
            }
        }

        private void OnGridCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0)
                return;

            var grid = (DataGridView)sender;
            var row = grid.Rows[e.RowIndex].DataBoundItem as DataRowView;
            if (row == null)
                return;

            e.CellStyle.BackColor = ResultColor(Convert.ToString(row["Result"]));
        }

        // ------------------------------------------------------------------ filter + details
        private void ApplyFilter()
        {
            var text = _filterBox.Text.Trim();
            var filter = string.Empty;

            if (text.Length > 0)
            {
                var like = "'%" + EscapeLike(text) + "%'";
                filter =
                    "[Type] LIKE " + like +
                    " OR [Entity] LIKE " + like +
                    " OR [Component] LIKE " + like +
                    " OR [Property] LIKE " + like +
                    " OR [Details] LIKE " + like;
            }

            foreach (var tab in _resultTabs)
            {
                if (tab.Table != null)
                    tab.Table.DefaultView.RowFilter = filter;
            }
        }

        /// <summary>Escapes a value for use inside a DataView LIKE expression.</summary>
        private static string EscapeLike(string value)
        {
            var builder = new StringBuilder();

            foreach (var c in value)
            {
                switch (c)
                {
                    case '\'': builder.Append("''"); break;
                    case '[': builder.Append("[[]"); break;
                    case ']': builder.Append("[]]"); break;
                    case '*': builder.Append("[*]"); break;
                    case '%': builder.Append("[%]"); break;
                    default: builder.Append(c); break;
                }
            }

            return builder.ToString();
        }

        private ResultTab CurrentTab()
        {
            return _resultTabs.FirstOrDefault(t => t.Page == _tabs.SelectedTab);
        }

        private void OnGridSelectionChanged(object sender, EventArgs e)
        {
            var tab = CurrentTab();
            if (tab != null && ReferenceEquals(sender, tab.Grid))
                ShowSelectedDetails();
        }

        private void ShowSelectedDetails()
        {
            var tab = CurrentTab();
            if (tab == null || tab.Grid.CurrentRow == null)
                return;

            var row = tab.Grid.CurrentRow.DataBoundItem as DataRowView;
            if (row == null)
                return;

            _details.Text = string.Join(Environment.NewLine, new[]
            {
                "Result    : " + row["Result"],
                "Type      : " + row["Type"],
                "Entity    : " + row["Entity"],
                "Component : " + row["Component"],
                "Property  : " + row["Property"],
                "Details   : " + row["Details"]
            });
        }

        // --------------------------export---------------------------------------- 
        private void ExportCsv()
        {
            if (_lastResult == null)
                return;

            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "CSV file (*.csv)|*.csv";
                dialog.FileName = "solution-conflicts-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".csv";

                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                try
                {
                    var builder = new StringBuilder();
                    builder.AppendLine("Result,Component type,Entity,Component,Property,Details");

                    foreach (var d in _lastResult.Differences)
                    {
                        builder.AppendLine(string.Join(",", new[]
                        {
                            Csv(ResultLabel(d.Type)),
                            Csv(d.ComponentType),
                            Csv(d.EntityName),
                            Csv(d.ComponentName),
                            Csv(d.Property),
                            Csv(d.Details)
                        }));
                    }

                    // UTF-8 with BOM so Excel shows Arabic and other non-ASCII names correctly
                    File.WriteAllText(dialog.FileName, builder.ToString(), new UTF8Encoding(true));
                    SetStatus("Exported to " + dialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ErrorFormatter.GetMessage(ex), "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static string Csv(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
        }

        // ------------------------------------------------------------------ helper type
        private sealed class ResultTab
        {
            public string Title { get; set; }
            public Func<Difference, bool> Match { get; set; }
            public TabPage Page { get; set; }
            public DataGridView Grid { get; set; }
            public DataTable Table { get; set; }
        }
    }
}
