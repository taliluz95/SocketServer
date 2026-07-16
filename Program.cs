using System;
using System.Collections.Generic;
using System.Threading;

namespace SocketServer
{
    class Program
    {
        static Config s_cfg;

        static void Main()
        {
            // CLR thread pool starts small and only grows ~1 thread/sec; every SAEA completion
            // runs on it, so a burst of connections would otherwise stall waiting for it to ramp up.
            ThreadPool.SetMinThreads(256, 256);

            s_cfg = new Config();
            var listener = new Listener();

            listener.Accepted += OnAccepted;
            listener.AcceptError += ex => Log.Error($"Accept error: {ex.Message}");
            listener.Start(s_cfg.BindIp, s_cfg.BindPort);

            Log.Info($"Listening on {s_cfg.BindIp}:{s_cfg.BindPort}, forwarding to {s_cfg.RemoteIp}:{s_cfg.RemotePort} - press any key to stop.");
            Console.ReadKey(true);
            listener.Stop();
        }

        static void OnAccepted(Connection client)
        {
            string clientEp = client.Socket.RemoteEndPoint.ToString();
            Log.Info($"Client connected: {clientEp}");

            // Connector.Connect is async, but Listener starts client's receive loop right away -
            // buffer anything that arrives before the remote is ready so it isn't lost.
            var pending = new List<byte[]>();
            client.Received += (c, buf, off, len) => pending.Add(Slice(buf, off, len));

            Connector.Connect(s_cfg.RemoteIp, s_cfg.RemotePort,
                remote =>
                {
                    string remoteEp = remote.Socket.RemoteEndPoint.ToString();
                    Log.Info($"Forwarding to {remoteEp}");
                    foreach (var data in pending) remote.Send(data);
                    pending.Clear();

                    client.Received += (c, buf, off, len) =>
                    {
                        if (s_cfg.VerboseLogging) Log.Info($"[C->P] recv {len}B {clientEp}");
                        var data = Slice(buf, off, len);
                        if (s_cfg.VerboseLogging) Log.Info($"[C->P] send {data.Length}B {remoteEp}");
                        remote.Send(data);
                    };
                    remote.Received += (c, buf, off, len) =>
                    {
                        if (s_cfg.VerboseLogging) Log.Info($"[P->C] recv {len}B {remoteEp}");
                        var data = Slice(buf, off, len);
                        if (s_cfg.VerboseLogging) Log.Info($"[P->C] send {data.Length}B {clientEp}");
                        client.Send(data);
                    };
                    client.Disconnected += c =>
                    {
                        Log.Warn($"Client disconnected: {clientEp}");
                        remote.RequestClose();
                    };
                    remote.Disconnected += c =>
                    {
                        Log.Warn($"Remote disconnected: {remoteEp}");
                        client.RequestClose();
                    };
                },
                err =>
                {
                    Log.Error($"Could not reach {s_cfg.RemoteIp}:{s_cfg.RemotePort}: {err}");
                    client.Close();
                });
        }

        static byte[] Slice(byte[] buf, int off, int len)
        {
            var copy = new byte[len];
            Buffer.BlockCopy(buf, off, copy, 0, len);
            return copy;
        }
    }
}
