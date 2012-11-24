using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.backup
{
    class HashCleanup
    {
        string hashFolder;
        int typeIndex;
        Dictionary<HashEntry, bool> usedHashs = new Dictionary<HashEntry, bool>();
        uint deleteDirCount = 0;
        uint deleteFileCount = 0;

        public HashCleanup(string hashFolder)
        {
            this.hashFolder = Path.GetFullPath(hashFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (!Directory.Exists(hashFolder))
                throw new ArgumentException(String.Format("Invalid hashfolder \"{0}\"!", hashFolder));
            this.typeIndex = this.hashFolder.Length + 1;
        }

        void MarkHashEntryAsUsed(string hashEntryPath, int level = 0)
        {
            if (hashEntryPath == null) return;
            Console.Title = String.Format("{0}: {1}", level, hashEntryPath);
            var hashEntry = new PathHashEntry(hashEntryPath, this.typeIndex);
            if (hashEntry.IsDirectory)
            {
                //
                // directory entry
                //
                if (!this.usedHashs.ContainsKey(hashEntry))
                {
                    this.usedHashs[hashEntry] = true;
                    foreach (var fileEntry in Directory.GetFileSystemEntries(hashEntryPath))
                        MarkHashEntryAsUsed(Win32.GetJunctionTarget(fileEntry), level + 1);
                }
            }
            else
            {
                //
                // file entry
                //
                this.usedHashs[hashEntry] = true;
            }
        }

        public void CheckDirectories(DirectoryInfo directory, int level = 0)
        {
            if (directory == null) return;
            Console.WriteLine(directory.FullName);
            try
            {
                foreach (var entry in directory.GetFileSystemInfos())
                {
                    // ignore hash folder                
                    if (entry.FullName.StartsWith(this.hashFolder, StringComparison.InvariantCultureIgnoreCase))
                        continue;
                    // check if the folder is a symbolic link
                    var t = Win32.GetJunctionTarget(entry.FullName);
                    if (t != null)
                    {
                        // yes mark as used and we are done
                        MarkHashEntryAsUsed(t, level + 1);
                        continue;
                    }
                    // descend in directories
                    CheckDirectories(entry as DirectoryInfo, level + 1);
                }
            }
            catch
            {
            }
        }

        public void CheckDirectories(string path)
        {
            CheckDirectories(new DirectoryInfo(path), 0);
        }

        void DeleteUnused(string hashDir, int typeIndex)
        {
            foreach (var entry in Directory.GetFileSystemEntries(hashDir))
            {
                var h = new PathHashEntry(entry, typeIndex);
                if (!this.usedHashs.ContainsKey(h))
                {
                    Console.WriteLine("Delete '{0}'", entry);
                    if (h.IsDirectory)
                    {
                        var entryInfo = new DirectoryInfo(entry);
                        foreach (var subEntry in entryInfo.GetFileSystemInfos())
                            subEntry.Delete();
                        entryInfo.Delete();
                        this.deleteDirCount++;
                    }
                    else
                    {
                        this.deleteFileCount++;
                        File.Delete(entry);
                    }
                }
            }
        }

        public void DeleteUnused()
        {
            for (var i = 0; i < 4096; i++)
                DeleteUnused(Path.Combine(this.hashFolder, "d", i.ToString("x3")), typeIndex);
            for (var i = 0; i < 4096; i++)
                DeleteUnused(Path.Combine(this.hashFolder, "f", i.ToString("x3")), typeIndex);
        }
    }
}
