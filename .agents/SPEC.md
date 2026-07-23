# Figma MCP Server — Implementation Specification

Status: implementation-ready  
Target milestone: connectivity vertical slice  
Target platform: Windows x64  
Companion runtime: .NET 10  
External MCP transport: stateless Streamable HTTP  
Plugin bridge transport: MessagePack over compressed binary WebSockets

## 1. Purpose

Build an unofficial MCP integration that lets any standards-compliant MCP client interact with one or more documents currently open through instances of the Figma plugin.

The integration consists of:

1. A persistent local companion process.
2. A Figma plugin running in each document the user wants to expose.
3. A binary WebSocket bridge between each plugin instance and the companion.
4. A stateless MCP Streamable HTTP endpoint exposed by the companion.

This milestone proves the complete connection and routing model. It intentionally exposes only connection discovery and document metadata. Scene-tree reads, large document transfers, and mutations follow in later milestones.

Normative terms such as **MUST**, **SHOULD**, and **MAY** describe implementation requirements.

## 2. Goals

- Expose a standards-compliant MCP Streamable HTTP endpoint at a stable localhost URL.
- Allow multiple MCP clients and agent sessions to share the same companion process.
- Allow any number of Figma plugin instances to connect concurrently.
- Let an agent discover all connected plugin instances.
- Require every document-specific call to identify its target explicitly.
- Read bounded metadata from the selected open Figma document.
- Use a versioned binary bridge that can be extended for larger operations.
- Produce a self-contained, single-file Windows x64 console executable.
- Require no authentication for this local-only milestone.

## 3. Non-goals

The milestone MUST NOT include:

- MCP `stdio` transport.
- Legacy HTTP+SSE transport.
- MCP-to-MCP forwarding or proxy processes.
- A server process per agent session.
- A Windows service, tray application, installer, or login autostart.
- TLS or authentication.
- Figma file mutation.
- Scene-tree traversal or full-document serialization.
- Bridge payload chunking.
- Native AOT publishing.
- Client-specific behavior for Codex, Claude Code, Copilot, OpenCode, or any other host.
- Figma private plugin APIs.

## 4. System architecture

```text
MCP client A ─┐
MCP client B ─┼── Streamable HTTP ──► 127.0.0.1:<port>/mcp
MCP client N ─┘                              │
                                            │
                                    .NET companion
                                    connection registry
                                      │           │
                           WebSocket  │           │  WebSocket
                                      ▼           ▼
                               Figma plugin  Figma plugin
                               document A     document B
```

The companion is one persistent HTTP server. Streamable HTTP clients configured with its URL connect to that existing process; they do not launch a new server for each agent session. If the user manually starts a second companion on the same port, startup MUST fail with a clear address-in-use message. No forwarding behavior is necessary or permitted.

Both `/mcp` and `/bridge` MUST use the same configured TCP port.

## 5. Repository layout

Use this target layout:

```text
/
├─ .agents/
│  └─ SPEC.md
├─ global.json
├─ Directory.Build.props
├─ Directory.Packages.props
├─ FigmaMcp.slnx
├─ README.md
├─ src/
│  └─ FigmaMcp.Server/
│     ├─ FigmaMcp.Server.csproj
│     ├─ Program.cs
│     ├─ Bridge/
│     ├─ Connections/
│     ├─ Mcp/
│     └─ Options/
├─ tests/
│  ├─ FigmaMcp.Server.Tests/
│  └─ fixtures/
│     └─ bridge-v1/
└─ figma-mcp-connector/
   ├─ manifest.json
   ├─ package.json
   ├─ scripts/
   ├─ src/
   │  ├─ main.ts
   │  ├─ ui.ts
   │  ├─ bridge/
   │  └─ ui-template.html
   ├─ code.js       # generated
   └─ ui.html       # generated
```

Keep the existing `figma-mcp-connector` directory and plugin ID. Replace the starter implementation rather than creating a second plugin.

