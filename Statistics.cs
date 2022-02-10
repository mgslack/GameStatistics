using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

/*
 * A class defined to collect and process game statistics and persist them
 * between game runs. Mainly used for card like games, but could be used for
 * other games that statistics make sense for.
 * NOTE: The AppRegName parameter passed to the constructor must contain the
 * root path for the key ("HKEY_CURRENT_USER..." or "HKEY_LOCAL_MACHINE..."
 * should be the only ones used).
 * 
 * Statistics kept are:
 * - Games Started.
 * - Games Won.
 * - Games Lost.
 * - Games Tied.
 * - Games Unfinished - derived value (started - won - lost - tied).
 * - Highest Score.
 * - Moves Made in Current Game (not persisted).
 * - Least Number of Moves (winning game only).
 * - Most Number of Moves (winning game only).
 * - Quickest Winning Time (TimeSpan).
 * - Played Last (date and time).
 * - Won Last (date and time).
 * The move and high score statistics can be ignored (unused). Move statistics
 * can be ignored by not calling 'MoveMade()' during the game. Highest score
 * statistic can be ignored by passing in 0 for the 'curScore' parameter to
 * GameWon or GameLost methods.
 * 
 * The game name (GameName) property can be set to the name of the game the
 * statistics are being tracked for. This is an optional property (defaulting
 * to blank), but will allow the name of the game to be displayed with the
 * statistics being tracked.
 * 
 * Custom statistics can be created and used (only int values). These are saved
 * and manipulated like the built-in statistics. Custom statistics can be
 * incremented, decremented, set to specific value or cleared (removed). The
 * name provided to track custom statistics under should be descriptive. Also,
 * custom statistics are only persisted if the AppRegName key is rooted to
 * HKEY_CURRENT_USER or HKEY_LOCAL_MACHINE.
 * 
 * Author:  M. G. Slack
 * Written: 2014-03-14
 * Version: 1.0.4.0
 * 
 * ----------------------------------------------------------------------------
 * 
 * Revised: 2014-04-20 - Added an additional 'StartGame' method to initialize
 *                       all but GamesStarted statistics. Added a 'GameDone()'
 *                       call to finalize tracking similar to 'GameWon()' or
 *                       'GameLost()'.
 *          2021-12-01 - Added call for 'tie' game (GameTied()).
 *          2021-12-10 - Tweaked unfinished stat to take account of tied games.
 *                       Also, determined there was a 'bug' in the code to
 *                       determine least moves, it would zero out the existing
 *                       least moves value if a game in auto mode didn't save
 *                       moves made by a user.  Extracted common code from 
 *                       GameWon and GameDone methods to TrackGameCompTimeAndMoves
 *                       method to fix least moves made in one place.
 *          2022-02-10 - Added new method to 'zero' moves mode counter.
 * 
 */
namespace GameStatistics
{
    public class Statistics
    {
        #region Properties
        private string _gameName = "";
        public string GameName {
            get { return _gameName; }
            set { if (!String.IsNullOrEmpty(value)) _gameName = value; }
        }

        private int _gamesStarted = 0;
        public int GamesStarted { get { return _gamesStarted; } }

        private int _gamesWon = 0;
        public int GamesWon { get { return _gamesWon; } }

        private int _gamesLost = 0;
        public int GamesLost { get { return _gamesLost; } }

        private int _gamesTied = 0;
        public int GamesTied { get { return _gamesTied; } }

        public int GamesNotFinished {
            get {
                if (_gamesStarted > 0)
                    return _gamesStarted - _gamesWon - _gamesLost - _gamesTied;
                else return 0;
            }
        }

        private int _highestScore = 0;
        public int HighestScore { get { return _highestScore; } }

        private int _movesMade = 0;
        public int MovesMade { get { return _movesMade; } }

        private int _leastMoves = 0;
        public int LeastMovesMade { get { return _leastMoves; } }

        private int _mostMoves = 0;
        public int MostMovesMade { get { return _mostMoves; } }

        private TimeSpan _quickestWinTime = TimeSpan.MinValue;
        public TimeSpan QuickestWinTime { get { return _quickestWinTime; } }

