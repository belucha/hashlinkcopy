using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.backup
{
    public class HashLinkActionEventArgs : EventArgs
    {
        public FileSystemInfo Info { get; private set; }
        public int Level { get; private set; }
        public HashLinkAction Action { get; private set; }
        public bool Cancel { get; private set; }
        public HashLinkActionEventArgs(FileSystemInfo info, int level, HashLinkAction action)
        {
            this.Info = info;
            this.Level = level;
            this.Action = action;
        }
        public override string ToString()
        {
            return String.Format("{0}: \"{1}\"", this.Action, this.Info.FullName);
        }
    }
    public enum HashLinkAction
    {
        EnterSourceDirectory,
        ProcessSourceFile,
        CopyFile,
        LinkFile,
        LinkDirectory,
    }
}

