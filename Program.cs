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
    class Program
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

        static void print(string name, object value)
        {
            Console.WriteLine("{0,-20}: {1}", name, value);
        }

        static void PrintDest(string fn)
        {
            Console.WriteLine("\"{0}\"=>\"{1}\"", fn, Win32.GetJunctionTarget(fn));
        }

        static int A(string[] args)
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

        static int Main(string[] args)
        {
            try
            {
                var hashCopy = new SymbolicHashLinkCopy();
                var view = new ConsoleViewer(hashCopy);
                if (args.Length < 1 || args.Length > 3)
                {
                    var exeName = Path.GetFileName(Application.ExecutablePath);
                    Console.WriteLine("{0} v{1} - Copyright © 2012, Daniel Gross, daniel@belucha.de", exeName, Application.ProductVersion);
                    Console.WriteLine("Symbolic and Hard link based backup tool using SHA1 and ADS for efficent performance!");
                    Console.WriteLine();
                    Console.WriteLine("Usage:");
                    Console.WriteLine("{0} SourcePath [DestinationPath] [HashDirectory]", exeName);
                    Console.WriteLine();
                    Console.WriteLine("Default destination is the current folder + *");
                    Console.WriteLine("\t\tA star '*' in the destination folder will be replaced by the current date in the form yyyy-mm-dd");
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
                print("Source path", sourceDirectory);
                // get the destination folder
                var destinationDirectory = args.Length >= 2 ? args[1] : Path.Combine(Directory.GetCurrentDirectory(), "*");
                destinationDirectory = Path.GetFullPath(destinationDirectory.Replace("*", DateTime.Now.ToString("yyyy-MM-dd")));
                print("Desitination path", destinationDirectory);
                // hash directory
                if (args.Length >= 3)
                {
                    hashCopy.HashDir = args[2];
                    print("Hash folder", hashCopy.HashDir);
                }
                // RUN
                hashCopy.Copy(sourceDirectory, destinationDirectory);
                // Statistics
                print("Start", hashCopy.Start);
                print("End", hashCopy.End);
                print("Duration", hashCopy.Elapsed);
                print("Directories", view.DirectoryCount);
                print("Files", view.FileCount);
                print("Linked Files", view.LinkFileCount);
                print("Linked Folders", view.LinkDirectoryCount);
                print("Copied Files", view.CopyCount);
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