`global.json` MUST pin SDK `10.0.302` with roll-forward disabled or restricted to the latest patch in the same feature band. The local .NET 11 preview SDK MUST NOT be selected implicitly.

Use central NuGet package management. Pin stable versions rather than floating ranges.

## 6. Companion application

### 6.1 Runtime and dependencies

The server project MUST:

- Target `net10.0`.
- Use `Microsoft.NET.Sdk.Web`.
- Enable nullable reference types.
- Enable implicit usings.
- Treat compiler warnings as errors.
- Reference the current pinned stable `ModelContextProtocol.AspNetCore` package.
- Reference a current secure `MessagePack-CSharp` release no older than `3.1.5`.

The server MUST use Kestrel and ASP.NET Core raw WebSockets. It MUST NOT use SignalR.

### 6.2 Command line

Supported invocation:

```text
figma-mcp-server.exe [--port <port>]
```

Configuration:

| Setting | Default | Validation |
|---|---:|---|
| `--port` | `3846` | Integer from `1` through `65535` |

Do not expose a host/address argument. The listener MUST always bind to `127.0.0.1`.

Exit codes:

| Code | Meaning |
|---:|---|
| `0` | Graceful shutdown |
| `2` | Invalid command-line input |
| `3` | Listener startup failure, including address already in use |
| `1` | Other fatal startup failure |

Startup output MUST include:

- Product name and version.
- MCP URL.
- Plugin bridge URL.
- Health URL.
- A clear instruction to keep the process running.

### 6.3 HTTP endpoints

#### `POST|GET|DELETE /mcp`

Map the official MCP SDK Streamable HTTP handler at `/mcp`.

Requirements:

- Transport MUST be stateless.
- The server MUST NOT allocate MCP transport sessions.
- Tool calls MUST return independent JSON responses.
- No unsolicited MCP notifications are required.
- The endpoint MUST implement the HTTP methods and content negotiation required by the current MCP Streamable HTTP specification through the official SDK.

#### `GET /health`

Return `200 application/json`:

```json
{
  "service": "figma-mcp-server",
  "version": "0.1.0",
  "bridge_protocol_version": 1,
  "mcp_endpoint": "/mcp",
  "bridge_endpoint": "/bridge",
  "port": 3846,
  "uptime_seconds": 120,
  "connected_plugins": 2
}
```

The endpoint MUST NOT expose document names, page names, selections, request payloads, or connection IDs.

#### `/bridge`

Accept WebSocket upgrades only.

Requirements:

- Require WebSocket subprotocol `figma-mcp-bridge.v1`.
- Select that subprotocol in the successful upgrade response.
- Reject ordinary HTTP requests with `400`.
- Reject unsupported subprotocols with `400`.
- Accept `Origin: null` or an absent `Origin`.
- Reject other browser origins.
- Treat origin validation as request hardening, not authentication.
- Keep the ASP.NET Core request pipeline alive until the socket processing task completes.

### 6.4 Host and origin validation

All endpoints MUST reject unexpected `Host` values. Valid authorities are:

- `127.0.0.1:<configured-port>`
- `localhost:<configured-port>`

`/mcp` MUST reject requests containing a browser `Origin` header. Normal native MCP clients do not require one.

Kestrel MUST listen only on `127.0.0.1`; it MUST never bind to `0.0.0.0`, a LAN address, or IPv6-any.

### 6.5 Shutdown

On `Ctrl+C`, process termination, or host cancellation:

1. Stop accepting new HTTP and WebSocket connections.
2. Cancel all pending plugin RPC requests.
3. Close connected plugin sockets with WebSocket close status `EndpointUnavailable` and reason `server_shutdown`.
4. Dispose the MCP transport and connection registry.
5. Stop Kestrel within a five-second shutdown budget.

## 7. MCP server contract

### 7.1 Server identity

Advertise:

```text
name: figma-mcp-server
version: 0.1.0
```

Server instructions MUST tell the agent:

