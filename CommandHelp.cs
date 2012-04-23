using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.hashlinkcopy
{
    class CommandHelp : CommandBase
    {
        public CommandHelp(IEnumerable<string> parameters)
            : base(parameters, 0, 20)
        {
        }
        public override void Run()
        {
            foreach (var command in this.Parameters)
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
