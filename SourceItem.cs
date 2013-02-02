using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.backup
{
    public class SourceItem
    {
        public SourceItem(string sourceString)
        {
            var splitted = sourceString.Split(new char[] { '=', }, 2);
            var newSourceString = splitted[0];
            this.FileSystemInfo = Directory.Exists(newSourceString) ? (FileSystemInfo)new DirectoryInfo(newSourceString) : (FileSystemInfo)new FileInfo(newSourceString);
            this.Name = splitted.Length == 2 ? splitted[1] : this.FileSystemInfo.Name;
        }
        public SourceItem(FileSystemInfo fileSystemInfo, string name = null)
        {
            this.FileSystemInfo = fileSystemInfo;
            this.Name = String.IsNullOrEmpty(name) ? this.FileSystemInfo.Name : name;
        }
        public string Name { get; private set; }
        public FileSystemInfo FileSystemInfo { get; private set; }

        public override string ToString()
        {
            return String.Format("{0}={1}", this.FileSystemInfo.FullName, Name);
        }
    }

}