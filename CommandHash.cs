using System;
using System.Security.Cryptography;
using System.IO;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.hashlinkcopy
{
    [Description(@"computes the hash of all files")]
    class CommandHash : CommandTreeWalker
    {
        static HashAlgorithm HASH_ALG = SHA1.Create();
        protected override void ProcessFile(FileInfo info, int level)
        {
            var hi = new HashInfo(info, CommandHash.HASH_ALG);
            var hf = hi.GetHashPath(HashDir);
            var hInfo = new FileInfo(hf);
            Logger.Root.WriteLine(Verbosity.Debug, "{0}=>{1}", info.FullName, hInfo.FullName);
        }
    }
}