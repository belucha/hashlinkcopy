using System;
using System.IO;
using System.Diagnostics;
using System.ComponentModel;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Linq;
using System.Text;

namespace de.intronik.backup
{
    public abstract class HashOperation : ICommand
    {
        protected const string DefaultHashDir = @"Hash";
        protected int debugSleepTime = 500;
        protected string[] parameters;
        protected bool setParams;
        protected string _hashDir;
        protected DateTime StartTime;
        protected DateTime EndTime;
        protected TimeSpan Duration { get { return this.EndTime.Subtract(this.StartTime); } }
        protected long ErrorCount;
        protected long ProcessedFiles;
        protected long ProcessedFolders;
        protected long ProcessedObjects { get { return this.ProcessedFiles + this.ProcessedFolders; } }
        protected long HashedFiles;
        protected long HashedBytes;
        protected long FolderLinks;
        protected long FileLinks;
        protected long LinkedObjects { get { return this.FileLinks + this.FolderLinks; } }
        protected long CopiedBytes;
        protected long CopiedFiles;
        protected long ExcludedCount;
        protected long TotalBytes;


        public HashOperation()
        {
            MaxLevel = 3;
        }

        [Option(Name = "Level", ShortDescription = "Number of directory levels to print")]
        public uint MaxLevel { get; set; }


        [Option(ShortDescription = "HashFolder", ValueText = "FolderName", LongDescription = "Location of the hash folder", DefaultValue = "\"\\Hash\"\\ on the target drive")]
        public string HashFolder
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

        protected void print(string name, object value)
        {
            Console.WriteLine("{0,-20}: {1}", name, value);
        }

        protected string PromptInput(string prompt, params object[] args)
        {
            return PromptInput(null, prompt, args);
        }

        protected string PromptInput(Func<string, bool> inputOkPredicate, string prompt, params object[] args)
        {
            while (true)
            {
                Console.Write(String.Format(prompt, args));
                var answer = Console.ReadLine().Trim();
                if (inputOkPredicate == null || inputOkPredicate(answer))
                    return answer;
            }
        }

        protected virtual void ShowStatistics()
        {
            print("Start", this.StartTime);
            print("End", this.EndTime);
            print("Duration", this.EndTime.Subtract(this.StartTime));
            print("Processed Files", ProcessedFiles);
            print("Processed Folders", ProcessedFolders);
            print("Processed Objects", ProcessedObjects);
            print("Processed Bytes", FormatBytes(TotalBytes));
            print("Linked Files", FileLinks);
            print("Linked Folders", FolderLinks);
            print("Linked Objects", LinkedObjects);
            print("Copied Bytes", FormatBytes(CopiedBytes));
            print("Copied Files", CopiedFiles);
            print("Hashed Bytes", FormatBytes(HashedBytes));
            print("Hashed Files", HashedFiles);
            print("Execluded Files", ExcludedCount);
            print("%Space saved", (1d - (double)CopiedBytes / (double)TotalBytes).ToString("0.0%"));
            print("%Operations saved", (1d - (double)LinkedObjects / (double)ProcessedObjects).ToString("0.0%"));
            print("Errors", ErrorCount);
        }

        protected void EnterSourceDirectory(DirectoryInfo directory, int level)
        {
            this.ProcessedFolders++;
            if (level <= this.MaxLevel)
                Console.WriteLine(directory.FullName);
            SetTitle(directory.FullName);
        }

        protected virtual void ProcessHashAction(FileHashEntry.HashAction action, FileInfo file, long processedBytes)
        {
            switch (action)
            {
                case FileHashEntry.HashAction.CalcStart:
                    break;
                case FileHashEntry.HashAction.CalcEnd:
                    this.HashedFiles++;
                    this.HashedBytes += file.Length;
                    break;
            }
        }

        protected void HandleError(FileSystemInfo source, Exception exception)
        {
            this.ErrorCount++;
            Console.WriteLine("\"{1}\": {0}, Message: \"{2}\"", exception.GetType().Name, source, exception.Message);
        }

