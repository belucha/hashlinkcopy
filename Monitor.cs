using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.hashlinkcopy
{
    public class Monitor
    {
        public static Monitor Root = new Monitor();
        public bool DryRun { get; set; }
        const string _pcCategory = @"HashLinkCopy";

        enum CounterType
        {
            processedFiles,
            skippedFiles,
            processedDir,
            skippedDir,
            copiedFiles,
            linkedFiles,
            hashedFiles,
            movedFiles,
            copiedBytes,
            linkedBytes,
            hashedBytes,
            movedBytes,
            deletedFiles,
            collisions,
            errors,
            max,
        };

        class Counter
        {
            PerformanceCounter pcCount;
            PerformanceCounter pcPerSecond;
            public CounterType CounterType { get; private set; }
            public long Count { get; private set; }
            public Counter(CounterType counterType)
            {
                this.CounterType = counterType;
            }
            public void CreatePc()
            {
                pcPerSecond = new PerformanceCounter(_pcCategory, CounterNamePerSecond, "", false);
                pcCount = new PerformanceCounter(_pcCategory, CounterNameTotal, "", false);
            }

            string CounterNamePerSecond { get { return String.Format("{0}_PerSecond", CounterType); } }
            string CounterNameTotal { get { return String.Format("{0}_Total", CounterType); } }

            public bool RecreatePcRequired()
            {
                return
                    !(PerformanceCounterCategory.CounterExists(CounterNamePerSecond, _pcCategory) &&
                    PerformanceCounterCategory.CounterExists(CounterNameTotal, _pcCategory));

            }

            public IEnumerable<CounterCreationData> CreateCounters()
            {
                yield return new CounterCreationData(String.Format(CounterNameTotal, CounterType), "", PerformanceCounterType.NumberOfItems64);
                yield return new CounterCreationData(String.Format(CounterNamePerSecond, CounterType), "", PerformanceCounterType.RateOfCountsPerSecond32);
            }


            public void Increment()
            {
                Count++;
                if (pcCount != null)
                {
                    pcCount.Increment();
                    pcPerSecond.Increment();
                }
            }
            public void Increment(long value)
            {
                Count += value;
                if (pcCount != null)
                {
                    pcCount.IncrementBy(value);
                    pcPerSecond.IncrementBy(value);
                }
            }
        }

        Counter[] counters;
        public bool EnablePC { get; set; }

        public Monitor()
        {
            this.EnablePC = false;
            this.counters = Enumerable
                    .Range(0, (int)CounterType.max)
                    .Select(i => (CounterType)i)
                    .Select(c => new Counter(c))
                    .ToArray();
        }

        public void Init()
        {
            if (this.EnablePC)
                try
                {
                    if (!PerformanceCounterCategory.Exists(_pcCategory) || this.counters.Any(counter => counter.RecreatePcRequired()))
                    {
                        Logger.WriteLine(Logger.Verbosity.Verbose, "Creating performance counters...");
                        if (PerformanceCounterCategory.Exists(_pcCategory))
                            PerformanceCounterCategory.Delete(_pcCategory);
                        PerformanceCounterCategory.Create(_pcCategory, @"HashLinkCopy performance counter", PerformanceCounterCategoryType.SingleInstance, new CounterCreationDataCollection(this.counters.SelectMany(c => c.CreateCounters()).ToArray()));
                        Logger.WriteLine(Logger.Verbosity.Verbose, "...succcess!");
                    }
                    else
                        Logger.WriteLine(Logger.Verbosity.Verbose, "Performance already registered!");
                    foreach (var c in this.counters)
                        c.CreatePc();
                }
                catch (Exception error)
                {
                    Logger.Warning("Failed to register performance counters! Make sure the program as administrive priveliges once!\nMessage {0}: {1}", error.GetType().Name, error.Message);
                }
        }

        void Count(CounterType counter)
        {
            this.counters[(int)counter].Increment();
        }
        void Count(CounterType counter, long size)
        {
            this.counters[(int)counter].Increment(size);
        }

        public static void ProcessFile(string path)
        {
            Root.Count(CounterType.processedFiles);
            Logger.WriteLine(Logger.Verbosity.Debug, "FILE: {0}", path);
        }
        public static void ProcessDirectory(string path)
        {
            Root.Count(CounterType.processedDir);
            Logger.WriteLine(Logger.Verbosity.Verbose, "FOLDER: {0}", path);
        }
        public static void SkipFile(string path, string reason)
        {
            Root.Count(CounterType.skippedFiles);
            Logger.WriteLine(Logger.Verbosity.Verbose, "Skipping file '{0}': {1}", path, reason);
        }
        public static void SkipDirectory(string path, string reason)
        {
            Root.Count(CounterType.skippedDir);
            Logger.WriteLine(Logger.Verbosity.Verbose, "Skipping folder '{0}': {1}", path, reason);
        }
        public static void CopyFile(string source, string dest, long size)
        {
            if (!Root.DryRun) File.Copy(source, dest);
            Root.Count(CounterType.copiedFiles);
            Root.Count(CounterType.copiedBytes, size);
            Logger.WriteLine(Logger.Verbosity.Verbose, "Copy file '{0}' to '{1}'", source, dest);
        }
        public static bool LinkFile(string source, string dest, long size)
        {
            if (Root.DryRun || Win32.CreateHardLink(dest, source, IntPtr.Zero))
            {
                Root.Count(CounterType.linkedFiles);
                Root.Count(CounterType.linkedBytes, size);
                Logger.WriteLine(Logger.Verbosity.Verbose, "Link file '{0}' to '{1}'", dest, source);
                return true;
            }
            else
                return false;
        }

        private static void DeleteFileSystemInfo(FileSystemInfo fsi)
        {
            if (!Root.DryRun)
                fsi.Attributes = FileAttributes.Normal;
            var di = fsi as DirectoryInfo;
            Root.Count(CounterType.deletedFiles);
            if (di != null)
                foreach (var dirInfo in di.GetFileSystemInfos())
                    DeleteFileSystemInfo(dirInfo);
            if (!Root.DryRun)
                fsi.Delete();
        }

        public static void DeleteDirectory(string path)
        {
            Logger.WriteLine(Logger.Verbosity.Verbose, "Deleting directory '{0}'", path);
            Monitor.DeleteFileSystemInfo(new DirectoryInfo(path));
        }

        public static void DeleteFile(string path)
        {
            Logger.WriteLine(Logger.Verbosity.Verbose, "Deleting file '{0}'", path);
            Monitor.DeleteFileSystemInfo(new FileInfo(path));
        }

        public static void CreateDirectory(string path)
        {
            Logger.WriteLine(Logger.Verbosity.Verbose, "Creating Directory '{0}'", path);
            if (!Root.DryRun) Directory.CreateDirectory(path);
        }

        public static void MoveFile(string source, string dest, long size)
        {
            if (!Root.DryRun) File.Move(source, dest);
            Root.Count(CounterType.movedFiles);
            Root.Count(CounterType.movedBytes, size);
            Logger.WriteLine(Logger.Verbosity.Verbose, "Moving file '{0}' to '{1}'", source, dest);
        }
        public static void HashFile(string source, long size)
        {
            Root.Count(CounterType.hashedFiles);
            Root.Count(CounterType.hashedBytes, size);
            Logger.WriteLine(Logger.Verbosity.Verbose, "SHA1 of '{0}' ({1}byte)", source, size);
        }
        public static void HashCollision(string path1, string path2)
        {
            Root.Count(CounterType.collisions);
            Logger.WriteLine(Logger.Verbosity.Error, "Hash Collision '{0}'<->'{1}'", path1, path2);
        }
        public static void Error(string path, Exception error)
        {
            Root.Count(CounterType.errors);
            Logger.Error("{0}:{1} processing '{2}'", error.GetType().Name, error.Message, path);
        }

        public static void PrintStatistics()
        {
            foreach (var c in Root.counters)
                Logger.WriteLine(Logger.Verbosity.Message, "{0,-20}: {1}", c.CounterType, c.Count);
        }
    }

}
