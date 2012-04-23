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

        public string GetHashPath(string basePath)
        {
            /*            var b = new StringBuilder(basePath, basePath.Length + hash.Length * 2 / 4 * 5);
                        for (var i = 0; i < hash.Length / 4; i++)
                            b.AppendFormat("\\{0:x2}{1:x2}", hash[i * 2 + 0], hash[i * 2 + 1]);
                        return b.ToString(); 
             */
            return Path.Combine(basePath, String.Join(@"\", this.Hash.Select(b => b.ToString("x2")).ToArray()));
        }
    }
}
