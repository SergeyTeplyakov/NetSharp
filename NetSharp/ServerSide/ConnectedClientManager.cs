using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace NetSharp.ServerSide
{
    public class ConnectedClientManager
    {
        readonly Host host;
        readonly ConcurrentDictionary<Guid, ConnectedClient> clients;
        int clientCount;

        public int ClientCount => clientCount;

        public ConnectedClientManager(Host host)
        {
            clients = new ConcurrentDictionary<Guid, ConnectedClient>();
            this.host = host;
        }

        internal ConnectedClient CreateNewClient()
        {
            ConnectedClient client = new ConnectedClient(host);
            client.RemoveLastHandler += Client_RemoveLastHandler;

            clients.TryAdd(client.ID, client);

            Interlocked.Increment(ref clientCount);

            host.RaiseHostStateChange(this, ReasonChange.AddClient, client);

            return client;
        }

        private void Client_RemoveLastHandler(object sender, EventArgs e)
        {
            ConnectedClient client = (ConnectedClient)sender;

            clients.TryRemove(client.ID, out client);

            Interlocked.Decrement(ref clientCount);

            host.RaiseHostStateChange(this, ReasonChange.RemoveClient);
        }

        public void RemoveAll()
        {
            ConnectedClient client;
            Task[] tasks = new Task[clientCount];
            int i = 0;

            foreach (var key in clients.Keys)
            {
                clients.TryRemove(key, out client);
                tasks[i++] = client.CloseAsync();
            }

            clientCount = 0;

            Task.WaitAll(tasks);
        }

        public ConnectedClient GetClientByID(Guid clientID)
        {
            ConnectedClient client;

            clients.TryGetValue(clientID, out client);

            return client;
        }
    }
}