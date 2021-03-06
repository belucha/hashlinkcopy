﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;

namespace de.intronik.backup
{
    [Command("backup", "copy", "cp", Description = "Creates a backup of all source folders", MinParameterCount = 2)]
    public class CommandBackup : CommandBase
    {
        #region private fields
        string _destinationDirectory;
        long copiedBytes;
        long copiedFiles;
        long excludedCount;
        string[] excludeList = new string[0];
        #endregion

        #region public options
        [Option(Name = "DateTimeFormat", ShortDescription = "target directory date time format string", LongDescription = "YYYY...4 digits year")]
        public string TimeStampFormatString { get; set; }

        [Option(Name = "ExcludeList", ShortDescription = "name of file with list of excludes")]
        public string ExcludeListFileName { get; set; }

        [Option(Name = "pcf", ShortDescription = "print copied file names", LongDescription = "If set all files that will copied will be printed on console", EmptyValue = "true")]
        public bool PrintCopiedFilenames { get; set; }
        #endregion

        #region public methods
        public CommandBackup()
        {
            this.TimeStampFormatString = @"yyyy-MM-dd_HH-mm";
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
                    this.HashFolder = GetDefaultHashDir(this._destinationDirectory);
            }
        }
        #endregion

        #region protected methods
        protected override void SetParameters(string[] value)
        {
            // destination folder
            this.DestinationDirectory = value.Last().Replace("*", DateTime.Now.ToString(TimeStampFormatString));
            base.SetParameters(value.Take(value.Length - 1).ToArray());
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
            var sources = inputList
                .Select(s => new SourceItem(s))
                .Where(info =>
                {
                    if (!info.FileSystemInfo.Exists)
                    {
                        HandleError(info.FileSystemInfo, new FileNotFoundException("The source folder or file does not exist!"));
                        return false;
                    }
                    else
                        return true;
                }).ToList();

            // load exclude list from file
            var eclList = new List<string>();
            if (File.Exists(ExcludeListFileName))
                eclList.AddRange(File.ReadAllLines(ExcludeListFileName)
                                        .Select(line => line.Trim())
                                        .Where(line => line.Length > 0 && !line.StartsWith(";"))
                                        .ToArray());
            excludeList = eclList.ToArray();

            // check for duplicates
            foreach (var group in sources.GroupBy(s => s.Name, StringComparer.InvariantCultureIgnoreCase))
            {
                var items = group.ToArray();
                if (items.Length > 1)
                    throw new ArgumentException(String.Format("The following directories have all the same name \"{1}\" in the target folder:\n{0}",
                        String.Join("\n", items.Select(i => i.FileSystemInfo.FullName)), group.Key));
            }

            // check if we have at least one item
            if (sources.Count == 0)
                throw new InvalidOperationException("At least one existing source is required!");

            // prepare hash directory
            this.PrepareHashDirectory();

            // prepare target directory
            var target = new DirectoryInfo(this.DestinationDirectory);
            //Console.WriteLine("checking target directory: \"{0}\"", target.FullName);
            if (target.Exists)
            {
                // TARGET has to be a FOLDER and can NOT be a LINK or FILE
                var path = new DirectoryInfo(this.DestinationDirectory);
                while (path != null) {
                    if ((path.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint) {
                        Console.WriteLine("error: target path \"{0}\" contains a ReparsePoint!", target.FullName);
                        throw new InvalidOperationException(String.Format("target path \"{0}\" contains a ReparsePoint!", target.FullName));
                    }
                    if ((path.Attributes & FileAttributes.Directory) != FileAttributes.Directory) {
                        Console.WriteLine("error: target \"{0}\" allready exists but is not a Directory!", target.FullName);
                        throw new InvalidOperationException(String.Format("target \"{0}\" exists but is not a Directory!", target.FullName));
                    }
                    path = Directory.GetParent(path.ToString());
                }
                var ts = DateTime.Now.ToString(TimeStampFormatString);
                Console.WriteLine("target directory \"{0}\" already exists! appending current time stamp ({1})!", target.FullName, ts);
                target = new DirectoryInfo(Path.Combine(target.FullName, ts));
                if (target.Exists) {
                    Console.WriteLine("error: target directory \"{0}\" allready exists but is not a Directory!", target.FullName);
                    throw new InvalidOperationException(String.Format("New target directory \"{0}\" also already exists!", target.FullName));
                }
            } else {
                var path = new DirectoryInfo(this.DestinationDirectory);
                path = Directory.GetParent(path.ToString());
                while (path != null) {
                    if ((path.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint) {
                        Console.WriteLine("error: target path \"{0}\" contains a ReparsePoint!", target.FullName);
                        throw new InvalidOperationException(String.Format("target path \"{0}\" contains a ReparsePoint!", target.FullName));
                    }
                    path = Directory.GetParent(path.ToString());
                }
                Console.WriteLine("using directory \"{0}\" as target", target.FullName);
            }
            // make sure the target parent folder exists
            target.Parent.Create();
            var hashEntry = sources.Count == 1 ? this.BuildHashEntry(sources.First()) : this.BuildHashEntry(target.Name, sources);
            if (hashEntry == null)
                throw new ApplicationException(String.Format("Failed to create link \"{0}\"!", target.FullName));
            // make link
            this.CreateLink(target.FullName, hashEntry, 1);
            Console.WriteLine("Linking of \"{0}\"=>\"{1}\" completed", target.FullName, hashEntry);
            return 0;
        }

        protected override void ShowStatistics()
        {
            base.ShowStatistics();
            print("Copied Bytes", FormatBytes(copiedBytes));
            print("Copied Files", copiedFiles);
            print("Execluded Files", excludedCount);
            print("%Space saved", (1d - (double)copiedBytes / (double)TotalBytes).ToString("0.0%"));
        }
        #endregion

        #region private methods
        void Excluded(FileSystemInfo info, int level)
        {
            excludedCount++;
            if (level <= this.MaxLevel)
                Console.WriteLine("\"{0}\" (excluded)", info.FullName);
        }

        bool FileSystemFilter(FileSystemInfo fileSystemInfo, int level)
        {
            //return excludeList.Any(exclude => String.Compare(fileSystemInfo.FullName, exclude, true) == 0);
            return excludeList.Any(exclude => fileSystemInfo.FullName.ToLower().EndsWith(exclude.ToLower()));
        }

        CopyFileCallbackAction MyCopyFileCallback(string source, string destination, object state, long totalFileSize, long totalBytesTransferred)
        {
            SetTitle("Copy \"{0}\" ({1:0.0%})", source, (double)totalBytesTransferred / (double)totalFileSize);
            return CopyFileCallbackAction.Continue;
        }

        FileHashEntry ProvideHash(FileHashEntry missingHash, int level)
        {
            var targetName = GetFullHashPath(missingHash);
            if (!File.Exists(targetName))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(targetName));
                FileRoutines.CopyFile(missingHash.FullName, targetName, CopyFileOptions.AllowDecryptedDestination | CopyFileOptions.Restartable, MyCopyFileCallback, level);
                this.copiedBytes += missingHash.Length;
                this.copiedFiles++;
                if (PrintCopiedFilenames)
                    Console.WriteLine("Copied file #{2}: \"{0}\"=>{1}", missingHash.FullName, missingHash, copiedFiles);
                File.SetAttributes(targetName, FileAttributes.Normal);
            }
            return missingHash;
        }

