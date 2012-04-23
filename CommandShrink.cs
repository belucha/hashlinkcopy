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
        public CommandShrink(IEnumerable<string> parameters)
            : base(parameters, 1)
        {
            if (String.IsNullOrEmpty(this.HashDir))
                this.HashDir = Path.Combine(this.Folder, ".\\..\\Hashs\\");
        }

        protected override void InitOptions()
        {
            base.InitOptions();
        }

        protected override void ProcessOption(OptionAttribute option)
        {
            base.ProcessOption(option);
        }

        protected override bool EnterDirectory(string path, int level)
        {
            return false;
        }

        protected override void ProcessFile(string path, int level)
        {
            var info = new FileInfo(path);
            var hi = new HashInfo(path);
            var hf = Path.GetFullPath(hi.GetHashPath(HashDir));
            // check if we need to copy the file
            if (!File.Exists(hf))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(hf));
                File.Move(path, hf);
                Monitor.MoveFile(path, hf, info.Length);
                File.SetAttributes(hf, FileAttributes.Normal);
            }
            else
            {
                var hInfo = new FileInfo(hf);
                if (hInfo.Length != info.Length)
                    Monitor.HashCollision(hf, path);
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
            }
            // create link
            if (!Win32.CreateHardLink(path, hf, IntPtr.Zero))
            {
                // 10bit link count overrun => move file
                File.Move(hf, path);
                Monitor.MoveFile(hf, path, info.Length);
            }
            else
                Monitor.LinkFile(hf, path, info.Length);
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