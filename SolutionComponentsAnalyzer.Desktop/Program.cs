using SolutionComponentsAnalyzer.UI.Forms;
using System;
using System.Windows.Forms;

namespace SolutionComponentsAnalyzer.Desktop
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // a bug in a button handler should show a message, not close the whole tool
            Application.ThreadException += (sender, e) =>
                MessageBox.Show(e.Exception.ToString(), "Unexpected error", MessageBoxButtons.OK, MessageBoxIcon.Error);

            Application.Run(new ComparerForm());
        }
    }
}