        HashEntry ProvideHash(DirectoryHashEntry missingHash, int level)
        {
            var targetName = GetFullHashPath(missingHash, false);
            if (!Directory.Exists(targetName))
            {
                // create link structure in temp directory first                    
                var tmpDirName = GetFullHashPath(missingHash, true) + "\\";
                if (Directory.Exists(tmpDirName))
                    Directory.Delete(tmpDirName, true);
                Directory.CreateDirectory(tmpDirName);
                foreach (var kvp in missingHash.Entries)
                    this.CreateLink(tmpDirName + kvp.Key, kvp.Value, level + 1);
                // we are done, rename directory
                FileRoutines.MoveDirectory(tmpDirName, targetName);
            }
            return missingHash;
        }

        HashEntry BuildHashEntry(SourceItem sourceItem, int level = 0)
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
            catch (ArgumentException e) {
                // Illegales Zeichen im Pfad.
                this.HandleError(sourceItem.FileSystemInfo, e);
                Console.WriteLine("{0} excluded because it contains inaccessable data", sourceItem.FileSystemInfo.FullName);
                return null;
            }
            catch (IOException e) {
                this.HandleError(sourceItem.FileSystemInfo, e);
                return null;
            }
            catch (Win32Exception e) {
                this.HandleError(sourceItem.FileSystemInfo, e);
                return null;
            }
            catch (UnauthorizedAccessException e) {
                this.HandleError(sourceItem.FileSystemInfo, e);
                return null;
            }
            catch (Exception e) {
                // print some details, on source of error
                this.HandleError(sourceItem.FileSystemInfo, e);
                throw e;
            }
        }

        HashEntry BuildHashEntry(string name, IEnumerable<SourceItem> sourceItems, int level = 0)
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
        #endregion

    }
}