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
        protected override void ProcessFile(FileInfo info, int level)
        {
            var path = info.FullName;
            var hi = new HashInfo(path);
            var hf = Path.GetFullPath(hi.GetHashPath(HashDir));
            // check if we need to copy the file
            if (!File.Exists(hf))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(hf));
                Monitor.Root.MoveFile(path, hf, info.Length);
                File.SetAttributes(hf, FileAttributes.Normal);
            }
            else
            {
                var hInfo = new FileInfo(hf);
                if (hInfo.Length != info.Length)
                {
                    Monitor.Root.HashCollision(hf, path);
                    return;
                }
                File.SetAttributes(path, FileAttributes.Normal);
                Monitor.Root.DeleteFile(path);
            }
            // create link
            if (!Monitor.Root.LinkFile(hf, path, info.Length))
                // 10bit link count overrun => move the hash file
                Monitor.Root.MoveFile(hf, path, info.Length);
            // adjust file attributes and the last write time
            try
            {
                File.SetLastWriteTimeUtc(path, info.LastWriteTimeUtc);
                File.SetAttributes(path, info.Attributes);
            }
            catch
            {
            }
        }
    }
}