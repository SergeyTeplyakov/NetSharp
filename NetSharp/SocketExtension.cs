using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetSharp
{
    static class SocketExtension
    {
        public static Task ConnectTaskAsync(this Socket socket, IPEndPoint remoteEP)
        {
            return Task.Factory.FromAsync(socket.BeginConnect, socket.EndConnect, remoteEP, null);
        }

        public static Task<Socket> AcceptTaskAsync(this Socket socket)
        {
            return Task<Socket>.Factory.FromAsync(socket.BeginAccept, socket.EndAccept, null);
        }

        public static Task<int> SendTaskAsync(this Socket socket, byte[] buffer, int offset, int size, SocketFlags flags)
        {
            return Task<int>.Factory.FromAsync((ac, state) => socket.BeginSend(buffer, offset, size, flags, ac, state), socket.EndSend, null);
        }

        public static Task<int> ReceiveTaskAsync(this Socket socket, byte[] buffer, int offset, int size, SocketFlags flags)
        {
            return Task<int>.Factory.FromAsync((ac, state) => socket.BeginReceive(buffer, offset, size, flags, ac, state), socket.EndReceive, null);
        }
    }
}
