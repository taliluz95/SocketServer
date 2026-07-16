using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;

namespace SocketServer
{
    /// <summary>A single TCP connection driven by SocketAsyncEventArgs (SAEA) - no per-I/O
    /// allocation, unlike classic Begin/End sockets.</summary>
    public class Connection
    {
        public Socket Socket { get; }
        public object Tag { get; set; }

        public event Action<Connection, byte[], int, int> Received;
        public event Action<Connection> Disconnected;

        readonly SocketAsyncEventArgs _recvArgs;
        readonly SocketAsyncEventArgs _sendArgs;
        readonly byte[] _recvBuffer;
        readonly ConcurrentQueue<byte[]> _sendQueue = new ConcurrentQueue<byte[]>();
        int _sending;
        int _closing; // 1 = RequestClose() called, draining queue
        int _closed;
        Timer _graceTimer;

        public Connection(Socket socket, int bufferSize = BufferPool.BufferSize)
        {
            Socket = socket;
            Socket.NoDelay = true;

            _recvBuffer = bufferSize == BufferPool.BufferSize ? BufferPool.Rent() : new byte[bufferSize];
            _recvArgs = new SocketAsyncEventArgs();
            _recvArgs.SetBuffer(_recvBuffer, 0, _recvBuffer.Length);
            _recvArgs.Completed += IoCompleted;

            _sendArgs = new SocketAsyncEventArgs();
            _sendArgs.Completed += IoCompleted;
        }

        /// <summary>Begins the receive loop.</summary>
        public void Start() => StartReceive();

        void StartReceive()
        {
            if (Volatile.Read(ref _closed) == 1) return;
            bool pending;
            try { pending = Socket.ReceiveAsync(_recvArgs); }
            catch (ObjectDisposedException) { return; }
            if (!pending) ProcessReceive(_recvArgs);
        }

        void IoCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.LastOperation == SocketAsyncOperation.Receive) ProcessReceive(e);
            else if (e.LastOperation == SocketAsyncOperation.Send) ProcessSend(e);
        }

        void ProcessReceive(SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success || e.BytesTransferred == 0)
            {
                // Last completion that will touch _recvBuffer - safe to return here.
                if (_recvBuffer.Length == BufferPool.BufferSize) BufferPool.Return(_recvBuffer);
                Close();
                return;
            }
            Received?.Invoke(this, e.Buffer, e.Offset, e.BytesTransferred);
            StartReceive();
        }

        /// <summary>Queues bytes to send. Thread-safe.</summary>
        public void Send(byte[] data)
        {
            if (data == null || data.Length == 0) return;
            if (Volatile.Read(ref _closed) == 1 || Volatile.Read(ref _closing) == 1) return;
            _sendQueue.Enqueue(data);
            if (Interlocked.CompareExchange(ref _sending, 1, 0) == 0) SendNext();
        }

        void SendNext()
        {
            if (!_sendQueue.TryDequeue(out byte[] data))
            {
                Volatile.Write(ref _sending, 0);
                // Re-check: item may have queued right as we cleared the flag.
                if (!_sendQueue.IsEmpty && Interlocked.CompareExchange(ref _sending, 1, 0) == 0) { SendNext(); return; }
                // Empty now - finish a pending graceful close.
                if (Volatile.Read(ref _closing) == 1) Close();
                return;
            }
            _sendArgs.SetBuffer(data, 0, data.Length);
            bool pending;
            try { pending = Socket.SendAsync(_sendArgs); }
            catch (ObjectDisposedException) { return; }
            if (!pending) ProcessSend(_sendArgs);
        }

        void ProcessSend(SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success) { Close(); return; }
            SendNext();
        }

        /// <summary>Graceful close: stops new sends, flushes the queue, then closes. Hard-closes
        /// after <paramref name="graceMs"/> if the peer never drains.</summary>
        public void RequestClose(int graceMs = 2000)
        {
            if (Interlocked.CompareExchange(ref _closing, 1, 0) != 0) return;
            _graceTimer = new Timer(_ => Close(), null, graceMs, Timeout.Infinite);
            if (Volatile.Read(ref _sending) == 0 && _sendQueue.IsEmpty) Close();
        }

        public void Close()
        {
            if (Interlocked.Exchange(ref _closed, 1) == 1) return;
            _graceTimer?.Dispose();
            try { Socket.Shutdown(SocketShutdown.Both); } catch { }
            Socket.Close();
            Disconnected?.Invoke(this);
        }
    }
}
