using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.backup
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class OptionAttribute : Attribute, IComparable<OptionAttribute>
    {
        public string Name { get; set; }
        public string ShortDescription { get; set; }
        public string LongDescription { get; set; }
        public string EmptyValue { get; set; }
        public string ValueText { get; set; }
        public string DefaultValue { get; set; }
        public int Priority { get; set; }

        public string GetName() { return String.IsNullOrEmpty(this.Name) ? property.Name : this.Name; }

        public bool IsMatch(string option)
        {
            return this.GetName().StartsWith(option, StringComparison.InvariantCultureIgnoreCase);
        }

        PropertyInfo property;
        object target;

        public void SetPropertyAndObject(PropertyInfo aProperty, object aTarget)
        {
            this.property = aProperty;
            this.target = aTarget;
        }

        public void ApplyValue(string value)
        {
            if (value == null)
                value = this.EmptyValue;
            if (value == null)
                throw new ArgumentException(String.Format("Option \"{0}\" requires a value!", GetName()));
            try
            {
                property.SetValue(target, TypeDescriptor.GetConverter(property.PropertyType).ConvertFromString(null, CultureInfo.InvariantCulture, value), null);
            }
            catch (Exception innerException)
            {
                // translate exception
                throw new ArgumentException(String.Format("Unable to set option \"{0}\"={1}, Message: \"{2}\"!", GetName(), value, innerException.Message), innerException);
            }

        }

        public IEnumerable<string> GetHelpText(bool appendLongDescription)
        {
            var valueRequired = String.IsNullOrEmpty(this.EmptyValue);
            /* Sample output for option with optional value argument
             * 
             *   --long[=ValueText] Very short single line description
             *                      Default if not specified: DefaultValue
             *                      When the value is ommited the
             *                      Long long long text line 1
             *                      Long long long text line 2
             *                      Long long long text line 3
             */
            var longForm = String.Format(valueRequired ? "--{0}:{1}" : "--{0}[:{1}]", GetName(), ValueText);
            yield return String.Format("{0} {1}", longForm.PadRight(20), ShortDescription);
            yield return "".PadLeft(21) + String.Format("Default: {0}", String.IsNullOrEmpty(DefaultValue) ? String.Format(CultureInfo.InvariantCulture, "{0}", property.GetValue(target, null)) : DefaultValue);
            if (appendLongDescription && String.IsNullOrEmpty(LongDescription) == false)
                foreach (var line in LongDescription.Split('\n'))
                    yield return "".PadLeft(21) + line;
        }

        public int CompareTo(OptionAttribute other)
        {
            return this.Priority.CompareTo(other.Priority);
        }

        public static IEnumerable<OptionAttribute> GetOptions(object target)
        {
            return target
                .GetType()
                .GetProperties()
                .Select(p =>
                {
                    var option = p.GetCustomAttributes(typeof(OptionAttribute), true).FirstOrDefault() as OptionAttribute;
                    if (option == null) return null;
                    option.SetPropertyAndObject(p, target);
                    return option;
                })
                .Where(o => o != null);
        }
    }
}
