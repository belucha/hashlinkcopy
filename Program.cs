using System;
using System.Security.Principal;
using System.Windows.Forms;
using System.Security.Cryptography;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.backup
{
    /// <summary>
    /// Extension to Console
    /// </summary>
    public static class PagedConsole
    {
        /// <summary>
        /// Paginated WriteLine
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public static void WriteLine(string format, params object[] args)
        {
            Console.WriteLine(format, args);
            if (Console.CursorTop == (Console.WindowHeight - 3))
            {
                Console.WriteLine("press any key for next page");
                Console.ReadKey(true);
                Console.Clear();
            }
        }
    }

    class Program
    {

        static void print(string name, object value)
        {
            Console.WriteLine("{0,-20}: {1}", name, value);
        }

        static void PrintDest(string fn)
        {
            Console.WriteLine("\"{0}\"=>\"{1}\"", fn, Win32.GetJunctionTarget(fn));
        }

        static int Main(string[] args)
        {
            try
            {
                var c = new HashCleanup(@"g:\Hash");
                c.CheckDirectories(@"g:\");
                c.DeleteUnused();
                Console.WriteLine("done");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            Console.ReadLine();
            return 0;
        }

        static int V(string[] args)
        {
            try
            {
                var hashCopy = new HashLinkCopy();
                if (args.Length < 1 || args.Length > 3)
                {
                    var exeName = Path.GetFileName(Application.ExecutablePath);
                    Console.WriteLine("{0} v{1} - Copyright © 2012, Daniel Gross, daniel@belucha.de", exeName, Application.ProductVersion);
                    Console.WriteLine("Symbolic and Hard link based backup tool using SHA1 and ADS for efficent performance!");
                    Console.WriteLine();
                    Console.WriteLine("Usage:");
                    Console.WriteLine("{0} SourceDirectory [DestinationDirectory] [HashDirectory]", exeName);
                    Console.WriteLine();
                    Console.WriteLine("Default DestinationDirectory is the current folder + *");
                    Console.WriteLine("\t\tA star '*' in the destination folder will be replaced by the name of the source folder");
                    Console.WriteLine("Default HashDirectory is the root path of the destination directory + Hash");
                    Console.WriteLine();
                    Console.WriteLine();
                    Console.WriteLine("Examples:");
                    Console.WriteLine("\t- Backup all files and subdirectories on drive D to drive F under the current date.");
                    Console.WriteLine("\t  The complete directory structure of D will be available within F:\\2012-11-13\\");
                    Console.WriteLine("\t  Command Line: {0} D:\\ F:\\2012-11-13\\", exeName);
                    Console.WriteLine("\t  Default hashdir is F:\\Hash\\");
                    return 1;
                }
                // source directory
                var sourceDirectory = Path.GetFullPath(args[0]);
                print("SourceDir", sourceDirectory);
                var sourceInfo = new DirectoryInfo(sourceDirectory);
                // get the destination folder
                var destinationDirectory = args.Length >= 2 ? args[1] : Path.Combine(Directory.GetCurrentDirectory(), "*");
                destinationDirectory = Path.GetFullPath(destinationDirectory.Replace("*", sourceInfo.Name));
                print("TargetDir", destinationDirectory);
                // hash directory
                hashCopy.HashDir = Path.GetFullPath(args.Length >= 3 ? args[2] : Path.Combine(Path.GetPathRoot(destinationDirectory), "Hash"));
                print("HashDir", hashCopy.HashDir);
                // RUN
                hashCopy.Run(sourceDirectory, destinationDirectory);
                // Statistics
                print("Start", hashCopy.Start);
                print("End", hashCopy.End);
                print("files", hashCopy.FileCount);
                print("directories", hashCopy.DirectoryCount);
                print("copied files", hashCopy.CopyCount);
                print("symbolic links", hashCopy.SymLinkCount);
                print("hard link", hashCopy.HardLinkCount);
                print("skipped hard links", hashCopy.SkippedHardLinkCount);
                print("moved files", hashCopy.MoveCount);
                print("junctions", hashCopy.JunctionCount);
                print("skipped junctions", hashCopy.SkippedJunctionCount);
                print("skipped tree count", hashCopy.SkippedTreeCount);
                print("error count", hashCopy.ErrorCount);
                print("duration", hashCopy.Elapsed);
                return 0;
            }
            catch (Exception e)
            {
                Console.WriteLine("Backup failed!");
                Console.WriteLine("\t{0,-20}{1}", "Error:", e.GetType().Name);
                Console.WriteLine("\t{0,-20}{1}", "Message:", e.Message);
                return 2;
            }
        }
    }
}
