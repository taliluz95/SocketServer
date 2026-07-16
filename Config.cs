using System.Configuration;
using System.Net;

namespace SocketServer
{
    /// <summary>Reads bind and remote IP/port from App.config.</summary>
    public class Config
    {
        public IPAddress BindIp { get; }
        public int BindPort { get; }
        public string RemoteIp { get; }
        public int RemotePort { get; }
        public bool VerboseLogging { get; }

        public Config()
        {
            BindIp = IPAddress.Parse(ConfigurationManager.AppSettings["BindIp"] ?? "0.0.0.0");
            BindPort = int.Parse(ConfigurationManager.AppSettings["BindPort"] ?? "9000");
            RemoteIp = ConfigurationManager.AppSettings["RemoteIp"] ?? "127.0.0.1";
            RemotePort = int.Parse(ConfigurationManager.AppSettings["RemotePort"] ?? "15779");
            VerboseLogging = bool.Parse(ConfigurationManager.AppSettings["VerboseLogging"] ?? "true");
        }
    }
}
