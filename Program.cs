using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.hashlinkcopy
{
    class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("{0}, v{1} - {2}",
                System.Windows.Forms.Application.ProductName,
                System.Windows.Forms.Application.ProductVersion,
                System.Windows.Forms.Application.CompanyName);
            try
            {
                // separate options
                var parameters = args.Where(arg => !arg.StartsWith("--")).ToArray();
                // parse operation mode
                if (parameters.Length < 1)
                {
                    Console.WriteLine("Usage:");
                    Console.WriteLine("\tHashLinkCopy.exe [{0}] [--Option1[:Value1] --Option2[=Value2]] [parameters]",
                        String.Join("|", CommandBase.GetCommandList().Select(t => t.Name.Substring("Command".Length).ToUpper()).ToArray()));
                    Console.WriteLine("General Help:");
                    Console.WriteLine("\tHashLinkCopy.exe HELP");
                    Console.WriteLine("Help on specific command:");
                    Console.WriteLine("\tHashLinkCopy.exe HELP COMMAND");
                    Console.WriteLine("\te.g. HashLinkCopy.exe HELP COPY");
                    return 1;
                }
                // get the operation mode
                var command = CommandBase.CreateCommandHandler(parameters[0]);
                command.Init(parameters.Skip(1).ToArray());
                command.ParseOptions(args.Where(arg => arg.StartsWith("--")));
                var start = DateTime.Now;
                command.Run();
                var et = DateTime.Now.Subtract(start);
                if (command.GetType() != typeof(CommandHelp))
                {
                    Logger.WriteLine(Logger.Verbosity.Message, "Total time {0}", et);
                    Monitor.PrintStatistics();
                }
                if (Logger.LOGFILE != null)
                    Logger.LOGFILE.Dispose();
                return 0;
            }
            catch (System.Reflection.TargetInvocationException error)
            {
                Logger.Error("{0}: {1}", error.InnerException.GetType().Name, error.InnerException.Message);
            }
            catch (Exception error)
            {
                Logger.Error("{0}: {1}", error.GetType().Name, error.Message);
            }
            if (Logger.LOGFILE != null)
                Logger.LOGFILE.Dispose();
            return 2;
        }
    }
}
