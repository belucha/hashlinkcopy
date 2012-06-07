using System;
using System.Security.Cryptography;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.hashcopy
{
    class HashEntry
    {
        public byte[] Hash;
        public FileSystemInfo Info;

        public bool IsDirectory { get { return (this.Info.Attributes & FileAttributes.Directory) == FileAttributes.Directory; } }

        public override string ToString()
        {
            var s = new StringBuilder(IsDirectory ? 43 : 42);
            for (var i = 0; i < 20; i++)
            {
                var b = Hash[i];
                var nibble = b >> 4;
                s.Append((Char)(nibble < 10 ? '0' + nibble : ('a' + nibble - 10)));
                nibble = b & 0xF;
                s.Append((Char)(nibble < 10 ? '0' + nibble : ('a' + nibble - 10)));
                if (i < 2)
                    s.Append('\\');
                if (i == 1 && this.IsDirectory)
                    s.Append('_');
            }
            return s.ToString();
        }
    }

    class Hasher
    {
        HashAlgorithm HASH_ALG;
        public string HashDir { get; private set; }
        public static bool DEMO = true;
        public long FileCount { get; private set; }
        public long DirectoryCount { get; private set; }
        public long CopyCount { get; private set; }
        public long SymLinkCount { get; private set; }
        public long SkippedSymLinkCount { get; private set; }

        public Hasher(string hashDir)
        {
            this.DirectoryCount = 0;
            this.FileCount = 0;
            this.CopyCount = 0;
            this.SymLinkCount = 0;
            this.SkippedSymLinkCount = 0;
            this.HASH_ALG = SHA1.Create();
            this.HashDir = hashDir.EndsWith("\\") ? hashDir : (hashDir + "\\");
        }

        public void CopyFile(string source, string dest)
        {
            if (DEMO) return;
            while (true)
            {
                var error = Win32.CopyFileEx(source, dest, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, Win32.CopyFileFlags.FAIL_IF_EXISTS) ?
                    0 :
                    System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                switch (error)
                {
                    case 0:     // ERROR_SUCCESS
                        this.CopyCount++;
                        return;
                    case 3:     // ERROR_PATH_NOT_FOUND
                        Directory.CreateDirectory(Path.GetDirectoryName(dest));
                        break;
                    case 80:    // ERROR_FILE_EXISTS
                    case 183:   // ERROR_ALREADY_EXISTS
                        return;
                    case 2:     // ERROR_FILE_NOT_FOUND
                    default:
                        var e = new System.ComponentModel.Win32Exception(error);
                        Console.Error.WriteLine("{0}: {1} ({2}) in {3}", e.GetType().Name, e.Message, error, source);
                        throw new ApplicationException(source, e);
                }
            }
        }

        public void CreateSymbolicLink(HashEntry entry, string dir, string name, string hashRoot)
        {
            var target = hashRoot + entry.ToString();
            if (Hasher.DEMO)
                Console.WriteLine(entry.IsDirectory ? "\t[{0}]=>[{1}]" : "\t{0}=>{1}", Path.GetFileName(name), target);
            else
                while (true)
                {
                    // create link and set error variable
                    var error = Win32.CreateSymbolicLink(Path.Combine(dir, name), target, entry.IsDirectory ? Win32.SymbolicLinkFlags.Directory : Win32.SymbolicLinkFlags.File) ? 0 : System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                    switch (error)
                    {
                        case 0:     // ERROR_SUCCESS
                            this.SymLinkCount++;
                            return;
                        case 3:     // ERROR_PATH_NOT_FOUND
                            Directory.CreateDirectory(dir);
                            break;
                        case 80:    // ERROR_FILE_EXISTS
                        case 183:   // ERROR_ALREADY_EXISTS
                            this.SkippedSymLinkCount++;
                            return;
                        case 1314:  // ERROR_PRIVILEGE_NOT_HELD
                            throw new System.Security.SecurityException("Not enough priveleges to create symbolic links!", new System.ComponentModel.Win32Exception(error));
                        case 2:     // ERROR_FILE_NOT_FOUND
                        default:
                            var e = new System.ComponentModel.Win32Exception(error);
                            Console.Error.WriteLine("{0}: {1} ({2}) in {3}", e.GetType().Name, e.Message, error, name);
                            throw new ApplicationException(name, e);
                    }
                }
        }

        public HashEntry Run(FileSystemInfo fileSystemInfo, int level)
        {
            try
            {
                if ((fileSystemInfo.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    //
                    // HANDLE SUBDIRECTORY
                    //
                    var sourceDirectory = (DirectoryInfo)fileSystemInfo;
                    Console.WriteLine("{1}[{0}]", fileSystemInfo.Name, "".PadLeft(level));
                    this.DirectoryCount++;
                    Console.Title = String.Format("Processing (d{1}/f{2}/s{3}/c{4}) [{0}]", fileSystemInfo.FullName, this.DirectoryCount, this.FileCount, this.SymLinkCount, this.CopyCount);
                    var m = new MemoryStream();
                    var w = new BinaryWriter(m);
                    var directoryEntries = sourceDirectory.EnumerateFileSystemInfos().Select(e => Run(e, level + 1)).Where(e => e != null).ToArray();
                    foreach (var subEntry in directoryEntries)
                    {
                        w.Write(subEntry.Info.Attributes.HasFlag(FileAttributes.Directory));
                        w.Write(subEntry.Hash);
                        w.Write(subEntry.Info.Name);
                    }
                    var directoryEntry = new HashEntry()
                    {
                        Hash = HASH_ALG.ComputeHash(m.ToArray()),
                        Info = fileSystemInfo,
                    };
                    var td = Path.Combine(this.HashDir, directoryEntry.ToString());
                    if (Directory.Exists(td)) return directoryEntry;
                    // create link structure in temp directory first
                    var tmpDirName = td + "_";
                    Directory.CreateDirectory(tmpDirName);
                    foreach (var subEntry in directoryEntries)
                        this.CreateSymbolicLink(subEntry, tmpDirName, subEntry.Info.Name, "..\\..\\..\\");
                    // we are done, rename directory
                    Directory.Move(tmpDirName, td);
                    return directoryEntry;
                }
                else
                {
                    //
                    // HANDLE FILE
                    //
                    this.FileCount++;
                    var fileEntry = new HashEntry()
                    {
                        Hash = new HashInfo((FileInfo)fileSystemInfo, HASH_ALG).Hash,
                        Info = fileSystemInfo,
                    };
                    CopyFile(fileSystemInfo.FullName, Path.Combine(this.HashDir, fileEntry.ToString()));
                    return fileEntry;
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("{0}{1}: {2} in {3}", "".PadLeft(level), e.GetType().Name, e.Message, fileSystemInfo.Name);
                return null;
            }
        }
    }

    class Program
    {

        static void Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage is {0} SOURCE DEST NAME [HASHDIR]", "de.intronik.hashcopy.exe");
                return;
            }
            try
            {
                Hasher.DEMO = false;
                var start = DateTime.Now;
                var source = new DirectoryInfo(args[0]);
                var targetFolder = Path.GetFullPath(args[1]);
                var hashDirectory = Path.GetFullPath(args.Length < 4 ? Path.Combine(targetFolder, "Hash") : args[3]);
                var name = args[2].Replace("*", String.Format("{0:yyyy-MM-dd_HH.mm}", DateTime.Now));
                Console.WriteLine("{0,-20}: {1}", "Source", source);
                Console.WriteLine("{0,-20}: {1}", "Target Folder", targetFolder);
                Console.WriteLine("{0,-20}: {1}", "Target Name", name);
                Console.WriteLine("{0,-20}: {1}", "Hash Folder", hashDirectory);
                var h = new Hasher(hashDirectory);
                var e = h.Run(source, 0);
                if (!Hasher.DEMO)
                    Directory.CreateDirectory(targetFolder);
                h.CreateSymbolicLink(e, targetFolder, name, h.HashDir);
                var et = DateTime.Now.Subtract(start);
                Console.WriteLine("Processed {0} source files and {1} source directories, {2} new files copied, {3} new symbolic links created in {4}", h.FileCount, h.DirectoryCount, h.CopyCount, h.SymLinkCount, et);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.ToString());
            }
        }
    }
}
