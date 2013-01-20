using System;
using System.Globalization;
using System.Reflection;
using System.ComponentModel;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.backup
{
    public abstract class BaseOperation
    {
        const string OperationOption = @"Operation";

        [Description("Number of directory levels to print")]
        [DefaultValue(0)]
        public uint Tree { get; set; }

        public DateTime StartTime { get; protected set; }
        public DateTime EndTime { get; protected set; }
        public TimeSpan Duration { get { return this.EndTime.Subtract(this.StartTime); } }

        public static BaseOperation GetOperation(IEnumerable<KeyValuePair<string, string>> options)
        {
            var ops = options.Where(kvp => String.Compare(kvp.Key, OperationOption, true) == 0).ToArray();
            if (ops.Length > 1)
                throw new InvalidOperationException("Option \"Operation\" is specified more than once!");
            var t = (ops.Length == 0 || String.IsNullOrEmpty(ops[0].Value)) ?
                typeof(HashLinkCopy) :
                Type.GetType(String.Format("{0}.{1}Operation", typeof(BaseOperation).Namespace, ops[0].Key), true, true);
            return Activator.CreateInstance(t) as BaseOperation;
        }

        protected IEnumerable<PropertyInfo> GetOptions()
        {
            return this
                .GetType()
                .FindMembers(MemberTypes.Property, BindingFlags.Public | BindingFlags.SetProperty | BindingFlags.GetProperty, (m, c) => m.GetCustomAttributes(typeof(DefaultValueAttribute), true).Length > 1, null)
                .Cast<PropertyInfo>();
        }

        protected static string ConvertOperationTypeToString(Type type)
        {
            return type.Name.Substring(0, type.Name.Length - OperationOption.Length);
        }

        public virtual void HandleOption(string name, string value)
        {
            if (String.Compare(name, OperationOption, true) 
                return;
            try
            {
                var info = GetOptions().FirstOrDefault(prop => String.Compare(prop.Name, name, true) == 0);
                if (info == null)
                    throw new ArgumentOutOfRangeException(String.Format("Unknown option \"{0}\" for operation \"{1}\"!", name, this.OperationName));
                if (String.IsNullOrEmpty(value))
                    info.SetValue(this, true, null);
                else
                {
                    var tc = TypeDescriptor.GetConverter(info.PropertyType);
                    info.SetValue(this, tc.ConvertFromString(null, CultureInfo.InvariantCulture, value), null);
                }
            }
            catch (Exception innerException)
            {
                // translate exception
                throw new InvalidOperationException(String.Format("Unable to set option \"{0}\"={1}, Message: \"{2}\"!", name, value, innerException.Message), innerException);
            }
        }

        public virtual string OperationName { get { return ConvertOperationTypeToString(this.GetType()); } }
        public string OperationDescription { get { return this.GetType().GetCustomAttributes(typeof(DescriptionAttribute), true).OfType<DescriptionAttribute>().First().Description; } }

        public virtual void PrintHelp(TextWriter output)
        {
            output.WriteLine("Operation: \"{0}\"", OperationName);
            output.WriteLine("Description:");
            foreach (var line in OperationDescription.Split('\n'))
                output.WriteLine("\t" + line.TrimEnd('\r'));
            output.WriteLine("Options for operation \"{0}\":");
        }
        public abstract void Run(string[] parameters);
    }
}
