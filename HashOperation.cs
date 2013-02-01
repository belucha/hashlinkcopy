using System;
using System.IO;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.backup
{
    public abstract class HashOperation : BaseOperation
    {

        public HashOperation()
        {
            MaxLevel = 3;
        }

        string _hashDir;

        protected string GetFullHashPath(HashEntry entry, bool tempfolder = false) { return this._hashDir + entry.ToString(tempfolder); }

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

        [Option(Name = "Level", ShortDescription = "Number of directory levels to print")]
        public uint MaxLevel { get; set; }

        protected UInt64 ErrorCount = 0;

        protected virtual void HandleError(object sourceObject, Exception exception)
        {
            this.ErrorCount++;
            Console.WriteLine("\"{1}\": {0}, Message: \"{2}\"", exception.GetType().Name, sourceObject, exception.Message);
        }


        public override void ShowStatistics()
        {
            base.ShowStatistics();
            print("ErrorCount", this.ErrorCount);
        }
    }
}
