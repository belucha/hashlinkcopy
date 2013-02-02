using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.backup
{
    /// <summary>
    /// HashEntry for a FileInfo object
    /// the hash is either calculated or read from an ADS data stream
    /// </summary>
    public class FileHashEntry : HashEntry
    {
        const string STREAM = @"de.Intronik.HashInfo"; // name of the ADS Stream used to store the cached hashed info
        const int SIZE = 8 + 8 + 20;
        const long CacheLimit = 4 << 10;
        byte[] _cachedHash;

        protected override bool GetIsDirectory() { return false; }
        protected override byte[] GetHash() { return this._cachedHash; }

        public FileInfo Info { get; private set; }
        public DateTime LastWriteTimeUtc { get { return this.Info.LastWriteTimeUtc; } }
        public Int64 Length { get { return this.Info.Length; } }
        public string FullName { get { return this.Info.FullName; } }

        public enum HashAction
        {
            CacheHit,
            CalcStart,
            CalcEnd,
        };

        public delegate void HashActionDelegate(HashAction action, FileInfo file, long processedBytes);

        public FileHashEntry(FileInfo info, HashActionDelegate action)
        {
            this.Info = info;
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
                if (action != null)
                    action(HashAction.CalcStart, info, 0);
                using (var inputStream = File.OpenRead(info.FullName))
                    this._cachedHash = hash = HashProvider.ComputeHash(inputStream);
                if (action != null)
                    action(HashAction.CalcEnd, info, 0);
                if (this.Length > CacheLimit)
                    this.SaveHashInfo();
            }
            else
            {
                this._cachedHash = hash;
                if (action != null)
                    action(HashAction.CacheHit, info, cachedLength);
            }
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
                    File.SetAttributes(this.Info.FullName, this.Info.Attributes & (~FileAttributes.ReadOnly));
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
