# Contributing to Figma MCP

## Before you start

- Read the [README](README.md) and the [normative specification](.agents/SPEC.md).
- For transport, bridge, or MCP tool changes, read the documents in the order listed in
  [AGENTS.md](AGENTS.md).

## Change boundaries

- MCP uses STDIO only: do not write text or logs to `stdout`.
- The bridge listens at `127.0.0.1:3846/bridge` and uses `figma-mcp-bridge.v2`.
- Every document-specific MCP call requires an active `connection_id`.
- Plugin changes preserve the typed MessagePack protocol, payload limits, and Figma Plugin API
  constraints.

## Validate before opening a pull request

```powershell
Push-Location .\packages\server
dotnet format FigmaMcp.slnx --verify-no-changes --no-restore
dotnet build FigmaMcp.slnx --configuration Release
dotnet test --solution FigmaMcp.slnx --configuration Release
Pop-Location

npm ci --prefix .\packages\plugin
npm run format:check --prefix .\packages\plugin
npm run lint --prefix .\packages\plugin
npm run typecheck --prefix .\packages\plugin
npm test --prefix .\packages\plugin
npm run build --prefix .\packages\plugin
```

Also run `git diff --check`. If the plugin, transport, or tool contract changed, validate the
scenario in Figma Desktop; synthetic bridge tests do not replace that check.

## Pull request

Describe the user goal, architectural effect, and completed validation. Do not include generated
`bin/`, `obj/`, `packages/plugin/dist/`, `packages/plugin/node_modules/`, personal IDE settings, or
secrets.
