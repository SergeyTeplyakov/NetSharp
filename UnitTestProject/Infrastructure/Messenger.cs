using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleMessenger
{
    interface IMessenger : IDisposable
    {
        void Send(byte[] data, string recipient);
        Task<byte[]> Receive();
    }

    static class Hub
    {
        static ConcurrentDictionary<string, Action<byte[]>> messengersNotify = new ConcurrentDictionary<string, Action<byte[]>>();

        public static event Action<IPEndPoint, string, string> Connected;

        public static void OnConnected(IPEndPoint endPoint, string localUniqueName, string remoteUniqueName)
        {
            Action<IPEndPoint, string, string> temp = Volatile.Read(ref Connected);

            if (temp != null)
            {
                temp(endPoint, localUniqueName, remoteUniqueName);
            }
        }

        static void Send(Message msg)
        {
            Action<byte[]> notify;

            messengersNotify.TryGetValue(msg.Recipient, out notify);

            notify(msg.Data);
        }

        static void NotifyRegister(string messangerName, Action<byte[]> notify)
        {
            messengersNotify.TryAdd(messangerName, notify);
        }

        static void NotifyUnregister(string messangerName)
        {
            Action<byte[]> notify;
            messengersNotify.TryRemove(messangerName, out notify);
        }

        public static IMessenger CreateMessanger(string uniqueName)
        {
            return new Messenger(uniqueName);
        }

        public class Messenger : IMessenger
        {
            string uniqueName;
            BlockingCollection<byte[]> messages;

            public Messenger(string uniqueName)
            {
                messages = new BlockingCollection<byte[]>();
                this.uniqueName = uniqueName;

                NotifyRegister(uniqueName, this.Notify);
            }

            void Notify(byte[] data)
            {
                messages.Add(data);
            }

            public void Send(byte[] data, string recipient)
            {
                Hub.Send(new Message(data, recipient));
            }

            public async Task<byte[]> Receive()
            {
                return await Task<byte[]>.Factory.StartNew(() =>
                {
                    byte[] data = messages.Take();
                    return data;
                });
            }

            public void Dispose()
            {
                NotifyUnregister(uniqueName);
                messages.Dispose();
            }
        }
    }

    public class Message
    {
        public string Recipient { get; private set; }
        public byte[] Data { get; private set; }

        public Message(byte[] data, string recipient)
        {
            Data = data;
            Recipient = recipient;
        }
    }
}
