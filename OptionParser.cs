using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace de.intronik.backup
{
    public class OptionParser : ICommand
    {
        public static string ExeName;
        public static ICommand ParseCommandLine(string[] args = null)
        {
            if (args == null)
                args = System.Environment.GetCommandLineArgs();
            OptionParser.ExeName = Path.GetFileName(args[0]);
            var commandString = args.Skip(1).Where(arg => !arg.StartsWith("--")).FirstOrDefault();
            var parameters = args.Skip(1).Where(arg => !arg.StartsWith("--")).Skip(1).ToArray();
            var optionStrings = args.Skip(1).Where(arg => arg.StartsWith("--")).ToArray();
            var commandType = String.IsNullOrEmpty(commandString) ? typeof(OptionParser) : Assembly.GetExecutingAssembly().GetTypes()
                .FirstOrDefault(t => t.GetCustomAttributes(typeof(CommandAttribute), true).Cast<CommandAttribute>().Any(c => c.IsMatch(commandString)));
            if (commandType == null)
                throw new ArgumentException(String.Format("Unknown command \"{0}\" please run \"{1} help\" to get a list of valid commands!", commandString, OptionParser.ExeName));
            var commandAttribute = commandType.GetCustomAttributes(typeof(CommandAttribute), true).Cast<CommandAttribute>().FirstOrDefault(c => c.IsMatch(commandString));
            if (commandAttribute != null)
            {
                if (parameters.Length < commandAttribute.MinParameterCount)
                    throw new ArgumentException(String.Format("The command {0} requires at least {1} parameters!", commandAttribute.Name, commandAttribute.MinParameterCount));
                if (parameters.Length > commandAttribute.MaxParameterCount)
                    throw new ArgumentException(String.Format("The command {0} accepts not more than {1} parameters!", commandAttribute.Name, commandAttribute.MaxParameterCount));
            }
            var obj = Activator.CreateInstance(commandType);
            var command = obj as ICommand;
            if (command == null)
                throw new InvalidOperationException(String.Format("{0} is not a valid command class!", obj));
            command.Parameters = parameters;
            var options = OptionAttribute.GetOptions(command);
            // apply options
            foreach (var optionString in optionStrings)
            {
                var splitted = optionString.Substring(2).Split(new char[] { '=', ':', }, 2);
                var name = splitted[0];
                var value = splitted.Length > 1 ? splitted[1] : null;
                var matches = options.Where(o => o.IsMatch(name)).ToArray();
                if (matches.Length == 0)
                    throw new ArgumentException(String.Format("Unknown option \"{0}\" for \"{1}\"!", optionString, command));
                if (matches.Length > 1)
                    throw new ArgumentException(String.Format("Option \"{0}\" is not ambiqus for operation \"{1}\"! Possible matches are: {2}", name, command,
                        String.Join("|", matches.Select(o => o.GetName()))));
                matches[0].ApplyValue(value);
            }
            return command;
        }

        public string[] Parameters { set { if (value.Length > 0) throw new ArgumentOutOfRangeException("Syntax display does not take any parameters!"); } }

        public int Run()
        {
            Console.WriteLine("Syntax: {0} command [--option:value]", OptionParser.ExeName);
            Console.WriteLine("Run with command \"help\" to get a list of available commands");
            return 0;
        }
    }
}
