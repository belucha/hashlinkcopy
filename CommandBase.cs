﻿using System;
using System.ComponentModel;
using System.Reflection;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.hashlinkcopy
{
    [Option("Verbosity", Help = "verbosity of the messages generated", Description = "None|Error|Warning|Message|Verbose|Debug", Default = "None")]
    [Option("DryRun", Help = "Disables any disk operations")]
    [Option("LogVerb", Help = "verbosity of the log file messages", Description = "None|Error|Warning|Message|Verbose|Debug", Default = "Message")]
    [Option("EnablePC", Help = "enable window performance counters", Description = "false,true", Default = "false")]
    abstract class CommandBase
    {

        public virtual void Init(string[] parameters)
        {
        }

        public virtual void ParseOptions(IEnumerable<string> options)
        {
            var optionsAvailable = OptionAttribute.List(this.GetType()).ToArray();
            foreach (var option in options)
                try
                {
                    this.ProcessOption(optionsAvailable.First(oa => oa.Match(option)));
                }
                catch (Exception inner)
                {
                    throw new ApplicationException(String.Format("Invalid/Unknown option '{0}', message: {1}", option, inner.Message), inner);
                }
        }

        public abstract void Run();

        protected virtual void ProcessOption(OptionAttribute option)
        {
            if (option.Name == "Verbosity")
                Logger.Root.Verbosity = option.ParseAsEnum<Verbosity>();
            if (option.Name == "LogVerb")
                Logger.Root.LogfileVerbosity = option.ParseAsEnum<Verbosity>();
            if (option.Name == "DryRun")
                Monitor.Root.DryRun = option.ParseAsBoolean();
        }

        public static Type GetCommandHandler(string command)
        {
            try
            {
                return Type.GetType(typeof(CommandBase).ToString().Replace("Base", command), true, true);
            }
            catch (Exception inner)
            {
                throw new ArgumentOutOfRangeException(String.Format("Unknown command {0}", command), inner);
            }
        }

        public static CommandBase CreateCommandHandler(string command)
        {
            return Activator.CreateInstance(GetCommandHandler(command)) as CommandBase;
        }

        public static IEnumerable<Type> GetCommandList()
        {
            return typeof(CommandBase).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(CommandBase)) && !t.IsAbstract && t.IsClass);
        }

    }
}