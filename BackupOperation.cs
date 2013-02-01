using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace de.intronik.backup
{
    [Command("backup", Description = "Creates a backup of all source folders", MinParameterCount = 2)]
    [Command("copy", Description = "Creates a backup of all source folders", MinParameterCount = 2)]
    [Command("cp", Description = "Creates a backup of all source folders", MinParameterCount = 2)]
    public class BackupOperation : HashOperation
    {
        const string TimeStampFormatString = @"yyyy-MM-dd_HH_mm";

        enum HashLinkAction
        {
            EnterSourceDirectory,
            ProcessSourceFile,
            CopyFileProgress,
            CopyFileDone,
            HashFile,
            HashFileDone,
            LinkFile,
            LinkDirectory,
        }

        #region private fields
        HashAlgorithm hashAlgorithm;
        string _destinationDirectory;
        long ProcessedFiles;
        long ProcessedFolders;
        long HashedFiles;
        long HashedBytes;
        long FolderLinks;
        long FileLinks;
        long CopiedBytes;
        long CopiedFiles;
        long ExcludedCount;
        long TotalBytes;
        string[] excludeList = new string[0];
        #endregion

        #region public methods
        public BackupOperation()
        {
            this.hashAlgorithm = SHA1.Create();
        }

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
                if (string.IsNullOrEmpty(this.HashFolder))
                    this.HashFolder = HashEntry.GetDefaultHashDir(this._destinationDirectory);
            }
        }
        #endregion

        #region private methods

        void CreateLink(string linkName, HashEntry entry, int level)
        {
            while (true)
            {
                var hc = entry.ToString();
                var target = this.HashFolder + hc;
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

        CopyFileCallbackAction MyCopyFileCallback(string source, string destination, object state, long totalFileSize, long totalBytesTransferred)
        {
            this.OnAction(HashLinkAction.CopyFileProgress, source, (int)state, (long)(10000d * (double)totalBytesTransferred / (double)totalFileSize + 0.5d));
            return CopyFileCallbackAction.Continue;
        }

        void CopyFile(FileSystemInfo info, string dest, int level)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            FileRoutines.CopyFile(info.FullName, dest, CopyFileOptions.AllowDecryptedDestination | CopyFileOptions.Restartable, MyCopyFileCallback, level);
            File.SetAttributes(dest, FileAttributes.Normal);
            this.OnAction(HashLinkAction.CopyFileDone, info.FullName, level, ((FileInfo)info).Length);
        }

#if DEBUG
        int sleepTime = 500;
#endif
        bool OnAction(HashLinkAction action, string path, int level, long progressOrLength = 0)
        {
#if DEBUG
            if (sleepTime != 0)
                System.Threading.Thread.Sleep(sleepTime);
            if (Console.KeyAvailable)
            {
                switch (Char.ToLower(Console.ReadKey(true).KeyChar))
                {
                    case 's':   // toggle sleep
                        sleepTime = sleepTime == 0 ? 500 : 0;
                        break;
                    case '0':
                        sleepTime = 0;
                        break;
                    case '1':
                        sleepTime = 10;
                        break;
                    case '2':
                        sleepTime = 50;
                        break;
                    case '3':
                        sleepTime = 100;
                        break;
                    case '4':
                        sleepTime = 250;
                        break;
                    case '5':
                        sleepTime = 500;
                        break;
                }
            }
#endif
            switch (action)
            {
                case HashLinkAction.EnterSourceDirectory:
                    // checks if the entry is in the exclude list            
                    if (excludeList.Any(exclude => String.Compare(path, exclude, true) == 0))
                    {
                        ExcludedCount++;
                        Console.WriteLine("\"{0}\" (excluded)", path);
                        return true;
                    }
                    if (level <= this.MaxLevel)
                        Console.WriteLine(path);
                    ProcessedFolders++;
                    SetTitle("\"{0}\"", path);
                    break;
                case HashLinkAction.ProcessSourceFile:
                    // checks if the entry is in the exclude list            
                    if (excludeList.Any(exclude => String.Compare(path, exclude, true) == 0))
                    {
                        ExcludedCount++;
                        Console.WriteLine("\"{0}\" (excluded)", path);
                        return true;
                    }
                    ProcessedFiles++;
                    TotalBytes += progressOrLength;
                    SetTitle("\"{0}\"", path);
                    break;
                case HashLinkAction.CopyFileProgress:
                    SetTitle("Copy \"{0}\" ({1:0.0%})", path, progressOrLength * 0.0001d);
                    break;
                case HashLinkAction.CopyFileDone:
                    CopiedBytes += progressOrLength;
                    CopiedFiles++;
                    break;
                case HashLinkAction.HashFile:
                    SetTitle("Hashing \"{0}\" ({1:0.0%})", path, progressOrLength * 0.0001d);
                    break;
                case HashLinkAction.HashFileDone:
                    HashedBytes += progressOrLength;
                    HashedFiles++;
                    break;
                case HashLinkAction.LinkDirectory:
                    FolderLinks++;
                    break;
                case HashLinkAction.LinkFile:
                    FileLinks++;
                    break;
            }
            return false;
        }

        void SetTitle(string format, params object[] args)
        {
            Console.Title = String.Format("[{1}/{2}]: {0}", String.Format(format, args), FolderLinks + FileLinks, ProcessedFiles + ProcessedFolders);
        }

        HashEntry BuildVirtualDirectoryHashEntry(Dictionary<string, FileSystemInfo> sourceItems, int level)
        {
            // required to throw error on correct entry
            object currentEntry = sourceItems;
            try
            {
                var m = new MemoryStream();
                var w = new BinaryWriter(m);
                var directoryEntries = new List<Tuple<FileSystemHashEntry, string>>(sourceItems.Count);
                foreach (var kvp in sourceItems)
                {
                    // start link generation in first directory level
                    currentEntry = kvp.Value;
                    var target = kvp.Key;
                    var subEntry = this.BuildFileSytemHashEntry(kvp.Value, level + 1);
                    if (subEntry != null)
                    {
                        w.Write(subEntry.Info.Attributes.HasFlag(FileAttributes.Directory));
                        w.Write(subEntry.Hash);
                        w.Write(target);
                        directoryEntries.Add(new Tuple<FileSystemHashEntry, string>(subEntry, target));
                    }
                    else
                        this.HandleError(currentEntry, new ApplicationException(String.Format("Copy \"{0}\"=>\"{1}\" failed!", kvp.Value.FullName, target)));
                }
                var directoryEntry = new VirtualDirectoryHashEntry(sourceItems, hashAlgorithm.ComputeHash(m.ToArray()));
                currentEntry = null;
                var targetHashDir = GetFullHashPath(directoryEntry, false);
                if (Directory.Exists(targetHashDir))
                    return directoryEntry;
                // create link structure in temp directory first                    
                var tmpDirName = GetFullHashPath(directoryEntry, true) + "\\";
                Directory.CreateDirectory(tmpDirName);
                foreach (var subEntry in directoryEntries)
                {
                    if (OnAction(subEntry.Item1.IsDirectory ? HashLinkAction.LinkDirectory : HashLinkAction.LinkFile, subEntry.Item1.Info.FullName, level))
                        return null;
                    this.CreateLink(tmpDirName + subEntry.Item2, subEntry.Item1, level);
                }
                // we are done, rename directory
                Directory.Move(tmpDirName, targetHashDir);
                return directoryEntry;
            }
            catch (IOException e)
            {
                this.HandleError(currentEntry, e);
                return null;
            }
            catch (Win32Exception e)
            {
                this.HandleError(currentEntry, e);
                return null;
            }
        }


        FileSystemHashEntry BuildFileSytemHashEntry(FileSystemInfo fileSystemInfo, int level)
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
                    if (OnAction(HashLinkAction.EnterSourceDirectory, fileSystemInfo.FullName, level))
                        return null;
                    var sourceDirectory = (DirectoryInfo)fileSystemInfo;
                    var m = new MemoryStream();
                    var w = new BinaryWriter(m);
                    var directoryEntries = new List<FileSystemHashEntry>(500);
                    foreach (var entry in sourceDirectory.EnumerateFileSystemInfos())
                    {
                        currentEntry = entry;
                        var subEntry = this.BuildFileSytemHashEntry(entry, level + 1);
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
                        if (OnAction(subEntry.IsDirectory ? HashLinkAction.LinkDirectory : HashLinkAction.LinkFile, subEntry.Info.FullName, level))
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
                    return OnAction(HashLinkAction.ProcessSourceFile, fileSystemInfo.FullName, level, ((FileInfo)fileSystemInfo).Length) ?
                        null :  // filtered
                        new FileHashEntry((FileInfo)fileSystemInfo, hashAlgorithm,
                            (bytes) => OnAction(HashLinkAction.HashFile, fileSystemInfo.FullName, level, ((FileInfo)fileSystemInfo).Length),
                            (bytes) => OnAction(HashLinkAction.HashFileDone, fileSystemInfo.FullName, level, ((FileInfo)fileSystemInfo).Length));
                }
            }
            catch (IOException e)
            {
                this.HandleError(currentEntry, e);
                return null;
            }
            catch (Win32Exception e)
            {
                this.HandleError(currentEntry, e);
                return null;
            }
        }
        #endregion


        protected override void OnParametersChanged()
        {
            base.OnParametersChanged();
            // destination folder
            this.DestinationDirectory = Parameters.Last().Replace("*", DateTime.Now.ToString(TimeStampFormatString));
            this.Parameters = Parameters.Take(Parameters.Length - 1).ToArray();
        }

        protected override int DoOperation()
        {
            // check destination directory
            if (string.IsNullOrEmpty(this.DestinationDirectory))
                throw new InvalidOperationException("Destination folder is not set!");

            // process source list
            var inputList = new List<string>();
            foreach (var source in this.Parameters)
            {
                if (source.StartsWith("@") && File.Exists(source.Substring(1)))
                    inputList.AddRange(File.ReadAllLines(source.Substring(1))
                                        .Select(line => line.Trim())
                                        .Where(line => line.Length > 0 && !line.StartsWith(";"))
                                        .ToArray());
                else
                    inputList.Add(source);
            }
            // create dictionary with new names and folder sources
            var sources = new Dictionary<string, FileSystemInfo>(inputList.Count, StringComparer.InvariantCultureIgnoreCase);
            foreach (var sourceString in inputList)
            {
                var splitted = sourceString.Split(new char[] { '=', }, 2);
                var newSourceString = splitted[0];
                var alias = splitted.Length == 2 ? splitted[1] : "";
                var info = Directory.Exists(newSourceString) ? (FileSystemInfo)new DirectoryInfo(newSourceString) : (FileSystemInfo)new FileInfo(newSourceString);
                if (!info.Exists)
                {
                    HandleError(info, new FileNotFoundException("The source folder or file does not exist!"));
                    continue;
                }
                if (String.IsNullOrEmpty(alias))
                    alias = info.Name;
                if (sources.ContainsKey(alias))
                    throw new ArgumentException(String.Format("Error adding \"{0}\" as \"{1}\". Duplicate target name or alias!", info, alias));
                sources.Add(alias, info);
            }

            if (sources.Count == 0)
                throw new InvalidOperationException("At least one existing source is required!");

            // prepare hash directory
            Console.WriteLine("Preparing hash folder \"{0}\"", this.HashFolder);
            this.PrepareHashDirectory();
            Console.WriteLine("Hash folder preparation completed!");


            // prepare target directory
            var target = new DirectoryInfo(this.DestinationDirectory);
            Console.WriteLine("Checking target directory: \"{0}\"", target.FullName);
            if (target.Exists)
            {
                var ts = DateTime.Now.ToString(TimeStampFormatString);
                Console.WriteLine("Warning target directory \"{0}\" already exists! Trying to append current time stamp ({1})!", target.FullName, ts);
                target = new DirectoryInfo(Path.Combine(target.FullName, ts));
                if (target.Exists)
                    throw new InvalidOperationException(String.Format("New target directory \"{0}\" also already exists!", target.FullName));
            }
            // make sure the target parent folder exists
            target.Parent.Create();
            var hashEntry = sources.Count == 1 ? this.BuildFileSytemHashEntry(sources.First().Value, 1) : this.BuildVirtualDirectoryHashEntry(sources, 1);
            if (hashEntry != null)
            {
                if (!OnAction(HashLinkAction.LinkDirectory, target.FullName, 1))
                    this.CreateLink(target.FullName, hashEntry, 1);
                Console.WriteLine("Linking of \"{0}\"=>\"{1}\" completed", target.FullName, hashEntry);
                return 0;
            }
            else
            {
                this.HandleError(sources, new ApplicationException(String.Format("Failed to create link \"{0}\"!", target.FullName)));
                return -1;
            }
        }

        public override void ShowStatistics()
        {
            base.ShowStatistics();
            print("Processed Files", ProcessedFiles);
            print("Processed Folders", ProcessedFolders);
            var processedObjects = ProcessedFiles + ProcessedFolders;
            print("Processed Objects", processedObjects);
            print("Processed Bytes", FormatBytes(TotalBytes));
            print("Linked Files", FileLinks);
            print("Linked Folders", FolderLinks);
            var linkedObjects = FileLinks + FolderLinks;
            print("Linked Objects", linkedObjects);
            print("Copied Bytes", FormatBytes(CopiedBytes));
            print("Copied Files", CopiedFiles);
            print("Hashed Bytes", FormatBytes(HashedBytes));
            print("Hashed Files", HashedFiles);
            print("Execluded Files", ExcludedCount);
            print("%Space saved", (1d - (double)CopiedBytes / (double)TotalBytes).ToString("0.0%"));
            print("%Operations saved", (1d - (double)linkedObjects / (double)processedObjects).ToString("0.0%"));
            print("Errors", ErrorCount);
        }
    }
}