        [Conditional("DEBUG")]
        private void DebugDelay()
        {
            if (debugSleepTime != 0)
                System.Threading.Thread.Sleep(debugSleepTime);
            if (Console.KeyAvailable)
            {
                switch (Char.ToLower(Console.ReadKey(true).KeyChar))
                {
                    case 's':   // toggle sleep
                        debugSleepTime = debugSleepTime == 0 ? 500 : 0;
                        break;
                    case '0':
                        debugSleepTime = 0;
                        break;
                    case '1':
                        debugSleepTime = 10;
                        break;
                    case '2':
                        debugSleepTime = 50;
                        break;
                    case '3':
                        debugSleepTime = 100;
                        break;
                    case '4':
                        debugSleepTime = 250;
                        break;
                    case '5':
                        debugSleepTime = 500;
                        break;
                }
            }
        }

        protected void ProcessSourceFile(FileInfo file, int level)
        {
            this.DebugDelay();
            this.ProcessedFiles++;
            this.TotalBytes += file.Length;
        }

        protected void CopiedSourceFile(FileHashEntry file, int level)
        {
            this.CopiedBytes += file.Length;
            this.CopiedFiles++;
        }

        protected void Excluded(FileSystemInfo info, int level)
        {
            ExcludedCount++;
            if (level <= this.MaxLevel)
                Console.WriteLine("\"{0}\" (excluded)", info.FullName);
        }

        protected void SetTitle(string format, params object[] args)
        {
            Console.Title = String.Format("[{1}/{2}]: {0}", String.Format(format, args), LinkedObjects, ProcessedObjects);
        }

        protected static string FormatBytes(long bytes)
        {
            var units = new string[] { "Byte", "Kb", "Mb", "Gb", };
            if (bytes < 1024)
                return String.Format("{0}{1}", bytes, units[0]);
            var b = new StringBuilder();
            for (var p = units.Length - 1; p >= 0; p--)
            {
                var c = (bytes >> (p * 10)) & 1023;
                if (c > 0)
                    b.AppendFormat("{0}{1} ", c, units[p]);
            }
            return b.ToString();
        }

        public string[] Parameters
        {
            get { return this.parameters; }
            set
            {
                this.parameters = value;
                if (setParams) return;
                try
                {
                    setParams = true;
                    this.OnParametersChanged();
                }
                finally
                {
                    setParams = false;
                }
            }
        }

        protected virtual void OnParametersChanged()
        {
        }

        public int Run()
        {
            this.StartTime = DateTime.Now;
            var res = this.DoOperation();
            this.EndTime = DateTime.Now;
            this.ShowStatistics();
            return res;
        }

        protected abstract int DoOperation();

        protected void PrepareHashDirectory()
        {
            // create new hash directory, with default attributes
            if (!Directory.Exists(this.HashFolder))
            {
                var di = Directory.CreateDirectory(this.HashFolder);
                di.Attributes = di.Attributes | FileAttributes.Hidden | FileAttributes.Compressed | FileAttributes.NotContentIndexed;
            }
            // make sure all hash directories exist!
            for (var i = 0; i < (1 << 12); i++)
            {
                Directory.CreateDirectory(Path.Combine(this.HashFolder, "f", i.ToString("X3")));
                Directory.CreateDirectory(Path.Combine(this.HashFolder, "d", i.ToString("X3")));
            }
            // delete temp directory content
            var tempDir = Path.Combine(this.HashFolder, "t");
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
            // recreate temp directory
            Directory.CreateDirectory(Path.Combine(this.HashFolder, "t"));
        }

        protected static string GetDefaultHashDir(string destinationPath)
        {
            return Path.Combine(Path.GetPathRoot(Path.GetFullPath(destinationPath)), DefaultHashDir) + Path.DirectorySeparatorChar.ToString();
        }

        protected string GetFullHashPath(HashEntry entry, bool tempfolder = false) { return this._hashDir + entry.ToString(tempfolder); }

        CopyFileCallbackAction MyCopyFileCallback(string source, string destination, object state, long totalFileSize, long totalBytesTransferred)
        {
            SetTitle("Copy \"{0}\" ({1:0.0%})", source, (double)totalBytesTransferred / (double)totalFileSize);
            return CopyFileCallbackAction.Continue;
        }

