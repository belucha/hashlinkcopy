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
    enum LinkGeneration
    {
        Symbolic,
        Junction,
    }

    class HashLinkCopy
    {
        #region private helper types
        delegate void LinkDelegate(string name, FileSystemHashEntry entry);

        #endregion

        #region privates
        LinkDelegate _createLink;
        HashAlgorithm HASH_ALG;

        void CopyFile(string source, string dest)
        {
            if (this.Demo) return;
            while (true)
            {
                var error = Win32.CopyFileEx(source, dest, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, Win32.CopyFileFlags.FAIL_IF_EXISTS) ?
                    0 :
                    System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                switch (error)
                {
                    case 0:     // ERROR_SUCCESS
                        this.CopyCount++;
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
                        var e = new System.ComponentModel.Win32Exception(error);
                        Console.Error.WriteLine("{0}: {1} ({2}) in {3}", e.GetType().Name, e.Message, error, source);
                        Console.WriteLine("{0}: {1} ({2}) in {3}", e.GetType().Name, e.Message, error, source);
                        throw new ApplicationException(source, e);
                }
            }
        }

        private static bool HasAdministratorPrivileges()
        {
            WindowsIdentity id = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(id);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        void CreateSymbolicLink(string name, FileSystemHashEntry entry)
        {
            while (true)
            {
                var hc = entry.ToString();
                var target = this.HashDir + hc;
                // check if copy file must be done
                if (!entry.IsDirectory && !File.Exists(target))
                    this.CopyFile(entry.Info.FullName, target);
                // remove target drive
                target = (target.Length > 2 && target[1] == ':') ? target.Substring(2) : target;
                // create link and set error variable
                var error = Win32.CreateSymbolicLink(name, target, entry.IsDirectory ? Win32.SymbolicLinkFlags.Directory : Win32.SymbolicLinkFlags.File) ? 0 : System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                switch (error)
                {
                    case 0:     // ERROR_SUCCESS
                        this.SymLinkCount++;
                        return;
                    case 80:    // ERROR_FILE_EXISTS
                    case 183:   // ERROR_ALREADY_EXISTS
                        throw new InvalidOperationException(String.Format("Error creating symbolic link \"{0}\"=>\"{1}\". Directory already exists!", name, target));

                    case 1314:  // ERROR_PRIVILEGE_NOT_HELD
                        throw new System.Security.SecurityException("Not enough priveleges to create symbolic links!", new System.ComponentModel.Win32Exception(error));
                    case 2:     // ERROR_FILE_NOT_FOUND
                    case 3:     // ERROR_PATH_NOT_FOUND
                    default:
                        var e = new System.ComponentModel.Win32Exception(error);
                        Console.Error.WriteLine("{0}: {1} ({2}) in {3}", e.GetType().Name, e.Message, error, name);
                        Console.WriteLine("{0}: {1} ({2}) in {3}", e.GetType().Name, e.Message, error, name);
                        throw new ApplicationException(name, e);
                }
            }
        }

        void CreateHardLinkOrJunction(string name, FileSystemHashEntry entry)
        {
            var target = HashDir + entry.ToString();
            if (entry.IsDirectory)
            {
                if (Directory.Exists(name))
                {
                    this.SkippedJunctionCount++;
                    return;
                }
                Directory.CreateDirectory(name);
                Win32.CreateJunction(name, target, false);
                this.JunctionCount++;
            }
            else
                while (true)
                {
                    var linkError = Win32.CreateHardLink(name, target, IntPtr.Zero) ? 0 : System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                    switch (linkError)
                    {
                        case 0:     // ERROR_SUCCESS
                            this.HardLinkCount++;
                            return;
                        case 183:   // ERROR_ALREADY_EXISTS
                            this.SkippedHardLinkCount++;
                            return; // target file already existing
                        case 1142:  // ERROR_TOO_MANY_LINKS
                            File.Move(target, name);
                            this.MoveCount++;
                            return;
                        case 2:     // ERROR_FILE_NOT_FOUND
                            CopyFile(entry.Info.FullName, target);
                            break;
                        case 3:     // ERROR_PATH_NOT_FOUND                        
                        default:
                            throw new System.ComponentModel.Win32Exception(linkError, String.Format("CreateHardLink({0},{1}) returned 0x{2:X8}h", name, target, linkError));
                    }
                }
        }


        FileSystemHashEntry BuildHashEntry(FileSystemInfo fileSystemInfo, int level)
        {
            try
            {
                if ((fileSystemInfo.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    //
                    // HANDLE SUBDIRECTORY
                    //
                    if (level <= MaxDisplayLevel)
                        Console.WriteLine("".PadLeft(level, ' ') + fileSystemInfo.FullName);
                    var sourceDirectory = (DirectoryInfo)fileSystemInfo;
                    //Console.WriteLine("{1}[{0}]", fileSystemInfo.Name, "".PadLeft(level));
                    this.DirectoryCount++;
                    Console.Title = String.Format("Processing (d{1}/f{2}/sl{3}/c{4}/m{5}/hl{6}/j{7}/st{8}) [{0}]",
                        fileSystemInfo.FullName,
                        this.DirectoryCount,
                        this.FileCount,
                        this.SymLinkCount,
                        this.CopyCount,
                        this.MoveCount,
                        this.HardLinkCount,
                        this.JunctionCount,
                        this.SkippedTreeCount);
                    var m = new MemoryStream();
                    var w = new BinaryWriter(m);
                    var directoryEntries = new List<FileSystemHashEntry>(500);
                    foreach (var entry in sourceDirectory.EnumerateFileSystemInfos())
                    {
                        var subEntry = this.BuildHashEntry(entry, level + 1);
                        if (subEntry != null)
                        {
                            w.Write(subEntry.Info.Attributes.HasFlag(FileAttributes.Directory));
                            w.Write(subEntry.Hash);
                            w.Write(subEntry.Info.Name);
                            directoryEntries.Add(subEntry);
                        }
                    }
                    var directoryEntry = new DirectoryHashEntry(sourceDirectory, HASH_ALG.ComputeHash(m.ToArray()));
                    var td = this.HashDir + directoryEntry.ToString();
                    if (Directory.Exists(td))
                    {
                        this.SkippedTreeCount++;
                        return directoryEntry;
                    }
                    // create link structure in temp directory first                    
                    var tmpDirName = this.HashDir + directoryEntry.ToString(true) + "\\";
                    Directory.CreateDirectory(tmpDirName);
                    foreach (var subEntry in directoryEntries)
                        this._createLink(tmpDirName + subEntry.Info.Name, subEntry);
                    // we are done, rename directory
                    Directory.Move(tmpDirName, td);
                    return directoryEntry;
                }
                else
                {
                    //
                    // HANDLE FILE
                    //
                    this.FileCount++;
                    return new FileHashEntry((FileInfo)fileSystemInfo, HASH_ALG);
                }
            }
            catch (Exception e)
            {
                this.ErrorCount++;
                Console.Error.WriteLine("{0}{1}: {2} in {3}", "".PadLeft(level), e.GetType().Name, e.Message, fileSystemInfo.Name);
                Console.WriteLine("{0}{1}: {2} in {3}", "".PadLeft(level), e.GetType().Name, e.Message, fileSystemInfo.Name);
                return null;
            }
        }
        #endregion

        #region statistics
        public long ErrorCount { get; private set; }
        public long FileCount { get; private set; }
        public long DirectoryCount { get; private set; }
        public long CopyCount { get; private set; }
        public long SymLinkCount { get; private set; }
        public long JunctionCount { get; private set; }
        public long SkippedJunctionCount { get; private set; }
        public long SkippedTreeCount { get; private set; }
        public long HardLinkCount { get; private set; }
        public long SkippedHardLinkCount { get; private set; }
        public long MoveCount { get; private set; }
        #endregion

        #region Options
        public int MaxDisplayLevel { get; set; }
        public DateTime Start { get; private set; }
        public DateTime End { get; private set; }
        public TimeSpan Elapsed { get { return End.Subtract(Start); } }
        string _hashDir;
        public string HashDir
        {
            get { return this._hashDir; }
            set
            {
                if (string.IsNullOrEmpty(value))
                    throw new InvalidOperationException("HashDir can't be empty!");
                value = Path.GetFullPath(value);
                // make sure the hash dir ends with a backslash
                var sep = Path.DirectorySeparatorChar.ToString();
                _hashDir = value.EndsWith(sep) ? value : (value + sep);
            }
        }
        public bool Demo { get; set; }
        public LinkGeneration LinkGeneration
        {
            get { return this._createLink == this.CreateSymbolicLink ? LinkGeneration.Symbolic : LinkGeneration.Junction; }
            set
            {
                switch (value)
                {
                    case LinkGeneration.Symbolic:
                        this._createLink = this.CreateSymbolicLink;
                        break;
                    case LinkGeneration.Junction:
                        this._createLink = this.CreateHardLinkOrJunction;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("LinkGeneration", value, "Unsupported link generation method!");
                }
            }
        }
        #endregion

        public HashLinkCopy()
        {
            this.HASH_ALG = SHA1.Create();
            this.LinkGeneration = System.Environment.OSVersion.Version.Major >= 6 ? LinkGeneration.Symbolic : LinkGeneration.Junction;
            this.Start = DateTime.Now;
            this.MaxDisplayLevel = 3;
        }


        public void Run(string sourceDirectory, string targetDir)
        {
            try
            {
                if (string.IsNullOrEmpty(this.HashDir))
                    throw new InvalidOperationException("HashDir can't be empty!");
                if (String.Compare(Path.GetPathRoot(this.HashDir), Path.GetPathRoot(targetDir), true) != 0)
                    throw new InvalidOperationException("Target folder and HashDir must be on same drive!");
                if (Directory.Exists(targetDir))
                    throw new InvalidOperationException(String.Format("Target directory \"{0}\" already exists!", targetDir));
                // check for priveleges
                if (!HasAdministratorPrivileges())
                    throw new System.Security.SecurityException("Access Denied. Administrator permissions are " +
                        "needed to use the selected options. Use an administrator command " +
                        "prompt to complete these tasks.");
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
                // Make sure the parent target directory exists
                var parentDirectory = Path.GetFullPath(Path.Combine(targetDir, ".."));
                Directory.CreateDirectory(parentDirectory);
                // start link generation in first directory level
                var hashEntry = this.BuildHashEntry(new DirectoryInfo(sourceDirectory), 1);
                if (hashEntry != null)
                    this._createLink(targetDir, hashEntry);
                else
                    throw new ApplicationException("Backup failed!");
            }
            finally
            {
                this.End = DateTime.Now;
            }
        }
    }
}