- Call `list_figma_connections` before document-specific work.
- Always pass the chosen `connection_id`.
- Connection IDs identify live plugin invocations, not permanent Figma files.
- A missing connection means the plugin must be opened or reconnected.

Keep the essential instructions within the first 512 characters.

### 7.2 Common result behavior

Successful tools MUST return:

- A structured JSON object in `structuredContent`.
- A JSON text representation of the same object in `content` for compatible fallback behavior.

The structured and text representations MUST contain equivalent data.

Failed tools MUST return `isError: true` with:

```json
{
  "error": {
    "code": "connection_not_found",
    "message": "No live Figma plugin connection exists for the supplied connection_id.",
    "connection_id": "optional-uuid"
  }
}
```

Do not return stack traces, exception type names, or internal paths through MCP.

### 7.3 Tool: `list_figma_connections`

Purpose: discover all currently connected Figma plugin instances.

Input schema:

```json
{
  "type": "object",
  "properties": {},
  "additionalProperties": false
}
```

Output schema:

```json
{
  "type": "object",
  "required": ["connections"],
  "properties": {
    "connections": {
      "type": "array",
      "items": {
        "type": "object",
        "required": [
          "connection_id",
          "plugin_version",
          "protocol_version",
          "document_name",
          "current_page",
          "editor_type",
          "mode",
          "connected_at",
          "last_seen_at"
        ],
        "properties": {
          "connection_id": {
            "type": "string",
            "format": "uuid"
          },
          "plugin_version": {
            "type": "string"
          },
          "protocol_version": {
            "type": "integer",
            "const": 1
          },
          "document_name": {
            "type": "string"
          },
          "current_page": {
            "type": "object",
            "required": ["id", "name"],
            "properties": {
              "id": { "type": "string" },
              "name": { "type": "string" }
            },
            "additionalProperties": false
          },
          "editor_type": {
            "type": "string"
          },
          "mode": {
            "type": "string"
          },
          "connected_at": {
            "type": "string",
            "format": "date-time"
          },
          "last_seen_at": {
            "type": "string",
            "format": "date-time"
          }
        },
        "additionalProperties": false
      }
    }
  },
  "additionalProperties": false
}
```

Behavior:

- Return only live, handshaken connections.
- Return `connections: []` when no plugins are connected.
- Sort by `connected_at` ascending, then `connection_id` ordinally for deterministic output.
- `last_seen_at` MUST update on any valid inbound bridge message, including heartbeat responses.
- Cached connection summaries MAY be used; this tool MUST NOT issue one RPC per plugin.

### 7.4 Tool: `get_figma_document_metadata`

Purpose: read a fresh, bounded metadata snapshot from one explicitly selected plugin.

Input schema:

```json
{
  "type": "object",
  "required": ["connection_id"],
  "properties": {
    "connection_id": {
      "type": "string",
      "format": "uuid"
    }
  },
  "additionalProperties": false
}
```

Output schema:

```json
{
  "type": "object",
  "required": [
    "connection_id",
    "document",
    "current_page",
    "selection",
    "editor"
  ],
  "properties": {
    "connection_id": {
      "type": "string",
      "format": "uuid"
    },
    "document": {
      "type": "object",
      "required": ["name", "type", "color_profile", "page_count", "pages"],
      "properties": {
        "name": { "type": "string" },
        "type": { "type": "string", "const": "DOCUMENT" },
        "color_profile": {
          "type": "string",
          "enum": ["LEGACY", "SRGB", "DISPLAY_P3"]
        },
        "page_count": {
          "type": "integer",
          "minimum": 0
        },
        "pages": {
          "type": "array",
          "items": {
            "type": "object",
            "required": ["id", "name"],
            "properties": {
              "id": { "type": "string" },
              "name": { "type": "string" }
            },
            "additionalProperties": false
          }
        }
      },
      "additionalProperties": false
    },
    "current_page": {
      "type": "object",
      "required": ["id", "name", "top_level_node_count"],
      "properties": {
        "id": { "type": "string" },
        "name": { "type": "string" },
        "top_level_node_count": {
          "type": "integer",
          "minimum": 0
        }
      },
      "additionalProperties": false
    },
    "selection": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["id", "name", "type"],
        "properties": {
          "id": { "type": "string" },
          "name": { "type": "string" },
          "type": { "type": "string" }
        },
        "additionalProperties": false
      }
    },
    "editor": {
      "type": "object",
      "required": ["type", "mode"],
      "properties": {
        "type": { "type": "string" },
        "mode": { "type": "string" }
      },
      "additionalProperties": false
    }
  },
  "additionalProperties": false
}
```

