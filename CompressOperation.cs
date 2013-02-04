using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.backup
{
    [Command("replace", "compress",
        Description = "replace existing folder structure by it's hash link",
        Syntax = "folderOrFile1 folderOrFile2 folderOrFileN",
        MinParameterCount = 1
        )]
    public class CompressOperation : HashOperation
    {
        #region private fields
        /// <summary>
        /// stores the position of the 'd' or 'f' character in the hash path string
        /// </summary>
        int typeIndex;
        long duplicateFiles;
        long duplicateBytes;
        long duplicateFolders;
        #endregion

        #region private methods
        HashEntry ReplaceByLink(FileSystemInfo fileSystemInfo, HashEntry hash, int level)
        {
            return hash.IsDirectory ?
                ReplaceByLink(fileSystemInfo as DirectoryInfo, hash, level) :
                ReplaceByLink(fileSystemInfo as FileInfo, hash, level);
        }

        HashEntry ReplaceByLink(DirectoryInfo dirInfo, HashEntry hash, int level)
        {
            var hashPath = this.GetFullHashPath(hash);
            // either move current file to hash folder or delete it
            dirInfo.Attributes = FileAttributes.Normal;
            if (Directory.Exists(hashPath))
            {
                this.duplicateFolders++;
                dirInfo.Delete(true);
            }
            else
                Directory.Move(dirInfo.FullName, hashPath); // otherwise the dirInfo is updated
            // create link
            CreateLink(dirInfo.FullName, hash, level);
            // return new hash (that free's up memory)
            return hash;
        }

        HashEntry ReplaceByLink(FileInfo fileInfo, HashEntry hash, int level)
        {
            var hashPath = this.GetFullHashPath(hash);
            // either move current file to hash folder or delete it
            fileInfo.Attributes = FileAttributes.Normal;
            if (File.Exists(hashPath))
            {
                this.duplicateFiles++;
                this.duplicateBytes += fileInfo.Length;
                fileInfo.Delete();
            }
            else
                File.Move(fileInfo.FullName, hashPath);// otherwise the fileInfo is updated
            // create link
            CreateLink(fileInfo.FullName, hash, level);
            // return new hash (that free's up memory)
            return hash;
        }

        HashEntry Handle(FileSystemInfo fileSystemInfo, int level)
        {
            return (fileSystemInfo.Attributes & FileAttributes.Directory) == FileAttributes.Directory ?
                Handle(fileSystemInfo as DirectoryInfo, level) :
                Handle(fileSystemInfo as FileInfo, level);
        }

        HashEntry Handle(FileInfo fileInfo, int level)
        {
            // first check if the file is already a symbolic hash link, if so we are done!
            var hashEntryPath = Win32.GetJunctionTarget(fileInfo.FullName);
            if (hashEntryPath != null)
            {
                if (hashEntryPath.StartsWith(this.HashFolder, StringComparison.InvariantCultureIgnoreCase))
                    return new PathHashEntry(hashEntryPath, this.typeIndex);
                Console.WriteLine("\"{0}\" does not target hash folder \"{1}\" and it is processed like a normal file!", hashEntryPath, HashFolder);
            }
            // build hash
            var hash = new FileHashEntry(fileInfo, this.ProcessHashAction);
            // replace by symlink
            return new PathHashEntry(ReplaceByLink(fileInfo, hash, level));
        }

        HashEntry Handle(DirectoryInfo dirInfo, int level)
        {
            // first check if the file is already a symbolic hash link, if so we are done!
            var hashEntryPath = Win32.GetJunctionTarget(dirInfo.FullName);
            if (hashEntryPath != null)
            {
                if (hashEntryPath.StartsWith(this.HashFolder, StringComparison.InvariantCultureIgnoreCase))
                    return new PathHashEntry(hashEntryPath, this.typeIndex);
                Console.WriteLine("\"{0}\" does not target hash folder \"{1}\" and it is processed like a normal folder!", hashEntryPath, HashFolder);
            }
            // build hash
            var subItems = dirInfo.GetFileSystemInfos();
            var hash = new DirectoryHashEntry(dirInfo.Name, subItems.Length);
            foreach (var subItem in subItems)
            {
                // handle sub item
                var subHash = Handle(subItem, level + 1);
                // add it
                hash.Entries.Add(subItem.Name, subHash);
            }
            var hashPath = this.GetFullHashPath(hash);
            // replace by symlink
            return new PathHashEntry(ReplaceByLink(dirInfo, hash, level));
        }
        #endregion private methods

        #region protected methods
        protected override void SetParameters(string[] value)
        {
            base.SetParameters(value);
            // determine default hash folder from the first folder (make sure destination path is rooted)
            var firstFolder = this.Parameters.First();
            if (!Path.IsPathRooted(firstFolder))
                firstFolder = Path.GetFullPath(Path.Combine(Path.GetPathRoot(Directory.GetCurrentDirectory()), firstFolder));
            // check for empty hash directories -> use default if none was given
            if (string.IsNullOrEmpty(this.HashFolder))
                this.HashFolder = GetDefaultHashDir(firstFolder);
        }

        protected override int DoOperation()
        {
            // transform to FileSystemInfo
            var sources = this.Parameters
                .Select(p => Directory.Exists(p) ? (FileSystemInfo)new DirectoryInfo(p) : (FileSystemInfo)new FileInfo(p));

            // prep hash folder
            PrepareHashDirectory();

            this.typeIndex = this.HashFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length + 1;

            foreach (var fi in sources)
            {
                // ignore non existing items
                if (!fi.Exists)
                {
                    HandleError(fi, new FileNotFoundException("Ignoring non existing file/folder", fi.FullName));
                    continue;
                }
                // ignore items that do not have the same root as the hash folder
                if (String.Compare(Path.GetPathRoot(fi.FullName), Path.GetPathRoot(this.HashFolder), true) != 0)
                {
                    HandleError(fi, new InvalidOperationException("Folder is not on the same drive as the hash folder"));
                    continue;
                }
                ReplaceByLink(fi, Handle(fi, 1), 1);
            }
            return 0;
        }

        protected override void ShowStatistics()
        {
            base.ShowStatistics();
            print("Duplicate Files", this.duplicateFiles);
            print("Duplicate Bytes", FormatBytes(duplicateBytes));
            print("Duplicate Folders", duplicateFolders);
        }
        #endregion
    }
}
