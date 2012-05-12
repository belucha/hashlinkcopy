using System;
using System.IO;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.hashlinkcopy
{
    [Description(@"tries to replace duplicates by hardlinks")]
    class CommandShrink : CommandTreeWalker
    {
        static System.Security.Cryptography.SHA1 SHA1 = System.Security.Cryptography.SHA1.Create();
        protected override void ProcessFile(FileInfo info, int level)
        {
            var hi = new HashInfo(info, CommandShrink.SHA1);
            var hf = Path.GetFullPath(hi.GetHashPath(HashDir));
            // check if we need to copy the file
            if (!File.Exists(hf))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(hf));
                Monitor.Root.MoveFile(info.FullName, hf, info.Length);
                File.SetAttributes(hf, FileAttributes.Normal);
            }
            else
            {
                var hInfo = new FileInfo(hf);
                if (hInfo.Length != info.Length)
                {
                    Monitor.Root.HashCollision(hf, info.FullName);
                    return;
                }
                Monitor.Root.DeleteFile(info);
            }
            // create link
            if (!Monitor.Root.LinkFile(hf, info.FullName, info.Length))
                // 10bit link count overrun => move the hash file
                Monitor.Root.MoveFile(hf, info.FullName, info.Length);
            // adjust file attributes and the last write time
            try
            {
                info.LastAccessTimeUtc = info.LastWriteTimeUtc;
                info.Attributes = info.Attributes;
            }
            catch
            {
            }
        }
    }
}