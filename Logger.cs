using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.hashlinkcopy
{
    public static class Logger
    {
        public enum Verbosity
        {
            None,
            Error,
            Warning,
            Message,
            Verbose,
            Debug,
        }

        public static int ErrorCount { get; set; }
        public static int WarningCount { get; set; }
        public static Verbosity VERBOSITY = Verbosity.Message;
        public static Verbosity LOGVERB = Verbosity.Message;
        public static string LOGFILE = null;
        public static void Error(string format, params object[] args)
        {
            Logger.ErrorCount++;
            if (Logger.VERBOSITY >= Verbosity.Error)
                Logger.WriteLine(Verbosity.Error, format, args);
        }
        public static void Warning(string format, params object[] args)
        {
            Logger.WarningCount++;
            if (Logger.VERBOSITY >= Verbosity.Warning)
                Logger.WriteLine(Verbosity.Warning, format, args);
        }
        public static void WriteLine(Verbosity verbosity, string format, params object[] args)
        {
            var s = String.Format(String.Format("{0}: ", verbosity) + format, args);
            if (Logger.VERBOSITY >= verbosity)
                Console.WriteLine(s);
            if (Logger.LOGVERB >= verbosity && !String.IsNullOrEmpty(Logger.LOGFILE))
                lock (typeof(Logger))
                    File.AppendAllText(Logger.LOGFILE, s + "\n", Encoding.UTF8);
        }
    }
}
