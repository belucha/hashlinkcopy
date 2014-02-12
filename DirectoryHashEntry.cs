using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace de.intronik.backup
{
    public class DirectoryHashEntry : HashEntry
    {
        byte[] _cachedHash;
        public Dictionary<string, HashEntry> Entries { get; private set; }
        public string Name { get; private set; }
        public DirectoryHashEntry(string name, int capacity)
        {
            this.Name = name;
            this.Entries = new Dictionary<string, HashEntry>(capacity, StringComparer.Ordinal);
        }
        protected override bool GetIsDirectory() { return true; }

        protected override byte[] GetHash()
        {
            if (this._cachedHash != null)
                return this._cachedHash;
            using (var m = new MemoryStream())
            {
                var w = new BinaryWriter(m);
                // write virtual folder structure
                foreach (var subEntry in this.Entries)
                {
                    w.Write(subEntry.Value.IsDirectory);
                    w.Write(subEntry.Value.Hash);
                    w.Write(subEntry.Key);
                }
                // compute hash
                return this._cachedHash = HashProvider.ComputeHash(m.ToArray());
            }
        }
    }
}
