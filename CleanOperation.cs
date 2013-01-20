using System;
using System.ComponentModel;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.backup
{
    [Description("Removes unused hash entries from the hash folder.\nLine two\nTodo")]
    public class CleanOperation : HashOperation
    {
        int typeIndex;
        Dictionary<HashEntry, bool> usedHashs = new Dictionary<HashEntry, bool>();
        public int UsedHashCount { get { return usedHashs.Count; } }
        public uint deleteDirCount = 0;
        public uint deleteFileCount = 0;
        public long totalBytesDeleted = 0;
        public bool EnableDelete { get; set; }

        void MarkHashEntryAsUsed(string hashEntryPath, int level = 0)
        {
            if (hashEntryPath == null) return;
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
            try
            {
                foreach (var entry in directory.GetFileSystemInfos())
                {
                    // ignore hash folder                
                    if (entry.FullName.StartsWith(this.HashFolder, StringComparison.InvariantCultureIgnoreCase))
                        continue;
                    if ((entry.Attributes & FileAttributes.System) == FileAttributes.System)
                        continue;
                    if ((entry.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
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

        void DeleteUnused(DirectoryInfo directory, int typeIndex)
        {
            foreach (var entry in directory.GetFileSystemInfos())
            {
                var h = new PathHashEntry(entry.FullName, typeIndex);
                if (!this.usedHashs.ContainsKey(h))
                {
                    if (h.IsDirectory)
                    {
                        if (EnableDelete)
                        {
                            foreach (var subEntry in ((DirectoryInfo)entry).GetFileSystemInfos())
                                subEntry.Delete();
                            ((DirectoryInfo)entry).Delete();
                        }
                        this.deleteDirCount++;
                    }
                    else
                    {
                        this.deleteFileCount++;
                        this.totalBytesDeleted += ((FileInfo)entry).Length;
                        if (EnableDelete)
                            entry.Delete();
                    }
                }
            }
        }

        public void DeleteUnused()
        {
            for (var i = 0; i < 4096; i++)
                DeleteUnused(new DirectoryInfo(Path.Combine(this.HashFolder, "d", i.ToString("x3"))), typeIndex);
            for (var i = 0; i < 4096; i++)
                DeleteUnused(new DirectoryInfo(Path.Combine(this.HashFolder, "f", i.ToString("x3"))), typeIndex);
        }

        protected override void PreHandleParameters()
        {
            // use current dir as default argument
            if (Parameters.Length == 0)
                this.Parameters = new string[] {
                    Directory.GetCurrentDirectory(),
                 };
            // set default hash folder
            this.HashFolder = HashEntry.GetDefaultHashDir(this.Parameters.First());
        }

        public override void PreRun()
        {
            base.PreRun();
            print("EnableDelete", EnableDelete ? "yes" : "no");
        }

        public override int Run()
        {
            // update hash folder
            this.HashFolder = Path.GetFullPath(HashFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (!Directory.Exists(HashFolder))
                throw new ArgumentException(String.Format("Invalid hashfolder \"{0}\"!", HashFolder));
            this.typeIndex = this.HashFolder.Length + 1;
            // check on hash dir
            foreach (var dir in this.Parameters)
            {
                Output.WriteLine("Scanning: \"{0}\"", dir);
                CheckDirectories(dir);
            }
            print("Used hashs", UsedHashCount);
            if (UsedHashCount == 0)
            {
                Output.WriteLine("No has files where marked as used! There is propably an error in the parameters!");
                EnableDelete = false;
                if (String.Compare(this.PromptInput("Continue anyway (this will erase the entire Hash directory)? [yes/NO] "), "yes", true) != 0)
                {
                    Output.WriteLine("aborted");
                    return 1;
                }
            }
            if (!EnableDelete)
                Output.WriteLine("Counting unused files and directories in hash folder...");
            else
                Output.WriteLine("Deleting files and directories from hash folder...");
            DeleteUnused();
            print(EnableDelete ? "deleted files" : "unused files", deleteFileCount);
            print(EnableDelete ? "deleted folders" : "unused folders", deleteDirCount);
            print(EnableDelete ? "space gained" : "possible space", FormatBytes(totalBytesDeleted));
            if ((deleteDirCount > 0 || deleteFileCount > 0) && !EnableDelete)
            {
                if (String.Compare(this.PromptInput("Do you want to delete these data (type \"yes\" completely or use command line option --enableDelete)? [yes/NO] "), "yes", true) != 0)
                {
                    Console.WriteLine("aborted");
                    return 1;
                }
                EnableDelete = true;
                Output.WriteLine("Deleting marked files and directories from hash folder...");
                DeleteUnused();
                if (UsedHashCount == 0)
                {
                    Output.WriteLine("Deleting hash root folder!");
                    Directory.Delete(HashFolder, true);
                }
            }
            Output.WriteLine("done");
            return 0;
        }
    }
}
