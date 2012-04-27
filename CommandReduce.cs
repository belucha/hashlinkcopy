using System;
using System.ComponentModel;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace de.intronik.hashlinkcopy
{
    [Option("Pattern", Help = "Pattern to match for backup folders", Description = @"The backup folder date time pattern that is matched
e.g. *YYYY-MM-DD_HH.NN.SS*", Default = "YYYY-MM-DD_HH.NN")]
    [Option("KeepMin", Help = "minimum number of backups to keep", Default = "30")]
    [Option("RuleFile", Help = "Name of a rule file", Description = "A rule file contains a set of rules.", Default = "None => default rules")]
    [Description(@"Removes old backup folders, based on the given rule set.
Default rules are:
    30 each day
    8 each week
    4 each month
    4 each 3 month
    2 each 6 month
    10 each year")]
    class CommandReduce : CommandBase
    {
        public List<string> DeletedFolders { get; private set; }
        public int KeepMin { get; private set; }
        public Rule[] Rules { get; private set; }
        public string Folder { get; private set; }


        public CommandReduce()
        {
            this.KeepMin = 30;
            this.Rules = new Rule[] {
                        new Rule(30, 1, Unit.day),
                        new Rule(8, 1, Unit.week),
                        new Rule(4, 1, Unit.month),
                        new Rule(4, 3, Unit.month),
                        new Rule(2, 6, Unit.month),
                        new Rule(10, 1, Unit.year),
                    };
            this.Pattern = @"YYYY-MM-DD_HH.NN";
            this.DeletedFolders = new List<string>();
        }

        public override void Init(string[] parameters)
        {
            base.Init(parameters);
            if (parameters.Length != 1)
                throw new ArgumentOutOfRangeException("Reduce requires exactly one target directory as parameter!");
            this.Folder = Path.GetFullPath(parameters[0]);
        }

        protected override void ProcessOption(OptionAttribute option)
        {
            base.ProcessOption(option);
            if (option.Name == "RuleFile")
            {
                var fn = Path.GetFullPath(option.Value);
                Logger.WriteLine(Logger.Verbosity.Message, "Reading reduce rules from '{0}'..", fn);
                var lines = File.ReadAllLines(fn)
                    // trim comments
                    .Select(line => { var i = line.IndexOf(';'); return (i < 0 ? line : line.Substring(0, i)).Trim(' ', '\t', '\n', '\a', '\r'); })
                    // remove empty lines
                    .Where(line => !String.IsNullOrEmpty(line))
                    .ToArray();
                this.Rules = lines.Select(line => new Rule(line)).OrderBy(rule => rule.Interval).ToArray();
            }
            else if (option.Name == "Pattern")
                this.Pattern = option.ParseAsString();
            else if (option.Name == "KeepMin")
                this.KeepMin = (int)(ushort)option.ParseAsLong();
        }

        public string Pattern { get; protected set; }

        public enum Unit
        {
            day = 1,
            week = 7,
            month = 30,
            year = 365,
        }

        public class Rule
        {
            static Regex ruleExpression = new Regex(@"^\s*(?<count>(\d+)?)\s*((times\s+(every|each))|x|=)?\s+(?<factor>(\d+)?)\s*(?<unit>(day|week|month|year))\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline | RegexOptions.ExplicitCapture);
            public Rule(int count, int factor, Unit unit)
            {
                this.Count = count;
                this.Factor = factor;
                this.Unit = unit;
            }
            public Rule(string line)
            {
                var match = ruleExpression.Match(line);
                if (!match.Success)
                    throw new InvalidOperationException(String.Format("Unkown rule format '{0}'", line));
                this.Count = int.Parse(match.Groups["count"].Value);
                this.Unit = (Unit)Enum.Parse(typeof(Unit), match.Groups["unit"].Value, true);
                int factor;
                if (!int.TryParse(match.Groups["factor"].Value, out factor))
                    factor = 1;
                this.Factor = factor;
            }
            public TimeSpan Interval { get { return TimeSpan.FromDays(this.Factor * (int)this.Unit); } }
            public int Count { get; private set; }
            public int Factor { get; private set; }
            public Unit Unit { get; private set; }
            public override string ToString()
            {
                return String.Format("Keep {0} intervals of {1}{2}", Count, Factor != 1 ? Factor.ToString() : "", Unit);
            }
        }

        public override void Run()
        {
            var backups = BackupFolder.GetBackups(this.Folder, this.Pattern).OrderByDescending(b => b.BackupDate).ToList();
            if (backups.Count < 1)
            {
                Logger.Warning("No backups found, that match the specified pattern {0}!", Pattern);
                return;
            }
            var end = backups[0].BackupDate;
            var remove = new Action<TimeSpan, int>((interval, count) =>
            {
                DateTime start;
                if (backups.Count <= this.KeepMin) return;
                for (var i = 0; i < count; i++)
                {
                    start = end.Subtract(interval);
                    var removeList = backups.Where(backup => (start < backup.BackupDate) && (backup.BackupDate <= end)).Skip(1).ToArray();
                    foreach (var item2Remove in removeList)
                    {
                        if (backups.Count <= this.KeepMin) return;
                        backups.Remove(item2Remove);
                        this.DeletedFolders.Add(item2Remove.Folder);
                    }
                    end = start;
                }
            });
            foreach (var rule in this.Rules)
                remove(rule.Interval, rule.Count);
            Logger.WriteLine(Logger.Verbosity.Message, "keeping {0} folders:", backups.Count);
            foreach (var backup in backups)
                Logger.WriteLine(Logger.Verbosity.Message, "\t+ {0}", backup.Folder);
            foreach (var f in this.DeletedFolders)
            {
                Logger.WriteLine(Logger.Verbosity.Message, "Deleting backup folder {0}...", f);
                var start = DateTime.Now;
                try
                {
                    Monitor.DeleteDirectory(f);
                    Logger.WriteLine(Logger.Verbosity.Message, "...completed after {0}", DateTime.Now.Subtract(start));
                }
                catch (Exception error)
                {
                    Logger.Error("...failed with {0}: {1} after {2}", error.GetType().Name, error.Message, DateTime.Now.Subtract(start));
                }
            }
        }
        public override string ToString()
        {
            return String.Format("Deleted {0} folders:\n{1}", this.DeletedFolders.Count,
                String.Join("\n", this.DeletedFolders.Select(f => "\t- " + f).ToArray()));
        }
    }
}
