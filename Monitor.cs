using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.hashlinkcopy
{
    public static class Monitor
    {
        static int processedFiles = 0;
        static int skippedFiles = 0;
        static int processedDir = 0;
        static int skippedDir = 0;
        static int copiedFiles = 0;
        static int linkedFiles = 0;
        static int hashedFiles = 0;
        static int movedFiles = 0;
        static long copiedBytes = 0;
        static long linkedBytes = 0;
        static long hashedBytes = 0;
        static long movedBytes = 0;
        static int collisions = 0;
        static int errors = 0;
        public static void ProcessFile(string path)
        {
            processedFiles++;
        }
        public static void ProcessDirectory(string path)
        {
            processedDir++;
            Logger.WriteLine(Logger.Verbosity.Verbose, "FOLDER: {0}", path);
        }
        public static void SkipFile(string path, string reason)
        {
            skippedFiles++;
            Logger.WriteLine(Logger.Verbosity.Verbose, "Skipping file '{0}': {1}", path, reason);
        }
        public static void SkipDirectory(string path, string reason)
        {
            skippedDir++;
            Logger.WriteLine(Logger.Verbosity.Verbose, "Skipping folder '{0}': {1}", path, reason);
        }
        public static void CopyFile(string source, string dest, long size)
        {
            copiedFiles++;
            copiedBytes += size;
            Logger.WriteLine(Logger.Verbosity.Message, "Copy file '{0}' to '{1}'", source, dest);
        }
        public static void LinkFile(string source, string dest, long size)
        {
            linkedFiles++;
            linkedBytes += size;
            Logger.WriteLine(Logger.Verbosity.Message, "Link file '{0}' to '{1}'", source, dest);
        }
        public static void MoveFile(string source, string dest, long size)
        {
            movedFiles++;
            movedBytes += size;
            Logger.WriteLine(Logger.Verbosity.Message, "Moving file '{0}' to '{1}'", source, dest);
        }
        public static void HashFile(string source, long size)
        {
            hashedFiles++;
            hashedBytes += size;
            Logger.WriteLine(Logger.Verbosity.Message, "SHA1 of '{0}' ({1}byte)", source, size);
        }
        public static void HashCollision(string path1, string path2)
        {
            collisions++;
            Logger.WriteLine(Logger.Verbosity.Error, "Hash Collision", path1, path2);
        }
        public static void Error(string path, Exception error)
        {
            errors++;
            Logger.Error("{0}:{1} processing '{2}'", error.GetType().Name, error.Message, path);
        }

        public static void PrintStatistics(TextWriter w)
        {
            w.WriteLine("{0,-20}: {1}", "Processed files", processedFiles);
            w.WriteLine("{0,-20}: {1}", "Skipped files", skippedFiles);
            w.WriteLine("{0,-20}: {1}", "processedDir", processedDir);
            w.WriteLine("{0,-20}: {1}", "skippedDir", skippedDir);
            w.WriteLine("{0,-20}: {1}", "copiedFiles", copiedFiles);
            w.WriteLine("{0,-20}: {1}", "linkedFiles", linkedFiles);
            w.WriteLine("{0,-20}: {1}", "hashedFiles", hashedFiles);
            w.WriteLine("{0,-20}: {1}", "movedFiles", movedFiles);
            w.WriteLine("{0,-20}: {1}", "copiedMBytes", copiedBytes >> 20);
            w.WriteLine("{0,-20}: {1}", "linkedMBytes", linkedBytes >> 20);
            w.WriteLine("{0,-20}: {1}", "hashedMBytes", hashedBytes >> 20);
            w.WriteLine("{0,-20}: {1}", "movedMBytes", movedBytes >> 20);
            w.WriteLine("{0,-20}: {1}", "collisions", collisions);
            w.WriteLine("{0,-20}: {1}", "errors", errors);
        }
    }
}
