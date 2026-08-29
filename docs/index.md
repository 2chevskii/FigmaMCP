---
layout: home

hero:
  name: Figma MCP
  text: A local companion for Figma documents
  tagline: Connect MCP clients to an open Figma document through a local, loopback-only bridge.
  actions:
    - theme: brand
      text: Get started
      link: /DEVELOPMENT
    - theme: alt
      text: Tool reference
      link: /TOOLS

features:
  - title: Local by design
    details: The companion runs on your machine and communicates with the Bridge plugin over a loopback WebSocket.
  - title: Explicit document access
    details: Every document-specific tool uses a live connection_id selected from the active Figma plugin connection.
  - title: Typed and bounded
    details: The bridge uses typed MessagePack contracts, bounded payloads, serialized calls, and controlled mutation semantics.
---

## Overview

Figma MCP is a local companion for Figma documents with the Figma Bridge plugin open. An MCP client
starts the .NET process and exchanges protocol messages with it over STDIO.

```mermaid
sequenceDiagram
    participant Client as MCP client
    participant Companion as Companion
    participant Registry as Connection registry
    participant Plugin as Bridge plugin
    participant Figma as Figma API

    Client->>Companion: Start the child process and establish MCP over STDIO
    Plugin->>Companion: Open the loopback WebSocket using figma-mcp-bridge.v2
    Companion->>Registry: Validate the hello message and register connection_id
    Companion-->>Plugin: Confirm registration with hello_ack
    Client->>Companion: Call a document tool with the selected connection_id
    Companion->>Registry: Resolve the live connection and allocate request_id
    Registry-->>Companion: Return the active plugin connection
    Companion->>Plugin: Send a bounded MessagePack bridge request
    Plugin->>Figma: Read or mutate the active Figma document
    Figma-->>Plugin: Return the operation result
    Plugin-->>Companion: Send the MessagePack response with request_id
    Companion-->>Client: Return the matched MCP result over stdout
```

The companion has two transport roles:

- STDIO serves MCP. Protocol messages use `stdin` and `stdout`; diagnostics use `stderr`.
- The loopback WebSocket endpoint at `/bridge` serves the Figma Bridge plugin.

The server, plugin, and bridge use explicit typed contracts. Document-specific MCP tools receive a
live `connection_id`, and bridge operations use bounded payloads, a 30-second deadline, and
idempotent mutation keys where applicable.

## Starting the companion

The MCP client starts the executable as a child process. A client configuration can look like this:

```json
{
  "mcpServers": {
    "figma": {
      "command": "C:\\path\\to\\FigmaMCP.exe"
    }
  }
}
```

The process listens on `127.0.0.1:3846/bridge` for the plugin by default. If no `--port` is supplied
and `3846` is occupied, it detects that before Kestrel starts, tries subsequent ports through `65535`,
and reports the selected fallback on `stderr`. Pass `--port <1-65535>` to use one specific bridge
port; an explicit port is never changed. Set the Bridge plugin to the port the server selected. The
bridge is loopback-only and must not bind to an external interface.

## Documentation map

- [Architecture](/ARCHITECTURE) explains transports, lifecycle, state, and security boundaries.
- [Development](/DEVELOPMENT) describes the repository layout, build commands, and local checks.
- [Tool reference](/TOOLS) defines the MCP tool contract.
- [Plugin API coverage](/plugin-api-tool-coverage) records supported and deferred Figma API areas.
