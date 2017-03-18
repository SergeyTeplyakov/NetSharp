using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetSharp
{
    public class ReconnectManager
    {
        ConcurrentDictionary<Guid, WaitingConnectionObject> waitingConnectionObjectsMap;

        public ReconnectManager()
        {
            waitingConnectionObjectsMap = new ConcurrentDictionary<Guid, WaitingConnectionObject>();
        }

        public async Task<Socket> AddRequestToReconnect(Guid handlerID)
        {
            using (WaitingConnectionObject wco = new WaitingConnectionObject())
            {
                waitingConnectionObjectsMap.TryAdd(handlerID, wco);

                await wco.BeginWaiting().ConfigureAwait(false);

                WaitingConnectionObject tmpWco;

                waitingConnectionObjectsMap.TryRemove(handlerID, out tmpWco);

                return wco.ConnectedSocket;
            }
        }

        public bool ProcessNewConnection(Guid handlerID, Socket connectedSocket)
        {
            WaitingConnectionObject wco;

            if (waitingConnectionObjectsMap.TryGetValue(handlerID, out wco))
            {
                wco.StopWaiting(connectedSocket);
                return true;
            }

            return false;
        }
    }

    class WaitingConnectionObject : IDisposable
    {
        CancellationTokenSource cts;
        TimeSpan time;

        public Socket ConnectedSocket { get; private set; }

        public WaitingConnectionObject()
        {
            cts = new CancellationTokenSource();
            time = TimeSpan.FromMinutes(Settings.TIME_WAITING_RECONNECT);
        }

        public async Task BeginWaiting()
        {
            try
            {
                await Task.Delay(time, cts.Token).ConfigureAwait(false);
            }
            catch(TaskCanceledException)
            {

            }
        }

        public void StopWaiting(Socket connectedSocket)
        {
            ConnectedSocket = connectedSocket;
            cts.Cancel(true);
        }

        public void Dispose()
        {
            cts.Dispose();
        }
    }
}