Behavior:

- `connection_id` is always required, including when only one plugin is connected.
- Validate UUID syntax before registry lookup.
- Resolve the connection once and send bridge method `get_document_metadata`.
- Apply the per-connection ten-second RPC timeout.
- Return the bridge response without adding private or inferred Figma identifiers.
- Do not call `figma.loadAllPagesAsync()`.
- Do not traverse descendants of page children or selected nodes.
- Do not expose `figma.fileKey`; it is unavailable to a normal unofficial plugin.

## 8. Bridge protocol

### 8.1 Encoding

Every WebSocket message MUST contain exactly one MessagePack object encoded as a binary frame.

Do not send JSON text frames.

MessagePack rules:

- Maps MUST use explicit string keys.
- C# DTOs MUST use `[MessagePackObject]` and string `[Key("...")]` attributes.
- Do not use typeless resolvers.
- Do not use contractless resolvers.
- Do not serialize arbitrary CLR type names.
- Do not use application-defined MessagePack extension types in protocol version 1.
- UUIDs MUST be lowercase canonical strings.
- Timestamps MUST be UTC ISO-8601 strings.
- Numbers crossing into JavaScript MUST remain within JavaScript safe integer range.
- Unknown map keys SHOULD be ignored for compatible protocol evolution.
- Missing required keys MUST reject the message.

Decoder limits:

| Limit | Value |
|---|---:|
| Decoded WebSocket message | 1 MiB |
| Object graph depth | 32 |
| String length | 256 KiB |
| Binary value length | 1 MiB |
| Array item count | 10,000 |
| Map entry count | 10,000 |

The C# decoder MUST use `MessagePackSecurity.UntrustedData`. The TypeScript decoder MUST configure equivalent input length and collection limits.

WebSocket fragmentation MAY occur at the transport level. The receiver MUST accumulate fragments until `EndOfMessage`, enforce the decoded-message byte limit during accumulation, then decode one envelope. Application-level chunking is not part of protocol version 1.

### 8.2 Compression

The companion MUST accept the socket with WebSocket per-message compression enabled:

```csharp
new WebSocketAcceptContext
{
    SubProtocol = "figma-mcp-bridge.v1",
    DangerousEnableCompression = true,
    DisableServerContextTakeover = true
}
```

The browser and server negotiate `permessage-deflate`. If the browser does not negotiate compression, the connection MUST remain valid and use uncompressed binary frames.

Because protocol version 1 contains no authentication secrets, per-message compression is acceptable for this localhost bridge. This decision MUST be revisited before secret-bearing messages are added.

### 8.3 Common envelope

All messages are maps using these common fields:

| Field | Type | Required | Meaning |
|---|---|---:|---|
| `type` | string | yes | Message discriminator |
| `protocol_version` | integer | yes | Must be `1` |
| `connection_id` | UUID string | message-specific | Plugin invocation identity |
| `request_id` | UUID string | message-specific | RPC correlation identity |
| `method` | string | message-specific | RPC method |
| `payload` | map or null | message-specific | Typed message data |
| `error` | map or null | message-specific | Protocol/RPC error |
| `sent_at` | ISO-8601 string | yes | Sender timestamp |

Error maps:

```text
code: string
message: string
```

Do not include stack traces.

### 8.4 Connection state machine

