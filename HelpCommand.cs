using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.backup
{
    [Command("help",
        Syntax = "[command]",
        Description = "list all available commands, or show detailed help for one command",
        MinParameterCount = 0,
        MaxParameterCount = 1
        )]
    class HelpCommand : ICommand
    {
        string[] parameters;
        public string[] Parameters
        {
            set { this.parameters = value; }
        }

        public int Run()
        {
            Console.WriteLine();
            var commandGroups = Assembly.GetExecutingAssembly().GetTypes().SelectMany(t =>
            {
                var a = t.GetCustomAttributes(typeof(CommandAttribute), true).Cast<CommandAttribute>().ToArray();
                foreach (var e in a)
                    e.Type = t;
                return a;
            });
            var command = parameters.Length > 0 ? commandGroups.FirstOrDefault(c => c.IsMatch(parameters[0])) : null;
            if (command == null)
            {
                if (parameters.Length > 0)
                    Console.Write("Unknown command \"{0}\", ", parameters[0]);
                Console.WriteLine("possible commands are:");
                foreach (var c in commandGroups)
                    Console.WriteLine("\t{0,-40} - {1}", c.Name + " " + c.Syntax, c.Description);
                return parameters.Length == 0 ? 0 : -1;     // in case of unknown command return error
            }
            // get help for a specific command
            Console.WriteLine("Help for command \"{0}\":", command.Name);
            Console.WriteLine("Syntax: \"{0} {1} {2}", OptionParser.ExeName, String.Join("|", command.Names), command.Syntax);
            Console.WriteLine("Parameters: {0}..{1}", command.MinParameterCount, command.MaxParameterCount);
            Console.WriteLine("Description: {0}", command.Description);
            Console.WriteLine("Options:");
            foreach (var option in OptionAttribute.GetOptions(Activator.CreateInstance(command.Type)))
            {
                foreach (var line in option.GetHelpText(true))
                    Console.WriteLine("\t" + line);
                Console.WriteLine();
            }
            return 0;
        }
    }
}
