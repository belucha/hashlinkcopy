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
            int errorCode = 0;
            try
            {
                Console.WriteLine("{0} - v{1}", Application.ProductName, Application.ProductVersion);
                //Console.WriteLine("Copyright © 2017, INTRONIK GmbH");
                errorCode = OptionParser.ParseCommandLine().Run();
            }
            catch (Exception e)
            {
                Console.WriteLine();
                Console.WriteLine("Error:");
                Console.WriteLine("\t{0,-20}{1}", "Type:", e.GetType().Name);
                Console.WriteLine("\t{0,-20}{1}", "Message:", e.Message);
                errorCode = 1;
            }
#if DEBUG
            Console.WriteLine("Error code is: {0}", errorCode);
            Console.WriteLine("Press return!");
            Console.ReadLine();
#endif
            return errorCode;
        }
    }
}
