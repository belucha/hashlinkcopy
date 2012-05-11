using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using Microsoft.Win32.SafeHandles;

namespace de.intronik.hashlinkcopy
{
    /// <summary>
    /// Wraps a FindFirstFile handle.
    /// </summary>
    sealed class SafeFindHandle : SafeHandleZeroOrMinusOneIsInvalid
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

    /// <summary>
    /// Contains information about a file returned by the 
    /// <see cref="FastDirectoryEnumerator"/> class.
    /// </summary>
    [Serializable]
    public class FileData
    {
        /// <summary>
        /// Attributes of the file.
        /// </summary>
        public readonly FileAttributes Attributes;

        public DateTime CreationTime
        {
            get { return this.CreationTimeUtc.ToLocalTime(); }
        }

        /// <summary>
        /// File creation time in UTC
        /// </summary>
        public readonly DateTime CreationTimeUtc;

        /// <summary>
        /// Gets the last access time in local time.
        /// </summary>
        public DateTime LastAccesTime
        {
            get { return this.LastAccessTimeUtc.ToLocalTime(); }
        }

        /// <summary>
        /// File last access time in UTC
        /// </summary>
        public readonly DateTime LastAccessTimeUtc;

        /// <summary>
        /// Gets the last access time in local time.
        /// </summary>
        public DateTime LastWriteTime
        {
            get { return this.LastWriteTimeUtc.ToLocalTime(); }
        }

        /// <summary>
        /// File last write time in UTC
        /// </summary>
        public readonly DateTime LastWriteTimeUtc;

        /// <summary>
        /// Size of the file in bytes
        /// </summary>
        public readonly long Size;

        /// <summary>
        /// Name of the file
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// Full path to the file.
        /// </summary>
        public readonly string Path;

        /// <summary>
        /// Returns true, when the entry is a directory
        /// </summary>
        public bool IsDirectory { get { return (this.Attributes & FileAttributes.Directory) == FileAttributes.Directory; } }

        /// <summary>
        /// Returns a <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
        /// </returns>
        public override string ToString()
        {
            return this.Name;
        }

        internal FileData(string dir)
        {
            this.Attributes = FileAttributes.Directory;
            this.Size = 0;
            this.CreationTimeUtc = DateTime.MinValue;
            this.LastWriteTimeUtc = DateTime.MinValue;
            this.Name = System.IO.Path.GetFileName(dir);
            this.Path = dir;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileData"/> class.
        /// </summary>
        /// <param name="dir">The directory that the file is stored at</param>
        /// <param name="findData">WIN32_FIND_DATA structure that this
        /// object wraps.</param>
        internal FileData(string dir, WIN32_FIND_DATA findData)
        {
            this.Attributes = findData.dwFileAttributes;


            this.CreationTimeUtc = ConvertDateTime(findData.ftCreationTime_dwHighDateTime,
                                                findData.ftCreationTime_dwLowDateTime);

            this.LastAccessTimeUtc = ConvertDateTime(findData.ftLastAccessTime_dwHighDateTime,
                                                findData.ftLastAccessTime_dwLowDateTime);

            this.LastWriteTimeUtc = ConvertDateTime(findData.ftLastWriteTime_dwHighDateTime,
                                                findData.ftLastWriteTime_dwLowDateTime);

            this.Size = CombineHighLowInts(findData.nFileSizeHigh, findData.nFileSizeLow);

            this.Name = findData.cFileName;
            this.Path = System.IO.Path.Combine(dir, findData.cFileName);
        }

        private static long CombineHighLowInts(uint high, uint low)
        {
            return (((long)high) << 0x20) | low;
        }

        private static DateTime ConvertDateTime(uint high, uint low)
        {
            long fileTime = CombineHighLowInts(high, low);
            return DateTime.FromFileTimeUtc(fileTime);
        }
    }

    /// <summary>
    /// Contains information about the file that is found 
    /// by the FindFirstFile or FindNextFile functions.
    /// </summary>
    [Serializable, StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto), BestFitMapping(false)]
    internal class WIN32_FIND_DATA
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


