/* HashLinkCopy.exe Daniel Groß, Intronik GmbH */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

/* example usage script:
@echo off
set backupDir=d:\Temp
rem Calculate date dependent backup directory (For German systems with a %DATE% format like 'dd.mm.yyyy' only.)
set targetBase=%backupDir%\%DATE:~8,2%-%DATE:~3,2%-%DATE:~0,2%
HashLinkCopy.exe COPY C:\Projekte %targetBase%\Projekte
HashLinkCopy.exe COPY C:\Einkauf %targetBase%\Einkauf
HashLinkCopy.exe COPY C:\Verwaltung %targetBase%\Verwaltung
 */

[assembly: AssemblyTitle("HashLinkCopy")]
[assembly: AssemblyDescription("SHA1 based efficent hard link copy/backup tool")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Intronik GmbH, Daniel Groß")]
[assembly: AssemblyProduct("HashLinkCopy")]
[assembly: AssemblyCopyright("Copyright © Intronik GmbH 2012")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Durch Festlegen von ComVisible auf "false" werden die Typen in dieser Assembly unsichtbar 
// für COM-Komponenten. Wenn Sie auf einen Typ in dieser Assembly von 
// COM zugreifen müssen, legen Sie das ComVisible-Attribut für diesen Typ auf "true" fest.
[assembly: ComVisible(false)]
// Die folgende GUID bestimmt die ID der Typbibliothek, wenn dieses Projekt für COM verfügbar gemacht wird
[assembly: Guid("206c5295-2567-4b0c-9176-c33e49311fa2")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace HashLinkCopy
{
    public enum Command
    {
        None,
        Copy,
        Hash,
        Clean,
        Reduce,
        Shrink,
        Help,
    }

    public enum Option
    {
        Verbosity,
        HashDir,
        SkipLevel,
        PerformanceCounter,
        Exclude,
        HashCacheLimit,
        ReduceRules,
        EnableDelete,
    }


    public class Reducer
    {
        /* sample reduce rules file:
*YYYY-MM-DD*			; folder pattern to match
30						; minium number of backups to keep
14 times every day
5 times each week
10 times every month
10 times each 6 month
10 times each year
*/
        string pattern;
        Regex regPattern;
        public List<string> DeletedFolders { get; private set; }
        public bool EnableDelete { get; private set; }
        public int KeepMin { get; private set; }
        public Rule[] Rules { get; private set; }
        public string Folder { get; private set; }
        public string Pattern
        {
            get { return pattern; }
            set
            {
                this.pattern = value;
                this.regPattern = new Regex("^" + Regex.Escape(this.pattern).ToLower()
                    .Replace("yyyy", @"(?<YYYY>\d\d\d\d)")
                    .Replace("yy", @"(?<YY>\d\d)")
                    .Replace("mm", @"(?<MM>\d\d)")
                    .Replace("dd", @"(?<DD>\d\d)")
                    .Replace("\\*", ".*")
                    .Replace("\\?", ".")
                    + "$",
                    RegexOptions.ExplicitCapture | RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);
            }
        }

        public IEnumerable<KeyValuePair<Option, string>> ParseOptions(IEnumerable<KeyValuePair<Option, string>> options)
        {
            foreach (var option in options)
                if (option.Key == Option.ReduceRules)
                {
                    var fn = Path.GetFullPath(option.Value);
                    Logger.WriteLine(Verbosity.Message, "Reading reduce rules from '{0}'..", fn);
                    var lines = File.ReadAllLines(fn)
                        // trim comments
                        .Select(line => { var i = line.IndexOf(';'); return (i < 0 ? line : line.Substring(0, i)).Trim(' ', '\t', '\n', '\a', '\r'); })
                        // remove empty lines
                        .Where(line => !String.IsNullOrEmpty(line))
                        .ToArray();
                    if (lines.Length < 3)
                        throw new InvalidOperationException("Invalid reduce rule set, at least 3 lines expected!");
                    // the first line contains the pattern
                    this.Pattern = lines[0];
                    // the second line the 
                    this.KeepMin = (int)ushort.Parse(lines[1]);
                    this.Rules = lines.Skip(2).Select(line => new Rule(line)).OrderBy(rule => rule.Interval).ToArray();
                }
                else if (option.Key == Option.EnableDelete)
                    this.EnableDelete = true;
                else yield return option;
        }

        public Reducer(string directory)
        {
            this.DeletedFolders = new List<string>();
            this.KeepMin = 30;
            this.Rules = new Rule[] {
                new Rule(30, 1, Unit.day),
                new Rule(8, 1, Unit.week),
                new Rule(4, 1, Unit.month),
                new Rule(4, 3, Unit.month),
                new Rule(2, 6, Unit.month),
                new Rule(10, 1, Unit.year),
            };
            this.Folder = Path.GetFullPath(directory);
            this.Pattern = @"*yyyy-mm-dd*";
        }

        class BackupFolder
        {
            public string Folder { get; set; }
            public DateTime BackupDate { get; set; }
            public override string ToString()
            {
                return String.Format("{0:yyyy-MM-dd} {1}", BackupDate, Folder);
            }
        }

        public enum Unit
        {
            day = 1,
            week = 7,
            month = 30,
            year = 365,
        }

        public class Rule
        {
            static Regex ruleExpression = new Regex(@"^\s*(?<count>(\d+)?)\s*((times\s+(every|each))|x|=)?\s+(?<factor>(\d+)?)\s*(?<unit>(day|week|month|year))\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline | RegexOptions.ExplicitCapture);
            public Rule(int count, int factor, Unit unit)
            {
                this.Count = count;
                this.Factor = factor;
                this.Unit = unit;
            }
            public Rule(string line)
            {
                var match = ruleExpression.Match(line);
                if (!match.Success)
                    throw new InvalidOperationException(String.Format("Unkown rule format '{0}'", line));
                this.Count = int.Parse(match.Groups["count"].Value);
                this.Unit = (Unit)Enum.Parse(typeof(Unit), match.Groups["unit"].Value, true);
                int factor;
                if (!int.TryParse(match.Groups["factor"].Value, out factor))
                    factor = 1;
                this.Factor = factor;
            }
            public TimeSpan Interval { get { return TimeSpan.FromDays(this.Factor * (int)this.Unit); } }
            public int Count { get; private set; }
            public int Factor { get; private set; }
            public Unit Unit { get; private set; }
            public override string ToString()
            {
                return String.Format("Keep {0} intervals of {1}{2}", Count, Factor != 1 ? Factor.ToString() : "", Unit);
            }
        }

        IEnumerable<BackupFolder> GetBackups()
        {
            foreach (var subDir in Directory.GetDirectories(this.Folder))
            {
                var match = this.regPattern.Match(Path.GetFileName(subDir));
                if (!match.Success) continue;
                var year = DateTime.Now.Year;
                var month = 1;
                var day = 1;
                if (!String.IsNullOrEmpty(match.Groups["YYYY"].Value))
                    year = int.Parse(match.Groups["YYYY"].Value);
                else
                    if (!String.IsNullOrEmpty(match.Groups["YY"].Value))
                        year = 2000 + int.Parse(match.Groups["YY"].Value);

                if (!String.IsNullOrEmpty(match.Groups["MM"].Value))
                    month = int.Parse(match.Groups["MM"].Value);
                if (!String.IsNullOrEmpty(match.Groups["DD"].Value))
                    day = int.Parse(match.Groups["DD"].Value);
                DateTime backupDate;
                try
                {
                    backupDate = new DateTime(year, month, day);
                }
                catch (Exception)
                {
                    // ignore folder, invalid date format
                    continue;
                }
                yield return new BackupFolder()
                {
                    Folder = subDir,
                    BackupDate = backupDate,
                };
            }
        }

        public void Run()
        {
            var backups = GetBackups().OrderByDescending(b => b.BackupDate).ToList();
            if (backups.Count < 1)
            {
                Logger.Warning("No backups found, that match the specified pattern {0}!", Pattern);
                return;
            }
            var end = backups[0].BackupDate;
            var remove = new Action<TimeSpan, int>((interval, count) =>
            {
                DateTime start;
                if (backups.Count <= this.KeepMin) return;
                for (var i = 0; i < count; i++)
                {
                    start = end.Subtract(interval);
                    var removeList = backups.Where(backup => (start <= backup.BackupDate) && (backup.BackupDate < end)).Skip(1).ToArray();
                    foreach (var item2Remove in removeList)
                    {
                        if (backups.Count <= this.KeepMin) return;
                        backups.Remove(item2Remove);
                        this.DeletedFolders.Add(item2Remove.Folder);
                    }
                    end = start;
                }
            });
            foreach (var rule in this.Rules)
                remove(rule.Interval, rule.Count);
            Logger.WriteLine(Verbosity.Message, "keeping {0} folders:", backups.Count);
            foreach (var backup in backups)
                Logger.WriteLine(Verbosity.Message, "\t+ {0}", backup.Folder);
            foreach (var f in this.DeletedFolders)
            {
                Logger.WriteLine(Verbosity.Message, "Deleting backup folder {0}...", f);
                var start = DateTime.Now;
                try
                {
                    if (this.EnableDelete)
                        Directory.Delete(f, true);
                    else
                        Logger.WriteLine(Verbosity.Warning, "Deleting is disabled! Enable with option --EnableDelete");
                    Logger.WriteLine(Verbosity.Message, "...completed after {0}", DateTime.Now.Subtract(start));
                }
                catch (Exception error)
                {
                    Logger.Error("...failed with {0}: {1} after {2}", error.GetType().Name, error.Message, DateTime.Now.Subtract(start));
                }
            }
        }
        public override string ToString()
        {
            return String.Format("Deleted {0} folders:\n{1}", this.DeletedFolders.Count,
                String.Join("\n", this.DeletedFolders.Select(f => "\t- " + f).ToArray()));
        }
    }


    public class Backup
    {
        public string SourceFolder { get; private set; }
        public string TargetFolder { get; private set; }
        public string HashFolder { get; set; }
        public int NewCopyCount { get; private set; }
        public int HardLinkLimitReached { get; private set; }
        public int TotalFileCount { get; private set; }
        public int UpdateHashCount { get; private set; }
        public int HashCollision { get; private set; }
        public TimeSpan Elapsed { get; private set; }
        public long TotalBytesCopied { get; private set; }
        public long TotalBytesLinked { get; private set; }
        public int SkipLevel { get; set; }
        public ExcludeList ExcludeList { get; set; }
        PerformanceCounter pcFilesPerSecond_;
        PerformanceCounter pcTotalFiles_;

        public IEnumerable<KeyValuePair<Option, string>> ParseOptions(IEnumerable<KeyValuePair<Option, string>> options)
        {
            foreach (var option in options)
                switch (option.Key)
                {
                    case Option.HashDir:
                        this.HashFolder = Path.GetFullPath(option.Value);
                        break;
                    case Option.SkipLevel:
                        this.SkipLevel = int.Parse(option.Value);
                        break;
                    case Option.PerformanceCounter:
                        this.PerformanceCounterEnabled = String.IsNullOrEmpty(option.Value) ? true : Boolean.Parse(option.Value);
                        break;
                    case Option.Exclude:
                        this.ExcludeList = new ExcludeList(option.Value);
                        break;
                    default:
                        yield return option;
                        break;
                }
        }

        public void CreatePerformanceCounters()
        {
            try
            {
                const string pcCategory = @"de.intronik";
                const string pcCounterNameFilesPerSecond = @"HashLinkCopy.FilesPerSecond";
                const string pcCounterTotalFiles = @"HashLinkCopy.TotalFiles";
                if (!(
                    PerformanceCounterCategory.CounterExists(pcCounterNameFilesPerSecond, pcCategory) &&
                    PerformanceCounterCategory.CounterExists(pcCounterTotalFiles, pcCategory)
                ))
                {
                    Console.WriteLine("Creating performance counters...");
                    var ccdc = new CounterCreationDataCollection();
                    ccdc.Add(new CounterCreationData(pcCounterNameFilesPerSecond, "Processed files per s", PerformanceCounterType.RateOfCountsPerSecond32));
                    ccdc.Add(new CounterCreationData(pcCounterTotalFiles, "Total number of processed files", PerformanceCounterType.NumberOfItems64));
                    if (PerformanceCounterCategory.Exists(pcCategory))
                        PerformanceCounterCategory.Delete(pcCategory);
                    PerformanceCounterCategory.Create(pcCategory, @"Intronik GmbH performance counter", PerformanceCounterCategoryType.SingleInstance, ccdc);
                    Console.WriteLine("...succcess!");
                }
                else
                    Console.WriteLine("Performance already registered!");
                this.pcFilesPerSecond_ = new PerformanceCounter(pcCategory, pcCounterNameFilesPerSecond, "", false);
                this.pcTotalFiles_ = new PerformanceCounter(pcCategory, pcCounterTotalFiles, "", false);
            }
            catch (Exception error)
            {
                Logger.Warning("Failed to register performance counters! Make sure the program as administrive priveliges once!\nMessage {0}: {1}", error.GetType().Name, error.Message);
                this.PerformanceCounterEnabled = false;
            }
        }

        public bool PerformanceCounterEnabled { get; set; }

        public Backup(string sourceFolder, string targetFolder)
        {
            this.PerformanceCounterEnabled = true;
            this.ExcludeList = new ExcludeList();
            this.SkipLevel = int.MaxValue;  // by default never skip
            this.SourceFolder = Path.GetFullPath(sourceFolder);
            // try to replace any stuff in target folder
            var startP = targetFolder.IndexOf('{');
            if (startP >= 0)
            {
                var endP = targetFolder.IndexOf('}', startP + 1);
                if (endP >= 0)
                {
                    var format = targetFolder.Substring(startP + 1, endP - startP - 1).Trim();
                    if (format.Length == 0) format = "yyyy-MM-dd_HH_mm_ss";
                    targetFolder = targetFolder.Substring(0, startP) + DateTime.Now.ToString(format) + targetFolder.Substring(endP + 1);
                }
            }
            this.TargetFolder = Path.GetFullPath(targetFolder);
            this.HashFolder = Path.GetFullPath(Path.Combine(this.TargetFolder, @"..\Hashs"));
        }

        public void Run(Command command)
        {
            if (this.PerformanceCounterEnabled)
                this.CreatePerformanceCounters();
            var startTime = DateTime.Now;
            if (!Directory.Exists(this.SourceFolder))
                throw new FileNotFoundException("Source folder not found!", this.SourceFolder);
            this.BackupDirectory(0, this.TargetFolder, this.SourceFolder, command);
            this.Elapsed = DateTime.Now.Subtract(startTime);
        }

        void BackupDirectory(int level, string targetFolder, string sourceFolder, Command command)
        {
            try
            {
                if (command == Command.Copy && level >= this.SkipLevel && Directory.Exists(targetFolder))
                {
                    Logger.WriteLine(Verbosity.Message, "Skipping existing target folder '{0}' at level {1}", targetFolder, level);
                    return;
                }
                if (this.ExcludeList.Exclude(sourceFolder.EndsWith(@"\") ? sourceFolder : (sourceFolder + @"\")))
                {
                    Logger.WriteLine(Verbosity.Verbose, "Excluding source folder: {0}", sourceFolder);
                    return;
                }
                Logger.WriteLine(Verbosity.Verbose, "FOLDER: {0}", sourceFolder);
                if (command == Command.Copy)
                    Directory.CreateDirectory(targetFolder);
                foreach (var filename in Directory.GetFiles(sourceFolder))
                    try
                    {
                        if (this.ExcludeList.Exclude(filename))
                        {
                            Logger.WriteLine(Verbosity.Verbose, "Excluding source file: {0}", filename);
                            continue;
                        }
                        string tf = "";
                        if (command == Command.Copy)
                        {
                            tf = Path.Combine(targetFolder, Path.GetFileName(filename));
                            if (File.Exists(tf)) continue;  // skip existing files
                        }
                        var hi = new HashInfo(filename, command == Command.Hash);
                        if (command != Command.Hash)
                        {
                            var hp = hi.GetHashPath(this.HashFolder);
                            if (hi.Updated) this.UpdateHashCount++;
                            if (!File.Exists(hp))
                            {
                                this.TotalBytesCopied += hi.Length;
                                this.NewCopyCount++;
                                Logger.WriteLine(Verbosity.Debug, "Copy {0} to {1}", filename, hp);
                                Directory.CreateDirectory(Path.GetDirectoryName(hp));
                                File.Copy(filename, hp);
                            }
                            // before creating a link, we check if the file sizes match
                            // otherwise we would have a hash collision
                            if (new FileInfo(hp).Length != hi.Length)
                            {
                                this.HashCollision++;
                                Logger.Error("FATAL Hashcollision '{0}' and '{1}'", hp, filename);
                            }
                            Logger.WriteLine(Verbosity.Debug, "Linking {0} to {1}", tf, hp);
                            if (!Win32.CreateHardLink(tf, hp, IntPtr.Zero))
                            {
                                Logger.WriteLine(Verbosity.Message, "Hardlink limit (1023), renaming {0} to {1}", hp, tf);
                                this.HardLinkLimitReached++;
                                // hard link creation failed, propably because the 1023 maximum links count is exceeded, we fix this by
                                // just use the hast file as my copy, the next required copy will then use this file
                                File.Move(hp, tf);
                                // copy attributes and last write time
                                File.SetLastAccessTimeUtc(tf, hi.LastWriteTimeUtc);
                                File.SetAttributes(tf, File.GetAttributes(filename));
                            }
                        }
                        if (this.PerformanceCounterEnabled)
                        {
                            this.pcFilesPerSecond_.Increment();
                            this.pcTotalFiles_.Increment();
                        }
                        this.TotalFileCount++;
                        if (command == Command.Copy)
                            this.TotalBytesLinked += hi.Length;
                    }
                    catch (Exception error)
                    {
                        Logger.Error("{0}:{1} processing file '{2}'", error.GetType().Name, error.Message, filename);
                    }
                foreach (var subDir in Directory.GetDirectories(sourceFolder))
                    BackupDirectory(level + 1, Path.Combine(targetFolder, Path.GetFileName(subDir)), subDir, command);
            }
            catch (Exception error)
            {
                Logger.Error("{0}:{1} processing source directory '{2}'", error.GetType().Name, error.Message, sourceFolder);
            }
        }

        public override string ToString()
        {
            return String.Format(@"Backup statistic:
    Total files  : {0}
    Total bytes  : {1}
    Copied files : {2}
    Copied bytes : {3}
    Hashs updated: {4}
    Elapsed time : {5}
    Error count  : {6}
    Warning count: {7}
    Link overrun : {8}
    Hash collisio: {9}",
    TotalFileCount,
    TotalBytesLinked,
    NewCopyCount,
    TotalBytesCopied,
    UpdateHashCount,
    Elapsed,
    Logger.ErrorCount,
    Logger.WarningCount,
    HardLinkLimitReached,
    HashCollision
    );
        }
    }

    class Program
    {
        static bool UnconsumedOptions(IEnumerable<KeyValuePair<Option, string>> options)
        {
            int c = 0;
            foreach (var option in options)
            {
                Console.WriteLine("\tUnhandled option --{0} with value {1}", option.Key, option.Value);
                c++;
            }
            return c > 0;
        }

        static int Clean(int level, string dir)
        {
            int count = 0;
            if (level < 19)
                for (var b = 0; b <= 255; b++)
                {
                    var f = Path.Combine(dir, b.ToString("x2"));
                    if (Directory.Exists(f))
                        count += Clean(level + 1, f);
                }
            else
                for (var b = 0; b <= 255; b++)
                {
                    var f = Path.Combine(dir, b.ToString("x2"));
                    if (Win32.GetFileLinkCount(f) == 1)
                    {
                        count++;
                        try
                        {
                            File.Delete(f);
                            Logger.WriteLine(Verbosity.Verbose, "Deleted {0}", f);
                        }
                        catch (Exception error)
                        {
                            Logger.Error("Deleting {0} failed with {1}: {2}", f, error.GetType().Name, error.Message);
                        }

                    }
                }
            return count;
        }

        static int Help()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("\tHashLinkCopy.exe [COPY|HASH|CLEAN|REDUCE|HELP] [options] [SourceDir] [TargetDir]");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("\tCOPY\tCopies SourceDir into TargetDir using HASHs. The TargetDir can contain {} which");
            Console.WriteLine("\t    \tis replaced by the current date. Formatting like {YYYY-MM-dd} is possible.");
            Console.WriteLine("\t    \tExample: HashLinkCopy.exe COPY C:\\Projekte\\ D:\\Backup\\{YYYY-MM-dd}\\");
            Console.WriteLine();
            Console.WriteLine("\tCLEAN\tCleans the target Hash directory from all files with no other reference.");
            Console.WriteLine("\t    \tOnly hash files are deleted!");
            Console.WriteLine("\t    \tExample: HashLinkCopy.exe CLEAN D:\\Backup\\Hashs\\");
            Console.WriteLine();
            Console.WriteLine("\tREDUCE\tRemoves old backups. In the given directory.");
            Console.WriteLine("\t    \tExample: HashLinkCopy.exe REDUCE --enableDelete --ReduceRules=RuleFile.txt D:\\Backup\\");
            Console.WriteLine("\t   The content of the rules file should be:");
            Console.WriteLine("\t\t\t; this is a comment line");
            Console.WriteLine("\t\t\tBackup-YY-MM-DD*   ; the first non empty line should be the backup folder match pattern");
            Console.WriteLine("\t\t\t20                 ; the second line contains the number of backups to keep regardless of the rules");
            Console.WriteLine("\t\t\t14 times each day  ; all lines following must contain rules");
            Console.WriteLine("\t\t\t7 times every 2 day; of intervals for which to keep one backup");
            Console.WriteLine("\t\t\t4 times every week");
            Console.WriteLine("So '8 times every 2 week' means to keep one backup for every 2 week interval for 8 two week intervals (4 month).");
            Console.WriteLine();
            Console.WriteLine("\tHASH\tRuns a HASH caching calcultion for the given directory and all sub directories.");
            Console.WriteLine("\t    \tExample: HashLinkCopy.exe HASH C:\\Projekte\\");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("\t--Verbosity:None|Error|Warning|Message|Verbose|Debug");
            Console.WriteLine("\t--SkipLevel:Number - skip existing directories at the specified recursion level");
            Console.WriteLine("\t--HashCacheLimit:Number - mininum file size before the hash value is cached. (default 4kb)");
            Console.WriteLine("\t--PerformanceCounter[:True|False] - Enable performance counter");
            Console.WriteLine("\t--HashDir:Path of hash directory");
            Console.WriteLine("\t   The Hash directory is by default one level higher than the target directory + Hashs");
            Console.WriteLine("\t--Exclude:Filename with exclude wild card list");
            Console.WriteLine("\t   The content of the exclude file could be:");
            Console.WriteLine("\t\t\t; this is a comment line");
            Console.WriteLine("\t\t\t*.bak  ; exclude all files with bak extension");
            Console.WriteLine("\t\t\t*\\.git\\  ; exclude all .git directories in any level");
            Console.WriteLine("\t\t\t*vshost*  ; exclude all files containing the string vshost anywhere in the path");
            Console.WriteLine();
            return 1;
        }
}
