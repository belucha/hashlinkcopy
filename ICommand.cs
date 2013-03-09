using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.backup
{
    public interface ICommand
    {
        string[] Parameters { set; }
        int Run();
    }

}
