using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace de.intronik.backup
{
    /// <summary>
    /// HashEntry for a FileSystemInfo object
    /// </summary>
    public class FileSystemHashEntry : HashEntry
    {
        public FileSystemInfo Info { get; private set; }

        public string FullName { get { return this.Info.FullName; } }

        protected override bool GetIsDirectory() { return (Info.Attributes & FileAttributes.Directory) == FileAttributes.Directory; }

        public FileSystemHashEntry(FileSystemInfo info)
            : base()
        {
            this.Info = info;
        }

    }
}
