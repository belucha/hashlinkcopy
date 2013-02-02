using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace de.intronik.backup
{
    [Command("backup", "copy", "cp", Description = "Creates a backup of all source folders", MinParameterCount = 2)]
    public class BackupOperation : HashOperation
    {
        [Option(Name = "DateTimeFormat", ShortDescription = "target directory date time format string", LongDescription = "YYYY...4 digits year")]
        public string TimeStampFormatString { get; set; }

        #region private fields
        string _destinationDirectory;
        string[] excludeList = new string[0];
        #endregion

        #region public methods
        public BackupOperation()
        {
            this.TimeStampFormatString = @"yyyy-MM-dd_HH_mm";
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

        #region private methods


        protected override bool FileSystemFilter(FileSystemInfo fileSystemInfo, int level)
        {
            return excludeList.Any(exclude => String.Compare(fileSystemInfo.FullName, exclude, true) == 0);
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
            var hashEntry = sources.Count == 1 ? this.BuildHashEntry(sources.First()) : this.BuildHashEntry(target.Name, sources);
            if (hashEntry == null)
                throw new ApplicationException(String.Format("Failed to create link \"{0}\"!", target.FullName));
            // make link
            this.CreateLink(target.FullName, hashEntry, 1);
            Console.WriteLine("Linking of \"{0}\"=>\"{1}\" completed", target.FullName, hashEntry);
            return 0;
        }
    }
}