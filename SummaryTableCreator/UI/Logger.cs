namespace SummaryTableCreator.UI
{
    public static class Logger
    {
        private static RichTextBox _console;

        /// <summary>
        /// Initialise the logger with the RichTextBox control.
        /// </summary>
        public static void Initialize(RichTextBox console)
        {
            _console = console;
        }

        /// <summary>
        /// Clears all text from the console.
        /// </summary>
        public static void Clear()
        {
            if (_console == null)
                throw new InvalidOperationException("Logger not initialized. Call Logger.Initialize(console) first.");

            _console.Invoke((Action)(() =>
            {
                _console.Clear();
            }));
        }

        /// <summary>
        /// Logs an OK message in green.
        /// </summary>
        public static void Okay(string message)
        {
            Log(message, Color.Green);
        }

        /// <summary>
        /// Logs an informational message in blue.
        /// </summary>
        public static void Info(string message)
        {
            Log(message, Color.SteelBlue);
        }

        /// <summary>
        /// Logs a warning message in orange.
        /// </summary>
        public static void Warning(string message)
        {
            Log(message, Color.Orange);
        }

        /// <summary>
        /// Logs an error message in red.
        /// </summary>
        public static void Error(string message)
        {
            Log(message, Color.Red);
        }

        private static void Log(string message, Color color)
        {
            if (_console == null)
                throw new InvalidOperationException("Logger not initialized. Call Logger.Initialize(console) first.");

            _console.Invoke((Action)(() =>
            {
                _console.SelectionStart = _console.TextLength;
                _console.SelectionLength = 0;
                _console.SelectionColor = color;
                _console.AppendText($"{message}\n");
                _console.SelectionColor = _console.ForeColor;
                _console.ScrollToCaret();
            }));
        }
    }
}
