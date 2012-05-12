// #define MULTITHREAD
using System;
using System.Threading;
using System.IO;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.hashlinkcopy
{
    [Description(@"copies one directory into the target path. An wildcard character * is replaced by the formatted date/time")]
    [Option(@"SkipLevel", Help = @"Skip existing folders at given path recursion depth")]
    [Option(@"Pattern", Help = @"Date formatting used to replace the * wild card in the target path", Default = @"YYYY-MM-DD_HH.NN")]
    class CommandCopy : CommandTreeWalker
    {
        public string Target { get; private set; }
        public string Pattern { get; private set; }
        public int SkipLevel { get; private set; }
#if MULTITHREAD
        const int maxBackgroundJobs = 50;
        Semaphore backgroundJobs;
#endif

        public override void Init(string[] parameters)
        {
            base.Init(parameters);
            this.Pattern = @"YYYY-MM-DD_HH.NN";
            // try to replace any stuff in target folder
            if (parameters.Length != 2)
                throw new ArgumentOutOfRangeException("Excactly 2 parameters (Source folder and target folder) are required for COPY!");
            var now = DateTime.Now;
            this.Target = parameters[1];
            this.HashDir = null;
            this.SkipLevel = int.MaxValue;
        }

        protected override void ProcessOption(OptionAttribute option)
        {
            base.ProcessOption(option);
            if (option.Name == "SkipLevel") this.SkipLevel = (int)option.ParseAsLong(new KeyValuePair<string, long>("disabled", int.MaxValue), new KeyValuePair<string, long>("off", int.MaxValue));
            else if (option.Name == "Pattern") this.Pattern = option.ParseAsString();
        }

        protected override bool CancelEnterDirectory(FileData dir, int level)
        {
            // get target folder
            var tf = this.RebasePath(dir.FullName, this.Target);
            // check if existing folder should be skipped
            var exists = Directory.Exists(tf);
            if (exists && level >= this.SkipLevel) return true;
            // check if target folder must be created
            if (!exists)
                // create it and duplicate creation&write time and attributes
                Monitor.Root.CreateDirectory(tf);
            return false;
        }

        void DoProcessFile(FileData info, int level)
        {
            var tf = this.RebasePath(info.FullName, this.Target);
            var tinfo = new FileData(tf);
            if (tinfo.Exists)
            {
                // skip existing target files with the same length
                if (tinfo.Length == info.Length) return;
                // length is not the same, delete target file
                Monitor.Root.DeleteFile(tinfo);
            }
            // we have no previous directory or the file changed, or linking failed, use hash algorithm
            var hi = new HashInfo(info.FullName);
            var hf = hi.GetHashPath(this.HashDir);
            lock (hf)   // lock the target hash path, we can do a lock on a string, since the string is interned!
            {
                // check if we need to copy the file
                var hashFile = new FileData(hf);
                // check for link count overrun
                switch (hashFile.NumberOfLinks)
                {
                    case 0:
                        // hash file does not exist yet -> copy file and then link it
                        Monitor.Root.CreateDirectory(Path.GetDirectoryName(hf));
                        Monitor.Root.CopyFile(info.FullName, hf, info.Length);
                        File.SetAttributes(hf, FileAttributes.Normal);
                        Monitor.Root.LinkFile(hf, tf, info.Length);
                        Monitor.Root.AdjustFileSettings(tf, info);
                        break;
                    case 1023:
                        // 10bit link count overrun => just use the hashfile as actual file
                        // the hash file will copied the next time it is needed
                        Monitor.Root.MoveFile(hf, tf, info.Length);
                        Monitor.Root.AdjustFileSettings(tf, info);
                        break;
                    default:
                        // hash file exitst, check for no collssion
                        if (hashFile.Length == info.Length)
                        {
                            // we do just do an link operation
                            Monitor.Root.LinkFile(hf, tf, info.Length);
                            Monitor.Root.AdjustFileSettings(tf, info);
                        }
                        else
                        {
                            // hash collission, copy target file directly
                            Monitor.Root.HashCollision(hf, info.FullName);
                            Monitor.Root.CopyFile(info.FullName, tf, info.Length);
                        }
                        break;
                }
            }
        }

#if MULTITHREAD
        void BackgroundWorkerProcessFile(object target)
        {
            var kvp = (KeyValuePair<FileData, int>)target;
            try
            {
                this.DoProcessFile(kvp.Key, kvp.Value);
            }
            catch (Exception error)
            {
                Monitor.Root.Error(kvp.Key.FullName, error);
            }
            // job is completed, decrement number of uncompleted job
            backgroundJobs.Release();
        }
#endif

        protected override void ProcessFile(FileData file, int level)
        {
#if !MULTITHREAD
            this.DoProcessFile(file, level);
#else           
            // do not start an endless amount of waiting threads
            this.backgroundJobs.WaitOne();
            // que job and increment number of uncompleted jobs
            ThreadPool.QueueUserWorkItem(this.BackgroundWorkerProcessFile, new KeyValuePair<FileData, int>(file, level));
#endif
        }

        protected override void LeaveDirectory(FileData dir, int level)
        {
            base.LeaveDirectory(dir, level);
            // at this point we can set the last write time of the copied directory
            if (!Monitor.Root.DryRun)
                Directory.SetLastWriteTimeUtc(this.RebasePath(dir.FullName, this.Target), dir.LastWriteTimeUtc);
        }

        public override void Run()
        {
            // format the target directory
            if (!this.Target.EndsWith("\\")) this.Target += "\\";
            var wildCardPos = this.Target.IndexOf(@"\*\");
            if (wildCardPos >= 0)
            {
                if (String.IsNullOrEmpty(HashDir))
                    this.HashDir = Path.Combine(this.Target.Substring(0, wildCardPos), "Hash");
                this.Target = this.Target.Replace(@"\*\", @"\" + DateTime.Now.ToString(this.Pattern.ToLower().Replace("mm", "MM").Replace("hh", "HH").Replace("nn", "mm").Replace("nn", "mm")) + @"\");
                if (this.Target.IndexOf('*') >= 0)
                    throw new InvalidOperationException("The target folder may only contain one date/time wildcard * and must be surrounded by \\!");
            }
            else
                this.HashDir = String.IsNullOrEmpty(this.HashDir) ? Path.Combine(this.Target, @"..\Hash") : this.HashDir;
            this.Target = Path.GetFullPath(this.Target);
            Directory.CreateDirectory(this.Target);
            Logger.Root.Logfilename = Path.Combine(this.Target, "Backup.log");
            this.HashDir = Path.GetFullPath(this.HashDir);
            Logger.Root.PrintInfo("Pattern", this.Pattern);
            Logger.Root.PrintInfo("Source folder", this.Folder);
            Logger.Root.PrintInfo("Target folder", this.Target);
            Logger.Root.PrintInfo("Hash folder", this.HashDir);
#if MULTITHREAD
            // prepare background jobs
            this.backgroundJobs = new Semaphore(maxBackgroundJobs, maxBackgroundJobs);
#endif
            base.Run();
#if MULTITHREAD
            Console.Write("Waiting for background jobs to complete...");
            for (var i = 0; i < maxBackgroundJobs; i++)
            {
                this.backgroundJobs.WaitOne();
                Console.Write(".");
            }
            Console.WriteLine("done.");
            this.backgroundJobs.Close();
#endif
        }
    }
}