        // Normally, DayLastPlayed set when 'StartGame()' is called. If
        // not calling that method (not tracking moves or game time), then
        // DayLastPlayed can be set manually. The value should be assigned
        // as DateTime.Now (full timestamp).
        private DateTime _dayLastPlayed = DateTime.MinValue;
        public DateTime LastPlayed {
            get { return _dayLastPlayed; }
            set { _dayLastPlayed = value; }
        }

        private DateTime _dayLastWon = DateTime.MinValue;
        public DateTime LastWon { get {return _dayLastWon; } }

        private DateTime _startTime = DateTime.MinValue;
        public DateTime StartTime {
            get { return _startTime; }
            set { _startTime = value; } // Typically, 'set' is for testing purposes
        }
        #endregion

        #region Registry Constants
        const string DEF_REG_NAME = @"HKEY_CURRENT_USER\Software\Slack and Associates\Games\DefStats";
        const string GAMES_STARTED = "GamesStarted";
        const string GAMES_WON = "GamesWon";
        const string GAMES_LOST = "GamesLost";
        const string GAMES_TIED = "GamesTied";
        const string HIGH_SCORE = "HighScore";
        const string LEAST_MOVES = "LeastNumberOfWinningMoves";
        const string MOST_MOVES = "MostNumberOfWinningMoves";
        const string WIN_TIME = "WinningTime";
        const string DAY_PLAYED = "DayLastPlayed";
        const string DAY_WON = "DayLastWon";
        const string CUSTOM_X = "CustomXX_";
        #endregion

        #region Private vars
        private string appRegName = "";
        private bool statsReset = false;
        private Dictionary<string, int> customStats = new Dictionary<string, int>();
        #endregion

        // --------------------------------------------------------------------

        #region Constructors
        /*
         * Default constructor, uses a default registry path/key to store
         * statistics under. Probably not useful except for testing.
         */
        public Statistics() : this("") { }

        /*
         * Constructor to initialize a new statistics instance. The AppRegName
         * is the registry path to save statistics in. This name needs to
         * include the root path (HKEY_CURRENT_USER, etc.). If blank or null,
         * a default registry key is used, which might not be very useful except
         * for testing.
         */
        public Statistics(string AppRegName)
        {
            appRegName = AppRegName;
            if (String.IsNullOrEmpty(appRegName)) appRegName = DEF_REG_NAME;
            ReadRegistry();
        }
        #endregion

        // --------------------------------------------------------------------

        #region Private Methods
        private string GetKeyName()
        {
            string keyName = "";

            if (appRegName.StartsWith("HKEY_CURRENT_USER"))
                keyName = appRegName.Substring(18);
            else if (appRegName.StartsWith("HKEY_LOCAL_MACHINE"))
                keyName = appRegName.Substring(19);

            return keyName;
        }

        private RegistryKey GetRootKey()
        {
            return (appRegName.StartsWith("HKEY_LOCAL")) ? Registry.LocalMachine : Registry.CurrentUser;
        }

        private void ReadCustomStats()
        {
            string keyName = GetKeyName();
            if (!String.IsNullOrEmpty(keyName)) {
                using (RegistryKey key = GetRootKey().OpenSubKey(keyName)) {
                    foreach (string valName in key.GetValueNames()) {
                        if (valName.StartsWith(CUSTOM_X)) {
                            string statName = valName.Substring(CUSTOM_X.Length);
                            int val = (int)key.GetValue(valName, 0);
                            customStats.Add(statName, val);
                        }
                    }
                }
            }
        }

        private void ReadRegistry()
        {
            string tempVal = "";
            TimeSpan tempSpan;
            DateTime tempDate;

            try {
                _gamesStarted = (int) Registry.GetValue(appRegName, GAMES_STARTED, _gamesStarted);
                _gamesWon = (int) Registry.GetValue(appRegName, GAMES_WON, _gamesWon);
                _gamesLost = (int) Registry.GetValue(appRegName, GAMES_LOST, _gamesLost);
                _gamesTied = (int) Registry.GetValue(appRegName, GAMES_TIED, _gamesTied);
                _highestScore = (int) Registry.GetValue(appRegName, HIGH_SCORE, _highestScore);
                _leastMoves = (int) Registry.GetValue(appRegName, LEAST_MOVES, _leastMoves);
                _mostMoves = (int) Registry.GetValue(appRegName, MOST_MOVES, _mostMoves);
                tempVal = (string) Registry.GetValue(appRegName, WIN_TIME, "");
                if ((tempVal != "") && (TimeSpan.TryParse(tempVal, out tempSpan)))
                    _quickestWinTime = tempSpan;
                tempVal = (string) Registry.GetValue(appRegName, DAY_PLAYED, "");
                if ((tempVal != "") && (DateTime.TryParse(tempVal, out tempDate)))
                    _dayLastPlayed = tempDate;
                tempVal = (string) Registry.GetValue(appRegName, DAY_WON, "");
                if ((tempVal != "") && (DateTime.TryParse(tempVal, out tempDate)))
                    _dayLastWon = tempDate;
                ReadCustomStats();
            }
            catch (Exception) { /* go with default values */ }
        }

