using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.backup
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class CommandAttribute : Attribute
    {
        public string Name { get; private set; }
        public CommandAttribute(string name)
        {
            this.Name = name;
            this.MinParameterCount = 0;
            this.MaxParameterCount = int.MaxValue;
        }
        public string Syntax { get; set; }
        public string Description { get; set; }
        public int MinParameterCount { get; set; }
        public int MaxParameterCount { get; set; }
        public bool IsMatch(string command)
        {
            return String.Compare(command, this.Name, StringComparison.InvariantCultureIgnoreCase) == 0;
        }
        public Type Type { get; set; }
    }
}
