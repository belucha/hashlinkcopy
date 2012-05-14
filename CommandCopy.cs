using System;
using System.Threading;
using System.IO;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace de.intronik.hashlinkcopy
{
    [Description(@"copies one directory into the target path. An wildcard character * is replaced by the formatted date/time")]
    [Option(@"SkipLevel", Help = @"Skip existing folders at given path recursion depth")]
    [Option(@"PrevBackupFolderRoot", Help = @"Root folder for backups", Default = @"")]
    [Option(@"Pattern", Help = @"Date formatting used to replace the * wild card in the target path", Default = @"YYYY-MM-DD_HH.NN")]
    [Option(@"RestoreAttributes", Help = @"Restore attributes", Default = @"false")]
    [Option(@"RestoreLastWriteTime", Help = @"Restore last write time of files", Default = @"false")]
    [Option(@"ClearArchiveBit", Help = @"Clears the archive bit of the source file", Default = @"false")]
    [Option(@"MultiThread", Help = @"Run multiple threads", Default = @"false")]
    [Option(@"UsePreviousBackup", Help = @"Use Previous backup", Default = @"false")]
    [Option(@"CheckFileLength", Help = @"Check file length", Default = @"false")]
    class CommandCopy : CommandTreeWalker
    {
        public string Target { get; private set; }
        public string PreviousBackup { get; private set; }
        public string Pattern { get; private set; }
        public string PrevBackupFolderRoot { get; private set; }
        public int SkipLevel { get; private set; }
        const int maxBackgroundJobs = 50;
        bool restoreAttributes = false;
        bool restoreLastWriteTime = false;
        bool clearArchiveBit = false;
        bool multiThread = false;
        bool usePreviousBackup = false;
        bool checkFileLength = false;
        Queue<HashAlgorithm> hashProviders = new Queue<HashAlgorithm>();

        public override void Init(string[] parameters)
        {
            base.Init(parameters);
            this.Pattern = @"YYYY-MM-DD_HH.NN";
            this.PrevBackupFolderRoot = null;
            // try to replace any stuff in target folder
            if (parameters.Length != 2)
                throw new ArgumentOutOfRangeException("Excactly 2 parameters (Source folder and target folder) are required for COPY!");
            var now = DateTime.Now;
            this.Target = parameters[1];
            this.HashDir = null;
            this.PreviousBackup = null;
            this.SkipLevel = int.MaxValue;
        }

        protected override void ProcessOption(OptionAttribute option)
        {
            base.ProcessOption(option);
            if (option.Name == "SkipLevel") this.SkipLevel = (int)option.ParseAsLong(new KeyValuePair<string, long>("disabled", int.MaxValue), new KeyValuePair<string, long>("off", int.MaxValue));
            else if (option.Name == "Pattern") this.Pattern = option.ParseAsString();
            else if (option.Name == "PrevBackupFolderRoot") this.PrevBackupFolderRoot = option.Value;
            else if (option.Name == "RestoreAttributes") this.restoreAttributes = option.ParseAsBoolean();
            else if (option.Name == "RestoreLastWriteTime") this.restoreLastWriteTime = option.ParseAsBoolean();
            else if (option.Name == "ClearArchiveBit") this.clearArchiveBit = option.ParseAsBoolean();
            else if (option.Name == "MultiThread") this.multiThread = option.ParseAsBoolean();
            else if (option.Name == "CheckFileLength") this.checkFileLength = option.ParseAsBoolean();
            else if (option.Name == "UsePreviousBackup") this.usePreviousBackup = option.ParseAsBoolean();
        }

        protected override bool CancelEnterDirectory(DirectoryInfo dir, int level)
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

        class BackgroundProcessInfo
        {
            public FileInfo FileInfo;
            public int Level;
            public HashAlgorithm HashAlgorithm;
        }


        void DoProcessFile(FileInfo sourceFileInfo, int level, HashAlgorithm hashProvider)
        {
            if (Monitor.Root.DryRun) return;
            var sourceFilename = sourceFileInfo.FullName;
            // build target file name
            var targetFilename = this.RebasePath(sourceFilename, this.Target);
            // check if previous version of file can be found, that we could link to, this way we avoid to calc the SHA1 and a copy of attributes and
            if (this.usePreviousBackup && this.PreviousBackup != null)
                try
                {
                    var previousBackupFilename = this.RebasePath(sourceFilename, this.PreviousBackup);
                    var previousBackupFileInfo = new FileInfo(previousBackupFilename);
                    if (previousBackupFileInfo.Length == sourceFileInfo.Length && previousBackupFileInfo.LastWriteTimeUtc == sourceFileInfo.LastWriteTimeUtc && previousBackupFileInfo.Attributes == sourceFileInfo.Attributes
                        && Monitor.Root.LinkFile(previousBackupFilename, targetFilename, sourceFileInfo.Length) == 0)
                        return; // we successfully linked to the previous file => so we are done
                }
                catch (FileNotFoundException)
                {
                    // the previous backup file was not found => just continue with normal operation
                }
            // we have no previous directory or the file changed, or linking failed, use hash algorithm
            var hash = new HashInfo(sourceFileInfo, hashProvider);
            var hashFilename = hash.GetHashPath(this.HashDir);
            // lock the target hash path
            lock (hashFilename)
            {
                // check if we need to copy the file
                int linkError;
                do
                {
                    linkError = Monitor.Root.LinkFile(hashFilename, targetFilename, sourceFileInfo.Length);
                    switch (linkError)
                    {
                        case 0:     // ERROR_SUCCESS
                            break;
                        case 183:   // ERROR_ALREADY_EXISTS
                            return; // target file already existing
                        case 2:     // ERROR_FILE_NOT_FOUND
                        case 3:     // ERROR_PATH_NOT_FOUND                        
                            if (linkError == 3)
                                Monitor.Root.CreateDirectory(Path.GetDirectoryName(hashFilename));
                            Monitor.Root.CopyFile(sourceFilename, hashFilename, sourceFileInfo.Length);
                            File.SetAttributes(hashFilename, FileAttributes.Normal);
                            // run again
                            break;
                        case 1142:  // ERROR_TOO_MANY_LINKS
                            Monitor.Root.MoveFile(hashFilename, targetFilename, sourceFileInfo.Length);
                            linkError = 0;
                            break;
                        default:
                            throw new System.ComponentModel.Win32Exception(linkError, String.Format("CreateHardLink({0},{1}) returned 0x{2:X8}h", hashFilename, targetFilename));
                    }
                } while (linkError != 0);
                var targetFileInfo = this.checkFileLength ? new FileInfo(targetFilename) : null;
                if (targetFileInfo != null)
                    if (targetFileInfo.Length != sourceFileInfo.Length)
                        Monitor.Root.HashCollision(hashFilename, targetFilename);
                // adjust file attributes and the last write time                
                // make sure the backed up files have identical attributes and write times as the original
                if (this.restoreAttributes && (targetFileInfo == null || targetFileInfo.Attributes != sourceFileInfo.Attributes))
                    File.SetAttributes(targetFilename, sourceFileInfo.Attributes);
                if (this.restoreLastWriteTime && (targetFileInfo == null || targetFileInfo.LastWriteTimeUtc != sourceFileInfo.LastWriteTimeUtc))
                    File.SetLastWriteTimeUtc(targetFilename, sourceFileInfo.LastWriteTimeUtc);
                if (this.clearArchiveBit)
                    File.SetAttributes(sourceFilename, sourceFileInfo.Attributes & (~FileAttributes.Archive));
            }
        }

        void BackgroundWorkerProcessFile(object target)
        {
            var p = (BackgroundProcessInfo)target;
            this.DoProcessFile(p.FileInfo, p.Level, p.HashAlgorithm);
            lock (this.hashProviders)
                this.hashProviders.Enqueue(p.HashAlgorithm);
        }

        protected override void ProcessFile(FileInfo file, int level)
        {
            if (this.multiThread)
            {
                HashAlgorithm alg = null;
                while (alg == null)
                    if (this.cancel) return;
                    else
                        lock (this.hashProviders)
                            if (this.hashProviders.Count > 0)
                                alg = this.hashProviders.Dequeue();
                            else
                                Thread.Sleep(50);
                ThreadPool.QueueUserWorkItem(this.BackgroundWorkerProcessFile, new BackgroundProcessInfo()
                {
                    HashAlgorithm = alg,
                    FileInfo = file,
                    Level = level,
                });
            }
            else
                this.DoProcessFile(file, level, this.hashProviders.Peek());

        }

        protected override void LeaveDirectory(DirectoryInfo dir, int level)
        {
            base.LeaveDirectory(dir, level);
            // at this point we can set the last write time of the copied directory
            if (!Monitor.Root.DryRun)
            {
                var targetDir = this.RebasePath(dir.FullName, this.Target);
                if (this.restoreAttributes)
                    File.SetAttributes(targetDir, dir.Attributes);
                if (this.restoreLastWriteTime)
                    Directory.SetLastWriteTimeUtc(targetDir, dir.LastWriteTimeUtc);
            }
        }

        public override void Run()
        {
            // format the target directory
            if (!this.Target.EndsWith("\\")) this.Target += "\\";
            var wildCardPos = this.Target.IndexOf(@"\*\");
            string backupFolderSuffix = "";
            if (wildCardPos >= 0)
            {
                if (String.IsNullOrEmpty(HashDir))
                    this.HashDir = Path.Combine(this.Target.Substring(0, wildCardPos), "Hash");
                if (String.IsNullOrEmpty(this.PrevBackupFolderRoot))
                {
                    this.PrevBackupFolderRoot = this.Target.Substring(0, wildCardPos + 1);
                    backupFolderSuffix = this.Target.Substring(wildCardPos + 3);
                }
                this.Target = this.Target.Replace(@"\*\", @"\" + DateTime.Now.ToString(this.Pattern.ToLower().Replace("mm", "MM").Replace("hh", "HH").Replace("nn", "mm").Replace("nn", "mm")) + @"\");
                if (this.Target.IndexOf('*') >= 0)
                    throw new InvalidOperationException("The target folder may only contain one date/time wildcard * and must be surrounded by \\!");
            }
            else
                this.HashDir = (String.IsNullOrEmpty(this.HashDir) ? Path.Combine(this.Target, @"..\Hash") : this.HashDir);
            this.Target = Path.GetFullPath(this.Target);
            Directory.CreateDirectory(this.Target);
            Logger.Root.Logfilename = Path.Combine(this.Target, "Backup.log");
            this.HashDir = Path.GetFullPath(this.HashDir).ToLower();
            if (!this.HashDir.EndsWith("\\")) this.HashDir += "\\";
            // search for old backups
            if (this.usePreviousBackup && !String.IsNullOrEmpty(this.PrevBackupFolderRoot))
            {
                if (!this.PrevBackupFolderRoot.EndsWith("\\"))
                    this.PrevBackupFolderRoot += "\\";
                Logger.Root.PrintInfo("Backup folder", "{0}{1}{2}", this.PrevBackupFolderRoot, wildCardPos >= 0 ? @"\*\" : "", backupFolderSuffix);
                var backups = BackupFolder
                    .GetBackups(this.PrevBackupFolderRoot, this.Pattern)
                    .Where(backup => String.Compare(backup.Folder + "\\" + backupFolderSuffix, this.Target, true) != 0)
                    .OrderByDescending(backup => backup.BackupDate)
                    .ToArray();
                Logger.Root.WriteLine(Verbosity.Message, "Found {0} previous backups in {1}, matching pattern {2}", backups.Length, this.PrevBackupFolderRoot, this.Pattern);
                if (backups.Length > 0)
                {
                    var newestBackup = backups[0];
                    this.PreviousBackup = newestBackup.Folder + "\\" + backupFolderSuffix;
                    Logger.Root.PrintInfo("Previous backup folder", this.PreviousBackup);
                }
            }
            Logger.Root.PrintInfo("Pattern", this.Pattern);
            Logger.Root.PrintInfo("Source folder", this.Folder);
            Logger.Root.PrintInfo("Target folder", this.Target);
            Logger.Root.PrintInfo("Hash folder", this.HashDir);
            // create own hash provider for each task
            for (var i = 0; i < (this.multiThread ? maxBackgroundJobs : 1); i++)
                this.hashProviders.Enqueue(SHA1.Create());
            base.Run();
            if (this.multiThread)
            {
                Console.Write("Waiting for background jobs to complete...");
                var x = Console.CursorLeft;
                while (true)
                {
                    int c;
                    lock (this.hashProviders)
                        c = this.hashProviders.Count;
                    if (c == maxBackgroundJobs)
                        break;
                    else
                        Thread.Sleep(50);
                    Console.CursorLeft = x;
                    Console.Write("{0,5}", c);
                }
                Console.WriteLine("done.");
            }
        }
    }
}
