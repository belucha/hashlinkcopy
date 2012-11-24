using System;
using System.IO;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.backup
{
    class DirectoryHashEntry : FileSystemHashEntry
    {
        public DirectoryHashEntry(DirectoryInfo info, byte[] hash)
            : base(info)
        {
            this.Hash = hash;
        }
    }
}
