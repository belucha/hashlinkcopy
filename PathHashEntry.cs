using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.backup
{
    public class PathHashEntry : HashEntry
    {
        bool _directory;
        byte[] _hash;

        public PathHashEntry(HashEntry hash)
        {
            this._hash = hash.Hash;
            this._directory = hash.IsDirectory;
        }

        /// <summary>
        /// Creates a HashEntry from the given directory path
        /// </summary>
        /// <param name="path"></param>
        /// <param name="entryTypeIndex"></param>
        public PathHashEntry(string path, int entryTypeIndex = 0)
        {
            this._hash = new byte[20];
            switch (Char.ToLower(path[entryTypeIndex]))
            {
                case 'd': this._directory = true; break;
                case 'f': this._directory = false; break;
                default:
                    throw new InvalidOperationException(String.Format("Unsupported hash filename \"{0}\" entry type '{1}' at index {2} is unknown!", path, path[entryTypeIndex], entryTypeIndex));
            }
            entryTypeIndex += 2;
            this._hash[0] = Byte.Parse(path.Substring(entryTypeIndex, 2), System.Globalization.NumberStyles.HexNumber);
            this._hash[1] = Byte.Parse(new string(new char[] { path[entryTypeIndex + 2], path[entryTypeIndex + 4] }), System.Globalization.NumberStyles.HexNumber);
            entryTypeIndex += 5;
            for (var i = 2; i < 20; i++, entryTypeIndex += 2)
                this._hash[i] = Byte.Parse(path.Substring(entryTypeIndex, 2), System.Globalization.NumberStyles.HexNumber);
        }

        protected override bool GetIsDirectory() { return this._directory; }
        protected override byte[] GetHash() { return this._hash; }
    }
}
