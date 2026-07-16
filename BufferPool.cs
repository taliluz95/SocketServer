using System.Collections.Concurrent;

namespace SocketServer
{
    /// <summary>Reusable pool of fixed-size receive buffers, avoiding per-connection allocation.</summary>
    public static class BufferPool
    {
        public const int BufferSize = 8192;
        static readonly ConcurrentBag<byte[]> s_pool = new ConcurrentBag<byte[]>();

        public static byte[] Rent() => s_pool.TryTake(out var buffer) ? buffer : new byte[BufferSize];

        public static void Return(byte[] buffer)
        {
            if (buffer != null && buffer.Length == BufferSize) s_pool.Add(buffer);
        }
    }
}
