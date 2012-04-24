using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace de.intronik.hashlinkcopy
{
    class BackupFolder
    {
        public string Folder { get; set; }
        public DateTime BackupDate { get; set; }
        public override string ToString()
        {
            return String.Format("{0:yyyy-MM-dd HH:mm:ss} {1}", BackupDate, Folder);
        }
        public static IEnumerable<BackupFolder> GetBackups(string rootFolder, string pattern)
        {
            var regPattern = new Regex("^" + Regex.Escape(pattern).ToLower()
                .Replace("yyyy", @"(?<YYYY>\d\d\d\d)")
                .Replace("yy", @"(?<YY>\d\d)")
                .Replace("mm", @"(?<MM>\d\d)")
                .Replace("dd", @"(?<DD>\d\d)")
                .Replace("hh", @"(?<HH>\d\d)")
                .Replace("nn", @"(?<NN>\d\d)")
                .Replace("ss", @"(?<SS>\d\d)")
                .Replace("\\*", ".*")
                .Replace("\\?", ".")
                + "$",
                RegexOptions.ExplicitCapture | RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (var subDir in Directory.GetDirectories(rootFolder))
            {
                var match = regPattern.Match(Path.GetFileName(subDir));
                if (!match.Success) continue;
                var year = DateTime.Now.Year;
                var month = 1;
                var day = 1;
                var hour = 0;
                var minute = 0;
                var second = 0;
                if (!String.IsNullOrEmpty(match.Groups["YYYY"].Value))
                    year = int.Parse(match.Groups["YYYY"].Value);
                else if (!String.IsNullOrEmpty(match.Groups["YY"].Value))
                    year = 2000 + int.Parse(match.Groups["YY"].Value);
                if (!String.IsNullOrEmpty(match.Groups["MM"].Value))
                    month = int.Parse(match.Groups["MM"].Value);
                if (!String.IsNullOrEmpty(match.Groups["DD"].Value))
                    day = int.Parse(match.Groups["DD"].Value);
                if (!String.IsNullOrEmpty(match.Groups["HH"].Value))
                    hour = int.Parse(match.Groups["HH"].Value);
                if (!String.IsNullOrEmpty(match.Groups["NN"].Value))
                    minute = int.Parse(match.Groups["NN"].Value);
                if (!String.IsNullOrEmpty(match.Groups["SS"].Value))
                    second = int.Parse(match.Groups["SS"].Value);
                DateTime backupDate;
                try
                {
                    backupDate = new DateTime(year, month, day, hour, minute, second);
                }
                catch (Exception)
                {
                    // ignore folder, invalid date format
                    continue;
                }
                yield return new BackupFolder()
                {
                    Folder = subDir,
                    BackupDate = backupDate,
                };
            }
        }
    }
}