        private void WriteRegistry()
        {
            if (_gamesStarted > 0)
                Registry.SetValue(appRegName, GAMES_STARTED, _gamesStarted);
            if (_gamesWon > 0)
                Registry.SetValue(appRegName, GAMES_WON, _gamesWon);
            if (_gamesLost > 0)
                Registry.SetValue(appRegName, GAMES_LOST, _gamesLost);
            if (_gamesTied > 0)
                Registry.SetValue(appRegName, GAMES_TIED, _gamesTied);
            if (_highestScore > 0)
                Registry.SetValue(appRegName, HIGH_SCORE, _highestScore);
            if (_leastMoves > 0)
                Registry.SetValue(appRegName, LEAST_MOVES, _leastMoves);
            if (_mostMoves > 0)
                Registry.SetValue(appRegName, MOST_MOVES, _mostMoves);
            if (_quickestWinTime != TimeSpan.MinValue)
                Registry.SetValue(appRegName, WIN_TIME, _quickestWinTime.ToString());
            if (_dayLastPlayed != DateTime.MinValue)
                Registry.SetValue(appRegName, DAY_PLAYED, _dayLastPlayed.ToString());
            if (_dayLastWon != DateTime.MinValue)
                Registry.SetValue(appRegName, DAY_WON, _dayLastWon.ToString());
            foreach (string key in customStats.Keys) {
                Registry.SetValue(appRegName, CUSTOM_X + key, customStats[key]);
            }
        }

        private bool ClearRegistry()
        {
            bool cleared = false;
            string keyName = GetKeyName();

            if (!String.IsNullOrEmpty(keyName)) {
                try {
                    using (RegistryKey key = GetRootKey().OpenSubKey(keyName, true)) {
                        if (key != null) {
                            key.DeleteValue(GAMES_STARTED, false);
                            key.DeleteValue(GAMES_WON, false);
                            key.DeleteValue(GAMES_LOST, false);
                            key.DeleteValue(GAMES_TIED, false);
                            key.DeleteValue(HIGH_SCORE, false);
                            key.DeleteValue(LEAST_MOVES, false);
                            key.DeleteValue(MOST_MOVES, false);
                            key.DeleteValue(WIN_TIME, false);
                            key.DeleteValue(DAY_PLAYED, false);
                            key.DeleteValue(DAY_WON, false);
                            foreach (string cust in customStats.Keys) {
                                key.DeleteValue(CUSTOM_X + cust, false);
                            }
                        }
                    }
                    cleared = true;
                }
                catch (Exception) { cleared = false; }
            }

            return cleared;
        }

        private bool ClearCustomStatFromRegistry(string statName)
        {
            bool cleared = false;
            string keyName = GetKeyName();

            if (!String.IsNullOrEmpty(keyName)) {
                try {
                    using (RegistryKey key = GetRootKey().OpenSubKey(keyName, true)) {
                        if (key != null) {
                            key.DeleteValue(CUSTOM_X + statName, false);
                        }
                    }
                    cleared = true;
                }
                catch (Exception) { cleared = false; }
            }

            return cleared;
        }

        private int IncDecCustomStat(string statName, bool incVal)
        {
            bool hasKey = customStats.ContainsKey(statName);
            int val = (hasKey) ? customStats[statName] : 0;

            if (incVal) val++; else val--;
            if (hasKey) customStats[statName] = val; else customStats.Add(statName, val);

            return val;
        }

        private void StartGame2(bool saveStats, bool trackGamesStarted)
        {
            statsReset = false;
            if (trackGamesStarted) _gamesStarted++;
            _movesMade = 0;
            _dayLastPlayed = DateTime.Now;
            if (saveStats) SaveStatistics();
            _startTime = DateTime.Now;
        }

