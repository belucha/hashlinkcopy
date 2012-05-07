using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.hashlinkcopy
{
    [Description(@"This screen")]
    class CommandHelp : CommandBase
    {
        string[] parameters;
        public override void Init(string[] parameters)
        {
            base.Init(parameters);
            this.parameters = parameters;
        }
        public override void Run()
        {
            if (parameters.Length == 0)
            {
                Console.WriteLine(
@"HELP:
=====
To get help on a specific command run
{0} HELP COMMAND
where COMMAND is one of:
", System.Windows.Forms.Application.ExecutablePath);
                foreach (var cmd in CommandBase.GetCommandList())
                {
                    var d = cmd.GetCustomAttributes(typeof(DescriptionAttribute), true).Cast<DescriptionAttribute>().FirstOrDefault();
                    Console.WriteLine("\t - {0,-10}: {1}", cmd.Name.Substring("Command".Length).ToUpper(), d != null ? d.Description : "");
                }
            }
            foreach (var command in this.parameters)
                try
                {
                    var t = GetCommandHandler(command);
                    var description = t.GetCustomAttributes(typeof(DescriptionAttribute), true).FirstOrDefault() as DescriptionAttribute;
                    Console.WriteLine();
                    Console.WriteLine("{0}:\n\t{1}", command, description != null ? description.Description : "NO DESCRIPTION AVAILABLE");
                    Console.WriteLine("Options:");
                    foreach (var option in OptionAttribute.List(t))
                    {
                        Console.WriteLine("\t{0,-40}{1}", "--" + option.Name, option.Help);
                        Console.WriteLine("\t{0,40}{1}", "Default:", option.Default);
                        bool first = true;
                        foreach (var dl in option.Description.Split('\n'))
                        {
                            Console.WriteLine("\t{0,40}{1}", first ? "Description:" : "", dl);
                            first = false;
                        }
                    }
                }
                catch (Exception)
                {
                    Console.WriteLine("Unknown command {0}", command);
                }
        }
    }
}
