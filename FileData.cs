using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.hashlinkcopy
{
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
        public readonly long Length;

        /// <summary>
        /// Name of the file
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// Full path to the file.
        /// </summary>
        public readonly string FullName;

        public readonly uint NumberOfLinks;

        /// <summary>
        /// Returns true, when the entry is a directory
        /// </summary>
        public bool IsDirectory { get { return (this.Attributes & FileAttributes.Directory) == FileAttributes.Directory; } }

        /// <summary>
        /// Returns true, when the file or directory exists
        /// </summary>
        public bool Exists { get { return this.NumberOfLinks > 0; } }

        public bool IsReadOnly { get { return (this.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly; } }

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

        internal FileData(string filepath, Win32.BY_HANDLE_FILE_INFORMATION data)
        {
            this.Attributes = data.FileAttributes;
            this.CreationTimeUtc = ConvertDateTime((uint)data.CreationTime.dwHighDateTime, (uint)data.CreationTime.dwLowDateTime);
            this.LastAccessTimeUtc = ConvertDateTime((uint)data.LastAccessTime.dwHighDateTime, (uint)data.LastAccessTime.dwLowDateTime);
            this.LastWriteTimeUtc = ConvertDateTime((uint)data.LastWriteTime.dwHighDateTime, (uint)data.LastWriteTime.dwLowDateTime);
            this.Length = CombineHighLowInts((uint)data.FileSizeHigh, (uint)data.FileSizeHigh);
            this.NumberOfLinks = data.NumberOfLinks;
            this.FullName = filepath;
            this.Name = System.IO.Path.GetFileName(filepath);
        }

        internal FileData(string path, bool directory, bool exist)
        {
            this.Attributes = directory ? FileAttributes.Directory : FileAttributes.Normal;
            this.Length = 0;
            this.CreationTimeUtc = DateTime.Now;
            this.LastWriteTimeUtc = DateTime.Now;
            this.NumberOfLinks = (uint)(exist ? 1 : 0);
            this.FullName = path;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileData"/> class.
        /// </summary>
        /// <param name="dir">The directory that the file is stored at</param>
        /// <param name="findData">WIN32_FIND_DATA structure that this
        /// object wraps.</param>
        internal FileData(string dir, Win32.WIN32_FIND_DATA findData)
        {
            this.Attributes = findData.dwFileAttributes;
            this.CreationTimeUtc = ConvertDateTime(findData.ftCreationTime_dwHighDateTime, findData.ftCreationTime_dwLowDateTime);
            this.LastAccessTimeUtc = ConvertDateTime(findData.ftLastAccessTime_dwHighDateTime, findData.ftLastAccessTime_dwLowDateTime);
            this.LastWriteTimeUtc = ConvertDateTime(findData.ftLastWriteTime_dwHighDateTime, findData.ftLastWriteTime_dwLowDateTime);
            this.Length = CombineHighLowInts(findData.nFileSizeHigh, findData.nFileSizeLow);
            this.NumberOfLinks = 1;
            this.Name = findData.cFileName;
            this.FullName = System.IO.Path.Combine(dir, findData.cFileName);
        }

        public FileData(string filename)
            : this(filename, Win32.GetFileData(filename))
        {

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
}
