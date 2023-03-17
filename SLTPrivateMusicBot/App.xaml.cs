namespace SLTPrivateMusicBot
{
    using SLTPrivateMusicBot.Player;
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Windows;
    using System.Windows.Documents;
    using System.Windows.Media;

    public partial class App : Application
    {
        private TextWriter _logger;

        internal static App Instance { get; private set; }

        public App()
        {
            Instance = this;
        }

        public static void Log(string text, Exception e = null)
        {
            if (Current != null)
            {
                Debugger.Log(0, null, text + "\n");
                Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        Instance._logger.WriteLine(text);
                        TryUILog(text);
                        if (e != null)
                        {
                            Instance._logger.WriteLine(e.GetType());
                            TryUILog(e.GetType().ToString());
                            foreach (string s in e.StackTrace.Split('\n'))
                            {
                                Instance._logger.WriteLine(s);
                                TryUILog(s);
                            }
                        }
                    }
                    catch
                    {
                        // NOOP
                    }
                });
            }
        }

        private static bool TryUILog(string text)
        {
            try
            {
                if (string.IsNullOrEmpty(text))
                {
                    return false;
                }

                if (SLTPrivateMusicBot.MainWindow.Current == null || SLTPrivateMusicBot.MainWindow.Current.TB_Log == null)
                {
                    return false;
                }

                Color color = Colors.Black;
                if (text.StartsWith("[INFO]"))
                {
                    color = Colors.Gray;
                }

                if (text.StartsWith("[FINE]"))
                {
                    color = Colors.DarkGreen;
                }

                if (text.StartsWith("[WARNING]") || text.StartsWith("[WARN]"))
                {
                    color = Colors.Goldenrod;
                }

                if (text.StartsWith("[ERROR]"))
                {
                    color = Colors.Red;
                }

                if (text.StartsWith("[FATAL]"))
                {
                    color = Colors.DarkRed;
                }

                TextRange tr = new TextRange(SLTPrivateMusicBot.MainWindow.Current.TB_Log.Document.ContentEnd, SLTPrivateMusicBot.MainWindow.Current.TB_Log.Document.ContentEnd);
                tr.Text = text + "\n";
                tr.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(color));
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void App_Exit(object sender, ExitEventArgs e)
        {
            this._logger.Flush();
            this._logger.Dispose();
            YoutubeDL.Cleanup();
        }

        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            this._logger.WriteLine("[FATAL] Unhandled exception at " + e.Exception.HResult);
            this._logger.WriteLine("[FATAL] " + e.Exception.GetType());
            foreach (string s in e.Exception.StackTrace.Split('\n'))
            {
                this._logger.WriteLine("[FATAL] " + s);
            }

            this._logger.Flush();
            this._logger.Dispose();
            YoutubeDL.Cleanup();
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                bool nologging = args.Any(s => s.Equals("-q") || s.Equals("--quiet"));
                bool production = !System.Diagnostics.Debugger.IsAttached || args.Any(s => s.Equals("-p") || s.Equals("--production"));
                for (int i = 0; i < args.Length; ++i)
                {
                    if ((args[i].Equals("-ar") || args[i].Equals("--audio-rate")) && i < args.Length - 1 && int.TryParse(args[i + 1], out int index))
                    {
                        SLTPrivateMusicBot.MainWindow.MusicRate = index;
                    }
                }

                this._logger = File.CreateText(Path.GetFullPath("./log.txt"));
            }
            catch
            {
                // NOOP
            }
        }
    }
}
