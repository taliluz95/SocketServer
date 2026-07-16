using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace SocketServer
{
    /// <summary>SAEA-based TCP listener. Raises <see cref="Accepted"/> for every incoming connection.</summary>
    public class Listener
    {
        readonly Socket _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        public event Action<Connection> Accepted;

        /// <summary>Raised when AcceptAsync itself throws - not a per-connection error, a failure
        /// to keep accepting at all.</summary>
        public event Action<Exception> AcceptError;

        /// <param name="backlog">OS pending-connection queue size.</param>
        /// <param name="concurrentAccepts">AcceptAsync ops kept outstanding at once, for parallel
        /// accepts under connection bursts.</param>
        public void Start(IPAddress address, int port, int backlog = 1024, int concurrentAccepts = 8)
        {
            _socket.Bind(new IPEndPoint(address, port));
            _socket.Listen(backlog);

            for (int i = 0; i < concurrentAccepts; i++)
            {
                var e = new SocketAsyncEventArgs();
                e.Completed += (s, args) => OnAccept(args);
                AcceptNext(e);
            }
        }

        public void Stop() { try { _socket.Close(); } catch { } }

        void AcceptNext(SocketAsyncEventArgs e)
        {
            e.AcceptSocket = null;
            bool pending;
            try
            {
                pending = _socket.AcceptAsync(e);
            }
            catch (ObjectDisposedException)
            {
                return; // listener was stopped deliberately - not an error
            }
            catch (Exception ex)
            {
                AcceptError?.Invoke(ex);
                // Retry shortly rather than losing this accept slot permanently.
                new Timer(_ => AcceptNext(e), null, 250, Timeout.Infinite);
                return;
            }
            if (!pending) OnAccept(e);
        }

        void OnAccept(SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                var connection = new Connection(e.AcceptSocket);
                Accepted?.Invoke(connection);
                connection.Start();
            }
            AcceptNext(e);
        }
    }
}
