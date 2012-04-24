using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.hashlinkcopy
{
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
            foreach (var command in this.parameters)
                try
                {
                    var t = GetCommandHandler(command);
                    var description = t.GetCustomAttributes(typeof(DescriptionAttribute), true).FirstOrDefault() as DescriptionAttribute;
                    Console.WriteLine();
                    Console.WriteLine("{0}:\n\t{1}", command, description != null ? description.Description : "NO DESCRIPTION AVAILABLE");
                    Console.WriteLine("Options:");
                    foreach (var option in OptionAttribute.List(t))
                        Console.WriteLine("\t{0,-20}{1}\n\t{2,20}{3}", "--" + option.Name, option.Help, "...", option.Description);
                }
                catch (Exception)
                {
                    Console.WriteLine("Unknown command {0}", command);
                }
        }
    }
}
