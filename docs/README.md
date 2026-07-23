# Figma MCP connector

This repository connects MCP clients to documents that are currently open in Figma. It consists of:

- a local .NET companion server in `server/`;
- a Figma development plugin in `plugin/`;
- a MessagePack WebSocket bridge between them.

The current milestone supports connection discovery and bounded document metadata. It does not mutate
Figma documents or traverse complete scene trees.

## Prerequisites

- Windows x64
- .NET SDK 10.0.302
- Node.js with npm
- Figma Desktop

## Start the companion server

From `server/`:

```powershell
dotnet run --project src/FigmaMcp.Server -- --port 3846
```

The server listens only on the IPv4 loopback interface:

| Endpoint      | URL                            |
| ------------- | ------------------------------ |
| MCP           | `http://127.0.0.1:3846/mcp`    |
| Plugin bridge | `ws://127.0.0.1:3846/bridge`   |
| Health        | `http://127.0.0.1:3846/health` |

Keep the process running while using the plugin. Pass a different port with `--port`; valid values are
from `1` through `65535`.

## Build and load the Figma plugin

From `plugin/`:

```powershell
npm install
npm run build
```

The build writes these generated files:

```text
plugin/dist/
├── manifest.json
├── plugin.js
└── ui.html
```

In Figma Desktop, import `plugin/dist/manifest.json` as a development plugin. Open the plugin in each
document that should be visible to MCP clients. The plugin UI defaults to port `3846` and persists a
custom port locally.

## Configure an MCP client

Configure the client to use Streamable HTTP at:

```text
http://127.0.0.1:3846/mcp
```

Call `list_figma_connections` first. Choose a live `connection_id`, then pass it explicitly to
`get_figma_document_metadata`. Connection IDs identify plugin invocations, not permanent Figma files.

## Troubleshooting

### The plugin cannot connect

1. Confirm the companion process is still running.
2. Open `http://127.0.0.1:3846/health`.
3. Confirm the port in the plugin UI matches the server port.
4. Rebuild the plugin and reload it in Figma after source changes.

### The server reports an invalid bridge message

The bridge accepts only binary MessagePack frames using subprotocol `figma-mcp-bridge.v1`. Payload
fields use snake_case, including `current_page`.

### The requested connection is missing

Reopen the plugin in the target document and call `list_figma_connections` again. A reconnect can
replace the live socket while retaining the same invocation ID.

## More documentation

- [Architecture](ARCHITECTURE.md)
- [Development](DEVELOPMENT.md)
