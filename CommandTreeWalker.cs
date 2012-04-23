using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace de.intronik.hashlinkcopy
{
    [Option("HashDir", Help = "location of the hashes", Description = @"defaults to targetpath\..\Hash\")]
    [Option("Exclude", Help = @"Exclude rules", Description = @"either an inline list separated by |, or a filename prceeded by @
examples:
--Exclude:""*.exe|bin\|*.bak""      excludes all executables and *.bak and the bin folders
--Exclude:@excludeFiles.txt         excludes all patterns listed in each line of excludeFiles.txt
")]
    [Option(@"HashCacheLimit", Help = @"Enables the SHA1 caching for files larger than the given size", Default = "4MB")]
    abstract class CommandTreeWalker : CommandBase
    {
        public string Folder { get; protected set; }
        public ExcludeList ExcludeList { get; private set; }
        public string HashDir { get; private set; }

        public CommandTreeWalker(IEnumerable<string> arguments, int parametersRequired)
            : base(arguments, parametersRequired, parametersRequired)
        {
            this.Folder = this.Parameters[0];
        }

        protected override void InitOptions()
        {
            base.InitOptions();
            this.ExcludeList = new ExcludeList();
            HashInfo.CacheLimit = 4 << 20;  // 4Mbyte
        }

        protected override void ProcessOption(OptionAttribute option)
        {
            base.ProcessOption(option);
            if (option.Name == "HashDir") this.HashDir = option.Value;
            else if (option.Name == "Exclude") this.ExcludeList = new ExcludeList(option.Value);
            else if (option.Name == "HashCacheLimit")
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

        /// <summary>
        /// callled when a directory is entered
        /// </summary>
        /// <param name="path"></param>
        /// <returns>true to cancel processing</returns>
        protected abstract bool EnterDirectory(string path, int level);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="level"></param>
        protected abstract void ProcessFile(string path, int level);

        void Process(int level, string path)
        {
            try
            {
                if (this.ExcludeList.Exclude(path.EndsWith(@"\") ? path : (path + @"\")))
                {
                    Monitor.SkipDirectory(path, "exclude list match");
                    return;
                }
                if (this.EnterDirectory(path, level))
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
                    this.Process(level + 1, subDir);
            }
            catch (Exception error)
            {
                Monitor.Error(path, error);
            }
        }

        public override void Run()
        {
            ProcessFile(this.Folder, 0);
        }

    }
}