1. The plugin UI opens a WebSocket using subprotocol `figma-mcp-bridge.v1`.
2. The plugin MUST send `hello` within five seconds of upgrade.
3. The server validates protocol version, connection UUID, and hello payload.
4. The server registers the connection.
5. The server sends `hello_ack`.
6. Normal context, RPC, and heartbeat messages may flow.
7. On disconnect, the server removes the socket only if it is still the currently registered socket for that connection ID.

Before a valid `hello`, all messages other than `hello` are protocol errors.

If another socket connects with the same `connection_id`, the registry MUST atomically install the new socket and close the previous socket with reason `connection_replaced`. This preserves identity across reconnection and prevents the stale socket from deleting the replacement when it finishes.

### 8.5 `hello`

Plugin to server:

```text
type: "hello"
protocol_version: 1
connection_id: <plugin invocation UUID>
sent_at: <timestamp>
payload:
  plugin_version: "0.1.0"
  editor_type: "figma"
  mode: "default"
  document_name: "Design System"
  current_page:
    id: "1:2"
    name: "Components"
```

### 8.6 `hello_ack`

Server to plugin:

```text
type: "hello_ack"
protocol_version: 1
connection_id: <same UUID>
sent_at: <timestamp>
payload:
  server_version: "0.1.0"
  request_timeout_ms: 10000
  max_message_bytes: 1048576
```

### 8.7 `context_changed`

Plugin to server:

```text
type: "context_changed"
protocol_version: 1
connection_id: <UUID>
sent_at: <timestamp>
payload:
  editor_type: "figma"
  mode: "default"
  document_name: "Design System"
  current_page:
    id: "1:2"
    name: "Components"
```

The server updates only its cached connection summary. This message MUST NOT contain selection contents or scene nodes.

### 8.8 RPC request

Server to plugin:

```text
type: "request"
protocol_version: 1
connection_id: <UUID>
request_id: <UUID>
method: "get_document_metadata"
sent_at: <timestamp>
payload: {}
```

Protocol version 1 supports exactly one RPC method:

```text
get_document_metadata
```

Unknown methods MUST return an RPC error with code `method_not_found`.

### 8.9 RPC response

Plugin to server:

```text
type: "response"
protocol_version: 1
connection_id: <UUID>
request_id: <same request UUID>
sent_at: <timestamp>
payload: <document metadata object>
```

The server MUST reject responses for unknown, completed, cancelled, or timed-out request IDs. Such responses MUST be logged at debug level without logging the payload.

### 8.10 RPC error

Plugin to server:

```text
type: "error"
protocol_version: 1
connection_id: <UUID>
request_id: <request UUID when applicable>
sent_at: <timestamp>
error:
  code: "figma_api_error"
  message: "Unable to read document metadata."
```

### 8.11 Heartbeats

- The server sends `ping` every 15 seconds.
- `ping.payload.nonce` is a new UUID.
- The plugin MUST answer with `pong` carrying the same nonce.
- Any valid inbound message updates `last_seen_at`.
- If no valid inbound message is received for 30 seconds, close the socket and remove the connection.
- Heartbeat timers MUST stop immediately when a socket disconnects or is replaced.

## 9. Connection registry and RPC routing

### 9.1 Registry

Implement a singleton `PluginConnectionRegistry` backed by a concurrency-safe dictionary keyed by `Guid`.

Each entry contains:

- Connection ID.
- Current WebSocket wrapper.
- Plugin and protocol versions.
- Cached connection summary.
- Connected and last-seen timestamps.
- Per-connection RPC serialization lock.
- Pending request correlation state.
- Lifetime cancellation token.

Registry snapshots MUST be immutable copies. MCP tool serialization MUST NOT enumerate a mutable dictionary directly.

### 9.2 Concurrency

- Requests to different plugin connections MAY execute concurrently.
- Requests to the same plugin MUST be serialized using one asynchronous semaphore.
- Do not block threads with `.Wait()`, `.Result`, or synchronous locks around async work.
- Release per-connection semaphores in `finally`.

