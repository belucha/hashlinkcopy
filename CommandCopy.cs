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
        const int maxBackgroundJobs = 50;
        Semaphore backgroundJobs;

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
            var tf = this.RebasePath(dir.Path, this.Target);
            // check if existing folder should be skipped
            var exists = Directory.Exists(tf);
            if (exists && level >= this.SkipLevel) return true;
            // check if target folder must be created
            if (!exists)
            {
                // create it and duplicate creation&write time and attributes
                Monitor.Root.CreateDirectory(tf);
                Directory.SetCreationTimeUtc(tf, dir.CreationTimeUtc);
                Directory.SetLastWriteTimeUtc(tf, dir.LastWriteTimeUtc);
                File.SetAttributes(tf, dir.Attributes);
            }
            return false;
        }

        void DoProcessFile(FileData info, int level)
        {
            var path = info.Path;
            // build target file name
            var tf = this.RebasePath(path, this.Target);
            // skip existing target files
            if (File.Exists(tf))
                return;
            // we have no previous directory or the file changed, or linking failed, use hash algorithm
            var hi = new HashInfo(path);
            var hf = Path.GetFullPath(hi.GetHashPath(this.HashDir));
            // lock the target hash path
            lock (String.Intern(hf))
            {
                // check if we need to copy the file
                if (!File.Exists(hf))
                {
                    Monitor.Root.CreateDirectory(Path.GetDirectoryName(hf));
                    Monitor.Root.CopyFile(path, hf, info.Size);
                    File.SetAttributes(hf, FileAttributes.Normal);
                }
                var hInfo = new FileInfo(hf);
                if (hInfo.Length != info.Size)
                {
                    Monitor.Root.HashCollision(hf, path);
                    Monitor.Root.DeleteFile(hf);
                    Monitor.Root.CopyFile(path, hf, info.Size);
                    return;
                }
                // create link
                if (!Monitor.Root.LinkFile(hf, tf, info.Size))
                    Monitor.Root.MoveFile(hf, tf, info.Size); // 10bit link count overrun => move file
                // adjust file attributes and the last write time
                try
                {
                    if (!Monitor.Root.DryRun)
                    {
                        // make sure the backed up files have identical attributes and write times as the original
                        File.SetAttributes(path, info.Attributes);
                        File.SetLastWriteTimeUtc(tf, info.LastWriteTimeUtc);
                        // remove the archive attribute of the original file
                        File.SetAttributes(path, info.Attributes & (~FileAttributes.Archive));
                    }
                }
                catch
                {
                }
            }
        }

        void BackgroundWorkerProcessFile(object target)
        {
            var kvp = (KeyValuePair<FileData, int>)target;
            try
            {
                this.DoProcessFile(kvp.Key, kvp.Value);
            }
            catch (Exception error)
            {
                Monitor.Root.Error(kvp.Key.Path, error);
            }
            // job is completed, decrement number of uncompleted job
            backgroundJobs.Release();
        }

        protected override void ProcessFile(FileData file, int level)
        {
            // do not start an endless amount of waiting threads
            this.backgroundJobs.WaitOne();
            // que job and increment number of uncompleted jobs
            ThreadPool.QueueUserWorkItem(this.BackgroundWorkerProcessFile, new KeyValuePair<FileData, int>(file, level));
        }

        protected override void LeaveDirectory(FileData dir, int level)
        {
            base.LeaveDirectory(dir, level);
            // at this point we can set the last write time of the copied directory
            if (!Monitor.Root.DryRun)
                Directory.SetLastWriteTimeUtc(this.RebasePath(dir.Path, this.Target), dir.LastWriteTimeUtc);
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
            // prepare background jobs
            this.backgroundJobs = new Semaphore(maxBackgroundJobs, maxBackgroundJobs);
            base.Run();
            Console.Write("Waiting for background jobs to complete...");
            for (var i = 0; i < maxBackgroundJobs; i++)
            {
                this.backgroundJobs.WaitOne();
                Console.Write(".");
            }
            Console.WriteLine("done.");
            this.backgroundJobs.Close();
        }
    }
}
