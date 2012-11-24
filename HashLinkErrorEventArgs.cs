using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.backup
{
    public class HashLinkErrorEventArgs : EventArgs
    {
        public FileSystemInfo Info { get; private set; }
        public Exception Error { get; private set; }
        public HashLinkErrorEventArgs(FileSystemInfo info, Exception error)
        {
            this.Info = info;
            this.Error = error;
        }
    }
}
