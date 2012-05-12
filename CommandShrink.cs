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
        protected override void ProcessFile(FileData info, int level)
        {
            var hi = new HashInfo(info.FullName);
            var hf = Path.GetFullPath(hi.GetHashPath(HashDir));
            var hInfo = new FileData(hf);
            switch (hInfo.NumberOfLinks)
            {
                case 0:
                    // has file does not exist, move source file and create link
                    Directory.CreateDirectory(Path.GetDirectoryName(hf));
                    Monitor.Root.MoveFile(info.FullName, hf, info.Length);
                    File.SetAttributes(hf, FileAttributes.Normal);
                    Monitor.Root.LinkFile(hf, info.FullName, info.Length);
                    break;
                case 1023:
                    // the hashed file is full
                    Monitor.Root.DeleteFile(info);
                    Monitor.Root.MoveFile(hf, info.FullName, info.Length);
                    Monitor.Root.AdjustFileSettings(info.FullName, info);
                    break;
                default:
                    // the hashed file is full
                    Monitor.Root.DeleteFile(info);
                    Monitor.Root.LinkFile(hf, info.FullName, info.Length);
                    Monitor.Root.AdjustFileSettings(info.FullName, info);
                    break;
            }
        }
    }
}