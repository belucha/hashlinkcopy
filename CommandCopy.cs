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
        public long HashCacheLimit { get; private set; }

        public CommandCopy(IEnumerable<string> parameters)
            : base(parameters, 2)
        {
            // try to replace any stuff in target folder
            var targetFolder = Parameters[1];
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
            this.Target = Path.GetFullPath(targetFolder);
            this.SkipLevel = int.MaxValue;
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

        protected override bool EnterDirectory(string path, int level)
        {
            // get target folder
            var tf = Path.Combine(this.Target, path.Substring(this.Folder.Length));
            // check if existing folder should be skipped
            var exists = Directory.Exists(tf);
            if (exists && level >= this.SkipLevel) return true;
            // check if target folder must be created
            if (!exists)
            {
                // create it and duplicate creation&write time and attributes
                tf = Directory.CreateDirectory(tf).FullName;
                Directory.SetCreationTimeUtc(tf, Directory.GetCreationTimeUtc(path));
                Directory.SetLastWriteTimeUtc(tf, Directory.GetLastWriteTimeUtc(path));
                File.SetAttributes(tf, File.GetAttributes(path));
            }
            return false;
        }

        protected override void ProcessFile(string path, int level)
        {
            // build target file name
            var tf = Path.Combine(Target, path.Substring(Folder.Length));
            // skip existing target files
            if (File.Exists(tf)) return;
            var info = new FileInfo(path);
            // check if previous version of file can be found, that we could link to, this way we avoid to calc the SHA1
            if (this.PreviousBackup != null)
            {
                var pf = Path.Combine(this.PreviousBackup, path.Substring(Folder.Length));
                if (File.Exists(pf))
                {
                    var pInfo = new FileInfo(pf);
                    if (pInfo.Length == info.Length && pInfo.LastWriteTimeUtc == info.LastWriteTimeUtc &&
                        (info.Attributes & FileAttributes.Archive) == FileAttributes.Normal)
                        if (Win32.CreateHardLink(tf, pf, IntPtr.Zero))
                        {
                            Monitor.LinkFile(pf, tf, info.Length);
                            File.SetAttributes(path, info.Attributes & (~FileAttributes.Archive));
                            return;
                        }
                }
            }
            // we have no previous directory or the file changed, use hash algorithm
            var hi = new HashInfo(path);
            var hf = hi.GetHashPath(HashDir);
            // check if we need to copy the file
            if (!File.Exists(hf))
            {
                File.Copy(path, hf);
                Monitor.CopyFile(path, hf, info.Length);
            }
            var hInfo = new FileInfo(hf);
            if (hInfo.Length != info.Length)
                Monitor.HashCollision(hf, path);
            // create link
            if (!Win32.CreateHardLink(tf, hf, IntPtr.Zero))
            {
                // 10bit link count overrun => move file
                File.Move(hf, tf);
                Monitor.MoveFile(hf, tf, info.Length);
            }
            else
                Monitor.LinkFile(hf, tf, info.Length);
            // adjust file attributes and the last write time
            File.SetLastWriteTimeUtc(tf, info.LastWriteTimeUtc);
            File.SetAttributes(tf, info.Attributes & (~FileAttributes.Archive));
            File.SetAttributes(path, info.Attributes & (~FileAttributes.Archive));
        }
    }
}
