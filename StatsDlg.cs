using System;
using System.Windows.Forms;

/*
 * Class used by the Game Statistics class to implement a statistics
 * display dialog. Dialog is used internally by the game Statistics
 * class when calling ShowStatistics.
 * 
 * Author:  M. G. Slack
 * Written: 2014-03-15
 * 
 * ----------------------------------------------------------------------------
 * 
 * Revised: yyyy-mm-dd - XXXX.
 * 
 */
namespace GameStatistics
{
    public partial class StatsDlg : Form
    {
        private string _stats = "";
        public string Stats { set { _stats = value; } }

        private bool _resetStats = false;
        public bool ResetStats { get { return _resetStats; } }

        public StatsDlg()
        {
            InitializeComponent();
        }

        private void StatsDlg_Load(object sender, EventArgs e)
        {
            if (String.IsNullOrEmpty(_stats))
                tbStats.Text = "No statistics to display.";
            else
                tbStats.Text = _stats;
        }

        private void ResetBtn_Click(object sender, EventArgs e)
        {
            string msg = "Are you sure you want to reset the statistics?" + Environment.NewLine +
                "Note: statistics will not be saved till a new game is started.";

            if (MessageBox.Show(msg, this.Text, MessageBoxButtons.YesNo,
                                MessageBoxIcon.Question) == DialogResult.Yes) {
                _resetStats = true;
            }
        }
    }
}