        private void TrackGameCompTimeAndMoves(DateTime stamp)
        {
            // set quickest win/complete time
            if (_startTime != DateTime.MinValue)
            {
                TimeSpan tmp = stamp.Subtract(_startTime);
                if ((_quickestWinTime == TimeSpan.MinValue) || (tmp < _quickestWinTime))
                    _quickestWinTime = tmp;
            }
            // set least/most moves made
            if ((_movesMade > 0) && (_leastMoves == 0 || _movesMade < _leastMoves))
                _leastMoves = _movesMade;
            if (_movesMade > _mostMoves) _mostMoves = _movesMade;
        }
        #endregion

        // --------------------------------------------------------------------

        #region Public Methods
        /*
         * Method to call to initialize move counter and start the game time
         * span. If not wanting to track move, start or quickest time
         * statistics, there is no need to call this method.
         */
        public void StartGame(bool saveStats)
        {
            StartGame2(saveStats, true);
        }

        /*
         * Method used to initialize the move counter and start game time
         * span. Will not initialize the 'GamesStarted' statistic (GSS).
         * If wanting GamesStarted, use 'StartGame()' instead.
         */
        public void StartGameNoGSS(bool saveStats)
        {
            StartGame2(saveStats, false);
        }

        /*
         * Method to call when a game has been won. Will finalize the
         * statistics being tracked and save them. Highest score can be
         * ignored by passing in 0 for curScore. If 'StartGame' was not
         * called before, the quickest time statistic is ignored and not
         * saved. Incrementing games won and last won day will always be
         * set and saved. Will not process the game won statistics if
         * reset statistics was called until the 'StartGame' method is
         * called again.
         */
        public void GameWon(int curScore)
        {
            // save off before anything else to get a 'better' time-span (if tracking).
            DateTime stamp = DateTime.Now;
            if (!statsReset) { // don't track if reset called after 'start'
                TrackGameCompTimeAndMoves(stamp);
                _gamesWon++;
                _dayLastWon = DateTime.Now;
                if (curScore > _highestScore) _highestScore = curScore;
                SaveStatistics();
            }
        }

        /*
         * Method to call when a game has been lost. Increments the
         * Games lost statistic and if curScore <> 0, may set the
         * highest score statistic. The statistics are saved after
         * processing. Does not process losing statistics if the
         * reset statistics method was called until the 'StartGame'
         * method is called again.
         */
        public void GameLost(int curScore)
        {
            if (!statsReset) { // don't 'track' if reset called after 'start'
                _gamesLost++;
                if (curScore > _highestScore) _highestScore = curScore;
                SaveStatistics();
            }
        }

        /*
         * Method to call when game is a tie.  Ends game statistic gathering
         * and saves off the statistics.  Does not process tie if the reset
         * statistics method was called until a 'start' game method is called
         * again.
         */
        public void GameTied()
        {
            if (!statsReset)
            {
                _gamesTied++;
                SaveStatistics();
            }
        }

        /*
         * Method to call when a game has been completed (neither won or lost
         * from the players perspective). Will finalize the statistics being
         * tracked and save them. Normally, this method would be called to
         * finalize statistics after the 'StartGameNoGSS' method is called.
         * Will not process the game won statistics if reset statistics was
         * called until one of the 'StartGame' methods are called again. Note,
         * this method is similar to both GameWon and GameLost other than the
         * built in won/lost stats are not tracked, but will track moves if
         * tracking them along with quickest win time. In addition, last won
         * date is not set.
         * The intent, along with the 'StartGameNoGSS()' call, is to track a
         * handful of custom statistics for games that are more than one
         * player (human vs. computer).
         */
        public void GameDone()
        {
            // save off before anything else to get a 'better' time-span (if tracking).
            DateTime stamp = DateTime.Now;
            if (!statsReset) {
                TrackGameCompTimeAndMoves(stamp);
                SaveStatistics();
            }
        }

        /*
         * Method to call after each move, if tracking move statistics.
         * If not tracking move statistics, don't call this method and
         * move statistics will not be tracked or saved.
         */
        public void MoveMade()
        {
            _movesMade++;
        }

        /*
         * Method used to zero out moves made.  Should be used if game
         * has auto-play feature that can be turned on anytime and tracking
         * moves made, but not when auto-played.  This can be used to
         * reset the moves made counter so 'least moves' counter is not
         * messed up.
         */
        public void ZeroMovesMade()
        {
            _movesMade = 0;
        }

