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
                this.Hash = hash = Monitor.Root.HashFile(hashProvider, info.FullName, this.Length);
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
            catch (Exception error)
            {
                Logger.Root.WriteLine(Verbosity.Debug, "Reading cached SHA1 of {0} failed with {1}:{2}", filename, error.GetType().Name, error.Message);
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
            catch (Exception error)
            {
                Logger.Root.Warning("Saving HashInfo of '{0}' failed with {1}:{2}", this.SourceFileInfo.FullName, error.GetType().Name, error.Message);
            }
        }

        /// <summary>
        /// Returns the has
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string CheckAndCorrectHashPath(string path)
        {
            // entferne alle pfadzeichen und prüfe
            var cleaned = path.Where(c => c != '\\').ToArray();
            if (cleaned.Length != 40 || cleaned.Any(c => (!Char.IsDigit(c)) && (Char.ToLower(c) > 'f' || Char.ToLower(c) < 'a'))) return null;
            // it is a valid has path, check if the folder separators are at the correct position
            if (path[2] == '\\' && path[5] == '\\' && path.Length == 42) return "";
            var b = new StringBuilder(new String(cleaned), 42);
            b.Insert(2, '\\');
            b.Insert(5, '\\');
            return b.ToString();
        }

        /// <summary>
        /// Returns an interned lower hase path to the hash file
        /// suiteabel for lock
        /// </summary>
        /// <param name="basePath"></param>
        /// <returns></returns>
        public string GetHashPath(string basePath)
        {
            var s = new StringBuilder(basePath, basePath.Length + 40 + 2);
            for (var i = 0; i < 20; i++)
            {
                var b = this.Hash[i];
                var nibble = b >> 4;
                s.Append((Char)(nibble < 10 ? '0' + nibble : ('a' + nibble - 10)));
                nibble = b & 0xF;
                s.Append((Char)(nibble < 10 ? '0' + nibble : ('a' + nibble - 10)));
                if (i < 2)
                    s.Append('\\');
            }
            return String.Intern(s.ToString());
        }
    }
}
