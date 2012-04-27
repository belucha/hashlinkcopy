using System;
using System.IO;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.hashlinkcopy
{
    [Description(@"copies one directory into the target path. An wildcard character * is replaced by the formatted date/time

Usage example:
    HashLinkCopy.exe COPY D:\Projekte Z:\Backup\*\Projekte\
        Makes a backup of D:\Projekte to Z:\Backup\YYYY-MM-DD_HH.NN\Projekte and uses Z:\Backup\Hash as hash folder
")]
    [Option(@"SkipLevel", Help = @"Skip existing folders at given path recursion depth")]
    [Option(@"PrevBackupFolderRoot", Help = @"Root folder for backups", Default = @"")]
    [Option(@"Pattern", Help = @"Date formatting used to replace the * wild card in the target path", Default = @"YYYY-MM-DD_HH.NN")]
    class CommandCopy : CommandTreeWalker
    {
        public string Target { get; private set; }
        public string PreviousBackup { get; private set; }
        public string Pattern { get; private set; }
        public string PrevBackupFolderRoot { get; private set; }
        public int SkipLevel { get; private set; }

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

        protected override void LeaveDirectory(string path, int level)
        {
            base.LeaveDirectory(path, level);
            // at this point we can set the last write time of the copied directory
            if (!Monitor.Root.DryRun)
                Directory.SetLastWriteTimeUtc(this.RebasePath(path, this.Target), Directory.GetLastWriteTimeUtc(path));
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
                    this.PrevBackupFolderRoot = this.Target.Substring(0, wildCardPos);
                    backupFolderSuffix = this.Target.Substring(wildCardPos + 3);
                }
                this.Target = this.Target.Replace(@"\*\", @"\" + DateTime.Now.ToString(this.Pattern.ToLower().Replace("mm", "MM").Replace("nn", "mm").Replace("nn", "mm")) + @"\");
                if (this.Target.IndexOf('*') >= 0)
                    throw new InvalidOperationException("The target folder may only contain one date/time wildcard * and must be surrounded by \\!");
            }
            else
                this.HashDir = Path.GetFullPath(Path.Combine(this.Target, @"..\Hash"));
            this.Target = Path.GetFullPath(this.Target);
            this.HashDir = Path.GetFullPath(this.HashDir);
            // search for old backups
            if (!String.IsNullOrEmpty(this.PrevBackupFolderRoot))
            {
                Logger.WriteLine(Logger.Verbosity.Message, "Backup folder  : {0}{1}{2}", this.PrevBackupFolderRoot, wildCardPos >= 0 ? @"\*\" : "", backupFolderSuffix);
                var backups = BackupFolder.GetBackups(this.PrevBackupFolderRoot, this.Pattern).OrderByDescending(backup => backup.BackupDate).ToArray();
                Logger.WriteLine(Logger.Verbosity.Message, "Found {0} previous backups in {1}, matching pattern {2}", backups.Length, this.PrevBackupFolderRoot, this.Pattern);
                if (backups.Length > 0)
                {
                    var newestBackup = backups[0];
                    this.PreviousBackup = newestBackup.Folder + "\\" + backupFolderSuffix;
                    Logger.WriteLine(Logger.Verbosity.Message, "Previous backup folder is {0}", this.PreviousBackup);
                }
            }
            Logger.WriteLine(Logger.Verbosity.Message, "Pattern        : {0}", this.Pattern);
            Logger.WriteLine(Logger.Verbosity.Message, "Source folder  : {0}", this.Folder);
            Logger.WriteLine(Logger.Verbosity.Message, "Target folder  : {0}", this.Target);
            Logger.WriteLine(Logger.Verbosity.Message, "Hash folder    : {0}", this.HashDir);
            base.Run();
        }
    }
}