        /*
         * Method to use to save statistics manually. Typically, the
         * statistic counters are saved when GameWon or GameLost is
         * called, but this may be called if wanting to save the
         * statistic counters when someone quits a game without
         * winning or losing and StartGame was called with the save
         * parameter set to false. Will not save if if the reset
         * statistics method was called until 'StartGame' is called
         * again.
         */
        public void SaveStatistics()
        {
            if (!statsReset) WriteRegistry();
        }

        /*
         * Method called to reset the game statistics back to the
         * initial state (no statistics). If called, statistics are
         * not tracked until 'StartGame' is called again or the
         * game is restarted. Note, will only reset stats stored
         * in HKEY_CURRENT_USER or HKEY_LOCAL_MACHINE and will pass
         * back true if stats were reset (cleared).
         */
        public bool ResetStatistics()
        {
            statsReset = ClearRegistry();

            if (statsReset) { // reset properties
                _gamesStarted = 0;
                _gamesWon = 0;
                _gamesLost = 0;
                _highestScore = 0;
                _leastMoves = 0;
                _mostMoves = 0;
                _quickestWinTime = TimeSpan.MinValue;
                _dayLastPlayed = DateTime.MinValue;
                _dayLastWon = DateTime.MinValue;
                customStats.Clear();
            }

            return statsReset;
        }

        /*
         * Method used to display the statistics gathered in a dialog
         * window to the caller.
         */
        public void ShowStatistics(IWin32Window owner)
        {
            StatsDlg stats = new StatsDlg();
            DialogResult res;

            stats.Stats = this.ToString();
            res = stats.ShowDialog(owner);
            if ((res == DialogResult.OK) && (stats.ResetStats)) ResetStatistics();
            stats.Dispose();
        }

        /*
         * Method used to save the formatted statistics to a file. Will pass
         * back true if the file was saved. If an error was thrown during the
         * save, the method returns false.
         */
        public bool SaveStatisticsToFile(string fileName)
        {
            return SaveStatisticsToFile(fileName, false);
        }

