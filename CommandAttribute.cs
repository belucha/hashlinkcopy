using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.backup
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class CommandAttribute : Attribute
    {
        public string Name { get { return this.Names.First(); } }
        public string[] Names { get; private set; }
        public CommandAttribute(string name, params string[] alternateNames)
        {
            this.Names = new string[] { name, }.Concat(alternateNames).ToArray();
            this.MinParameterCount = 0;
            this.MaxParameterCount = int.MaxValue;
        }
        public string Syntax { get; set; }
        public string Description { get; set; }
        public int MinParameterCount { get; set; }
        public int MaxParameterCount { get; set; }
        public bool IsMatch(string command)
        {
            return this.Names.Any(name => String.Compare(command, name, StringComparison.InvariantCultureIgnoreCase) == 0);
        }
        public Type Type { get; set; }
    }
}