    /// <summary>
    /// Provides the implementation of the 
    /// <see cref="T:System.Collections.Generic.IEnumerator`1"/> interface
    /// </summary>
    [System.Security.SuppressUnmanagedCodeSecurity]
    public class FileEnumerator : IEnumerator<FileData>
    {
        enum FINDEX_INFO_LEVELS
        {
            Standard,
            Basic,
            MaxInfoLevel,
        }
        enum FINDEX_SEARCH_OPTIONS
        {
            SearchNameMatch,
            SearchLimitToDirectories,
            SearchLimitToDevices
        }

        [Flags]
        enum FINDEX_ADDITIONAL_FLAGS
        {
            None,
            CaseSensitive = 1,
            LargeFetch = 2,
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern SafeFindHandle FindFirstFile(string fileName,
            [In, Out] WIN32_FIND_DATA data);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern SafeFindHandle FindFirstFileEx(string fileName, FINDEX_INFO_LEVELS fInfoLevelId, [In, Out] WIN32_FIND_DATA data, FINDEX_SEARCH_OPTIONS fSearchOp,
            IntPtr searchParams, FINDEX_ADDITIONAL_FLAGS flags);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool FindNextFile(SafeFindHandle hndFindFile,
                [In, Out, MarshalAs(UnmanagedType.LPStruct)] WIN32_FIND_DATA lpFindFileData);


        private string m_path;

        private SafeFindHandle m_hndFindFile;
        private WIN32_FIND_DATA m_win_find_data = new WIN32_FIND_DATA();

        /// <summary>
        /// Initializes a new instance of the <see cref="FileEnumerator"/> class.
        /// </summary>
        /// <param name="path">The path to search.</param>
        /// <param name="filter">The search string to match against files in the path.</param>
        /// <param name="searchOption">
        /// One of the SearchOption values that specifies whether the search 
        /// operation should include all subdirectories or only the current directory.
        /// </param>
        public FileEnumerator(string path)
        {
            m_path = path;
        }

        #region IEnumerator<FileData> Members

        /// <summary>
        /// Gets the element in the collection at the current position of the enumerator.
        /// </summary>
        /// <value></value>
        /// <returns>
        /// The element in the collection at the current position of the enumerator.
        /// </returns>
        public FileData Current
        {
            get { return new FileData(m_path, m_win_find_data); }
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, 
        /// or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (m_hndFindFile != null)
            {
                m_hndFindFile.Dispose();
            }
        }

        #endregion

        #region IEnumerator Members

        /// <summary>
        /// Gets the element in the collection at the current position of the enumerator.
        /// </summary>
        /// <value></value>
        /// <returns>
        /// The element in the collection at the current position of the enumerator.
        /// </returns>
        object System.Collections.IEnumerator.Current
        {
            get { return new FileData(m_path, m_win_find_data); }
        }

        /// <summary>
        /// Advances the enumerator to the next element of the collection.
        /// </summary>
        /// <returns>
        /// true if the enumerator was successfully advanced to the next element; 
        /// false if the enumerator has passed the end of the collection.
        /// </returns>
        /// <exception cref="T:System.InvalidOperationException">
        /// The collection was modified after the enumerator was created.
        /// </exception>
        public bool MoveNext()
        {
            bool retval = false;

            //If the handle is null, this is first call to MoveNext in the current 
            // directory.  In that case, start a new search.
            if (m_hndFindFile == null)
            {
                new FileIOPermission(FileIOPermissionAccess.PathDiscovery, m_path).Demand();
                m_hndFindFile = FindFirstFile(Path.Combine(m_path, "*"), m_win_find_data);
                retval = !m_hndFindFile.IsInvalid;
            }
            else
            {
                //Otherwise, find the next item.
                retval = FindNextFile(m_hndFindFile, m_win_find_data);
            }

            // do not report "." and ".." directories !!
            while (retval && (String.Compare(m_win_find_data.cFileName, ".") == 0 || String.Compare(m_win_find_data.cFileName, "..") == 0))
                retval = FindNextFile(m_hndFindFile, m_win_find_data);

            return retval;
        }

        /// <summary>
        /// Sets the enumerator to its initial position, which is before the first element in the collection.
        /// </summary>
        /// <exception cref="T:System.InvalidOperationException">
        /// The collection was modified after the enumerator was created.
        /// </exception>
        public void Reset()
        {
            m_hndFindFile = null;
        }

        #endregion
    }
}
