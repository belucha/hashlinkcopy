using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Runtime.InteropServices;
using FILETIME = System.Runtime.InteropServices.ComTypes.FILETIME;
using SafeFileHandle = Microsoft.Win32.SafeHandles.SafeFileHandle;

namespace de.intronik.hashlinkcopy
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

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern SafeFileHandle CreateFile(
            string lpFileName,
            [MarshalAs(UnmanagedType.U4)] FileAccess dwDesiredAccess,
            [MarshalAs(UnmanagedType.U4)] FileShare dwShareMode,
            IntPtr lpSecurityAttributes,
            [MarshalAs(UnmanagedType.U4)] FileMode dwCreationDisposition,
            [MarshalAs(UnmanagedType.U4)] FileAttributes dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

        [DllImport("kernel32.dll", SetLastError = true, CharSet=CharSet.Unicode)]
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
