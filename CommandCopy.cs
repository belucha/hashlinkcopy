using System;
using System.IO;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.hashlinkcopy
{
    [Description(@"copies one directory into another %yyyy%mm%dd can be used in the target path")]
    [Option(@"SkipLevel", Help = @"Skip existing folders at given path recursion depth")]
    [Option(@"PrevBackupFolderMask", Help = @"Pattern to match previous backup folders", Default = @"*YYYY-MM-DD*")]
    [Option(@"PrevBackupFolderRoot", Help = @"Root folder for backups", Default = @"")]
    class CommandCopy : CommandTreeWalker
    {
        public string Target { get; private set; }
        public string PreviousBackup { get; private set; }
        public string PrevBackupFolderMask { get; private set; }
        public string PrevBackupFolderRoot { get; private set; }
        public int SkipLevel { get; private set; }

        public override void Init(string[] parameters)
        {
            base.Init(parameters);
            this.PrevBackupFolderMask = @"*YYYY-MM-DD*";
            this.PrevBackupFolderRoot = null;
            // try to replace any stuff in target folder
            if (parameters.Length != 2)
                throw new ArgumentOutOfRangeException("Excactly 2 parameters (Source folder and target folder) are required for COPY!");
            var now = DateTime.Now;
            var target = parameters[1]
                .Replace("%yyyy", now.Year.ToString("0000"))
                .Replace("%YYYY", now.Year.ToString("0000"))
                .Replace("%YY", now.Year.ToString("00"))
                .Replace("%yy", now.Year.ToString("00"))
                .Replace("%MM", now.Month.ToString("00"))
                .Replace("%mm", now.Month.ToString("00"))
                .Replace("%DD", now.Day.ToString("00"))
                .Replace("%dd", now.Day.ToString("00"))
                .Replace("%HH", now.Hour.ToString("00"))
                .Replace("%hh", now.Hour.ToString("00"))
                .Replace("%NN", now.Minute.ToString("00"))
                .Replace("%nn", now.Minute.ToString("00"))
                .Replace("%SS", now.Second.ToString("00"))
                .Replace("%ss", now.Second.ToString("00"))
                .Replace("%%", "%");
            if (target.IndexOf('%') >= 0)
                throw new ArgumentOutOfRangeException("Unknown escape sequence in target path {0}", target);
            this.Target = Path.GetFullPath(target);
            // hashdir is now relative to the target path
            this.HashDir = Path.GetFullPath(Path.Combine(this.Target, "..\\Hash\\"));
            this.PreviousBackup = null;
            this.SkipLevel = int.MaxValue;
        }

        protected override void ProcessOption(OptionAttribute option)
        {
            base.ProcessOption(option);
            if (option.Name == "SkipLevel") this.SkipLevel = int.Parse(option.Value);
            else if (option.Name == "PrevBackupFolderMask") this.PrevBackupFolderMask = option.Value;
            else if (option.Name == "PrevBackupFolderRoot") this.PrevBackupFolderRoot = Path.GetFullPath(option.Value);
        }

        protected override bool CancelEnterDirectory(string path, int level)
        {
            // get target folder
            var tf = this.RebasePath(path, this.Target);
            // check if existing folder should be skipped
            var exists = Directory.Exists(tf);
            if (exists && level >= this.SkipLevel) return true;
            // check if target folder must be created
            if (!exists)
            {
                // create it and duplicate creation&write time and attributes
                Monitor.CreateDirectory(tf);
                Directory.SetCreationTimeUtc(tf, Directory.GetCreationTimeUtc(path));
                Directory.SetLastWriteTimeUtc(tf, Directory.GetLastWriteTimeUtc(path));
                File.SetAttributes(tf, File.GetAttributes(path));
            }
            return false;
        }

        protected override void ProcessFile(string path, int level)
        {
            // build target file name
            var tf = this.RebasePath(path, this.Target);
            // skip existing target files
            if (File.Exists(tf)) return;
            var info = new FileInfo(path);
            // check if previous version of file can be found, that we could link to, this way we avoid to calc the SHA1
            if (this.PreviousBackup != null)
            {
                var pf = this.RebasePath(path, this.PreviousBackup);
                if (File.Exists(pf))
                {
                    var pInfo = new FileInfo(pf);
                    if (pInfo.Length == info.Length && pInfo.LastWriteTimeUtc == info.LastWriteTimeUtc &&
                        (info.Attributes & FileAttributes.Archive) == 0)
                        if (Monitor.LinkFile(pf, tf, info.Length))
                        {
                            File.SetAttributes(path, info.Attributes & (~FileAttributes.Archive));
                            return;
                        }
                }
            }
            // we have no previous directory or the file changed, or linking failed, use hash algorithm
            var hi = new HashInfo(path);
            var hf = Path.GetFullPath(hi.GetHashPath(this.HashDir));
            // check if we need to copy the file
            if (!File.Exists(hf))
            {
                Monitor.CreateDirectory(Path.GetDirectoryName(hf));
                Monitor.CopyFile(path, hf, info.Length);
                File.SetAttributes(hf, FileAttributes.Normal);
            }
            var hInfo = new FileInfo(hf);
            if (hInfo.Length != info.Length)
            {
                Monitor.HashCollision(hf, path);
                Monitor.CopyFile(path, hf, info.Length);
                return;
            }
            // create link
            if (!Monitor.LinkFile(hf, tf, info.Length))
                Monitor.MoveFile(hf, tf, info.Length); // 10bit link count overrun => move file
            // adjust file attributes and the last write time
            try
            {
                File.SetLastWriteTimeUtc(tf, info.LastWriteTimeUtc);
                File.SetAttributes(tf, info.Attributes & (~FileAttributes.Archive));
                File.SetAttributes(path, info.Attributes & (~FileAttributes.Archive));
            }
            catch
            {
            }
        }

        public override void Run()
        {
            // search for old backups
            if (!String.IsNullOrEmpty(this.PrevBackupFolderRoot))
            {
                var backups = BackupFolder.GetBackups(this.PrevBackupFolderRoot, this.PrevBackupFolderMask).OrderByDescending(backup => backup.BackupDate).ToArray();
                Logger.WriteLine(Logger.Verbosity.Message, "Found {0} previous backups in {1}, matching pattern {2}", backups.Length, this.PrevBackupFolderRoot, this.PrevBackupFolderMask);
                if (backups.Length > 0)
                {
                    var newestBackup = backups[0];
                    this.PreviousBackup = newestBackup.Folder;
                    Logger.WriteLine(Logger.Verbosity.Message, "Previous backup folder is {0}", newestBackup);
                }
            }
            base.Run();
        }
    }
}