This policy is required now even though the initial operation is read-only, because future write tools must preserve deterministic Figma operation order.

### 9.3 RPC correlation

- Generate a unique request UUID for each bridge call.
- Register the pending completion before sending the frame.
- Wait for response, plugin error, disconnection, host cancellation, or timeout.
- Remove completion state exactly once.
- Ignore and debug-log late responses.
- Map failures to MCP errors:

| Bridge condition | MCP code |
|---|---|
| Missing registry entry | `connection_not_found` |
| Socket closes before response | `plugin_disconnected` |
| Ten-second timeout | `plugin_timeout` |
| Version mismatch | `unsupported_protocol` |
| Invalid/oversized MessagePack | `plugin_protocol_error` |
| Plugin RPC error | `plugin_protocol_error` |

## 10. Figma plugin

### 10.1 Build system

Add `esbuild` so MessagePack and TypeScript modules can be bundled for both Figma runtimes.

Build outputs:

- Bundle `src/main.ts` as an ES2020 IIFE to root `code.js`.
- Bundle `src/ui.ts` as an ES2020 browser IIFE.
- Inject the bundled UI script into `src/ui-template.html`.
- Write the result to root `ui.html`.

`code.js` and `ui.html` are generated artifacts and MUST NOT be hand-edited.

Add scripts:

```json
{
  "build": "...",
  "typecheck": "...",
  "lint": "...",
  "test": "...",
  "watch": "..."
}
```

The plugin bundle MUST include all runtime dependencies. Do not load MessagePack or other scripts from a CDN.

### 10.2 Manifest

Retain:

- Existing plugin ID.
- `api: "1.0.0"`.
- `editorType: ["figma"]`.
- `documentAccess: "dynamic-page"`.
- No proposed or private APIs.

Set:

```json
{
  "main": "code.js",
  "ui": "ui.html",
  "networkAccess": {
    "allowedDomains": ["*"],
    "reasoning": "Connects to a user-configured local companion server port used to expose the currently open Figma document through MCP."
  }
}
```

This broad permission is an explicit consequence of allowing arbitrary localhost ports; Figma does not document a localhost wildcard-port pattern.

### 10.3 Plugin UI

Show a compact persistent UI approximately 360×240 pixels containing:

- Product title.
- Port number input.
- Save/reconnect button.
- Connection state.
- Current connection ID when connected.
- Current document and page names when connected.
- Last error summary.
- Close button.

Port behavior:

- Default to `3846`.
- Accept only decimal integers from `1` through `65535`.
- Store the value through main code using `figma.clientStorage`.
- On save, close the old WebSocket normally and connect to the new port.
- Invalid input MUST remain visible and MUST NOT modify persisted storage.

Connection states:

```text
loading_config
disconnected
connecting
connected
reconnecting
invalid_port
protocol_error
```

Reconnect delays:

```text
500 ms, 1 s, 2 s, 5 s, then 10 s maximum
```

Apply ±20% random jitter. Reset the delay after a successful `hello_ack`. Do not reconnect after the user closes the plugin.

Generate `connection_id` once per plugin invocation using `crypto.randomUUID()` in the UI iframe. Do not persist it.

Set:

```js
socket.binaryType = "arraybuffer"
```

Send encoded `Uint8Array` values and reject text messages.

### 10.4 Main/UI messages

Internal UI messages use plain structured-clone objects only for control:

```text
config_loaded { port }
set_port { port }
close_plugin
bridge_frame { bytes: Uint8Array }
context_dirty
```

Rules:

- Bridge envelopes MUST remain encoded as `Uint8Array` while crossing the UI boundary.
- The UI forwards server request bytes to main code.
- Main code decodes requests, performs Figma work, encodes the response, and returns response bytes.
- The UI sends those response bytes directly over WebSocket.
- Do not decode and re-encode bridge responses in the UI.
- `Uint8Array` is the only binary typed array used across `figma.ui.postMessage`.

