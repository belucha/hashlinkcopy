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
        public static TextWriter LOGFILE = null;
        public static void AddLogFile(string filename)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filename));
            LOGFILE = new StreamWriter(filename, true);
        }
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
            var s = String.Format("{0}: ", verbosity) + format;
            if (Logger.VERBOSITY >= verbosity)
                Console.WriteLine(s, args);
            if (Logger.LOGVERB >= verbosity && Logger.LOGFILE != null)
                Logger.LOGFILE.WriteLine(s, args);
        }
    }
}
