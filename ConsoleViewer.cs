using System;
using System.CodeDom.Compiler;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.backup
{
    public class ConsoleViewer
    {
        IndentedTextWriter display;
        IndentedTextWriter log;
        bool cancel;

        public long ErrorCount { get; private set; }
        public long FileCount { get; private set; }
        public long DirectoryCount { get; private set; }
        public long CopyCount { get; private set; }
        public long LinkFileCount { get; private set; }
        public long LinkDirectoryCount { get; private set; }
        public int DisplayLimit { get; set; }


        public ConsoleViewer(HashLinkCopyBase hashLinkCopy)
        {
            DisplayLimit = 3;
            Console.TreatControlCAsInput = false;
            Console.CancelKeyPress += new ConsoleCancelEventHandler(Console_CancelKeyPress);
            this.log = new IndentedTextWriter(Console.Error, "\t");
            this.display = new IndentedTextWriter(Console.Out, "  ");
            hashLinkCopy.Action += new EventHandler<HashLinkActionEventArgs>(hashLinkCopy_Action);
            hashLinkCopy.Error += new EventHandler<HashLinkErrorEventArgs>(hashLinkCopy_Error);
        }

        void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Console.Write("Cancel operation (y)es/(N)o? ");
            var res = Console.ReadKey(true);
            if (Char.ToLower(res.KeyChar) == 'y') cancel = true;
        }

        void hashLinkCopy_Error(object sender, HashLinkErrorEventArgs e)
        {
            this.log.WriteLine("Error {0} on file \"{1}\", Message: \"{2}\"", e.Error.GetType().Name, e.Info.FullName, e.Error.Message);
            this.display.WriteLine("Error {0} on file \"{1}\", Message: \"{2}\"", e.Error.GetType().Name, e.Info.FullName, e.Error.Message);
            if (cancel)
                throw e.Error;
        }

        void hashLinkCopy_Action(object sender, HashLinkActionEventArgs e)
        {
            switch (e.Action)
            {
                case HashLinkAction.EnterSourceDirectory:
                    DirectoryCount++;
                    if (e.Level < DisplayLimit)
                    {
                        display.Indent = e.Level;
                        display.WriteLine(e.Info.FullName);
                    }
                    break;
                case HashLinkAction.ProcessSourceFile:
                    FileCount++;
                    break;
                case HashLinkAction.CopyFile:
                    CopyCount++;
                    break;
                case HashLinkAction.LinkDirectory:
                    LinkDirectoryCount++;
                    break;
                case HashLinkAction.LinkFile:
                    LinkFileCount++;
                    break;
            }
            Console.Title = String.Format("{0} [d{1}/f{2}/c{3}/ld{4}/lf{5}]", e, DirectoryCount, FileCount, CopyCount, LinkDirectoryCount, LinkFileCount);
            if (cancel)
                throw new ApplicationException("Aborted!");
        }
    }
}
