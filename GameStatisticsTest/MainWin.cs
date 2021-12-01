using System;
using System.Windows.Forms;
using GameStatistics;

/*
 * Class defines the main window used by the Game Statistics tester to
 * manipulate the statistics and verify usage and processing.
 * 
 * Author:  M. G. Slack
 * Written: 2014-03-15
 * 
 */
namespace GameStatisticsTest
{
    public partial class MainWin : Form
    {
        private const string TEST_REG_NAME = @"HKEY_CURRENT_USER\Software\Slack and Associates\Games\TesterStats";

        private Statistics stats = new Statistics(TEST_REG_NAME);
        private Random rnd = new Random();

        public MainWin()
        {
            InitializeComponent();
        }

        private void MainWin_Load(object sender, EventArgs e)
        {
            stats.GameName = "TesterStats";
        }

        private void StartBtn_Click(object sender, EventArgs e)
        {
            StartBtn.Enabled = false;
            EndBtn.Enabled = true;

            DateTime dt = new DateTime(DateTime.Now.Ticks - rnd.Next(500000));
            int moves = rnd.Next(1500);

            stats.StartGame(false);
            stats.StartTime.Subtract(dt);

            for (int i = 0; i < moves; i++) stats.MoveMade();

            if (rnd.Next(100) >= 50)
                stats.IncCustomStatistic("MyStatistic");
            else
                stats.DecCustomStatistic("MyStatistic");

            stats.SaveStatistics(); // test aborted (not finished) games
        }

        private void EndBtn_Click(object sender, EventArgs e)
        {
            EndBtn.Enabled = false;
            StartBtn.Enabled = true;

            int nn = rnd.Next(100);
            if (nn > 66)
                stats.GameWon(rnd.Next(150));
            else if (nn > 33)
                stats.GameLost(rnd.Next(140));
            else
                stats.GameTied();
        }

        private void ShowBtn_Click(object sender, EventArgs e)
        {
            stats.ShowStatistics(this);
        }

        private void ResetBtn_Click(object sender, EventArgs e)
        {
            if (stats.ResetStatistics()) {
                MessageBox.Show("Statistics reset back to initial state.");
                EndBtn.Enabled = false;
                StartBtn.Enabled = true;
            }
            else
                MessageBox.Show("Statistics not reset!");
        }

        private void SaveBtn_Click(object sender, EventArgs e)
        {
            if (SaveDlg.ShowDialog(this) == DialogResult.OK)
                stats.SaveStatisticsToFile(SaveDlg.FileName, true);
        }

        private void ExitBtn_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
