using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace de.intronik.hashcopy
{
    /// <summary>
    /// Provides the SHA1 info of a file, either from the cached value in the ADS stream
    /// or by calculating the data.
    /// </summary>    
    public class HashInfo
    {
        const string STREAM = @"de.Intronik.HashInfo"; // name of the ADS Stream used to store the cached hashed info
        const int SIZE = 8 + 8 + 20;
        public byte[] Hash { get; private set; }
        public DateTime LastWriteTimeUtc { get { return this.SourceFileInfo.LastWriteTimeUtc; } }
        public Int64 Length { get { return this.SourceFileInfo.Length; } }
        public static long CacheLimit = 4 << 10;

        public FileInfo SourceFileInfo { get; private set; }

        public HashInfo(FileInfo info, HashAlgorithm hashProvider)
        {
            // check if info is still valid
            this.SourceFileInfo = info;
            DateTime cachedLastWriteTime;
            long cachedLength;
            byte[] hash;
            if (this.Length > HashInfo.CacheLimit)
                ReadHashInfo(info.FullName, out cachedLastWriteTime, out cachedLength, out hash);
            else
            {
                hash = null;
                cachedLength = long.MinValue;
                cachedLastWriteTime = DateTime.MinValue;
            }
            if (hash == null || hash.Length != 20 || cachedLastWriteTime != this.LastWriteTimeUtc || cachedLength != this.Length)
            {
                using (var inputStream = File.OpenRead(info.FullName))
                    this.Hash = hash = hashProvider.ComputeHash(inputStream);
                if (this.Length > HashInfo.CacheLimit)
                    this.SaveHashInfo();
            }
            else
                this.Hash = hash;
        }

        static void ReadHashInfo(string filename, out DateTime lastWriteTime, out long length, out byte[] hash)
        {
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
                    lastWriteTime = new DateTime(r.ReadInt64());
                    length = r.ReadInt64();
                    hash = r.ReadBytes(20);
                }

            }
            catch (Exception)
            {
                hash = null;
                lastWriteTime = DateTime.MinValue;
                length = long.MinValue;
            }
        }

        void SaveHashInfo()
        {
            try
            {
                // remove RO attribute
                var ro = (this.SourceFileInfo.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly;
                if (ro)
                    File.SetAttributes(this.SourceFileInfo.FullName, SourceFileInfo.Attributes & (~FileAttributes.ReadOnly));
                using (var w = new BinaryWriter(new FileStream(Win32.CreateFile(this.SourceFileInfo.FullName + ":" + STREAM,
                    FileAccess.Write, FileShare.Write, IntPtr.Zero,
                    FileMode.Create, 0, IntPtr.Zero), FileAccess.Write)))
                {
                    w.Write(this.LastWriteTimeUtc.Ticks);
                    w.Write(this.Length);
                    w.Write(this.Hash);
                }
                // fix last write time, since the ADS write changes the value
                File.SetLastWriteTimeUtc(this.SourceFileInfo.FullName, this.LastWriteTimeUtc);
                // restore RO attribute
                if (ro)
                    File.SetAttributes(this.SourceFileInfo.FullName, this.SourceFileInfo.Attributes);
            }
            catch (Exception)
            {
            }
        }
    }
}