### 10.5 Figma metadata collection

`get_document_metadata` reads:

- `figma.root.name`.
- `figma.root.type`.
- `figma.root.documentColorProfile`.
- `figma.root.children`, but only each page ID and name.
- `figma.currentPage.id`.
- `figma.currentPage.name`.
- `figma.currentPage.children.length`.
- `figma.currentPage.selection`, but only node ID, name, and type.
- `figma.editorType`.
- `figma.mode`.

It MUST NOT:

- Access `figma.fileKey`.
- Call `loadAllPagesAsync`.
- Traverse a page child recursively.
- Serialize a raw Figma node.
- Return plugin data, styles, fills, text, geometry, images, or component properties.

Return plain DTOs only. Catch Figma API access errors and return `figma_api_error`.

### 10.6 Context updates

Register Figma event handlers for current-page and selection changes. Send `context_changed` after the current page changes. Selection changes do not need to update the cached connection summary because selection is fetched fresh by `get_figma_document_metadata`.

The fresh metadata tool response remains authoritative. Cached hello/context information is used only by `list_figma_connections`.

### 10.7 Plugin lifecycle

- Call `figma.showUI` once.
- Do not call `figma.closePlugin()` after a bridge request.
- Close only when requested by the user or Figma.
- On plugin close, stop reconnect timers and close the WebSocket normally.
- Do not attempt asynchronous work from Figma's close callback.

## 11. Logging

Use structured console logging.

Information-level events:

- Server started/stopped.
- Plugin connected/disconnected/replaced.
- MCP tool started/completed.
- Port and endpoint URLs.

Warning-level events:

- Invalid bridge message.
- Heartbeat timeout.
- RPC timeout.
- Unsupported protocol version.
- Late response.

Error-level events:

- Listener startup failure.
- Unhandled bridge loop failure.
- MCP tool failure caused by an unexpected exception.

Each request log MAY contain:

- Request ID.
- Connection ID.
- Method/tool name.
- Encoded and decoded byte counts.
- Duration.
- Outcome code.

Logs MUST NOT contain:

- Document or page names.
- Selection names.
- MessagePack payload dumps.
- MCP tool result bodies.
- Stack traces at normal information level.

## 12. Packaging

Create a Windows x64 publish profile with:

```xml
<TargetFramework>net10.0</TargetFramework>
<RuntimeIdentifier>win-x64</RuntimeIdentifier>
<SelfContained>true</SelfContained>
<PublishSingleFile>true</PublishSingleFile>
<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
<PublishTrimmed>false</PublishTrimmed>
<EnableCompressionInSingleFile>false</EnableCompressionInSingleFile>
<DebugType>embedded</DebugType>
```

The release command MUST produce one runnable `.exe` plus no required adjacent runtime files.

Do not enable `PublishAot` in this milestone. Native AOT may be added only after:

1. The complete application publishes without actionable AOT/trimming warnings.
2. The official MCP and MessagePack dependencies are proven compatible.
3. All integration and packaging tests pass against the AOT artifact.

## 13. Tests

### 13.1 Companion unit tests

Cover:

- Default and explicit port parsing.
- Invalid, missing, negative, zero, and out-of-range ports.
- Registry add, snapshot, disconnect, and replacement.
- Stale socket cleanup not removing a replacement socket.
- Deterministic connection sorting.
- Requests serialized for one connection.
- Requests parallelized across different connections.
- Timeout cleanup.
- Pending request cancellation on disconnect and shutdown.
- Late and unknown response IDs.
- Heartbeat last-seen and expiry behavior.
- Bridge-to-MCP error mapping.

### 13.2 MessagePack compatibility tests

Store canonical semantic fixtures under `tests/fixtures/bridge-v1`.

For every message type:

- Encode in TypeScript and decode in C#.
- Encode in C# and decode in TypeScript.
- Compare decoded semantic objects, not raw bytes; MessagePack map key ordering is not significant.
- Verify missing required keys fail.
- Verify unknown optional keys are ignored.
- Verify custom extensions and typeless CLR payloads are rejected.
- Verify maximum depth, string, binary, array, map, and total-message limits.

