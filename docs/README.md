# Figma MCP connector

This repository provides a local .NET companion in `server/` and a Figma plugin in `plugin/`. It exposes connection discovery and bounded document metadata only; it does not mutate Figma or traverse scene trees.

## Run

Install the .NET 10.0.302 SDK, then run `dotnet run --project src/FigmaMcp.Server -- --port 3846` from `server/`. Configure an MCP client with `http://127.0.0.1:3846/mcp`; health is at `http://127.0.0.1:3846/health`.

Publish a self-contained Windows executable from `server/` with `dotnet publish src/FigmaMcp.Server -p:PublishProfile=win-x64`.

## Plugin

Run `npm install`, `npm run typecheck`, `npm run lint`, `npm test`, and `npm run build` in `plugin/`. The build writes the Figma entry points to `plugin/dist/plugin.js` and `plugin/dist/ui.html`. In Figma Desktop, import `plugin/manifest.json` as a development plugin and open it in each document. The UI persists its port and reconnects with bounded jittered backoff.

The local bridge requires `figma-mcp-bridge.v1` and MessagePack binary frames. It is bound only to `127.0.0.1`; the server rejects unexpected Hosts, browser origins at `/mcp`, unsupported bridge origins, text frames, and messages over 1 MiB.

Call `list_figma_connections` before document work and explicitly pass the live `connection_id` to `get_figma_document_metadata`.
