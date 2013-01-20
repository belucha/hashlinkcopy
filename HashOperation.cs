using System;
using System.IO;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.backup
{
    public abstract class HashOperation : BaseOperation
    {
        public DateTime StartTime { get; protected set; }
        public DateTime EndTime { get; protected set; }
        public TimeSpan Duration { get { return this.EndTime.Subtract(this.StartTime); } }
        public long ErrorCount { get; private set; }
        [DefaultValue("")]
        public string HashFolder { get; set; }

        [Description("Number of directory levels to print")]
        [DefaultValue(0)]
        public uint Tree { get; set; }

        protected virtual void HandleError(FileSystemInfo info, Exception exception)
        {
            unchecked { this.ErrorCount++; }
            Console.WriteLine("\"{1}\": {0}, Message: \"{2}\"", exception.GetType().Name, info.FullName, exception.Message);
        }

        public override void PreRun()
        {
            base.PreRun();
            print("Hash dir", this.HashFolder);
        }
        
        public override void ShowStatistics()
        {
            base.ShowStatistics();
            print("Start", this.StartTime);
            print("End", this.EndTime);
            print("Duration", this.EndTime.Subtract(this.StartTime));
            print("ErrorCount", this.ErrorCount);
        }
    }
}
