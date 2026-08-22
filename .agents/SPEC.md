# Figma MCP Specification

**Status:** The local architecture is implemented; the full Figma Desktop scenario still requires manual validation.
**Platform:** Windows x64.
**Runtime:** .NET 10.
**MCP transport:** STDIO.
**Bridge transport:** Binary WebSocket with MessagePack and the `figma-mcp-bridge.v2` subprotocol.

## Normative architecture

Figma MCP is a local companion for Figma documents with the Figma Bridge plugin open. An MCP client
starts the companion as a child process and exchanges MCP messages over STDIO.

```text
MCP client ── stdin/stdout ──> local companion ── ws://127.0.0.1:3846/bridge ──> Bridge plugin
                                                                                         │
                                                                                  Figma Plugin API
```

### Components and scope

- One .NET process: `FigmaMCP`.
- The official MCP SDK with the STDIO transport.
- A loopback WebSocket endpoint at `/bridge` for the plugin.
- An in-memory registry of live plugin connections and pending RPCs.
- The MCP tool contract in `docs/TOOLS.md`, including explicit `connection_id`, bounded typed
  payloads, a 30-second bridge deadline, and idempotent mutations.
- The existing Bridge plugin protocol and its local companion URL setting. The bridge does not
  require or transmit an access token.

### STDIO contract

The companion reads the MCP protocol from `stdin` and writes protocol responses **only** to `stdout`.
Logs, diagnostics, startup information, and errors go to `stderr`. Do not write human-readable text,
logger output, or banners to `stdout`.

An MCP client configures the executable, for example:

```json
{
  "mcpServers": {
    "figma": { "command": "C:\\path\\to\\figma-mcp-server.exe" }
  }
}
```

### Bridge protocol

Do not change `packages/plugin/` source, manifest, UI, settings, or the bridge protocol without an
explicit user request. The companion is compatible with this protocol:

- Path `/bridge` and subprotocol `figma-mcp-bridge.v2`.
- One MessagePack map in each binary WebSocket frame.
- Envelopes: `hello`, `hello_ack`, `context_changed`, `request`, `response`, `error`, `ping`, and `pong`.
- Lowercase canonical UUIDs and UTC ISO-8601 timestamps.
- A 16 MiB message limit and a 12 MiB limit for base64-encoded binary payloads.
- Allowlisted typed operations only; no JavaScript execution or arbitrary reflection.

### Loopback boundary

- Listen only on `127.0.0.1:3846/bridge`; do not bind to `0.0.0.0`, a LAN interface, or IPv6-any.
- Accept only `127.0.0.1:<port>` and `localhost:<port>` Host values.
- Accept only WebSocket upgrades using `figma-mcp-bridge.v2`, binary frames, and a missing or `null`
  Origin. Reject text frames and other browser origins.

### Connection and tool semantics

1. The plugin sends `hello`; the companion validates it, registers the connection, and returns `hello_ack`.
2. The MCP client calls `list_figma_connections`, then passes the selected live `connection_id` to
   every document-specific tool.
3. Requests for one connection execute sequentially; requests for different connections can execute
   concurrently. Responses are matched by `request_id`.
4. A `connection_id` identifies a plugin invocation, not a persistent Figma file. A replacement
   connection uses compare-and-swap, so a stale socket cannot remove the replacement.
5. Disconnection completes pending requests with a controlled MCP error. Restarting the companion
   clears only in-memory state, and the plugin reconnects normally.

### Required validation

- Server: restore, formatting check, build, and test.
- Plugin: formatting check, lint, type-check, test, and build.
- End-to-end: a real STDIO client, Figma Desktop, and the loopback-only bridge.
- Verify that companion `stdout` contains MCP protocol messages only.

### Related documentation

- `AGENTS.md` — agent instructions and documentation map.
- `docs/README.md` — product overview and entry points.
- `docs/ARCHITECTURE.md` — transports, lifecycle, and security boundary.
- `docs/DEVELOPMENT.md` — repository layout, build commands, and local validation.
- `docs/TOOLS.md` — normative MCP tool contract.
- `docs/PLUGIN_API_TOOL_COVERAGE.md` — Figma Plugin API coverage and limits.
