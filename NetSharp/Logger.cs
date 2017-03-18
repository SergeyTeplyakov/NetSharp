using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetSharp
{
    public enum Source { Client, Server, Lib }

    public static class Logger
    {
        static Action<DateTime, Source, string, Exception, Data> logWriter;

        public static Action<DateTime, Source, string, Exception, Data> LogWriter
        {
            get
            {
                return logWriter;
            }
            set
            {
                if (value == null)
                    throw new ArgumentNullException("value");

                logWriter = value;
            }
        }

        internal static void Write(Source source, string message, Exception ex = null, Data data = null)
        {
            logWriter?.Invoke(DateTime.Now, source, message, ex, data);
        }
    }

    public class Data
    {
        Dictionary<string, bool> changeFields;

        public string HandlerName { get; private set; }
        public Guid ClientID { get; private set; }
        public Guid HandlerID { get; private set; }

        private Data()
        {
            changeFields = new Dictionary<string, bool>();
        }

        internal static Data Create()
        {
            return new Data();
        }

        internal Data SetHandlerID(Guid handlerID)
        {
            changeFields[nameof(HandlerID)] = true;
            HandlerID = handlerID;
            return this;
        }

        internal Data SetClientID(Guid clientID)
        {
            changeFields[nameof(ClientID)] = true;
            ClientID = clientID;
            return this;
        }

        internal Data SetHandlerName(string handlerName)
        {
            changeFields[nameof(HandlerName)] = true;
            HandlerName = handlerName;
            return this;
        }

        public override string ToString()
        {
            bool isSet;
            StringBuilder sb = new StringBuilder();

            if (changeFields.TryGetValue(nameof(ClientID), out isSet) && isSet)
                sb.AppendLine($"{nameof(ClientID)}: {ClientID}");

            if (changeFields.TryGetValue(nameof(HandlerID), out isSet) && isSet)
                sb.AppendLine($"{nameof(HandlerID)}: {HandlerID}");

            if (changeFields.TryGetValue(nameof(HandlerName), out isSet) && isSet)
                sb.Append($"{nameof(HandlerName)}: {HandlerName}");

            return sb.ToString();
        }

        internal Data SetHandlerData(Guid id, string name)
        {
            SetHandlerID(id);
            SetHandlerName(name);
            return this;
        }
    }
}