using SolutionComponentsAnalyzer.Core.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace SolutionComponentsAnalyzer.XrmToolBox
{
    /// <summary>
    /// The results part of the design: toolbar (Compare / Export / Filter / summary), the result tabs,
    /// the details box and the author line. This is the XrmToolBox plugin's OWN copy (the desktop app has a separate one), so changing one never affects the other.
    ///
    /// The host does the reading/comparing and then calls ShowResult(...).
    /// </summary>
    internal sealed class ResultsView : UserControl
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

        private readonly Button _compareButton = new Button();
        private readonly Button _exportButton = new Button();
        private readonly TextBox _filterBox = new TextBox();
        private readonly Label _summary = new Label();
        private readonly TabControl _tabs = new TabControl();
        private readonly TextBox _details = new TextBox();

        private readonly List<ResultTab> _resultTabs = new List<ResultTab>();

        private ComparisonResult _lastResult;

        public ResultsView()
        {
            BuildUi();
        }

        // ------------------------------- public API ----------------------------------- 
        //Raised when the user presses the Compare button. The host starts the comparison
        public event EventHandler CompareRequested;

        //Short status text (for example "Exported to ..."). The host can show it in its status bar
        public event Action<string> StatusMessage;

        public bool HasResult
        {
            get { return _lastResult != null; }
        }

        //Disables Compare / Export while the host is working
        public void SetBusy(bool busy)
        {
            _compareButton.Enabled = !busy;
            _exportButton.Enabled = !busy && _lastResult != null;
        }

        public void ShowResult(ComparisonResult result)
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

            _exportButton.Enabled = true;
            ShowSelectedDetails();
        }

        // ------------------------------------------------------------------ UI
        private void BuildUi()
        {
            Dock = DockStyle.Fill;
            Font = new Font("Segoe UI", 9F);

            // --- toolbar
            _compareButton.Text = "Compare";
            _compareButton.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            _compareButton.Size = new Size(110, 32);
            _compareButton.Click += (s, e) =>
            {
                var handler = CompareRequested;
                if (handler != null)
                    handler(this, EventArgs.Empty);
            };

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
            _summary.Text = "Choose a solution on each side, then press Compare.";

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

            // Dock order matters in WinForms: the Fill control is added first, edges after it
            // (the control added last is laid out first, i.e. ends up outermost).
            Controls.Add(_tabs);
            Controls.Add(_details);
            Controls.Add(toolbar);
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
                BackgroundColor = Color.White,
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

        // ------------------------------------------------------------------ results
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

        // ------------------------------------------------------------------ export
        private void ExportCsv()
        {
            if (_lastResult == null)
                return;

            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "CSV file (*.csv)|*.csv";
                dialog.FileName = "solution-conflicts-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".csv";

                if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
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

                    var handler = StatusMessage;
                    if (handler != null)
                        handler("Exported to " + dialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(FindForm(), ErrorFormatter.GetMessage(ex), "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
