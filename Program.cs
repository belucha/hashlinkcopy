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
            return ToString('_');
        }

        public string ToString(char directoryPrefix)
        {
            var s = new StringBuilder(IsDirectory ? 44 : 43);
            for (var i = 0; i < 20; i++)
            {
                var b = Hash[i];
                var nibble = b >> 4;
                s.Append((Char)(nibble < 10 ? '0' + nibble : ('a' + nibble - 10)));
                nibble = b & 0xF;
                if (i == 1)
                {
                    s.Append('\\');
                    if (this.IsDirectory)
                        s.Append(directoryPrefix);
                }
                s.Append((Char)(nibble < 10 ? '0' + nibble : ('a' + nibble - 10)));
            }
            return s.ToString();
        }
    }

    class Hasher
    {
        public delegate void LinkDelegate(string name, HashEntry entry);
        HashAlgorithm HASH_ALG;
        public string HashDir { get; private set; }
        public static bool DEMO = true;
        public long ErrorCount { get; private set; }
        public long FileCount { get; private set; }
        public long DirectoryCount { get; private set; }
        public long CopyCount { get; private set; }
        public long SymLinkCount { get; private set; }
        public long JunctionCount { get; private set; }
        public long SkippedJunctionCount { get; private set; }
        public long SkippedTreeCount { get; private set; }
        public long HardLinkCount { get; private set; }
        public long SkippedHardLinkCount { get; private set; }
        public long MoveCount { get; private set; }
        public long SkippedSymLinkCount { get; private set; }
        public LinkDelegate CreateLink { get; set; }

        public Hasher(string hashDir)
        {
            this.HASH_ALG = SHA1.Create();
            this.HashDir = hashDir.EndsWith("\\") ? hashDir : (hashDir + "\\");
            this.CreateLink = CreateHardLinkOrJunction;
            // make sure all hash directories exist!
            for (var i = 0; i < (1 << 12); i++)
                Directory.CreateDirectory(hashDir + i.ToString("X3"));
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
                        File.SetAttributes(dest, FileAttributes.Normal);
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

        public void CreateSymbolicLink(string name, HashEntry entry)
        {
            while (true)
            {
                var hc = entry.ToString();
                var target = this.HashDir + hc;
                // check if copy file must be done
                if (!entry.IsDirectory && !File.Exists(target))
                    this.CopyFile(entry.Info.FullName, target);
                // remove target drive
                target = (target.Length > 2 && target[1] == ':') ? target.Substring(2) : target;
                // create link and set error variable
                var error = Win32.CreateSymbolicLink(name, target, entry.IsDirectory ? Win32.SymbolicLinkFlags.Directory : Win32.SymbolicLinkFlags.File) ? 0 : System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                switch (error)
                {
                    case 0:     // ERROR_SUCCESS
                        this.SymLinkCount++;
                        return;
                    case 80:    // ERROR_FILE_EXISTS
                    case 183:   // ERROR_ALREADY_EXISTS
                        this.SkippedSymLinkCount++;
                        return;
                    case 1314:  // ERROR_PRIVILEGE_NOT_HELD
                        throw new System.Security.SecurityException("Not enough priveleges to create symbolic links!", new System.ComponentModel.Win32Exception(error));
                    case 2:     // ERROR_FILE_NOT_FOUND
                    case 3:     // ERROR_PATH_NOT_FOUND
                    default:
                        var e = new System.ComponentModel.Win32Exception(error);
                        Console.Error.WriteLine("{0}: {1} ({2}) in {3}", e.GetType().Name, e.Message, error, name);
                        throw new ApplicationException(name, e);
                }
            }
        }

        public void CreateHardLinkOrJunction(string name, HashEntry entry)
        {
            var target = HashDir + entry.ToString();
            if (entry.IsDirectory)
            {
                if (Directory.Exists(name))
                {
                    this.SkippedJunctionCount++;
                    return;
                }
                Directory.CreateDirectory(name);
                Win32.CreateJunction(name, target, false);
                this.JunctionCount++;
            }
            else
                while (true)
                {
                    var linkError = Win32.CreateHardLink(name, target, IntPtr.Zero) ? 0 : System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                    switch (linkError)
                    {
                        case 0:     // ERROR_SUCCESS
                            this.HardLinkCount++;
                            return;
                        case 183:   // ERROR_ALREADY_EXISTS
                            this.SkippedHardLinkCount++;
                            return; // target file already existing
                        case 1142:  // ERROR_TOO_MANY_LINKS
                            File.Move(target, name);
                            this.MoveCount++;
                            return;
                        case 2:     // ERROR_FILE_NOT_FOUND
                            CopyFile(entry.Info.FullName, target);
                            break;
                        case 3:     // ERROR_PATH_NOT_FOUND                        
                        default:
                            throw new System.ComponentModel.Win32Exception(linkError, String.Format("CreateHardLink({0},{1}) returned 0x{2:X8}h", name, target, linkError));
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
                    //Console.WriteLine("{1}[{0}]", fileSystemInfo.Name, "".PadLeft(level));
                    this.DirectoryCount++;
                    Console.Title = String.Format("Processing (d{1}/f{2}/sl{3}/c{4}/m{5}/hl{6}/j{7}/st{8}) [{0}]",
                        fileSystemInfo.FullName,
                        this.DirectoryCount,
                        this.FileCount,
                        this.SymLinkCount,
                        this.CopyCount,
                        this.MoveCount,
                        this.HardLinkCount,
                        this.JunctionCount,
                        this.SkippedTreeCount);
                    var m = new MemoryStream();
                    var w = new BinaryWriter(m);
                    var directoryEntries = new List<HashEntry>(500);
                    foreach (var entry in sourceDirectory.EnumerateFileSystemInfos())
                    {
                        var subEntry = this.Run(entry, level + 1);
                        if (subEntry != null)
                        {
                            w.Write(subEntry.Info.Attributes.HasFlag(FileAttributes.Directory));
                            w.Write(subEntry.Hash);
                            w.Write(subEntry.Info.Name);
                            directoryEntries.Add(subEntry);
                        }
                    }
                    var directoryEntry = new HashEntry()
                    {
                        Hash = HASH_ALG.ComputeHash(m.ToArray()),
                        Info = fileSystemInfo,
                    };
                    var td = this.HashDir + directoryEntry.ToString();
                    if (Directory.Exists(td))
                    {
                        this.SkippedTreeCount++;
                        return directoryEntry;
                    }
                    // create link structure in temp directory first                    
                    var tmpDirName = this.HashDir + directoryEntry.ToString('$') + "\\";
                    Directory.CreateDirectory(tmpDirName);
                    foreach (var subEntry in directoryEntries)
                        this.CreateLink(tmpDirName + subEntry.Info.Name, subEntry);
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
                    return fileEntry;
                }
            }
            catch (Exception e)
            {
                this.ErrorCount++;
                Console.Error.WriteLine("{0}{1}: {2} in {3}", "".PadLeft(level), e.GetType().Name, e.Message, fileSystemInfo.Name);
                return null;
            }
        }
    }

    class Program
    {

        static void print(string name, object value)
        {
            Console.WriteLine("{0,-20}: {1}", name, value);
        }
        static void Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage is {0} SOURCE DEST NAME [HASHDIR] [symbolic]", "de.intronik.hashcopy.exe");
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
                print("Source", source);
                print("Target Folder", targetFolder);
                print("Target Name", name);
                print("Hash Folder", hashDirectory);
                var h = new Hasher(hashDirectory);
                // select which algorithm to use
                h.CreateLink = (args.Length > 4 && String.Compare(args[4], "symbolic", true) == 0) ?
                        (Hasher.LinkDelegate)h.CreateSymbolicLink :
                        (Hasher.LinkDelegate)h.CreateHardLinkOrJunction;

                h.CreateLink(Path.Combine(targetFolder, name), h.Run(source, 0));
                var et = DateTime.Now.Subtract(start);
                print("Source", source);
                print("Target Folder", targetFolder);
                print("Target Name", name);
                print("Hash Folder", hashDirectory);
                print("files", h.FileCount);
                print("directories", h.DirectoryCount);
                print("copied files", h.CopyCount);
                print("symbolic links", h.SymLinkCount);
                print("s. symbolic links", h.SkippedSymLinkCount);
                print("hard link", h.HardLinkCount);
                print("skipped hard links", h.SkippedHardLinkCount);
                print("moved files", h.MoveCount);
                print("junctions", h.JunctionCount);
                print("skipped junctions", h.SkippedJunctionCount);
                print("skipped tree count", h.SkippedTreeCount);
                print("error count", h.ErrorCount);
                print("duration", et);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.ToString());
            }
        }
    }
}
