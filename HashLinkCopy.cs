using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace de.intronik.backup
{
    public class HashLinkCopy
    {
        #region private fields
        delegate void CreateLinkDelegate(string linkName, FileSystemHashEntry target, int level);
        HashAlgorithm hashAlgorithm;
        string _hashDir;
        string _destinationDirectory;
        CreateLinkDelegate createFileLink;
        CreateLinkDelegate createDirectoryLink;
        #endregion

        #region public methods
        public HashLinkCopy()
        {
            this.hashAlgorithm = SHA1.Create();
            this.DirectoryLinkCreation = backup.DirectoryLinkCreation.Symlink;
            this.FileLinkCreation = backup.FileLinkCreation.Symlink;
        }

        public event EventHandler<HashLinkActionEventArgs> Action;
        public event EventHandler<HashLinkErrorEventArgs> Error;


        public string DestinationDirectory
        {
            get { return this._destinationDirectory; }
            set
            {
                // make sure destination path is rooted
                if (!Path.IsPathRooted(value))
                    value = Path.GetFullPath(Path.Combine(Path.GetPathRoot(Directory.GetCurrentDirectory()), value));
                this._destinationDirectory = Path.GetFullPath(value);
                // check for empty hash directories -> use default if none was given
                if (string.IsNullOrEmpty(this.HashDir))
                    this.HashDir = HashEntry.GetDefaultHashDir(this._destinationDirectory);
            }
        }

        public string HashDir
        {
            get { return this._hashDir; }
            set
            {
                if (String.IsNullOrEmpty(value))
                    throw new ArgumentNullException();
                this._hashDir = Path.GetFullPath(value);
                var c = _hashDir.LastOrDefault();
                if (c != Path.DirectorySeparatorChar && c != Path.AltDirectorySeparatorChar)
                    this._hashDir = String.Format("{0}{1}", _hashDir, Path.DirectorySeparatorChar);
            }
        }

        /// <summary>
        /// Controls the way file links are created
        /// </summary>
        public FileLinkCreation FileLinkCreation
        {
            get
            {
                if (this.createFileLink == this.createHardLink) return backup.FileLinkCreation.Hardlink;
                if (this.createFileLink == this.createSymbolicLink) return backup.FileLinkCreation.Symlink;
                if (this.createFileLink == this.createReparseSymbolicLink) return backup.FileLinkCreation.RSymlink;
                throw new ArgumentOutOfRangeException("FileLinkCreation");
            }
            set
            {
                switch (value)
                {
                    case backup.FileLinkCreation.Hardlink: this.createFileLink = this.createHardLink; break;
                    case backup.FileLinkCreation.Symlink: this.createFileLink = this.createSymbolicLink; break;
                    case backup.FileLinkCreation.RSymlink: this.createFileLink = this.createReparseSymbolicLink; break;
                    default:
                        throw new ArgumentOutOfRangeException("FileLinkCreation");
                }
            }
        }

        /// <summary>
        /// Controls the way directory links are created
        /// </summary>
        public DirectoryLinkCreation DirectoryLinkCreation
        {
            get
            {
                if (this.createDirectoryLink == this.createJunction) return backup.DirectoryLinkCreation.Junction;
                if (this.createDirectoryLink == this.createSymbolicLink) return backup.DirectoryLinkCreation.Symlink;
                if (this.createDirectoryLink == this.createReparseSymbolicLink) return backup.DirectoryLinkCreation.RSymlink;
                throw new ArgumentOutOfRangeException("DirectoryLinkCreation");
            }
            set
            {
                switch (value)
                {
                    case backup.DirectoryLinkCreation.Junction: this.createDirectoryLink = this.createJunction; break;
                    case backup.DirectoryLinkCreation.Symlink: this.createDirectoryLink = this.createSymbolicLink; break;
                    case backup.DirectoryLinkCreation.RSymlink: this.createDirectoryLink = this.createReparseSymbolicLink; break;
                    default:
                        throw new ArgumentOutOfRangeException("DirectoryLinkCreation");
                }
            }
        }

        static Tuple<DirectoryInfo, string> DecodeSourceFolder(string folderString)
        {
            var splitted = folderString.Split(new char[] { '=', }, 2);
            var newFolderString = splitted[0];
            var alias = splitted.Length == 2 ? splitted[1] : "";
            var info = new DirectoryInfo(newFolderString);
            if (String.IsNullOrEmpty(alias))
                alias = info.Name;
            return new Tuple<DirectoryInfo, string>(info, alias);
        }

        public void CopyFolders(string[] sourceFolders)
        {
            if (string.IsNullOrEmpty(this.DestinationDirectory))
                throw new InvalidOperationException("Destination folder is not set!");
            // prepare hash directory
            Console.WriteLine("Preparing hash folder");
            this.PrepareHashDirectory();
            Console.WriteLine("Hash folder preparation completed!");
            // prepare target directory
            Console.WriteLine("Checking target directory: \"{0}\"", this.DestinationDirectory);
            if (Directory.Exists(this.DestinationDirectory))
            {
                var ts = DateTime.Now.ToString("yyyy-MM-dd_HH_mm");
                Console.WriteLine("Warning target directory already exists! Trying to append current time stamp ({0})!", ts);
                this.DestinationDirectory = Path.Combine(this.DestinationDirectory, ts);
                if (Directory.Exists(DestinationDirectory))
                    Console.WriteLine("Warning new target directory \"{0}\" also already exists! Trying to continue using this directory anyway!", this.DestinationDirectory);
            }
            // do not create separate sub folder for a single source folder
            string singleFolderDestinationDir = sourceFolders.Length == 1 ? this.DestinationDirectory : null;
            if (sourceFolders.Length == 1)
                this.DestinationDirectory = Path.GetFullPath(Path.Combine(this.DestinationDirectory, ".."));
            // ensure root folder exists
            Console.WriteLine("Creating target directory: \"{0}\"", this.DestinationDirectory);
            Directory.CreateDirectory(this.DestinationDirectory);
            // process source folders
            foreach (var sourceFolderAndAlias in sourceFolders)
            {
                // start link generation in first directory level
                var decodedSourceFolderAndAlias = DecodeSourceFolder(sourceFolderAndAlias);
                var target = Path.Combine(this.DestinationDirectory, decodedSourceFolderAndAlias.Item2);
                if (singleFolderDestinationDir != null) target = singleFolderDestinationDir;
                Console.WriteLine("Linking \"{0}\"=>\"{1}\"...", decodedSourceFolderAndAlias.Item1.FullName, target);
                if (Directory.Exists(target))
                {
                    OnHashLinkError(new HashLinkErrorEventArgs(decodedSourceFolderAndAlias.Item1, new InvalidOperationException("Target folder already exists!")));
                    continue;
                }
                var hashEntry = this.BuildHashEntry(decodedSourceFolderAndAlias.Item1, 1);
                if (hashEntry != null)
                {
                    if (!OnAction(HashLinkAction.LinkDirectory, decodedSourceFolderAndAlias.Item1, 0))
                        this.CreateLink(target, hashEntry, 0);
                    Console.WriteLine("Linking of \"{0}\"=>\"{1}\" completed", decodedSourceFolderAndAlias.Item1.FullName, target);
                }
                else
                    OnHashLinkError(new HashLinkErrorEventArgs(decodedSourceFolderAndAlias.Item1, new ApplicationException(String.Format("Copy \"{0}\"=>\"{1}\" failed!", decodedSourceFolderAndAlias.Item1.FullName, target))));
            }
        }
        #endregion

        #region private methods

        void createHardLink(string name, FileSystemHashEntry entry, int level)
        {
            var target = HashDir + entry.ToString();
            while (true)
            {
                var linkError = Win32.CreateHardLink(name, target, IntPtr.Zero) ? 0 : System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                switch (linkError)
                {
                    case 0:     // ERROR_SUCCESS
                        return;
                    case 183:   // ERROR_ALREADY_EXISTS (target file already existing)
                        throw new InvalidOperationException("Target file already exists!");
                    case 1142:  // ERROR_TOO_MANY_LINKS
                        File.Move(target, name);
                        return;
                    case 2:     // ERROR_FILE_NOT_FOUND
                        this.CopyFile(entry.Info, target, level);
                        break;
                    case 3:     // ERROR_PATH_NOT_FOUND                        
                    default:
                        throw new Win32Exception(linkError, String.Format("CreateHardLink({0},{1}) returned 0x{2:X8}h", name, target, linkError));
                }
            }
        }

        void createJunction(string name, FileSystemHashEntry entry, int level)
        {
            var target = HashDir + entry.ToString();
            Directory.CreateDirectory(name);
            Win32.CreateJunction(name, target);
        }

        void createSymbolicLink(string linkName, FileSystemHashEntry entry, int level)
        {
            while (true)
            {
                var hc = entry.ToString();
                var target = this.HashDir + hc;
                // check if copy file must be done
                if (!entry.IsDirectory && !File.Exists(target))
                    this.CopyFile(entry.Info, target, level);
                // remove target drive
                target = (target.Length > 2 && target[1] == ':') ? target.Substring(2) : target;
                // create link and set error variable
                var error = Win32.CreateSymbolicLink(linkName, target, entry.IsDirectory ? Win32.SymbolicLinkFlags.Directory : Win32.SymbolicLinkFlags.File) ? 0 : System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                switch (error)
                {
                    case 0:     // ERROR_SUCCESS
                        return;
                    case 80:    // ERROR_FILE_EXISTS
                    case 183:   // ERROR_ALREADY_EXISTS
                        throw new InvalidOperationException(String.Format("Error creating symbolic link \"{0}\"=>\"{1}\". Directory already exists!", linkName, target));
                    case 1314:  // ERROR_PRIVILEGE_NOT_HELD
                        throw new System.Security.SecurityException("Not enough priveleges to create symbolic links!", new System.ComponentModel.Win32Exception(error));
                    case 2:     // ERROR_FILE_NOT_FOUND
                    case 3:     // ERROR_PATH_NOT_FOUND
                    default:
                        throw new System.ComponentModel.Win32Exception(error, linkName);
                }
            }
        }

        void createReparseSymbolicLink(string linkName, FileSystemHashEntry entry, int level)
        {
            throw new NotSupportedException("Createing symlinks this way is currently not supported!");
            /*
            var hc = entry.ToString();
            var target = this.HashDir + hc;
            // check if copy file must be done
            if (!entry.IsDirectory && !File.Exists(target))
                this.CopyFile(entry.Info, target, level);
            // remove target drive
            target = (target.Length > 2 && target[1] == ':') ? target.Substring(2) : target;
            // create empty dir or file
            if (entry.IsDirectory)
                Directory.CreateDirectory(linkName);
            else
                File.WriteAllText(linkName, "");
            // create link
            Win32.CreateSymbolLink(linkName, target, true);
             */
        }


        void CreateLink(string linkName, FileSystemHashEntry target, int level)
        {
            (target.IsDirectory ? this.createDirectoryLink : this.createFileLink)(linkName, target, level);
        }

        CopyFileCallbackAction MyCopyFileCallback(string source, string destination, object state, long totalFileSize, long totalBytesTransferred)
        {
            var info = (KeyValuePair<FileSystemInfo, int>)state;
            this.OnAction(HashLinkAction.CopyFile, info.Key, info.Value, String.Format("=>\"{0}\" ({1:0.0%})", destination, (double)totalBytesTransferred / (double)totalFileSize));
            return CopyFileCallbackAction.Continue;
        }

        void CopyFile(FileSystemInfo info, string dest, int level)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            FileRoutines.CopyFile(info.FullName, dest, CopyFileOptions.AllowDecryptedDestination | CopyFileOptions.Restartable, MyCopyFileCallback,
                new KeyValuePair<FileSystemInfo, int>(info, level));
            File.SetAttributes(dest, FileAttributes.Normal);
        }

        string GetFullHashPath(HashEntry entry, bool tempfolder = false) { return this._hashDir + entry.ToString(tempfolder); }

        void PrepareHashDirectory()
        {
            // create new hash directory, with default attributes
            if (!Directory.Exists(this.HashDir))
            {
                var di = Directory.CreateDirectory(this.HashDir);
                di.Attributes = di.Attributes | FileAttributes.Hidden | FileAttributes.Compressed | FileAttributes.NotContentIndexed;
            }
            // make sure all hash directories exist!
            for (var i = 0; i < (1 << 12); i++)
            {
                Directory.CreateDirectory(Path.Combine(this.HashDir, "f", i.ToString("X3")));
                Directory.CreateDirectory(Path.Combine(this.HashDir, "d", i.ToString("X3")));
            }
            // delete temp directory content
            var tempDir = Path.Combine(this.HashDir, "t");
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
            // recreate temp directory
            Directory.CreateDirectory(Path.Combine(this.HashDir, "t"));
        }


        bool OnAction(HashLinkAction action, FileSystemInfo info, int level, string extendedInfo = null)
        {
            if (this.Action == null) return false;
            var e = new HashLinkActionEventArgs(info, level, action, extendedInfo);
            this.Action(this, e);
            return e.Cancel;
        }

        void OnHashLinkError(HashLinkErrorEventArgs e)
        {
            if (this.Error != null)
                this.Error(this, e);
        }

        FileSystemHashEntry BuildHashEntry(FileSystemInfo fileSystemInfo, int level)
        {
            // required to throw error on correct entry
            FileSystemInfo currentEntry = null;
            try
            {
                currentEntry = fileSystemInfo;
                if ((fileSystemInfo.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    //
                    // HANDLE SUBDIRECTORY
                    //

                    // allow filters to apply, or display tools
                    if (OnAction(HashLinkAction.EnterSourceDirectory, fileSystemInfo, level))
                        return null;
                    var sourceDirectory = (DirectoryInfo)fileSystemInfo;
                    var m = new MemoryStream();
                    var w = new BinaryWriter(m);
                    var directoryEntries = new List<FileSystemHashEntry>(500);
                    foreach (var entry in sourceDirectory.EnumerateFileSystemInfos())
                    {
                        currentEntry = entry;
                        var subEntry = this.BuildHashEntry(entry, level + 1);
                        if (subEntry != null)
                        {
                            w.Write(subEntry.Info.Attributes.HasFlag(FileAttributes.Directory));
                            w.Write(subEntry.Hash);
                            w.Write(subEntry.Info.Name);
                            directoryEntries.Add(subEntry);
                        }
                    }
                    var directoryEntry = new DirectoryHashEntry(sourceDirectory, hashAlgorithm.ComputeHash(m.ToArray()));
                    currentEntry = sourceDirectory;
                    var targetHashDir = GetFullHashPath(directoryEntry, false);
                    if (Directory.Exists(targetHashDir))
                        return directoryEntry;
                    // create link structure in temp directory first                    
                    var tmpDirName = GetFullHashPath(directoryEntry, true) + "\\";
                    Directory.CreateDirectory(tmpDirName);
                    foreach (var subEntry in directoryEntries)
                    {
                        if (OnAction(subEntry.IsDirectory ? HashLinkAction.LinkDirectory : HashLinkAction.LinkFile, subEntry.Info, level))
                            return null;
                        this.CreateLink(tmpDirName + subEntry.Info.Name, subEntry, level);
                    }
                    // we are done, rename directory
                    Directory.Move(tmpDirName, targetHashDir);
                    return directoryEntry;
                }
                else
                {
                    //
                    // HANDLE FILE
                    //
                    return OnAction(HashLinkAction.ProcessSourceFile, fileSystemInfo, level) ? null : new FileHashEntry((FileInfo)fileSystemInfo, hashAlgorithm);
                }
            }
            catch (IOException e)
            {
                OnHashLinkError(new HashLinkErrorEventArgs(currentEntry, e));
                return null;
            }
            catch (Win32Exception e)
            {
                OnHashLinkError(new HashLinkErrorEventArgs(currentEntry, e));
                return null;
            }
        }
        #endregion
    }

    public enum DirectoryLinkCreation
    {
        /// <summary>
        /// Normal Windows Vista Symlink API function
        /// </summary>
        Symlink,
        /// <summary>
        /// Junctions are always absolute and are created using reparse points
        /// Admin rights are required
        /// </summary>
        Junction,
        /// <summary>
        /// Create Symlink via DeviceIO Control and reparse points
        /// No Admin rights are required
        /// </summary>
        RSymlink,
    }

    public enum FileLinkCreation
    {
        Symlink,
        Hardlink,
        /// <summary>
        /// Link to files using symlink
        /// Admin rights are required
        /// </summary>
        RSymlink,
    }
}