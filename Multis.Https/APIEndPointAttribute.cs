using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multis.Https
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public class EndPointPathAttribute(string path, bool log = false, string log_message = "") : Attribute
    {
        public string EntirePath { get; set; } = "";
        public string LocalPath { get; } = path;
        public bool ShouldLog { get; } = log;
        private string LogMessage { get; } = log_message;

        public string GetLogMessage()
        {
            if(LogMessage == "")
            {
                return $"Registered endpoint: {EntirePath}";
            }
            return LogMessage;
        }

        public EndPointPathAttribute(string path, string logMessage) : this(path, true, logMessage)
        {
        }
    }
}
