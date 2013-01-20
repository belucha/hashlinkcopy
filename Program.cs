using System;
using System.Threading;
using System.Security.Principal;
using System.Windows.Forms;
using System.Security.Cryptography;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.backup
{
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                Console.WriteLine("{0} v{1} - Copyright © 2013, Daniel Gross, daniel@belucha.de", BaseOperation.ExeName, Application.ProductVersion);
                var options = args
                    .Where(arg => arg.StartsWith("--")).Select(arg => arg.Substring(2).Split(new char[] { ':', '=', }, 2))
                    .Select(pair => new KeyValuePair<string, string>(pair[0], pair.Length > 1 ? pair[1] : null));
                var arguments = args.Where(arg => !arg.StartsWith("--")).ToArray();
                var operation = BaseOperation.GetOperation(options, arguments);
                operation.Output = Console.Out;
                var res = operation.HandleOptions(options);
                if (res < 0)
                    return res;
                operation.PreRun();
                res = operation.Run();
                operation.ShowStatistics();
                return res;
            }
            catch (Exception e)
            {
                Console.WriteLine("\t{0,-20}{1}", "Error:", e.GetType().Name);
                Console.WriteLine("\t{0,-20}{1}", "Message:", e.Message);
                return -1;
            }
        }
    }
}
