using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.hashlinkcopy
{
    public class Logger : IDisposable
    {
        StringBuilder cachedText = new StringBuilder();
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public Verbosity Verbosity = Verbosity.None;
        public Verbosity LogfileVerbosity = Verbosity.Message;
        public string Logfilename = null;

        public static Logger Root;

        public void Error(string format, params object[] args)
        {
            this.ErrorCount++;
            if (Verbosity >= Verbosity.Error)
                WriteLine(Verbosity.Error, format, args);
        }
        public void Warning(string format, params object[] args)
        {
            WarningCount++;
            if (Verbosity >= Verbosity.Warning)
                WriteLine(Verbosity.Warning, format, args);
        }
        public void WriteLine(Verbosity verbosity, string format, params object[] args)
        {
            if (Console.KeyAvailable)
            {
                var ki = Console.ReadKey(true);
                switch (ki.Key)
                {
                    case ConsoleKey.D0:
                    case ConsoleKey.D1:
                    case ConsoleKey.D2:
                    case ConsoleKey.D3:
                    case ConsoleKey.D4:
                    case ConsoleKey.D5:
                        var nv = (Verbosity)(ki.Key - ConsoleKey.D0);
                        Verbosity = nv;
                        Console.Clear();
                        break;
                    default:
                        break;
                }
            }
            var s = String.Format(String.Format("{0}: ", verbosity) + format, args);
            if (Verbosity >= verbosity)
                Console.WriteLine(s);
            if (LogfileVerbosity >= verbosity)
                WriteLogFile(s, false);
        }

        void WriteLogFile(string s, bool forceWrite)
        {
            lock (this)
            {
                this.cachedText.AppendLine(s);
                if (forceWrite || this.cachedText.Length > 1023)
                {
                    if (!String.IsNullOrEmpty(this.Logfilename))
                        File.AppendAllText(this.Logfilename, this.cachedText.ToString(), Encoding.UTF8);
                    this.cachedText = new StringBuilder();
                }
            }
        }

        public void PrintInfo(string key, object value)
        {
            PrintInfo(key, "{0}", value);
        }
        public void PrintInfo(string key, string value, params object[] args)
        {
            WriteLine(Verbosity.Message, "{0,-20}: {1}", key, String.Format(value, args));
        }

        public void Dispose()
        {
            if (LogfileVerbosity > Verbosity.None)
                WriteLogFile("=========END OF LOG==============", true);
        }
    }
    public enum Verbosity
    {
        None,
        Error,
        Warning,
        Message,
        Verbose,
        Debug,
    }
}
