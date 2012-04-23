using System;
using System.IO;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.hashlinkcopy
{
    [Description(@"copies one directory into another")]
    [Option(@"SkipLevel", Help = @"Skip existing folders at given path recursion depth")]
    [Option(@"PreviousFolders", Help = @"Pattern to match previous backup folders")]
    class CommandCopy : CommandTreeWalker
    {
        public string Target { get; private set; }
        public string PreviousBackup { get; private set; }
        public int SkipLevel { get; private set; }

        public CommandCopy(IEnumerable<string> parameters)
            : base(parameters, 2)
        {
            // try to replace any stuff in target folder
            var targetFolder = Parameters[1];
            var startP = targetFolder.IndexOf('{');
            if (startP >= 0)
            {
                string format;
                var endP = targetFolder.IndexOf('}', startP + 1);
                if (endP >= 0)
                {
                    format = targetFolder.Substring(startP + 1, endP - startP - 1).Trim();
                    if (format.Length == 0) format = "yyyy-MM-dd_HH_mm_ss";
                    targetFolder = targetFolder.Substring(0, startP) + DateTime.Now.ToString(format) + targetFolder.Substring(endP + 1);
                    // search for old backups
                    if (this.PreviousBackup == null)
                    {
                        string newestFolder = null;
                        DateTime newestFolderDate = DateTime.MinValue;
                        foreach (var subDir in Directory.GetDirectories(Path.Combine(targetFolder, ".\\..\\")))
                        {
                            var name = Path.GetFileName(subDir);
                            DateTime folderTime;
                            if (DateTime.TryParseExact(name, format, System.Globalization.CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.None, out folderTime))
                            {
                                if (folderTime >= newestFolderDate)
                                {
                                    newestFolder = subDir;
                                    newestFolderDate = folderTime;
                                }
                            }
                        }
                        if (newestFolderDate > DateTime.MinValue)
                        {
                            this.PreviousBackup = Path.GetFullPath(newestFolder);
                            Logger.WriteLine(Logger.Verbosity.Message, "Previous backup folder is: {0}", this.PreviousBackup);
                        }
                    }
                }
            }
            this.Target = Path.GetFullPath(targetFolder);
            if (String.IsNullOrEmpty(this.HashDir))
                this.HashDir = Path.GetFullPath(Path.Combine(this.Target, "..\\Hashs\\"));
        }

        protected override void InitOptions()
        {
            base.InitOptions();
            this.PreviousBackup = null;
            this.SkipLevel = int.MaxValue;
        }

        protected override void ProcessOption(OptionAttribute option)
        {
            base.ProcessOption(option);
            if (option.Name == "SkipLevel") this.SkipLevel = int.Parse(option.Value);
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
    }
}
