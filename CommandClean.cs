using System;
using System.IO;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.hashlinkcopy
{
    [Description("Cleans a hash directory from abandoned hash files (linkcount==1)")]
    class CommandClean : CommandTreeWalker
    {
        protected override void ProcessFile(FileData file, int level)
        {
            var s = this.RebasePath(file.FullName, "");
            var r = HashInfo.CheckAndCorrectHashPath(s);
            if (r == null) return;  // not a hashfile
            var count = Win32.GetFileData(file.FullName).NumberOfLinks;
            if (r == "" && count > 1) return;  // valid hash file and count is > 1
            if (count > 1)
                Monitor.Root.MoveFile(file.FullName, Path.Combine(this.Folder, r), 0);    // hash file and used, but with invalid format, move to new format
            else
                Monitor.Root.DeleteFile(file);  // not in use => delete
        }
        public override void Run()
        {
            base.Run();
        }
        protected override void LeaveDirectory(FileData dir, int level)
        {
            base.LeaveDirectory(dir, level);
            if (Directory.GetFileSystemEntries(dir.FullName).Length == 0)
                Monitor.Root.DeleteDirectory(dir.FullName);
        }
    }
}
