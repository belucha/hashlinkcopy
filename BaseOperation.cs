using System;
using System.Globalization;
using System.Reflection;
using System.ComponentModel;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace de.intronik.backup
{
    public abstract class BaseOperation : ICommand
    {
        protected static string FormatBytes(long bytes)
        {
            var units = new string[] { "Byte", "Kb", "Mb", "Gb", };
            if (bytes < 1024)
                return String.Format("{0}{1}", bytes, units[0]);
            var b = new StringBuilder();
            for (var p = units.Length - 1; p >= 0; p--)
            {
                var c = (bytes >> (p * 10)) & 1023;
                if (c > 0)
                    b.AppendFormat("{0}{1} ", c, units[p]);
            }
            return b.ToString();
        }

        public DateTime StartTime { get; protected set; }
        public DateTime EndTime { get; protected set; }
        public TimeSpan Duration { get { return this.EndTime.Subtract(this.StartTime); } }

        protected string[] parameters;
        private bool setParams;

        public string[] Parameters
        {
            get { return this.parameters; }
            set
            {
                this.parameters = value;
                if (setParams) return;
                try
                {
                    setParams = true;
                    this.OnParametersChanged();
                }
                finally
                {
                    setParams = false;
                }
            }
        }

        protected virtual void OnParametersChanged()
        {
        }

        public int Run()
        {
            this.StartTime = DateTime.Now;
            var res = this.DoOperation();
            this.EndTime = DateTime.Now;
            this.ShowStatistics();
            return res;
        }

        public virtual void ShowStatistics()
        {
            print("Start", this.StartTime);
            print("End", this.EndTime);
            print("Duration", this.EndTime.Subtract(this.StartTime));
        }

        protected abstract int DoOperation();

        protected void print(string name, object value)
        {
            Console.WriteLine("{0,-20}: {1}", name, value);
        }

        public string PromptInput(string prompt, params object[] args)
        {
            return PromptInput(null, prompt, args);
        }

        public string PromptInput(Func<string, bool> inputOkPredicate, string prompt, params object[] args)
        {
            while (true)
            {
                Console.Write(String.Format(prompt, args));
                var answer = Console.ReadLine().Trim();
                if (inputOkPredicate == null || inputOkPredicate(answer))
                    return answer;
            }
        }
    }
}
