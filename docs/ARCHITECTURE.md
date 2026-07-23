# Architecture

## Components

```text
MCP client ── Streamable HTTP ──> .NET companion ── WebSocket ──> Figma plugin UI
                                        │                            │
                                        │                            └─ browser networking
                                        └─ live connection registry
                                                                     │
                                                        Figma plugin controller
                                                                     │
                                                               Figma Plugin API
```

The companion is a persistent, stateless MCP HTTP server. Figma plugin instances maintain WebSocket
connections to it. Document-specific calls always name one live connection explicitly.

The plugin has two execution environments:

- `plugin/src/main.ts` runs in Figma's plugin sandbox and reads the document through the Plugin API.
- `plugin/src/ui.ts` runs in a browser iframe and owns the WebSocket because the sandbox does not
  provide browser networking.

The controller and UI exchange typed messages through Figma's `postMessage` bridge.

## Connection lifecycle

1. The plugin UI creates one invocation UUID.
2. It opens `/bridge` with subprotocol `figma-mcp-bridge.v2`.
3. It sends `hello` within five seconds.
4. The server validates the payload and installs the connection in the registry.
5. The server replies with `hello_ack`.
6. MCP tool calls become correlated bridge requests and responses.
7. Context changes update the cached connection summary.
8. Disconnects fail pending requests and remove only the matching socket.

Installing a replacement connection and removing a stale connection use compare-and-swap semantics.
A stale socket therefore cannot remove a newer socket with the same connection ID.

## Wire format

Each WebSocket message contains one MessagePack map in a binary frame. The shared envelope includes:

- `type`
- `protocol_version`
- `sent_at`
- optional `connection_id`, `request_id`, `method`, `payload`, and `error`

Messages are limited to 16 MiB. UUIDs use lowercase canonical form and timestamps use UTC ISO-8601.
Payload property names use snake_case. Requests carry one allowlisted operation name and an explicit
structured payload; the plugin does not expose JavaScript evaluation or arbitrary property
reflection.

The server serializes operations per live connection. Each request has a 30-second deadline. Mutation
payloads can carry `dry_run` and `idempotency_key`; the plugin retains the 200 most recent results per
invocation so a retry can return the original result without replaying the write.

Binary image, video, and export data is base64 encoded within the structured response and capped at
12 MiB, leaving envelope overhead below the transport ceiling.

## Local security boundary

The companion:

- binds only to `127.0.0.1`;
- accepts only `127.0.0.1:<port>` and `localhost:<port>` Host headers;
- rejects browser Origin headers at `/mcp`;
- accepts only a missing or `null` bridge Origin;
- requires the versioned bridge subprotocol.

These checks harden a local-only integration. The current milestone does not provide authentication or
TLS and must not be exposed to a network interface.
