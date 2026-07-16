# SocketServer
A lightweight, high-performance TCP socket library for .NET Framework, built directly on
`SocketAsyncEventArgs` (SAEA) instead of the classic Begin/End (APM) pattern - no per-I/O
allocation on the hot path, and built to scale to thousands of concurrent connections.

## Introduction
When creating and testing this project I've personally used .NET framework 4.8 because I was testing my own use case.
However, you can change that to whatever your needs require, I'd also recommend consuming this as a dll library 
which represents "raw" layer socket at your project, that being said I've hooked up a tiny quick-start in Program.cs file
So it is runnable.
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
- C# `latest` language version

## Getting Started

The included `Program.cs` is a minimal TCP proxy/relay demo: it listens on `BindIp:BindPort` and
forwards every accepted connection to `RemoteIp:RemotePort`, configured via `App.config`:

```xml
<appSettings>
  <add key="BindIp" value="127.0.0.1" />
  <add key="BindPort" value="13779" />
  <add key="RemoteIp" value="Real Server IP" />
  <add key="RemotePort" value="Real Server Port" />
  <add key="VerboseLogging" value="true" />
</appSettings>
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
