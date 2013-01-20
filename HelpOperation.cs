using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.backup
{
    public class HelpOperation: BaseOperation
    {
        public override int Run()
        {
            Output.WriteLine("Help");
            return 0;
        }
    }
}
