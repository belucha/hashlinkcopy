using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace de.intronik.backup
{
    /// <summary>
    /// HashEntry for a FileInfo object
    /// the hash is either calculated or read from an ADS data stream
    /// </summary>
    public class FileHashEntry : FileSystemHashEntry
    {
        const string STREAM = @"de.Intronik.HashInfo"; // name of the ADS Stream used to store the cached hashed info
        const int SIZE = 8 + 8 + 20;
        const long CacheLimit = 4 << 10;


        public DateTime LastWriteTimeUtc { get { return ((FileInfo)this.Info).LastWriteTimeUtc; } }
        public Int64 Length { get { return ((FileInfo)this.Info).Length; } }

        public FileHashEntry(FileInfo info, HashAlgorithm hashProvider, Action<long> hashFile, Action<long> hashFileDone)
            : base(info)
        {
            // check if info is still valid
            DateTime cachedLastWriteTime;
            long cachedLength;
            byte[] hash;
            if (this.Length > CacheLimit)
                ReadHashInfo(info.FullName, out cachedLastWriteTime, out cachedLength, out hash);
            else
            {
                hash = null;
                cachedLength = long.MinValue;
                cachedLastWriteTime = DateTime.MinValue;
            }
            if (hash == null || hash.Length != 20 || cachedLastWriteTime != this.LastWriteTimeUtc || cachedLength != this.Length)
            {
                if (hashFile != null)
                    hashFile(info.Length);
                using (var inputStream = File.OpenRead(info.FullName))
                    this.Hash = hash = hashProvider.ComputeHash(inputStream);
                if (hashFileDone != null)
                    hashFileDone(info.Length);
                if (this.Length > CacheLimit)
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
                var ro = (this.Info.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly;
                if (ro)
                    File.SetAttributes(this.Info.FullName, Info.Attributes & (~FileAttributes.ReadOnly));
                using (var w = new BinaryWriter(new FileStream(Win32.CreateFile(this.Info.FullName + ":" + STREAM,
                    FileAccess.Write, FileShare.Write, IntPtr.Zero,
                    FileMode.Create, 0, IntPtr.Zero), FileAccess.Write)))
                {
                    w.Write(this.LastWriteTimeUtc.Ticks);
                    w.Write(this.Length);
                    w.Write(this.Hash);
                }
                // fix last write time, since the ADS write changes the value
                File.SetLastWriteTimeUtc(this.Info.FullName, this.LastWriteTimeUtc);
                // restore RO attribute
                if (ro)
                    File.SetAttributes(this.Info.FullName, this.Info.Attributes);
            }
            catch (Exception)
            {
            }
        }
    }
}
