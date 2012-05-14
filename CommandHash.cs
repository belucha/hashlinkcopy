using System;
using System.IO;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.hashlinkcopy
{
    [Description(@"tries to replace duplicates by hardlinks")]
    class CommandHash : CommandTreeWalker
    {
        protected override void ProcessFile(FileData info, int level)
        {
            var path = info.Path;
            var hi = new HashInfo(path);
            var hf = Path.GetFullPath(hi.GetHashPath(HashDir));
            var hInfo = new FileInfo(hf);
            Logger.Root.WriteLine(Verbosity.Debug, "{0}=>{1}", info.Path, hInfo.FullName);
        }
    }
}