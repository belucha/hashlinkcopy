using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.hashlinkcopy
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public class OptionAttribute : Attribute
    {
        public string Name { get; private set; }
        public string Description { get; set; }
        public string Help { get; set; }
        public string Default { get; set; }
        public OptionAttribute(string name)
        {
            this.Description = "";
            this.Help = "";
            this.Name = name;
        }

        public bool Match(string option)
        {
            var splitted = option.Substring(2).Trim().Split(new char[] { ':', '=', }, 2);
            if (String.Compare(splitted[0], this.Name, true) != 0) return false;
            this.Value = splitted.Length > 1 ? splitted[1].Trim() : "";
            return true;
        }

        public string Value { get; private set; }

        public static IEnumerable<OptionAttribute> List(Type type)
        {
            return type.GetCustomAttributes(typeof(OptionAttribute), true).Cast<OptionAttribute>();
        }
    }
}
