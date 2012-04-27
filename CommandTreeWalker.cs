using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace de.intronik.hashlinkcopy
{
    [Option("Exclude", Help = @"Exclude rules", Description = @"either an inline list separated by |, or a filename prceeded by @
examples:
--Exclude:""*.exe|bin\|*.bak""      excludes all executables and *.bak and the bin folders
--Exclude:@excludeFiles.txt         excludes all patterns listed in each line of excludeFiles.txt
")]
    [Option(@"HashCacheLimit", Help = @"Enables the SHA1 caching for files larger than the given size", Default = "4kB")]
    [Option("HashDir", Help = "location of the hashes", Default = @"..\Hash\")]
    abstract class CommandTreeWalker : CommandBase
    {
        public string Folder { get; protected set; }
        public ExcludeList ExcludeList { get; private set; }
        public string HashDir { get; protected set; }

        public CommandTreeWalker()
        {
            this.ExcludeList = new ExcludeList();
        }

        public override void Init(string[] parameters)
        {
            base.Init(parameters);
            if (parameters.Length < 1)
                throw new ArgumentOutOfRangeException("Directory parameter is missing!");
            this.Folder = Path.GetFullPath(parameters[0]);
            this.HashDir = null;
        }

        protected override void ProcessOption(OptionAttribute option)
        {
            base.ProcessOption(option);
            if (option.Name == "HashDir") this.HashDir = option.ParseAsString();
            else if (option.Name == "Exclude") this.ExcludeList = new ExcludeList(option.Value);
            else if (option.Name == "HashCacheLimit")
            {
                if (String.Compare(option.Value, "disabled", true) == 0)
                    HashInfo.CacheLimit = long.MaxValue;
                else
                {
                    var m = Regex.Match(option.Value, @"^(?<size>\d+)\s*((?<unit>k|m|g)(b|byte)?)?\s*$",
                        RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline);
                    var u = m.Groups["unit"].Value;
                    long f = 1;
                    if (!String.IsNullOrEmpty(u))
                    {
                        if (u.ToLower() == "k") f = 1024;
                        else if (u.ToLower() == "m") f = 1 << 20;
                        else if (u.ToLower() == "g") f = 1 << 30;
                    }
                    HashInfo.CacheLimit = long.Parse(m.Groups["size"].Value) * f;
                }
            }
        }

        /// <summary>
        /// callled when a directory is entered
        /// </summary>
        /// <param name="path"></param>
        /// <returns>true to cancel processing</returns>
        protected virtual bool CancelEnterDirectory(string path, int level)
        {
            return false;
        }

        /// <summary>
        /// Called, when processing a directory is completed
        /// </summary>
        /// <param name="path"></param>
        /// <param name="level"></param>
        protected virtual void LeaveDirectory(string path, int level)
        {
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="level"></param>
        protected abstract void ProcessFile(string path, int level);

        /// <summary>
        /// Returns the coresponding target path for a given source path
        /// e.g. sourcePath : C:\Projekte\Hallo\Me.txt
        ///      Folder     : C:\Projekte
        ///      newBasePath: E:\Backup\2012-04-19
        /// yields: E:\Backup\2012-04-19\Hallo\Me.txt
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="newBasePath"></param>
        /// <returns></returns>
        protected string RebasePath(string sourcePath, string newBasePath)
        {
            var subPath = sourcePath.Substring(this.Folder.Length).TrimStart('\\');
            return Path.Combine(newBasePath, subPath);
        }

        protected void Process(string path, int level)
        {
            try
            {
                if (this.ExcludeList.Exclude(path.EndsWith(@"\") ? path : (path + @"\")))
                {
                    Monitor.SkipDirectory(path, "exclude list match");
                    return;
                }
                if (this.CancelEnterDirectory(path, level))
                {
                    Monitor.SkipDirectory(path, String.Format("level {0}", level));
                    return;
                }
                Monitor.ProcessDirectory(path);
                //
                // PROCESS FILES
                //
                foreach (var filename in Directory.GetFiles(path))
                    try
                    {
                        if (this.ExcludeList.Exclude(filename))
                        {
                            Monitor.SkipFile(filename, "exclude list match");
                            continue;
                        }
                        Monitor.ProcessFile(filename);
                        this.ProcessFile(filename, level);
                    }
                    catch (Exception error)
                    {
                        Monitor.Error(filename, error);
                    }
                //
                // PROCESS SUB DIRS
                //
                foreach (var subDir in Directory.GetDirectories(path))
                    this.Process(subDir, level + 1);

                //
                // LEAVE DIR
                //
                this.LeaveDirectory(path, level);
            }
            catch (Exception error)
            {
                Monitor.Error(path, error);
            }
        }

        public override void Run()
        {
            Process(this.Folder, 0);
        }

    }
}
