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
        protected override void ProcessFile(string path, int level)
        {
            var s = this.RebasePath(path, "");
            var r = HashInfo.CheckAndCorrectHashPath(s);
            if (r == null) return;  // not a hashfile
            var count = Win32.GetFileLinkCount(path);
            if (r == "" && count > 1) return;  // valid hash file and count is > 1
            if (count > 1)
                Monitor.MoveFile(path, Path.Combine(this.Folder, r), 0);    // hash file and used, but with invalid format, move to new format
            else
                Monitor.DeleteFile(path);  // not in use => delete
        }
        public override void Run()
        {
            base.Run();
        }
        protected override void LeaveDirectory(string path, int level)
        {
            base.LeaveDirectory(path, level);
            if (Directory.GetFileSystemEntries(path).Length == 0)
                Monitor.DeleteDirectory(path);
        }
    }
}
