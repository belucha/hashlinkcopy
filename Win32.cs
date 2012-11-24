using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using FILETIME = System.Runtime.InteropServices.ComTypes.FILETIME;
using SafeFileHandle = Microsoft.Win32.SafeHandles.SafeFileHandle;

namespace de.intronik.backup
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

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern UInt32 GetFinalPathNameByHandle(SafeFileHandle handle, out string lpszFilePath, UInt32 cchFilePath, UInt32 dwFlags);
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

        #region JunctionPoint
        /// <summary>
        /// The file or directory is not a reparse point.
        /// </summary>
        private const int ERROR_NOT_A_REPARSE_POINT = 4390;

        /// <summary>
        /// The reparse point attribute cannot be set because it conflicts with an existing attribute.
        /// </summary>
        private const int ERROR_REPARSE_ATTRIBUTE_CONFLICT = 4391;

        /// <summary>
        /// The data present in the reparse point buffer is invalid.
        /// </summary>
        private const int ERROR_INVALID_REPARSE_DATA = 4392;

        /// <summary>
        /// The tag present in the reparse point buffer is invalid.
        /// </summary>
        private const int ERROR_REPARSE_TAG_INVALID = 4393;

        /// <summary>
        /// There is a mismatch between the tag specified in the request and the tag present in the reparse point.
        /// </summary>
        private const int ERROR_REPARSE_TAG_MISMATCH = 4394;

        /// <summary>
        /// Command to set the reparse point data block.
        /// </summary>
        private const int FSCTL_SET_REPARSE_POINT = 0x000900A4;

        /// <summary>
        /// Command to get the reparse point data block.
        /// </summary>
        private const int FSCTL_GET_REPARSE_POINT = 0x000900A8;

        /// <summary>
        /// Command to delete the reparse point data base.
        /// </summary>
        private const int FSCTL_DELETE_REPARSE_POINT = 0x000900AC;

        /// <summary>
        /// Reparse point tag used to identify mount points and junction points.
        /// </summary>
        private const uint IO_REPARSE_TAG_MOUNT_POINT = 0xA0000003;

        /// <summary>
        /// Used for symbolic link support. See section 2.1.2.4.
        /// </summary>
        private const uint IO_REPARSE_TAG_SYMLINK = 0xA000000C;

        [Flags]
        private enum EFileAttributes : uint
        {
            Readonly = 0x00000001,
            Hidden = 0x00000002,
            System = 0x00000004,
            Directory = 0x00000010,
            Archive = 0x00000020,
            Device = 0x00000040,
            Normal = 0x00000080,
            Temporary = 0x00000100,
            SparseFile = 0x00000200,
            ReparsePoint = 0x00000400,
            Compressed = 0x00000800,
            Offline = 0x00001000,
            NotContentIndexed = 0x00002000,
            Encrypted = 0x00004000,
            Write_Through = 0x80000000,
            Overlapped = 0x40000000,
            NoBuffering = 0x20000000,
            RandomAccess = 0x10000000,
            SequentialScan = 0x08000000,
            DeleteOnClose = 0x04000000,
            OpenReparsePoint = 0x00200000,
            PosixSemantics = 0x01000000,
            BackupSemantics = 0x02000000,
            OpenNoRecall = 0x00100000,
            FirstPipeInstance = 0x00080000
        }

        /// <summary>
        /// This prefix indicates to NTFS that the path is to be treated as a non-interpreted
        /// path in the virtual file system.
        /// </summary>
        private const string NonInterpretedPathPrefix = @"\??\";

        [StructLayout(LayoutKind.Sequential)]
        private struct REPARSE_DATA_BUFFER_JUNCTION
        {
            /// <summary>
            /// Reparse point tag. Must be a Microsoft reparse point tag.
            /// </summary>
            public uint ReparseTag;

            /// <summary>
            /// Size, in bytes, of the data after the Reserved member. This can be calculated by:
            /// (4 * sizeof(ushort)) + SubstituteNameLength + PrintNameLength + 
            /// (namesAreNullTerminated ? 2 * sizeof(char) : 0);
            /// </summary>
            public ushort ReparseDataLength;

            /// <summary>
            /// Reserved; do not use. 
            /// </summary>
            public ushort Reserved;

            /// <summary>
            /// Offset, in bytes, of the substitute name string in the PathBuffer array.
            /// </summary>
            public ushort SubstituteNameOffset;

            /// <summary>
            /// Length, in bytes, of the substitute name string. If this string is null-terminated,
            /// SubstituteNameLength does not include space for the null character.
            /// </summary>
            public ushort SubstituteNameLength;

            /// <summary>
            /// Offset, in bytes, of the print name string in the PathBuffer array.
            /// </summary>
            public ushort PrintNameOffset;

            /// <summary>
            /// Length, in bytes, of the print name string. If this string is null-terminated,
            /// PrintNameLength does not include space for the null character. 
            /// </summary>
            public ushort PrintNameLength;

            /// <summary>
            /// A buffer containing the unicode-encoded path string. The path string contains
            /// the substitute name string and print name string.
            /// </summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x3FF0)]
            public byte[] PathBuffer;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct REPARSE_DATA_BUFFER_SYMLINK
        {
            /// <summary>
            /// Reparse point tag. Must be a Microsoft reparse point tag.
            /// </summary>
            public uint ReparseTag;

            /// <summary>
            /// Size, in bytes, of the data after the Reserved member. This can be calculated by:
            /// (4 * sizeof(ushort)) + SubstituteNameLength + PrintNameLength + 
            /// (namesAreNullTerminated ? 2 * sizeof(char) : 0);
            /// </summary>
            public ushort ReparseDataLength;

            /// <summary>
            /// Reserved; do not use. 
            /// </summary>
            public ushort Reserved;

            /// <summary>
            /// Offset, in bytes, of the substitute name string in the PathBuffer array.
            /// </summary>
            public ushort SubstituteNameOffset;

            /// <summary>
            /// Length, in bytes, of the substitute name string. If this string is null-terminated,
            /// SubstituteNameLength does not include space for the null character.
            /// </summary>
            public ushort SubstituteNameLength;

            /// <summary>
            /// Offset, in bytes, of the print name string in the PathBuffer array.
            /// </summary>
            public ushort PrintNameOffset;

            /// <summary>
            /// Length, in bytes, of the print name string. If this string is null-terminated,
            /// PrintNameLength does not include space for the null character. 
            /// </summary>
            public ushort PrintNameLength;

            /// <summary>
            ///  A 32-bit field that specifies whether the substitute name is a full path name or a path name relative to the directory containing the symbolic link.
            ///  This field contains one of the values in the following table.
            ///  Symbol                 Value	    Meaning
            ///                         0x00000000  The substitute name is a full path name.
            ///  SYMLINK_FLAG_RELATIVE  0x00000001  The substitute name is a path name relative to the directory containing the symbolic link.
            /// </summary>
            public uint Flags;

            /// <summary>
            /// A buffer containing the unicode-encoded path string. The path string contains
            /// the substitute name string and print name string.
            /// </summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x3FF0)]
            public byte[] PathBuffer;
        }


        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool DeviceIoControl(IntPtr hDevice, uint dwIoControlCode,
            IntPtr InBuffer, int nInBufferSize,
            IntPtr OutBuffer, int nOutBufferSize,
            out int pBytesReturned, IntPtr lpOverlapped);

        /// <summary>
        /// Creates a junction point from the specified directory to the specified target directory.
        /// </summary>
        /// <remarks>
        /// Only works on NTFS.
        /// </remarks>
        /// <param name="junctionPoint">The junction point path</param>
        /// <param name="targetDir">The target directory</param>
        /// <param name="overwrite">If true overwrites an existing reparse point or empty directory</param>
        /// <exception cref="IOException">Thrown when the junction point could not be created or when
        /// an existing directory was found and <paramref name="overwrite" /> if false</exception>
        public static void CreateJunction(string junctionPoint, string targetDir)
        {
            using (SafeFileHandle handle = OpenReparsePoint(junctionPoint, FileAccess.Write))
            {
                byte[] targetDirBytes = Encoding.Unicode.GetBytes(NonInterpretedPathPrefix + targetDir);

                REPARSE_DATA_BUFFER_JUNCTION reparseDataBuffer = new REPARSE_DATA_BUFFER_JUNCTION();

                reparseDataBuffer.ReparseTag = IO_REPARSE_TAG_MOUNT_POINT;
                reparseDataBuffer.ReparseDataLength = (ushort)(targetDirBytes.Length + 12);
                reparseDataBuffer.SubstituteNameOffset = 0;
                reparseDataBuffer.SubstituteNameLength = (ushort)targetDirBytes.Length;
                reparseDataBuffer.PrintNameOffset = (ushort)(targetDirBytes.Length + 2);
                reparseDataBuffer.PrintNameLength = 0;
                reparseDataBuffer.PathBuffer = new byte[0x3ff0];
                Array.Copy(targetDirBytes, reparseDataBuffer.PathBuffer, targetDirBytes.Length);

                int inBufferSize = Marshal.SizeOf(reparseDataBuffer);
                IntPtr inBuffer = Marshal.AllocHGlobal(inBufferSize);

                try
                {
                    Marshal.StructureToPtr(reparseDataBuffer, inBuffer, false);

                    int bytesReturned;
                    bool result = DeviceIoControl(handle.DangerousGetHandle(), FSCTL_SET_REPARSE_POINT,
                        inBuffer, targetDirBytes.Length + 20, IntPtr.Zero, 0, out bytesReturned, IntPtr.Zero);

                    if (!result)
                        ThrowLastWin32Error("Unable to create junction point.");
                }
                finally
                {
                    Marshal.FreeHGlobal(inBuffer);
                }
            }
        }

        public static void CreateSymbolLink(string symbolicLink, string targetDir, bool relative = true)
        {
            using (SafeFileHandle handle = OpenReparsePoint(symbolicLink, FileAccess.Write))
            {
                byte[] targetDirBytes = Encoding.Unicode.GetBytes(targetDir);

                REPARSE_DATA_BUFFER_SYMLINK reparseDataBuffer = new REPARSE_DATA_BUFFER_SYMLINK();

                reparseDataBuffer.ReparseTag = IO_REPARSE_TAG_SYMLINK;
                reparseDataBuffer.ReparseDataLength = (ushort)(2 * targetDirBytes.Length + 12);
                reparseDataBuffer.PrintNameOffset = 0;
                reparseDataBuffer.PrintNameLength = 0;// (ushort)(targetDirBytes.Length);
                reparseDataBuffer.SubstituteNameOffset = reparseDataBuffer.PrintNameLength;
                reparseDataBuffer.SubstituteNameLength = reparseDataBuffer.PrintNameLength;
                reparseDataBuffer.PathBuffer = new byte[0x3ff0];
                reparseDataBuffer.Flags = relative ? (uint)0x000000001 : (uint)0x000000000;
                Array.Copy(targetDirBytes, 0, reparseDataBuffer.PathBuffer, reparseDataBuffer.PrintNameOffset, reparseDataBuffer.PrintNameLength);
                Array.Copy(targetDirBytes, 0, reparseDataBuffer.PathBuffer, reparseDataBuffer.SubstituteNameOffset, reparseDataBuffer.SubstituteNameLength);

                int inBufferSize = Marshal.SizeOf(reparseDataBuffer);
                IntPtr inBuffer = Marshal.AllocHGlobal(inBufferSize);

                try
                {
                    Marshal.StructureToPtr(reparseDataBuffer, inBuffer, false);

                    int bytesReturned;
                    bool result = DeviceIoControl(handle.DangerousGetHandle(), FSCTL_SET_REPARSE_POINT,
                        inBuffer, targetDirBytes.Length + 20, IntPtr.Zero, 0, out bytesReturned, IntPtr.Zero);

                    if (!result)
                        ThrowLastWin32Error("Unable to create symlink point.");
                }
                finally
                {
                    Marshal.FreeHGlobal(inBuffer);
                }
            }
        }

        /// <summary>
        /// Deletes a junction point at the specified source directory along with the directory itself.
        /// Does nothing if the junction point does not exist.
        /// </summary>
        /// <remarks>
        /// Only works on NTFS.
        /// </remarks>
        /// <param name="junctionPoint">The junction point path</param>
        public static void DeleteJunction(string junctionPoint)
        {
            if (!Directory.Exists(junctionPoint))
            {
                if (File.Exists(junctionPoint))
                    throw new IOException("Path is not a junction point.");
                return;
            }

            using (SafeFileHandle handle = OpenReparsePoint(junctionPoint, FileAccess.Write))
            {
                REPARSE_DATA_BUFFER_JUNCTION reparseDataBuffer = new REPARSE_DATA_BUFFER_JUNCTION();

                reparseDataBuffer.ReparseTag = IO_REPARSE_TAG_MOUNT_POINT;
                reparseDataBuffer.ReparseDataLength = 0;
                reparseDataBuffer.PathBuffer = new byte[0x3ff0];

                int inBufferSize = Marshal.SizeOf(reparseDataBuffer);
                IntPtr inBuffer = Marshal.AllocHGlobal(inBufferSize);
                try
                {
                    Marshal.StructureToPtr(reparseDataBuffer, inBuffer, false);

                    int bytesReturned;
                    bool result = DeviceIoControl(handle.DangerousGetHandle(), FSCTL_DELETE_REPARSE_POINT,
                        inBuffer, 8, IntPtr.Zero, 0, out bytesReturned, IntPtr.Zero);

                    if (!result)
                        ThrowLastWin32Error("Unable to delete junction point.");
                }
                finally
                {
                    Marshal.FreeHGlobal(inBuffer);
                }

                try
                {
                    Directory.Delete(junctionPoint);
                }
                catch (IOException ex)
                {
                    throw new IOException("Unable to delete junction point.", ex);
                }
            }
        }

        /// <summary>
        /// Determines whether the specified path exists and refers to a junction point.
        /// </summary>
        /// <param name="path">The junction point path</param>
        /// <returns>True if the specified path represents a junction point</returns>
        /// <exception cref="IOException">Thrown if the specified path is invalid
        /// or some other error occurs</exception>
        public static bool IsJunction(string path)
        {
            if (!Directory.Exists(path))
                return false;

            using (SafeFileHandle handle = OpenReparsePoint(path, FileAccess.Read))
            {
                string target = InternalGetTarget(handle);
                return target != null;
            }
        }

        /// <summary>
        /// Gets the target of the specified junction point.
        /// </summary>
        /// <remarks>
        /// Only works on NTFS.
        /// </remarks>
        /// <param name="junctionPoint">The junction point path</param>
        /// <returns>The target of the junction point</returns>
        /// <exception cref="IOException">Thrown when the specified path does not
        /// exist, is invalid, is not a junction point, or some other error occurs</exception>
        public static string GetJunctionTarget(string junctionPoint)
        {
            try
            {
                using (SafeFileHandle handle = OpenReparsePoint(junctionPoint, FileAccess.Read))
                {
                    string target = InternalGetTarget(handle); // IO_REPARSE_TAG_MOUNT_POINT
                    if (target == null)
                        //throw new IOException("Path is not a junction point.");
                        return null;
                    if (target.Length > 1 && (target[0] == Path.DirectorySeparatorChar || target[1] == Path.AltDirectorySeparatorChar))
                        return Path.Combine(Path.GetPathRoot(junctionPoint), target.Substring(1));
                    return target;

                }
            }
            catch
            {
                return null;
            }
        }

        private static string InternalGetTarget(SafeFileHandle handle, UInt32 expectedReparseType = 0)
        {
            int outBufferSize = Marshal.SizeOf(typeof(REPARSE_DATA_BUFFER_JUNCTION));
            IntPtr outBuffer = Marshal.AllocHGlobal(outBufferSize);

            try
            {
                int bytesReturned;
                bool result = DeviceIoControl(handle.DangerousGetHandle(), FSCTL_GET_REPARSE_POINT,
                    IntPtr.Zero, 0, outBuffer, outBufferSize, out bytesReturned, IntPtr.Zero);

                if (!result)
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error == ERROR_NOT_A_REPARSE_POINT)
                        return null;

                    ThrowLastWin32Error("Unable to get information about junction point.");
                }

                var reparseDataBufferJunction = (REPARSE_DATA_BUFFER_JUNCTION)Marshal.PtrToStructure(outBuffer, typeof(REPARSE_DATA_BUFFER_JUNCTION));
                var reparseDataBufferSymlink = (REPARSE_DATA_BUFFER_SYMLINK)Marshal.PtrToStructure(outBuffer, typeof(REPARSE_DATA_BUFFER_SYMLINK));

                if (expectedReparseType != 0 && reparseDataBufferJunction.ReparseTag != expectedReparseType)
                    return null;

                // SYMBOLIK LINKS START 4 BYTES LATER
                // see: http://msdn.microsoft.com/en-us/library/windows/hardware/ff552012(v=vs.85).aspx
                if (reparseDataBufferSymlink.ReparseTag == IO_REPARSE_TAG_SYMLINK)
                    return Encoding.Unicode.GetString(reparseDataBufferSymlink.PathBuffer, reparseDataBufferSymlink.SubstituteNameOffset, reparseDataBufferSymlink.SubstituteNameLength);

                string targetDir = Encoding.Unicode.GetString(reparseDataBufferJunction.PathBuffer,
                    reparseDataBufferJunction.SubstituteNameOffset, reparseDataBufferJunction.SubstituteNameLength);

                if (targetDir.StartsWith(NonInterpretedPathPrefix))
                    targetDir = targetDir.Substring(NonInterpretedPathPrefix.Length);

                return targetDir;
            }
            finally
            {
                Marshal.FreeHGlobal(outBuffer);
            }
        }

        private static SafeFileHandle OpenReparsePoint(string reparsePoint, FileAccess accessMode)
        {
            var reparsePointHandle = CreateFile(reparsePoint, accessMode, FileShare.Read | FileShare.Write | FileShare.Delete, IntPtr.Zero, FileMode.Open,
                (FileAttributes)(uint)(EFileAttributes.BackupSemantics | EFileAttributes.OpenReparsePoint), IntPtr.Zero);

            if (Marshal.GetLastWin32Error() != 0)
                ThrowLastWin32Error("Unable to open reparse point.");

            return reparsePointHandle;
        }
        private static void ThrowLastWin32Error(string message)
        {
            throw new IOException(message, Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
        }
        #endregion JunctionPoint

        /// <summary>
        /// Returns true, when the program has admin priveleges
        /// </summary>
        /// <returns></returns>
        public static bool HasAdministratorPrivileges()
        {
            WindowsIdentity id = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(id);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

    }
}
