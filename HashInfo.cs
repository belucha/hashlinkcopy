using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace de.intronik.hashlinkcopy
{
    /// <summary>
    /// Provides the SHA1 info of a file, either from the cached value in the ADS stream
    /// or by calculating the data.
    /// </summary>    
    public class HashInfo
    {
        static SHA1 hashProvider = SHA1.Create();
        const string STREAM = @"de.Intronik.HashInfo"; // name of the ADS Stream used to store the cached hashed info
        const int SIZE = 8 + 8 + 20;
        public byte[] Hash { get; private set; }
        public DateTime LastWriteTimeUtc { get; private set; }
        public Int64 Length { get; private set; }
        public static long CacheLimit = 4 << 10;
        public HashInfo(string filename)
        {
            // check if info is still valid
            var i = new System.IO.FileInfo(filename);
            if (i.Length > HashInfo.CacheLimit)
                try
                {
                    using (var r = new BinaryReader(new FileStream(Win32.CreateFile(filename + ":" + STREAM,
                            FileAccess.Read,
                            FileShare.Read,
                            IntPtr.Zero,
                            FileMode.Open,
                            0,
                            IntPtr.Zero), FileAccess.Read)))
                    {
                        this.LastWriteTimeUtc = new DateTime(r.ReadInt64());
                        this.Length = r.ReadInt64();
                        this.Hash = r.ReadBytes(20);
                    }
                }
                catch (Exception error)
                {
                    Logger.WriteLine(Logger.Verbosity.Debug, "Reading cached SHA1 of {0} failed with {1}:{2}", filename, error.GetType().Name, error.Message);
                }
            else
            {
                // the file is small in size
                this.Length = i.Length;
                this.LastWriteTimeUtc = i.LastWriteTimeUtc;
            }
            if (this.Hash == null || this.Hash.Length != 20 || this.LastWriteTimeUtc != i.LastWriteTimeUtc || this.Length != i.Length)
            {
                Monitor.HashFile(filename, i.Length);
                using (var inputStream = File.OpenRead(filename))
                    this.Hash = hashProvider.ComputeHash(inputStream);
                if (i.Length > HashInfo.CacheLimit)
                {
                    this.LastWriteTimeUtc = i.LastWriteTimeUtc;
                    this.Length = i.Length;
                    try
                    {
                        // remove RO attribute
                        if ((i.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                            File.SetAttributes(filename, i.Attributes & (~FileAttributes.ReadOnly));
                        using (var w = new BinaryWriter(new FileStream(Win32.CreateFile(filename + ":" + STREAM,
                            FileAccess.Write, FileShare.Write, IntPtr.Zero,
                            FileMode.Create, 0, IntPtr.Zero), FileAccess.Write)))
                        {
                            w.Write(this.LastWriteTimeUtc.Ticks);
                            w.Write(this.Length);
                            w.Write(this.Hash);
                        }
                        // fix last write time, since the ADS write changes the value
                        File.SetLastWriteTimeUtc(filename, this.LastWriteTimeUtc);
                        // restore RO attribute
                        if ((i.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                            File.SetAttributes(filename, i.Attributes);
                    }
                    catch (Exception error)
                    {
                        Logger.Warning("Saving HashInfo of '{0}' failed with {1}:{2}", filename, error.GetType().Name, error.Message);
                    }
                }
            }
        }

        /// <summary>
        /// returns true, when the file is a valid hash char
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool IsValidHashPath(string path)
        {
            return path.ToCharArray().Reverse().Where(c => c != '\\' && c != '/').Take(40).All(c => Char.IsDigit(c) || (Char.ToLower(c) <= 'f' && Char.ToLower(c) >= 'a'));
        }

        public string GetHashPath(string basePath)
        {
            var s = new StringBuilder(String.Concat(this.Hash.Select(b => b.ToString("x2")).ToArray()));
            // 0123456789012345678901234567890123456789
            // 0         1         2         3
            // a78733087ab883cf8923ca893123affbcd770012
            s.Insert(40 - (5), '\\');
            s.Insert(40 - (10), '\\');
            s.Insert(40 - (15), '\\');
            s.Insert(40 - (20), '\\');
            s.Insert(40 - (23), '\\');
            s.Insert(40 - (26), '\\');
            s.Insert(40 - (29), '\\');
            s.Insert(40 - (32), '\\');
            s.Insert(40 - (34), '\\');
            s.Insert(40 - (36), '\\');
            s.Insert(40 - (38), '\\');
            // we should a obtain a value hashgrouping of
            // a7\87\33\08\7ab\883\cf8\923\ca893\123af\fbcd7\70012
            return Path.Combine(basePath, s.ToString());
        }
    }
}
