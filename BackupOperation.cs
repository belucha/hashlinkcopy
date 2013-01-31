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

        void CopyFolders(string[] sourceFolders)
        {
            if (string.IsNullOrEmpty(this.DestinationDirectory))
                throw new InvalidOperationException("Destination folder is not set!");
            // prepare hash directory
            Console.WriteLine("Preparing hash folder \"{0}\"", this.HashFolder);
            this.PrepareHashDirectory();
            Console.WriteLine("Hash folder preparation completed!");
            // prepare target directory
            Console.WriteLine("Checking target directory: \"{0}\"", this.DestinationDirectory);
            if (Directory.Exists(this.DestinationDirectory))
            {
                var ts = DateTime.Now.ToString(TimeStampFormatString);
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
                    this.HandleError(decodedSourceFolderAndAlias.Item1, new InvalidOperationException("Target folder already exists!"));
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
                    this.HandleError(decodedSourceFolderAndAlias.Item1, new ApplicationException(String.Format("Copy \"{0}\"=>\"{1}\" failed!", decodedSourceFolderAndAlias.Item1.FullName, target)));
            }
        }
        #endregion

        #region private methods

        void CreateLink(string linkName, FileSystemHashEntry entry, int level)
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
            var info = (KeyValuePair<FileSystemInfo, int>)state;
            this.OnAction(HashLinkAction.CopyFileProgress, info.Key, info.Value, String.Format(" ({1:0.0%})", destination, (double)totalBytesTransferred / (double)totalFileSize));
            return CopyFileCallbackAction.Continue;
        }

        void CopyFile(FileSystemInfo info, string dest, int level)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            FileRoutines.CopyFile(info.FullName, dest, CopyFileOptions.AllowDecryptedDestination | CopyFileOptions.Restartable, MyCopyFileCallback,
                new KeyValuePair<FileSystemInfo, int>(info, level));
            File.SetAttributes(dest, FileAttributes.Normal);
            this.OnAction(HashLinkAction.CopyFileDone, info, level);
        }

#if DEBUG
        int sleepTime = 500;
#endif
        bool OnAction(HashLinkAction action, FileSystemInfo info, int level, string extendedInfo = null)
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
            // checks if the entry is in the exclude list            
            if (excludeList.Any(exclude => String.Compare(info.Name, exclude, true) == 0))
            {
                ExcludedCount++;
                Console.WriteLine("{0} (excluded)", info.FullName);
                return true;
            }
            else
            {
                switch (action)
                {
                    case HashLinkAction.EnterSourceDirectory:
                        ProcessedFolders++;
                        break;
                    case HashLinkAction.ProcessSourceFile:
                        ProcessedFiles++;
                        TotalBytes += ((FileInfo)info).Length;
                        break;
                    case HashLinkAction.CopyFileProgress:
                        break;
                    case HashLinkAction.CopyFileDone:
                        CopiedBytes += ((FileInfo)info).Length;
                        CopiedFiles++;
                        break;
                    case HashLinkAction.HashFile:
                        break;
                    case HashLinkAction.HashFileDone:
                        HashedBytes += ((FileInfo)info).Length;
                        HashedFiles++;
                        break;
                    case HashLinkAction.LinkDirectory:
                        FolderLinks++;
                        break;
                    case HashLinkAction.LinkFile:
                        FileLinks++;
                        break;
                }
                if (action == HashLinkAction.EnterSourceDirectory && level <= this.MaxLevel)
                    Console.WriteLine(info.FullName);
            }
            Console.Title = String.Format("[{3}/{4}] {0}: \"{1}\"{2}", action, info.FullName, extendedInfo, FolderLinks + FileLinks, ProcessedFiles + ProcessedFolders);
            return false;
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
                    return OnAction(HashLinkAction.ProcessSourceFile, fileSystemInfo, level) ?
                        null :  // filtered
                        new FileHashEntry((FileInfo)fileSystemInfo, hashAlgorithm,
                            (bytes) => OnAction(HashLinkAction.HashFile, fileSystemInfo, level),
                            (bytes) => OnAction(HashLinkAction.HashFileDone, fileSystemInfo, level));
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
            // source directory
            // RUN
            var sourceList = Parameters;
            if (sourceList.Length == 1 && sourceList[0].StartsWith("@") && File.Exists(sourceList[0].Substring(1)))
                sourceList = File
                    .ReadAllLines(sourceList[0].Substring(1))
                    .Select(line => line.Trim())
                    .Where(line => line.Length > 0 && !line.StartsWith(";"))
                    .ToArray();
            this.StartTime = DateTime.Now;
            CopyFolders(sourceList);
            this.EndTime = DateTime.Now;
            return 0;
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