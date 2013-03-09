using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace de.intronik.backup
{
    [Command("reduce", 
        Description = "scan folders and remove old backups",
        Syntax = "folder list",
        MinParameterCount = 1
        )]
    public class CommandReduce : CommandBase
    {
        [Option(ShortDescription = "minimum number of backups to keep", LongDescription = "General rule to keep at least the specified number of records")]
        public int KeepMin { get; private set; }
        public Rule[] Rules { get; private set; }

        [Option(ShortDescription = "Pattern to match for backup folders", LongDescription = @"The backup folder date time pattern that is matched e.g. *YYYY-MM-DD_HH.NN.SS*")]
        public string Pattern { get; protected set; }

        public List<string> DeletedFolders { get; private set; }

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
            this.Pattern = @"yyyy-MM-dd_HH_mm";
            this.DeletedFolders = new List<string>();
        }

        [Option(ShortDescription = "Name of a rule file", LongDescription = "A rule file contains a set of rules.", DefaultValue = "None => default rules")]
        public string RuleFile
        {
            get { return ""; }
            set
            {
                throw new NotImplementedException();
            }
        }


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


        protected override int DoOperation()
        {
            throw new NotImplementedException();
        }
    }
}
