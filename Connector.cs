using System;
using System.Net;
using System.Net.Sockets;

namespace SocketServer
{
    /// <summary>SAEA-based outbound TCP connector, used when the proxy dials out to the real servers.</summary>
    public static class Connector
    {
        public static void Connect(string host, int port, Action<Connection> onConnected, Action<SocketError> onFailed)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var e = new SocketAsyncEventArgs { RemoteEndPoint = new DnsEndPoint(host, port) };
            e.Completed += (s, args) => Complete(socket, args, onConnected, onFailed);

            bool pending;
            try { pending = socket.ConnectAsync(e); }
            catch (Exception) { onFailed(SocketError.SocketError); return; }
            if (!pending) Complete(socket, e, onConnected, onFailed);
        }

        static void Complete(Socket socket, SocketAsyncEventArgs e, Action<Connection> onConnected, Action<SocketError> onFailed)
        {
            if (e.SocketError == SocketError.Success)
            {
                var connection = new Connection(socket);
                onConnected(connection);
                connection.Start();
            }
            else onFailed(e.SocketError);
        }
    }
}