### 13.3 WebSocket integration tests

Use a real loopback Kestrel listener on an ephemeral test port.

Cover:

- Successful subprotocol negotiation.
- Rejection without the required subprotocol.
- Rejection of unexpected origins.
- Successful per-message-deflate negotiation.
- Successful uncompressed fallback.
- Hello timeout.
- Unsupported protocol version.
- Multiple simultaneous plugin sockets.
- Same-ID replacement.
- Binary request/response correlation.
- Text frame rejection.
- Fragmented binary message reassembly.
- Oversized message closure.
- Heartbeat timeout and cleanup.

### 13.4 MCP integration tests

Use the official MCP C# client over Streamable HTTP.

Cover:

- Initialize.
- Tool listing and schemas.
- Empty `list_figma_connections`.
- Multiple live connections.
- Fresh document metadata routed to the correct connection.
- Required and malformed connection IDs.
- Unknown connection.
- Plugin disconnect during request.
- Plugin timeout.
- Stateless behavior across independent HTTP client instances.
- Equivalent structured and text tool results.

### 13.5 Plugin automated checks

Cover pure TypeScript behavior with a browser-capable test environment:

- Port validation and persistence messages.
- Reconnect delay progression and reset.
- MessagePack encode/decode limits.
- Connection state transitions.
- Request dispatch and response correlation.
- Text frame rejection.
- Figma metadata DTO projection using mocked Figma API objects.
- No descendant access in the metadata projector.

Always run:

```text
npm run typecheck
npm run lint
npm test
npm run build
```

### 13.6 Packaging smoke test

1. Publish the `win-x64` self-contained single-file artifact.
2. Copy only the executable to a clean temporary directory.
3. Start it on an unused port.
4. Wait for `/health`.
5. Perform an MCP initialize and tool-list request.
6. Stop it gracefully.
7. Assert no adjacent runtime files were required.

### 13.7 Manual Figma acceptance

Verify in Figma Desktop:

1. Plugin opens with port `3846`.
2. Port changes persist after closing and reopening the plugin.
3. Plugin connects when the companion is running.
4. Plugin reconnects after the companion restarts.
5. Two documents with the plugin open appear as two connection IDs.
6. `get_figma_document_metadata` returns the selected document's metadata.
7. Changing pages updates the connection summary.
8. Closing one plugin removes only that connection.
9. Invalid ports and connection failures produce actionable UI states.

## 14. Acceptance criteria

The milestone is complete when:

- A standards-compliant MCP client can connect to `/mcp` without client-specific code.
- Multiple independent MCP clients can use the same companion concurrently.
- Multiple Figma plugin instances remain connected concurrently.
- `list_figma_connections` reports all and only live plugin instances.
- Every metadata call requires and honors an explicit `connection_id`.
- Metadata contains only the fields specified here and never traverses descendants.
- The bridge uses MessagePack binary frames and successfully negotiates compression when supported.
- Invalid and oversized bridge data cannot crash or exhaust the companion.
- Companion restart is recovered by plugin reconnection without restarting Figma.
- The Windows x64 executable runs without an installed .NET runtime.
- All automated and manual acceptance tests pass.

## 15. Forward compatibility constraints

Future implementation work MUST preserve these principles:

- Every document-specific MCP tool requires `connection_id`.
- No tool returns an unbounded document tree.
- Large reads use explicit selection, depth, pagination, resources, or task/chunk mechanisms.
- Bridge chunking, when added, is versioned and bounded; it must not silently reinterpret protocol version 1 messages.
- Write tools continue to serialize operations per plugin connection.
- New bridge fields remain additive whenever possible.
- Authentication, if added, requires revisiting wildcard network access, WebSocket compression, and origin handling.
- Additional operating-system artifacts share the same MCP and bridge contracts.
