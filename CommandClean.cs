using System;
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
            if (HashInfo.IsValidHashPath(path) && Win32.GetFileLinkCount(path) == 1)
                Monitor.DeleteFile(path);
        }
    }
}
