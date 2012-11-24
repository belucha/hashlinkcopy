using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.backup
{
    public class JunctionHashLinkCopy : HashLinkCopyBase
    {
        protected override void CreateLink(string name, FileSystemHashEntry entry, int level)
        {
            var target = HashDir + entry.ToString();
            if (entry.IsDirectory)
            {
                Directory.CreateDirectory(name);
                Win32.CreateJunction(name, target, false);
            }
            else
                while (true)
                {
                    var linkError = Win32.CreateHardLink(name, target, IntPtr.Zero) ? 0 : System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                    switch (linkError)
                    {
                        case 0:     // ERROR_SUCCESS
                            return;
                        case 183:   // ERROR_ALREADY_EXISTS
                            return; // target file already existing
                        case 1142:  // ERROR_TOO_MANY_LINKS
                            File.Move(target, name);
                            return;
                        case 2:     // ERROR_FILE_NOT_FOUND
                            this.CopyFile(entry.Info, target, level);
                            break;
                        case 3:     // ERROR_PATH_NOT_FOUND                        
                        default:
                            throw new System.ComponentModel.Win32Exception(linkError, String.Format("CreateHardLink({0},{1}) returned 0x{2:X8}h", name, target, linkError));
                    }
                }
        }
        protected override bool AdminPermissionsRequired { get { return false; } }
    }
}
