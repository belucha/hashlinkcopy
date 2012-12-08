using System;
using System.Threading;
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
        static long ErrorCount;
        static long FileCount;
        static long DirectoryCount;
        static long CopyCount;
        static long LinkFileCount;
        static long LinkDirectoryCount;
        static int DisplayLimit = 3;
        static long ExcludedCount;
        static string[] excludeList = new string[0];

        static void print(string name, object value)
        {
            Console.WriteLine("{0,-20}: {1}", name, value);
        }

        static void hashLinkCopy_Error(object sender, HashLinkErrorEventArgs e)
        {
            ErrorCount++;
            Console.WriteLine("Error {0} on file \"{1}\", Message: \"{2}\"", e.Error.GetType().Name, e.Info.FullName, e.Error.Message);
        }

        static void Print(int level, bool newLine, string input, params object[] args)
        {
            try
            {
                var w = Console.BufferWidth;
                var text = String.Format("".PadLeft(level) + input, args);
                if (text.Length >= w)
                    text = text.Remove(level + 4, text.Length - w + 8).Insert(level + 4, "...");
                Console.CursorLeft = 0;
                if (newLine)
                    Console.WriteLine(text.PadRight(w - 1));
                else
                    Console.Write(text.PadRight(w - 1));
            }
            catch
            {
                // ignore
            }
        }

        static void hashLinkCopy_Action(object sender, HashLinkActionEventArgs e)
        {
#if DEBUG
            // just to make display debug slower
            Thread.Sleep(500);
#endif
            // checks if the entry is in the exclude list            
            if (excludeList.Any(exclude => String.Compare(e.Info.Name, exclude, true) == 0))
            {
                ExcludedCount++;
                Print(e.Level, false, "{0} (excluded)", e.Info.FullName);
                e.Cancel = true;
            }
            else
            {
                switch (e.Action)
                {
                    case HashLinkAction.EnterSourceDirectory:
                        DirectoryCount++;
                        break;
                    case HashLinkAction.ProcessSourceFile:
                        FileCount++;
                        break;
                    case HashLinkAction.CopyFile:
                        CopyCount++;
                        break;
                    case HashLinkAction.LinkDirectory:
                        LinkDirectoryCount++;
                        break;
                    case HashLinkAction.LinkFile:
                        LinkFileCount++;
                        break;
                }
                if (e.Action == HashLinkAction.EnterSourceDirectory && e.Level <= DisplayLimit)
                    Print(e.Level, true, e.Info.FullName);
                else
                    Print(e.Level, false, "{0} ({1})", e.Info.FullName, e.Action);
            }
            Console.Title = String.Format("BackupTool [dirs:{0}/files:{1}/copied:{2}/linked folders:{3}/linked files:{4}/excluded:{5}/Errors:{6}]", DirectoryCount, FileCount, CopyCount, LinkDirectoryCount, LinkFileCount, ExcludedCount, ErrorCount);
        }

        static int Help()
        {
            var exeName = Path.GetFileName(Application.ExecutablePath);
            Console.WriteLine("{0} v{1} - Copyright © 2012, Daniel Gross, daniel@belucha.de", exeName, Application.ProductVersion);
            Console.WriteLine("Symbolic and Hard link based backup tool using SHA1 and ADS for efficent performance!");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("{0} SourcePath [DestinationPath]", exeName);
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

        static string FormatBytes(long bytes)
        {
            var units = new string[] { "Byte", "Kb", "Mb", "Gb", };
            if (bytes < 1024)
                return String.Format("{0}{1}", bytes, units[0]);
            var b = new StringBuilder();
            for (var p = units.Length - 1; p >= 0; p--)
            {
                var c = (bytes >> (p * 10)) & 1023;
                if (c > 0)
                    b.AppendFormat("{0}{1} ", c, units[p]);
            }
            return b.ToString();
        }

        public enum Operation
        {
            Backup = 1,
            Clean,
            //  other operations
            Default = Backup,
            Copy = Backup,
        }

        public enum LinkCreation
        {
            Symbolic,
            JunctionAndHardlink,
            RSymbolic,
        }

        public enum Option
        {
            Operation,
            HashDir,
            Exclude,
            EnableDelete,
            LinkCreation,
            DirectoryLinkCreation,
            FileLinkCreation,
            Junction,
        }

        static int aMain(string[] args)
        {
            try
            {
                var s = Win32.GetJunctionTarget(@"d:\Projekte\Backup\2012-11-24\");
                var symlink = @"D:\Temp\Hej";
                var target = @"\Lyrics\";
                Directory.CreateDirectory(symlink);
                Win32.CreateSymbolLink(symlink, target, true);
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
            List<KeyValuePair<Option, string>> options;
            string[] parameters;
            string hashDir = null;
            var hashCopy = new HashLinkCopy();
            var operation = Operation.Default;
            bool enableDelete = false;
            try
            {
                options = args
                    .Where(arg => arg.StartsWith("--")).Select(arg => arg.Substring(2).Split(new char[] { ':', '=', }, 2))
                    .Select(pair => new KeyValuePair<Option, string>((Option)Enum.Parse(typeof(Option), pair[0], true), pair.Length > 1 ? pair[1] : null))
                    .OrderBy(kvp => kvp.Key)
                    .ToList();
                parameters = args.Where(arg => !arg.StartsWith("--")).ToArray();
                foreach (var kvp in options)
                    try
                    {
                        switch (kvp.Key)
                        {
                            case Option.Operation:
                                operation = (Operation)Enum.Parse(typeof(Operation), kvp.Value, true);
                                break;
                            case Option.HashDir:
                                hashDir = kvp.Value;
                                break;
                            case Option.EnableDelete:
                                if (kvp.Value != null)
                                    throw new InvalidOperationException("No value allowed for this option!");
                                enableDelete = true;
                                break;
                            case Option.Exclude:
                                if (kvp.Value.StartsWith("@"))
                                    excludeList = File.ReadAllLines(kvp.Value.Substring(1));
                                else
                                    excludeList = kvp.Value.Split(';');
                                break;
                            case Option.FileLinkCreation:
                                hashCopy.FileLinkCreation = (FileLinkCreation)Enum.Parse(typeof(FileLinkCreation), kvp.Value, true);
                                break;
                            case Option.DirectoryLinkCreation:
                                hashCopy.DirectoryLinkCreation = (DirectoryLinkCreation)Enum.Parse(typeof(DirectoryLinkCreation), kvp.Value, true);
                                break;
                            case Option.LinkCreation:
                                var res = (LinkCreation)Enum.Parse(typeof(LinkCreation), kvp.Value, true);
                                hashCopy.FileLinkCreation = (FileLinkCreation)(int)res;
                                hashCopy.DirectoryLinkCreation = (DirectoryLinkCreation)(int)res;
                                break;
                            case Option.Junction:
                                if (kvp.Value != null)
                                    throw new InvalidOperationException("No value allowed for this option!");
                                hashCopy.FileLinkCreation = FileLinkCreation.Hardlink;
                                hashCopy.DirectoryLinkCreation = DirectoryLinkCreation.Junction;
                                break;
                            default:
                                throw new ArgumentOutOfRangeException(String.Format("Unknown option {0}!", kvp.Key));
                        }
                    }
                    catch (Exception error)
                    {
                        throw new InvalidOperationException(String.Format("{2} while processing option: --{0} with value \"{1}\"!\nMessage: \"{3}\"", kvp.Key, kvp.Value, error.GetType().Name, error.Message), error);
                    }
                if (parameters.Length == 0 && operation == Operation.Backup)
                    return Help();
                switch (operation)
                {
                    case Operation.Clean:
                        break;
                    case Operation.Backup:
                        if (parameters.Length < 1 || parameters.Length > 2)
                            throw new ArgumentOutOfRangeException("Invalid numer of parameters!\nAt least one parameter, but a maximum of two parameters are possible!");
                        break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error parsing command line arguments!");
                Console.WriteLine("\t{0,-20}{1}", "Error:", e.GetType().Name);
                Console.WriteLine("\t{0,-20}{1}", "Message:", e.Message);
                Console.WriteLine();
                return Help();
            }

            try
            {
                switch (operation)
                {
                    case Operation.Backup:
                        {
                            hashCopy.Action += hashLinkCopy_Action;
                            hashCopy.Error += hashLinkCopy_Error;
                            if (!String.IsNullOrEmpty(hashDir))
                                hashCopy.HashDir = hashDir;
                            // source directory
                            var sourceDirectory = Path.GetFullPath(parameters[0]);
                            print("Source path", sourceDirectory);
                            // get the destination folder
                            var destinationDirectory = parameters.Length >= 2 ? parameters[1] : Path.Combine(Directory.GetCurrentDirectory(), "*");
                            destinationDirectory = Path.GetFullPath(destinationDirectory.Replace("*", DateTime.Now.ToString("yyyy-MM-dd")));
                            print("Desitination path", destinationDirectory);
                            // RUN
                            hashCopy.Copy(sourceDirectory, destinationDirectory);
                            // Statistics
                            print("Start", hashCopy.Start);
                            print("End", hashCopy.End);
                            print("Duration", hashCopy.Elapsed);
                            print("Directories", DirectoryCount);
                            print("Files", FileCount);
                            print("Linked Files", LinkFileCount);
                            print("Linked Folders", LinkDirectoryCount);
                            print("Copied Files", CopyCount);
                            print("Copied Files", ExcludedCount);
                        }
                        break;
                    case Operation.Clean:
                        {
                            if (parameters.Length == 0)
                                parameters = new string[] {
                                    Directory.GetCurrentDirectory(),
                                };
                            if (String.IsNullOrEmpty(hashDir))
                                hashDir = HashEntry.GetDefaultHashDir(parameters.First());
                            var c = new HashCleanup(hashDir)
                            {
                                EnableDelete = enableDelete,
                            };
                            print("Hash dir", hashDir);
                            print("Enabled delete", c.EnableDelete ? "yes" : "no");
                            foreach (var dir in parameters)
                            {
                                Print(0, true, "Scanning: \"{0}\"", dir);
                                c.CheckDirectories(dir);
                            }
                            print("Used hashs", c.UsedHashCount);
                            if (c.UsedHashCount == 0)
                            {
                                Console.WriteLine("No has files where marked as used! There is propably an error in the parameters!");
                                Console.Write("Continue anyway (this will erase the entire Hash directory)? [yes/NO] ");
                                enableDelete = c.EnableDelete = false;
                                if (String.Compare(Console.ReadLine().Trim(), "yes", true) != 0)
                                {
                                    Console.WriteLine("aborted");
                                    return 1;
                                }
                            }
                            if (!enableDelete)
                                Console.WriteLine("Counting unused files and directories in hash folder...");
                            else
                                Console.WriteLine("Deleting files and directories from hash folder...");
                            c.DeleteUnused();
                            print(enableDelete ? "deleted files" : "unused files", c.deleteFileCount);
                            print(enableDelete ? "deleted folders" : "unused folders", c.deleteDirCount);
                            print(enableDelete ? "space gained" : "possible space", FormatBytes(c.totalBytesDeleted));
                            if ((c.deleteDirCount > 0 || c.deleteFileCount > 0) && !enableDelete)
                            {
                                Console.Write("Do you want to delete these data (type \"yes\" completely or use command line option --enableDelete)? [yes/NO] ");
                                if (String.Compare(Console.ReadLine().Trim(), "yes", true) != 0)
                                {
                                    Console.WriteLine("aborted");
                                    return 1;
                                }
                                c.EnableDelete = true;
                                Console.WriteLine("Deleting marked files and directories from hash folder...");
                                c.DeleteUnused();
                                if (c.UsedHashCount == 0)
                                {
                                    Console.WriteLine("Deleting hash root folder!");
                                    Directory.Delete(hashDir, true);
                                }
                            }
                            Console.WriteLine("done");
                        }
                        break;
                    default:
                        throw new NotSupportedException(String.Format("Operation {0} is not supported!", operation));
                }
                return 0;
            }
            catch (Exception e)
            {
                Console.WriteLine("{0} failed!", operation);
                Console.WriteLine("\t{0,-20}{1}", "Error:", e.GetType().Name);
                Console.WriteLine("\t{0,-20}{1}", "Message:", e.Message);
                return 2;
            }
        }
    }
}
