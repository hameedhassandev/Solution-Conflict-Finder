using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SolutionComponentsAnalyzer.XrmToolBox
{
    /// <summary>
    /// Same look as the desktop environment box, but the connection itself is made by XrmToolBox,
    /// so there is no URL / password here: just the connection name, optional buttons and the solution list.
    /// </summary>
    internal sealed class ConnectedEnvironmentPanel : UserControl
    {
        private readonly Label _environment = new Label();
        private readonly ComboBox _solutions = new ComboBox();
        private readonly Button _actionButton = new Button();
        private readonly Button _reloadButton = new Button();

        public ConnectedEnvironmentPanel(string title, string actionButtonText)
        {
            BuildUi(title, actionButtonText);
        }

        /// <summary>The optional first button (for example "Connect other environment...").</summary>
        public event EventHandler ActionClicked;

        public event EventHandler ReloadClicked;

        /// <summary>The solution chosen in the list, or null if nothing valid is chosen.</summary>
        public SolutionChoice SelectedSolution
        {
            get { return _solutions.SelectedItem as SolutionChoice; }
        }

        public void SetEnvironment(string text, bool connected)
        {
            _environment.Text = text;
            _environment.ForeColor = connected ? Color.SeaGreen : SystemColors.GrayText;
        }

        public void SetSolutions(IList<SolutionChoice> choices)
        {
            _solutions.Items.Clear();
            _solutions.Items.AddRange(choices.Cast<object>().ToArray());
            _solutions.SelectedIndex = -1;
            _solutions.Text = string.Empty;
            _solutions.Enabled = choices.Count > 0;
        }

        public void ClearSolutions()
        {
            SetSolutions(new List<SolutionChoice>());
        }

        public void SetBusy(bool busy)
        {
            _actionButton.Enabled = !busy;
            _reloadButton.Enabled = !busy;
            _solutions.Enabled = !busy && _solutions.Items.Count > 0;
        }

        // ------------------------------------------------------------------ UI
        private void BuildUi(string title, string actionButtonText)
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
                RowCount = 3,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (var i = 0; i < 3; i++)
                table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // row 0: connection name
            _environment.AutoSize = false;
            _environment.AutoEllipsis = true;
            _environment.Height = 22;
            _environment.TextAlign = ContentAlignment.MiddleLeft;
            _environment.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            AddRow(table, 0, "Environment", _environment);

            // row 1: buttons
            var buttons = new FlowLayoutPanel
            {
                AutoSize = true,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Padding(0, 3, 0, 3)
            };

            if (!string.IsNullOrEmpty(actionButtonText))
            {
                _actionButton.Text = actionButtonText;
                _actionButton.AutoSize = true;
                _actionButton.MinimumSize = new Size(90, 27);
                _actionButton.Click += (s, e) =>
                {
                    var handler = ActionClicked;
                    if (handler != null)
                        handler(this, EventArgs.Empty);
                };
                buttons.Controls.Add(_actionButton);
            }

            _reloadButton.Text = "Reload solutions";
            _reloadButton.AutoSize = true;
            _reloadButton.MinimumSize = new Size(90, 27);
            _reloadButton.Click += (s, e) =>
            {
                var handler = ReloadClicked;
                if (handler != null)
                    handler(this, EventArgs.Empty);
            };
            buttons.Controls.Add(_reloadButton);
            table.Controls.Add(buttons, 1, 1);

            // row 2: solution list (typing filters it, prefix match on the unique name)
            _solutions.DropDownStyle = ComboBoxStyle.DropDown;
            _solutions.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            _solutions.AutoCompleteSource = AutoCompleteSource.ListItems;
            _solutions.Enabled = false;
            AddRow(table, 2, "Solution", _solutions);

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
    }
}
