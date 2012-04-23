using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace de.intronik.hashlinkcopy
{
    public class ExcludeList
    {
        /*
         * *\tmp\    ; exclude all tmp directories
         * *.exe     ; exclude all executables
         * *tmp/*.
         */
        Regex[] excludeList;
        public ExcludeList()
        {
            this.excludeList = new Regex[0];
        }
        public ExcludeList(string list)
        {
            var lines = list.StartsWith("@") ? list.Trim('\"').Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries) : File.ReadAllLines(list);
            this.excludeList = lines
                // trim comments
                .Select(line => { var i = line.IndexOf(';'); return (i < 0 ? line : line.Substring(0, i)).Trim(' ', '\t', '\n', '\a', '\r'); })
                // remove empty lines
                .Where(line => !String.IsNullOrEmpty(line))
                // transform wildcards in regulair expression pattern
                .Select(wildcard => "^" + Regex.Escape(wildcard).Replace("\\*", ".*").Replace("\\?", ".") + "$")
                // compile pattern into regulair expresion
                .Select(pattern => new Regex(pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.IgnoreCase))
                // save as array, so the expression is evaluated
                .ToArray();
        }
        public bool Exclude(string path)
        {
            return this.excludeList.Any(re => re.IsMatch(path));
        }
    }

}
