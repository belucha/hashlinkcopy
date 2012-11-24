using System;
using System.Security.Principal;
using System.Windows.Forms;
using System.Security.Cryptography;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.backup
{
    public abstract class HashLinkCopyBase
    {
        protected HashAlgorithm hashAlgorithm;
        protected abstract void CreateLink(string linkName, FileSystemHashEntry target, int level);
        protected abstract bool AdminPermissionsRequired { get; }

        protected void CopyFile(FileSystemInfo info, string dest, int level)
        {
            while (true)
            {
                var error = Win32.CopyFileEx(info.FullName, dest, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, Win32.CopyFileFlags.FAIL_IF_EXISTS) ?
                    0 :
                    System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                switch (error)
                {
                    case 0:     // ERROR_SUCCESS
                        this.OnAction(HashLinkAction.CopyFile, info, level);
                        File.SetAttributes(dest, FileAttributes.Normal);
                        return;
                    case 3:     // ERROR_PATH_NOT_FOUND
                        Directory.CreateDirectory(Path.GetDirectoryName(dest));
                        break;
                    case 80:    // ERROR_FILE_EXISTS
                    case 183:   // ERROR_ALREADY_EXISTS
                        return;
                    case 2:     // ERROR_FILE_NOT_FOUND
                    default:
                        throw new ApplicationException(info.FullName, new System.ComponentModel.Win32Exception(error));
                }
            }
        }

        const string DefaultHashDir = @"Hash";
        string _hashDir;
        string GetFullHashPath(HashEntry entry, bool tempfolder = false) { return this._hashDir + entry.ToString(tempfolder); }
        void PrepareHashDirectory()
        {
            Directory.CreateDirectory(this.HashDir);
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

        public event EventHandler<HashLinkActionEventArgs> Action;
        public event EventHandler<HashLinkErrorEventArgs> Error;

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


        protected virtual bool OnAction(HashLinkAction action, FileSystemInfo info, int level)
        {
            if (this.Action == null) return false;
            var e = new HashLinkActionEventArgs(info, level, action);
            this.Action(this, e);
            return e.Cancel;
        }

        protected virtual void OnHashLinkError(HashLinkErrorEventArgs e)
        {
            if (this.Error != null)
                this.Error(this, e);
        }

        private FileSystemHashEntry BuildHashEntry(FileSystemInfo fileSystemInfo, int level)
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
            catch (Exception e)
            {
                OnHashLinkError(new HashLinkErrorEventArgs(currentEntry, e));
                return null;
            }
        }
        public DateTime Start { get; private set; }
        public DateTime End { get; private set; }
        public TimeSpan Elapsed { get { return End.Subtract(Start); } }

        public HashLinkCopyBase()
        {
            this.hashAlgorithm = SHA1.Create();
        }

        private static bool HasAdministratorPrivileges()
        {
            WindowsIdentity id = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(id);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        public void Copy(string sourcePath, string destinationPath)
        {
            // check if target folder is valid
            if (string.IsNullOrEmpty(sourcePath))
                throw new ArgumentOutOfRangeException("Source path can't be empty!");
            if (string.IsNullOrEmpty(destinationPath))
                throw new ArgumentOutOfRangeException("Destination path can't be empty!");
            // check for permissions
            if (AdminPermissionsRequired && !HasAdministratorPrivileges())
                throw new System.Security.SecurityException("Access Denied. Administrator permissions are " +
                    "needed to use the selected options. Use an administrator command " +
                    "prompt to complete these tasks.");
            try
            {
                this.Start = DateTime.Now;
                // get file system info on source
                FileSystemInfo sourceFileSystemInfo = Directory.Exists(sourcePath) ? (FileSystemInfo)new DirectoryInfo(sourcePath) : (File.Exists(sourcePath) ? (FileSystemInfo)new FileInfo(sourcePath) : null);
                if (sourceFileSystemInfo == null)
                    throw new InvalidOperationException(String.Format("Source path \"{0}\" is invalid!", sourcePath));
                // make sure destination path is rooted
                if (!Path.IsPathRooted(destinationPath))
                    destinationPath = Path.GetFullPath(Path.Combine(Path.GetPathRoot(sourceFileSystemInfo.FullName), destinationPath));
                // check for empty hash directories -> use default if none was given
                if (string.IsNullOrEmpty(this.HashDir))
                    this.HashDir = Path.Combine(Path.GetPathRoot(destinationPath), DefaultHashDir);
                // check if hash dir and taget folder are on the same volume
                if (String.Compare(Path.GetPathRoot(this.HashDir), Path.GetPathRoot(destinationPath), true) != 0)
                    throw new InvalidOperationException("Target folder and HashDir must be on same volume!");
                // check if the destination path is existing -> if yes append source name
                if ((sourceFileSystemInfo.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    if (Directory.Exists(destinationPath))
                    {
                        destinationPath = Path.Combine(destinationPath, sourceFileSystemInfo.Name);
                        if (Directory.Exists(destinationPath))
                            throw new InvalidOperationException(String.Format("Target folder \"{0}\" is already existing!", destinationPath));
                    }
                }
                else
                {
                    if (File.Exists(destinationPath))
                        throw new InvalidOperationException(String.Format("Target file \"{0}\" is already existing!", destinationPath));
                }
                // prepare hash directory
                this.PrepareHashDirectory();
                // Make sure the parent target directory exists
                var parentDirectory = Path.GetFullPath(Path.Combine(destinationPath, ".."));
                Directory.CreateDirectory(parentDirectory);
                // start link generation in first directory level
                var hashEntry = this.BuildHashEntry(sourceFileSystemInfo, 1);
                if (hashEntry != null)
                {
                    if (!OnAction(sourceFileSystemInfo is DirectoryInfo ? HashLinkAction.LinkDirectory : HashLinkAction.LinkFile, sourceFileSystemInfo, 1))
                        this.CreateLink(destinationPath, hashEntry, 1);
                }
                else
                    throw new ApplicationException("Copy failed!");
            }
            finally
            {
                this.End = DateTime.Now;
            }
        }
    }
}