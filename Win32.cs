using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.ConstrainedExecution;
using System.Security.Permissions;
using Microsoft.Win32.SafeHandles;
using FILETIME = System.Runtime.InteropServices.ComTypes.FILETIME;
using SafeFileHandle = Microsoft.Win32.SafeHandles.SafeFileHandle;

namespace de.intronik.hashlinkcopy
{
    /// <summary>
    /// Some helpers to acces the special NTFS functions
    /// </summary>
    public class Win32
    {
        /// <summary>
        /// Wraps a FindFirstFile handle.
        /// </summary>
        public sealed class SafeFindHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            [DllImport("kernel32.dll")]
            private static extern bool FindClose(IntPtr handle);

            /// <summary>
            /// Initializes a new instance of the <see cref="SafeFindHandle"/> class.
            /// </summary>
            [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
            internal SafeFindHandle()
                : base(true)
            {
            }

            /// <summary>
            /// When overridden in a derived class, executes the code required to free the handle.
            /// </summary>
            /// <returns>
            /// true if the handle is released successfully; otherwise, in the 
            /// event of a catastrophic failure, false. In this case, it 
            /// generates a releaseHandleFailed MDA Managed Debugging Assistant.
            /// </returns>
            protected override bool ReleaseHandle()
            {
                return FindClose(base.handle);
            }
        }

        #region Win32 API Defines
        /// <summary>
        /// Contains information about the file that is found 
        /// by the FindFirstFile or FindNextFile functions.
        /// </summary>
        [Serializable, StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto), BestFitMapping(false)]
        public class WIN32_FIND_DATA
        {
            public FileAttributes dwFileAttributes;
            public uint ftCreationTime_dwLowDateTime;
            public uint ftCreationTime_dwHighDateTime;
            public uint ftLastAccessTime_dwLowDateTime;
            public uint ftLastAccessTime_dwHighDateTime;
            public uint ftLastWriteTime_dwLowDateTime;
            public uint ftLastWriteTime_dwHighDateTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public int dwReserved0;
            public int dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string cAlternateFileName;

            /// <summary>
            /// Returns a <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
            /// </summary>
            /// <returns>
            /// A <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
            /// </returns>
            public override string ToString()
            {
                return "File name=" + cFileName;
            }
        }

        [Serializable, StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto), BestFitMapping(false)]
        public struct BY_HANDLE_FILE_INFORMATION
        {
            public FileAttributes FileAttributes;
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


        [Serializable, StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto), BestFitMapping(false)]
        public struct FILE_BASIC_INFO
        {
            public FILETIME CreationTime;
            public FILETIME LastAccessTime;
            public FILETIME LastWriteTime;
            public FILETIME ChangeTime;
            public FileAttributes FileAttributes;
        }
        public enum FINDEX_INFO_LEVELS
        {
            Standard,
            Basic,
            MaxInfoLevel,
        }
        public enum FINDEX_SEARCH_OPTIONS
        {
            SearchNameMatch,
            SearchLimitToDirectories,
            SearchLimitToDevices
        }

        [Flags]
        public enum FINDEX_ADDITIONAL_FLAGS
        {
            None,
            CaseSensitive = 1,
            LargeFetch = 2,
        }

        public enum FILE_INFO_BY_HANDLE_CLASS
        {
            FileBasicInfo = 0,
            FileStandardInfo = 1,
            FileNameInfo = 2,
            FileRenameInfo = 3,
            FileDispositionInfo = 4,
            FileAllocationInfo = 5,
            FileEndOfFileInfo = 6,
            FileStreamInfo = 7,
            FileCompressionInfo = 8,
            FileAttributeTagInfo = 9,
            FileIdBothDirectoryInfo = 10,  // 0xA
            FileIdBothDirectoryRestartInfo = 11,  // 0xB
            FileIoPriorityHintInfo = 12,  // 0xC
            FileRemoteProtocolInfo = 13,  // 0xD
            FileFullDirectoryInfo = 14,  // 0xE
            FileFullDirectoryRestartInfo = 15,  // 0xF
            FileStorageInfo = 16,  // 0x10
            FileAlignmentInfo = 17,  // 0x11
            FileIdInfo = 18,  // 0x12
            MaximumFileInfoByHandlesClass,
        };

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern SafeFindHandle FindFirstFile(string fileName, [In, Out] WIN32_FIND_DATA data);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern SafeFindHandle FindFirstFileEx(string fileName, FINDEX_INFO_LEVELS fInfoLevelId, [In, Out] WIN32_FIND_DATA data, FINDEX_SEARCH_OPTIONS fSearchOp,
            IntPtr searchParams, FINDEX_ADDITIONAL_FLAGS flags);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool FindNextFile(SafeFindHandle hndFindFile,
                [In, Out, MarshalAs(UnmanagedType.LPStruct)] WIN32_FIND_DATA lpFindFileData);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
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

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetFileInformationByHandle(SafeFileHandle handle, out BY_HANDLE_FILE_INFORMATION lpFileInformation);

#if WIN7
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetFileInformationByHandle(SafeFileHandle handle, FILE_INFO_BY_HANDLE_CLASS info, FILE_INFO_BY_HANDLE_CLASS lpFileInformation, uint bufferSize);
#endif
        #endregion

        internal static BY_HANDLE_FILE_INFORMATION GetFileData(string filepath)
        {
            using (var handle = CreateFile(filepath, FileAccess.Read, FileShare.Read, IntPtr.Zero, FileMode.Open, FileAttributes.Archive, IntPtr.Zero))
            {
                var fileInfo = new BY_HANDLE_FILE_INFORMATION()
                {
                    NumberOfLinks = 0,
                };
                if (handle.IsInvalid) return fileInfo;
                GetFileInformationByHandle(handle, out fileInfo);
                return fileInfo;
            }
        }
    }
}