        protected virtual FileHashEntry ProvideHash(FileHashEntry missingHash, int level)
        {
            var targetName = GetFullHashPath(missingHash);
            if (!File.Exists(targetName))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(targetName));
                FileRoutines.CopyFile(missingHash.FullName, targetName, CopyFileOptions.AllowDecryptedDestination | CopyFileOptions.Restartable, MyCopyFileCallback, level);
                File.SetAttributes(targetName, FileAttributes.Normal);
                this.CopiedSourceFile(missingHash, level);
            }
            return missingHash;
        }

        protected virtual HashEntry ProvideHash(DirectoryHashEntry missingHash, int level)
        {
            var targetName = GetFullHashPath(missingHash, false);
            if (!Directory.Exists(targetName))
            {
                // create link structure in temp directory first                    
                var tmpDirName = GetFullHashPath(missingHash, true) + "\\";
                Directory.CreateDirectory(tmpDirName);
                foreach (var kvp in missingHash.Entries)
                    this.CreateLink(tmpDirName + kvp.Key, kvp.Value, level + 1);
                // we are done, rename directory
                Directory.Move(tmpDirName, targetName);
            }
            return missingHash;
        }

        protected void CreateLink(string linkName, HashEntry entry, int level)
        {
            while (true)
            {
                var hashTargetPath = GetFullHashPath(entry);
                // remove target drive
                hashTargetPath = (hashTargetPath.Length > 2 && hashTargetPath[1] == ':') ? hashTargetPath.Substring(2) : hashTargetPath;
                // create link and set error variable
                var error = Win32.CreateSymbolicLink(linkName, hashTargetPath, entry.IsDirectory ? Win32.SymbolicLinkFlags.Directory : Win32.SymbolicLinkFlags.File) ? 0 : System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                switch (error)
                {
                    case 0:     // ERROR_SUCCESS
                        if (entry.IsDirectory)
                            this.FolderLinks++;
                        else
                            this.FileLinks++;
                        return;
                    case 80:    // ERROR_FILE_EXISTS
                    case 183:   // ERROR_ALREADY_EXISTS
                        throw new InvalidOperationException(String.Format("Error creating symbolic link \"{0}\"=>\"{1}\". Directory already exists!", linkName, hashTargetPath));
                    case 1314:  // ERROR_PRIVILEGE_NOT_HELD
                        throw new System.Security.SecurityException("Not enough priveleges to create symbolic links!", new System.ComponentModel.Win32Exception(error));
                    case 2:     // ERROR_FILE_NOT_FOUND
                    case 3:     // ERROR_PATH_NOT_FOUND
                    default:
                        throw new System.ComponentModel.Win32Exception(error, linkName);
                }
            }
        }

        protected virtual bool FileSystemFilter(FileSystemInfo fileSystemInfo, int level)
        {
            return false;
        }

        protected HashEntry BuildHashEntry(SourceItem sourceItem, int level = 0)
        {
            // check if the entry should be filtered
            if (FileSystemFilter(sourceItem.FileSystemInfo, level))
            {
                Excluded(sourceItem.FileSystemInfo, level);
                return null;
            }
            try
            {
                if ((sourceItem.FileSystemInfo.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    // it's a folder
                    var dirInfo = sourceItem.FileSystemInfo as DirectoryInfo;
                    EnterSourceDirectory(dirInfo, level);
                    return BuildHashEntry(sourceItem.Name, dirInfo.GetFileSystemInfos().Select(f => new SourceItem(f)), level + 1);
                }
                else
                {
                    // is a file
                    var fileInfo = sourceItem.FileSystemInfo as FileInfo;
                    ProcessSourceFile(fileInfo, level);
                    return new PathHashEntry(this.ProvideHash(new FileHashEntry(fileInfo, ProcessHashAction), level));
                }
            }
            catch (IOException e)
            {
                this.HandleError(sourceItem.FileSystemInfo, e);
                return null;
            }
            catch (Win32Exception e)
            {
                this.HandleError(sourceItem.FileSystemInfo, e);
                return null;
            }
        }

        protected HashEntry BuildHashEntry(string name, IEnumerable<SourceItem> sourceItems, int level = 0)
        {
            // required to throw error on correct entry
            FileSystemInfo currentEntry = null;
            try
            {
                var directoryEntry = new DirectoryHashEntry(name, 100);
                foreach (var sourceItem in sourceItems)
                {
                    // start link generation in first directory level
                    currentEntry = sourceItem.FileSystemInfo;
                    // build hash for that folder
                    var subEntry = BuildHashEntry(sourceItem, level + 1);
                    // check if the sub entry is invalid or has been filtered!
                    if (subEntry == null) continue;
                    // store hash and sub entry
                    directoryEntry.Entries.Add(sourceItem.Name, subEntry);
                }
                return new PathHashEntry(this.ProvideHash(directoryEntry, level + 1));
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
    }
}