        /*
         * Method used to save the formatted statistics to a file. Will pass
         * back true if the file was saved. If showError is true, and an
         * error was thrown during the file save, the error is displayed
         * in a dialog. The method will return false if an error was thrown.
         */
        public bool SaveStatisticsToFile(string fileName, bool showError)
        {
            bool saved = true;

            try {
                using (StreamWriter file = new StreamWriter(fileName)) {
                    file.Write(this.ToString());
                }
            }
            catch (Exception ex) {
                saved = false;
                if (showError)
                    MessageBox.Show("Error: " + ex.Message, "Save Statistics to File Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return saved;
        }
        #endregion

        // --------------------------------------------------------------------

        #region Custom Statistic Methods
        /*
         * Method returns the custom statistic specified by statName. If
         * the statistic has not been created or set, a zero (0) is passed
         * back as the value of the statistic.
         */
        public int CustomStatistic(string statName)
        {
            return (customStats.ContainsKey(statName)) ? customStats[statName] : 0;
        }

        /*
         * Method creates and sets a custom statistic with a specified value.
         * If the statistic already exists, the value is set to value passed in
         * and false is returned. If the statistic didn't exist, the method
         * returns true after creating the statistic and setting the value to
         * what was passed in.
         */
        public bool CreateCustomStatistic(string statName, int value)
        {
            bool hasKey = customStats.ContainsKey(statName);

            if (hasKey) customStats[statName] = value; else customStats.Add(statName, value);

            return !hasKey;
        }

        /*
         * Method allows for setting a custom statistic to a specific value.
         * Will create the statistic if it doesn't exist. Will pass back
         * true if the statistic was created, passes back false if the
         * statistic already existed. In either case, the statistic is
         * set to the value passed in.
         */
        public bool SetCustomStatistic(string statName, int value)
        {
            return CreateCustomStatistic(statName, value);
        }

        /*
         * Method increments a custom statistic value and passes back the
         * value after incrementing. If the statistic 'statName' does not
         * exist, it is created with a value of 1.
         */
        public int IncCustomStatistic(string statName)
        {
            return IncDecCustomStat(statName, true);
        }

        /*
         * Method decrements a custom statistic value and passes back the
         * value after decrementing. If the statistic 'statName' does not
         * exist, it is created with a value of -1.
         */
        public int DecCustomStatistic(string statName)
        {
            return IncDecCustomStat(statName, false);
        }

        /*
         * Method resets (removes) the custom statistic from the tracking
         * data. Any subsequent call for the custom statistic will be
         * treated as a new statistic. Will return true if the custom
         * statistic was successfully reset (cleared).
         */
        public bool ResetCustomStatistic(string statName)
        {
            bool cleared = ClearCustomStatFromRegistry(statName);

            if (cleared) customStats.Remove(statName);

            return cleared;
        }
        #endregion

        // --------------------------------------------------------------------

        #region Overridden Method (ToString)

        /*
         * Method used to return the statistics tracking as a string. String
         * returned contains platform line end characters.
         */
        public override string ToString()
        {
            const int STR_SIZE = 600;
            const int DEF_DIV_SIZE = 40;
            const string DT_FMT = "yyyy-MM-dd HH:mm:ss";
            const string TM_FMT = "hh':'mm':'ss";

            StringBuilder stats = new StringBuilder(STR_SIZE);

            if (!String.IsNullOrEmpty(_gameName)) stats.Append(_gameName).Append(" ");
            stats.Append("Game Statistics");

            int divSize = DEF_DIV_SIZE;
            if (stats.Length > DEF_DIV_SIZE) divSize = stats.Length;
            stats.AppendLine().Append("".PadLeft(divSize, '=')).AppendLine();
            string divider = "".PadLeft(divSize, '-');

            if (_dayLastPlayed != DateTime.MinValue)
                stats.Append("Last Played: ").Append(_dayLastPlayed.ToString(DT_FMT)).AppendLine();
            if (_dayLastWon != DateTime.MinValue)
                stats.Append("Last Won...: ").Append(_dayLastWon.ToString(DT_FMT)).AppendLine();
            if ((_dayLastPlayed != DateTime.MinValue) || (_dayLastWon != DateTime.MinValue))
                stats.Append(divider).AppendLine();

            if (_gamesStarted > 0) {
                stats.Append("Games Started: ").Append(_gamesStarted).AppendLine();
                int gnf = GamesNotFinished;
                if (((_gamesLost > 0) || (_gamesWon > 0)) && (gnf > 0))
                    stats.Append(" - Games Aborted or Not Finished: ").Append(gnf).AppendLine();
            }
            if (_gamesWon > 0)
                stats.Append("Games Won....: ").Append(_gamesWon).AppendLine();
            if (_gamesLost > 0)
                stats.Append("Games Lost...: ").Append(_gamesLost).AppendLine();
            if (_gamesTied > 0)
                stats.Append("Games Tied...: ").Append(_gamesTied).AppendLine();
            if (_highestScore > 0)
                stats.Append("Highest Score Made: ").Append(_highestScore).AppendLine();
            if ((_gamesStarted > 0) || (_gamesWon > 0) || (_gamesLost > 0) || (_highestScore > 0))
                stats.Append(divider).AppendLine();

            if (_movesMade > 0)
                stats.Append("Current Moves Made.......: ").Append(_movesMade).AppendLine();
            if (_leastMoves > 0)
                stats.Append("Minimum Moves Made to Win: ").Append(_leastMoves).AppendLine();
            if (_mostMoves > 0)
                stats.Append("Maximum Moves Made to Win: ").Append(_mostMoves).AppendLine();
            if ((_movesMade > 0) || (_leastMoves > 0) || (_mostMoves > 0))
                stats.Append(divider).AppendLine();

            if (_quickestWinTime != TimeSpan.MinValue) {
                string tmf = TM_FMT;
                if (_quickestWinTime.Days > 0) tmf = "d'.'" + tmf; else tmf += "'.'ffff";
                stats.Append("Quickest Time to Win: ").Append(_quickestWinTime.ToString(tmf)).AppendLine();
                stats.Append(divider).AppendLine();
            }

            if (customStats.Count > 0) {
                foreach (string statName in customStats.Keys) {
                    stats.Append(statName).Append(": ").Append(customStats[statName]).AppendLine();
                }
            }

            divider = stats.ToString();
            if (divider.LastIndexOf('=') >= (divider.Length - 3))
                divider += "[No statistics gathered.]";

            return divider;
        }
        #endregion
    }
}
