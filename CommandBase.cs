using System;
using System.ComponentModel;
using System.Reflection;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.hashlinkcopy
{
    [Option("Verbosity", Help = "verbosity of the messages generated", Description = "None|Error|Warning|Message|Verbose|Debug")]
    abstract class CommandBase
    {
        public List<string> Parameters { get; protected set; }
        public CommandBase(IEnumerable<string> arguments, int minParameters, int maxParameters)
        {
            this.Parameters = new List<string>();
            this.InitOptions();
            var optionsAvailable = OptionAttribute.List(this.GetType()).ToArray();
            foreach (var argument in arguments)
                if (argument.StartsWith("--"))
                    this.ProcessOption(optionsAvailable.First(oa => oa.Match(argument)));
                else
                    this.Parameters.Add(argument);
            if (this.Parameters.Count < minParameters)
                throw new ArgumentOutOfRangeException(String.Format("{0} requires at least {1} parameter(s)!", this.GetType().Name, minParameters));
            if (this.Parameters.Count > maxParameters)
                throw new ArgumentOutOfRangeException(String.Format("{0} accepts no more than {1} parameter(s)!", this.GetType().Name, maxParameters));
        }
        public abstract void Run();

        protected virtual void InitOptions()
        {
        }

        protected virtual void ProcessOption(OptionAttribute option)
        {
            if (option.Name == "Verbosity")
                Logger.VERBOSITY = (Logger.Verbosity)Enum.Parse(typeof(Logger.Verbosity), option.Value, true);
        }

        public static Type GetCommandHandler(string command)
        {
            return Type.GetType(typeof(CommandBase).ToString().Replace("Base", command), true, true);
        }

        public static CommandBase CreateCommandHandler(IEnumerable<string> arguments)
        {
            return Activator.CreateInstance(GetCommandHandler(arguments.First()), arguments.Skip(1)) as CommandBase;
        }

        public static IEnumerable<Type> GetCommandList()
        {
            return typeof(CommandBase).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(CommandBase)) && !t.IsAbstract && t.IsClass);
        }

    }
}