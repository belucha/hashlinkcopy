using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Runtime.InteropServices;
using FILETIME = System.Runtime.InteropServices.ComTypes.FILETIME;
using SafeFileHandle = Microsoft.Win32.SafeHandles.SafeFileHandle;

namespace de.intronik.hashcopy
{
    /// <summary>
    /// Some helpers to acces the special NTFS functions
    /// </summary>
    public class Win32
    {
        #region Win32 API Defines
        [StructLayout(LayoutKind.Sequential)]
        struct BY_HANDLE_FILE_INFORMATION
        {
            public uint FileAttributes;
            public FILETIME CreationTime;
            public FILETIME LastAccessTime;
            public FILETIME LastWriteTime;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }

        public enum SymbolicLinkFlags
        {
            File,
            Directory,
        }

        public enum CopyFileFlags
        {
            /// <summary>
            /// An attempt to copy an encrypted file will succeed even if the destination copy cannot be encrypted.
            /// </summary>
            ALLOW_DECRYPTED_DESTINATION = 0x00000008,
            /// <summary>
            /// If the source file is a symbolic link, the destination file is also a symbolic link pointing to the same file that the source symbolic link is pointing to.
            /// Windows Server 2003 and Windows XP:  This value is not supported.
            /// </summary>
            COPY_SYMLINK = 0x00000800,
            /// <summary>
            /// The copy operation fails immediately if the target file already exists. 
            /// </summary>
            FAIL_IF_EXISTS = 0x00000001,
            /// <summary>
            /// The copy operation is performed using unbuffered I/O, bypassing system I/O cache resources. Recommended for very large file transfers. 
            /// Windows Server 2003 and Windows XP:  This value is not supported.
            /// </summary>
            NO_BUFFERING = 0x00001000,
            /// <summary>
            /// The file is copied and the original file is opened for write access.
            /// </summary>
            OPEN_SOURCE_FOR_WRITE = 0x00000004,
            /// <summary>
            /// Progress of the copy is tracked in the target file in case the copy fails. 
            /// The failed copy can be restarted at a later time by specifying the same values 
            /// for lpExistingFileName and lpNewFileName as those used in the call that failed. 
            /// This can significantly slow down the copy operation as the new file may be 
            /// flushed multiple times during the copy operation.
            /// </summary>
            RESTARTABLE = 0x00000002,
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern SafeFileHandle CreateFile(
            string lpFileName,
            [MarshalAs(UnmanagedType.U4)] FileAccess dwDesiredAccess,
            [MarshalAs(UnmanagedType.U4)] FileShare dwShareMode,
            IntPtr lpSecurityAttributes,
            [MarshalAs(UnmanagedType.U4)] FileMode dwCreationDisposition,
            [MarshalAs(UnmanagedType.U4)] FileAttributes dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("Kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

        [DllImport("Kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool CreateSymbolicLink(string lpFileName, string lpExistingFileName, SymbolicLinkFlags flags);

        [DllImport("Kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool CopyFileEx(string lpExistingFileName, string lpFileName, IntPtr lpProgressRoutine, IntPtr lpData, IntPtr pCancel, CopyFileFlags flags);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool GetFileInformationByHandle(SafeFileHandle handle, out BY_HANDLE_FILE_INFORMATION lpFileInformation);
        #endregion

        public static int GetFileLinkCount(string filepath)
        {
            using (var handle = CreateFile(filepath, FileAccess.Read, FileShare.Read, IntPtr.Zero, FileMode.Open, FileAttributes.Archive, IntPtr.Zero))
            {
                if (handle.IsInvalid) return 0;
                var fileInfo = new BY_HANDLE_FILE_INFORMATION();
                if (GetFileInformationByHandle(handle, out fileInfo))
                    return (int)fileInfo.NumberOfLinks;
            }
            return 0;
        }
    }
}
