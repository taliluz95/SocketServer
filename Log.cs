using System;

namespace SocketServer
{
    /// <summary>Basic thread-safe console logger.</summary>
    public static class Log
    {
        static readonly object s_lock = new object();

        public static void Info(string msg) => Write(msg, ConsoleColor.Gray);
        public static void Warn(string msg) => Write(msg, ConsoleColor.Yellow);
        public static void Error(string msg) => Write(msg, ConsoleColor.Red);

        static void Write(string msg, ConsoleColor color)
        {
            lock (s_lock)
            {
                Console.ForegroundColor = color;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msg}");
                Console.ResetColor();
            }
        }
    }
}
