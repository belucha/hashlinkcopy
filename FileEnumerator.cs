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
    /// Provides the implementation of the 
    /// <see cref="T:System.Collections.Generic.IEnumerator`1"/> interface
    /// </summary>
    [System.Security.SuppressUnmanagedCodeSecurity]
    public class FileEnumerator : IEnumerator<FileData>
    {
        private string m_path;

        private Win32.SafeFindHandle m_hndFindFile;
        private Win32.WIN32_FIND_DATA m_win_find_data = new Win32.WIN32_FIND_DATA();

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
                m_hndFindFile = Win32.FindFirstFile(Path.Combine(m_path, "*"), m_win_find_data);
                retval = !m_hndFindFile.IsInvalid;
            }
            else
            {
                //Otherwise, find the next item.
                retval = Win32.FindNextFile(m_hndFindFile, m_win_find_data);
            }

            // do not report "." and ".." directories !!
            while (retval && (String.Compare(m_win_find_data.cFileName, ".") == 0 || String.Compare(m_win_find_data.cFileName, "..") == 0))
                retval = Win32.FindNextFile(m_hndFindFile, m_win_find_data);

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