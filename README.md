# SocketServer

A lightweight, high-performance TCP socket library for .NET Framework, built directly on
`SocketAsyncEventArgs` (SAEA) instead of the classic Begin/End (APM) pattern - no per-I/O
allocation on the hot path, and built to scale to thousands of concurrent connections.

## Introduction

*(to be completed)*

## Features

- **SAEA-based I/O** - each connection reuses one recv args and one send args for its entire
  lifetime, avoiding per-operation allocation.
- **Pooled receive buffers** - `BufferPool` reuses fixed-size buffers across connect/disconnect
  cycles instead of allocating fresh arrays.
- **Graceful and hard close** - `RequestClose()` drains any queued sends before closing, with a
  configurable grace-period fallback for unresponsive peers.
- **Resilient accept loop** - transient `AcceptAsync` failures are retried automatically rather
  than silently losing accept capacity.
- **Outbound connections** - `Connector` dials out asynchronously using the same SAEA model, for
  building proxies/relays as well as listen-only servers.
- **Thread-safe sends** - `Connection.Send()` can be called from any thread.

## Requirements

- .NET Framework 4.8
- C# `latest` language version

## Getting Started

The included `Program.cs` is a minimal TCP proxy/relay demo: it listens on `BindIp:BindPort` and
forwards every accepted connection to `RemoteIp:RemotePort`, configured via `App.config`:

```xml
<appSettings>
  <add key="BindIp" value="127.0.0.1" />
  <add key="BindPort" value="13779" />
  <add key="RemoteIp" value="25.45.33.235" />
  <add key="RemotePort" value="15779" />
  <add key="VerboseLogging" value="true" />
</appSettings>
```

Build and run:

```
dotnet build
dotnet bin/Debug/net48/SocketServer.dll
```

## Consuming as a Library

Add a reference to `SocketServer.dll` (or the project itself) from your own application - the
library has no dependency on `Program.cs`/`Config.cs`, which are only the bundled demo.

```csharp
using System;
using System.Net;
using SocketServer;

class EchoServer
{
    static void Main()
    {
        var listener = new Listener();

        listener.Accepted += OnClientConnected;
        listener.AcceptError += ex => Console.WriteLine($"Accept error: {ex.Message}");

        listener.Start(IPAddress.Any, 9000);
        Console.WriteLine("Echo server listening on port 9000 - press any key to stop.");
        Console.ReadKey(true);

        listener.Stop();
    }

    static void OnClientConnected(Connection client)
    {
        Console.WriteLine($"Client connected: {client.Socket.RemoteEndPoint}");

        client.Received += (conn, buffer, offset, length) =>
        {
            // Echo whatever was received straight back.
            var data = new byte[length];
            Buffer.BlockCopy(buffer, offset, data, 0, length);
            conn.Send(data);
        };

        client.Disconnected += conn =>
            Console.WriteLine($"Client disconnected: {conn.Socket.RemoteEndPoint}");

        client.Start();
    }
}
```

### Dialing out with `Connector`

```csharp
Connector.Connect("example.com", 443,
    onConnected: connection =>
    {
        connection.Received += (conn, buffer, offset, length) => { /* handle response */ };
        connection.Start();
        connection.Send(requestBytes);
    },
    onFailed: error => Console.WriteLine($"Connect failed: {error}"));
```

### API at a glance

| Type | Purpose |
|---|---|
| `Listener` | Accepts inbound TCP connections. `Start(address, port, backlog, concurrentAccepts)` / `Stop()`. |
| `Connector` | Dials outbound TCP connections asynchronously. `Connect(host, port, onConnected, onFailed)`. |
| `Connection` | One TCP connection. `Send(byte[])`, `RequestClose()`, `Close()`, `Received`/`Disconnected` events. |
| `BufferPool` | Shared pool of reusable 8KB receive buffers. |
| `Log` | Basic console logger (`Info`/`Warn`/`Error`) - used by the demo, not required by the library. |
| `Config` | Reads `App.config` settings - only used by the demo. |

## Project Structure

```
SocketServer/
├── Connection.cs      # A single SAEA-driven TCP connection
├── Listener.cs         # Inbound connection acceptor
├── Connector.cs         # Outbound connection dialer
├── BufferPool.cs         # Reusable receive buffer pool
├── Log.cs                 # Basic console logger (demo)
├── Config.cs                # App.config reader (demo)
├── Program.cs                 # Demo entry point - a minimal TCP relay
├── App.config                   # Demo bind/remote settings
└── SocketServer.csproj
```
