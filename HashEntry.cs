using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.backup
{
    /// <summary>
    /// Base class representing a hash entry
    /// </summary>
    public abstract class HashEntry
    {
        const string DefaultHashDir = @"Hash";

        public static string GetDefaultHashDir(string destinationPath)
        {
            return Path.Combine(Path.GetPathRoot(Path.GetFullPath(destinationPath)), DefaultHashDir) + Path.DirectorySeparatorChar.ToString();
        }


        public FileSystemInfo Info { get { return GetFileSystemInfo(); } }

        protected abstract bool GetIsDirectory();
        protected abstract FileSystemInfo GetFileSystemInfo();

        /// <summary>
        /// True, when the entry is a directory, false when it is a file
        /// </summary>
        public bool IsDirectory { get { return GetIsDirectory(); } }

        public byte[] Hash { get; protected set; }

        public override int GetHashCode()
        {
            return BitConverter.ToInt32(this.Hash, 0);
        }

        public override bool Equals(object obj)
        {
            var hv = obj as HashEntry;
            return hv != null && hv.IsDirectory == this.IsDirectory && hv.Hash.SequenceEqual(this.Hash);
        }

        public override string ToString()
        {
            return ToString(false);
        }

        public string ToString(bool tempFile)
        {
            var s = new StringBuilder(45);
            s.Append(tempFile ? "t\\" : (this.IsDirectory ? "d\\" : "f\\"));
            for (var i = 0; i < 20; i++)
            {
                var b = Hash[i];
                var nibble = b >> 4;
                s.Append((Char)(nibble < 10 ? '0' + nibble : ('a' + nibble - 10)));
                if (i == 1 && !tempFile)
                    s.Append('\\');
                nibble = b & 0xF;
                s.Append((Char)(nibble < 10 ? '0' + nibble : ('a' + nibble - 10)));
            }
            return s.ToString();
        }
    }
}
