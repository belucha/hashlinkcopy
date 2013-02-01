using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace de.intronik.backup
{
    public class VirtualDirectoryHashEntry : HashEntry
    {
        public IDictionary<string, FileSystemInfo> Folders { get; private set; }
        public VirtualDirectoryHashEntry(IDictionary<string, FileSystemInfo> sourceFolders, byte[] hash)
        {
            this.Folders = sourceFolders;
            this.Hash = hash;
        }

        protected override bool GetIsDirectory()
        {
            return true;
        }

        protected override FileSystemInfo GetFileSystemInfo()
        {
            throw new InvalidOperationException();
        }
    }
}
