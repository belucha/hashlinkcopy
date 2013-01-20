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

        public TextWriter Output { get; set; }

        protected static string FormatBytes(long bytes)
        {
            var units = new string[] { "Byte", "Kb", "Mb", "Gb", };
            if (bytes < 1024)
                return String.Format("{0}{1}", bytes, units[0]);
            var b = new StringBuilder();
            for (var p = units.Length - 1; p >= 0; p--)
            {
                var c = (bytes >> (p * 10)) & 1023;
                if (c > 0)
                    b.AppendFormat("{0}{1} ", c, units[p]);
            }
            return b.ToString();
        }


        public string[] Parameters { get; set; }

        protected virtual void PreHandleParameters()
        {
        }

        public virtual void PreRun()
        {
        }


        public static string ExeName { get { return Path.GetFileName(System.Windows.Forms.Application.ExecutablePath); } }

        public static BaseOperation GetOperation(IEnumerable<KeyValuePair<string, string>> options, string[] parameters)
        {
            var ops = options.Where(kvp => String.Compare(kvp.Key, OperationOption, true) == 0).ToArray();
            if (ops.Length > 1)
                throw new InvalidOperationException("Option \"Operation\" is specified more than once!");
            var t = (ops.Length == 0 || String.IsNullOrEmpty(ops[0].Value)) ?
                (parameters.Length == 0 ? typeof(HelpOperation) : typeof(BackupOperation)) :
                Type.GetType(String.Format("{0}.{1}Operation", typeof(BaseOperation).Namespace, ops[0].Value), true, false);
            if (t == null)
                throw new InvalidOperationException(String.Format("Unkwown operation \"{0}\"!", ops[0].Value));
            var res = Activator.CreateInstance(t) as BaseOperation;
            if (res == null)
                throw new InvalidOperationException(String.Format("Unknown Operation {0}", ConvertOperationTypeToString(t)));
            res.Parameters = parameters;
            res.PreHandleParameters();
            return res;
        }

        protected IEnumerable<PropertyInfo> GetOptions()
        {
            return this
                .GetType()
                .GetProperties()
                .Where(p => p.GetCustomAttributes(typeof(DefaultValueAttribute), true).Length > 0);
        }

        protected static string ConvertOperationTypeToString(Type type)
        {
            return type.Name.EndsWith(OperationOption) ? type.Name.Substring(0, type.Name.Length - OperationOption.Length) : type.Name;
        }

        public int HandleOptions(IEnumerable<KeyValuePair<string, string>> options)
        {
            foreach (var option in options)
            {
                if (String.Compare(option.Key, OperationOption, true) == 0)
                    continue;
                var res = HandleOption(option.Key, option.Value);
                if (res < 0) return res;
            }
            return 0;
        }

        protected virtual int HandleOption(string name, string value)
        {
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
                return 0;
            }
            catch (Exception innerException)
            {
                // translate exception
                throw new InvalidOperationException(String.Format("Unable to set option \"{0}\"={1}, Message: \"{2}\"!", name, value, innerException.Message), innerException);
            }
        }

        public virtual string OperationName { get { return ConvertOperationTypeToString(this.GetType()); } }
        public string OperationDescription { get { return this.GetType().GetCustomAttributes(typeof(DescriptionAttribute), true).OfType<DescriptionAttribute>().First().Description; } }

        public virtual void PrintHelp()
        {
            Output.WriteLine("Operation: \"{0}\"", OperationName);
            Output.WriteLine("Description:");
            foreach (var line in OperationDescription.Split('\n'))
                Output.WriteLine("\t" + line.TrimEnd('\r'));
            Output.WriteLine("Options for operation \"{0}\":");
        }
        public abstract int Run();

        protected void print(string name, object value)
        {
            Output.WriteLine("{0,-20}: {1}", name, value);
        }

        public virtual void ShowStatistics()
        {
            print("Operation", OperationName);
        }

        public string PromptInput(string prompt, params object[] args)
        {
            return PromptInput(prompt, args);
        }

        public string PromptInput(Func<string, bool> inputOkPredicate, string prompt, params object[] args)
        {
            while (true)
            {
                Output.Write(String.Format(prompt, args));
                var answer = Console.ReadLine().Trim();
                if (inputOkPredicate == null || inputOkPredicate(answer))
                    return answer;
            }
        }
    }
}
