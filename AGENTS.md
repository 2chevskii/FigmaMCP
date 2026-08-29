# Project context for agents

## Project

`figma-mcp` is a local Figma companion. An MCP client starts one .NET process and communicates with
it through STDIO. The process also serves the loopback WebSocket `/bridge` for the Figma Bridge plugin.

Change the Bridge plugin only within the scope explicitly requested by the user. Do not change its
source, manifest, UI, settings, or `figma-mcp-bridge.v2` protocol without explicit direction.

## Required reading

Read these documents in order before architecture or server changes:

1. [`.agents/SPEC.md`](.agents/SPEC.md) — normative specification and project scope.
2. [`docs/index.md`](docs/index.md) — product overview and entry points.
3. [`docs/architecture.md`](docs/architecture.md) — transports, bridge lifecycle, state, and security
   boundary.
4. [`docs/development.md`](docs/development.md) — repository layout, build commands, and validation.
5. [`docs/tools.md`](docs/tools.md) — MCP tool contract, schemas, `connection_id`, limits, and errors.
6. [`docs/plugin-api-tool-coverage.md`](docs/plugin-api-tool-coverage.md) — Figma Plugin API coverage,
   deferred capabilities, and manifest constraints.

The public repository documents complement this technical documentation:

- [`README.md`](README.md) — overview, quick start, and validation status.
- [`CONTRIBUTING.md`](CONTRIBUTING.md) — contribution requirements.
- [`.github/SECURITY.md`](.github/SECURITY.md) — vulnerability reporting.

When documents conflict, use this precedence:
the latest explicit user instruction → `AGENTS.md` → the normative sections of `.agents/SPEC.md` →
`docs/architecture.md` → other documents → existing code.

## Local agent skills

- [`.agents/skills/dotnet-csharpier/SKILL.md`](.agents/skills/dotnet-csharpier/SKILL.md) formats and
  checks the C#/XML server project with the local CSharpier .NET tool. Use it for formatting work in
  `packages/server/`.
- [`.agents/skills/plugin-prettier/SKILL.md`](.agents/skills/plugin-prettier/SKILL.md) formats and
  checks the Figma plugin through its local Prettier package. Use it for formatting work in
  `packages/plugin/`.
- [`.agents/skills/repository-commits/SKILL.md`](.agents/skills/repository-commits/SKILL.md) prepares
  Conventional Commits and groups unrelated changes by purpose and timing. Use it only when the user
  explicitly requests commits.

## Implementation invariants

- MCP reads the protocol from `stdin`, writes protocol messages only to `stdout`, and writes logs and
  diagnostics to `stderr`.
- The bridge uses WebSocket/MessagePack `figma-mcp-bridge.v2` at `127.0.0.1:3846/bridge`; do not bind
  it to an external interface.
- Every document-specific MCP tool requires a live explicit `connection_id`.
- The plugin connection registry and pending RPCs are in memory. Requests for one connection run
  sequentially, and a stale socket cannot remove a replacement connection.
- Preserve bounded typed payloads, size limits, a 30-second bridge RPC deadline, and idempotent
  mutations.
- Do not add databases, external services, packages, test infrastructure, or configuration beyond
  the user's explicit scope.

## Working rules

- Before changing files, inspect `git status --short`: the worktree can contain unrelated user edits.
  Do not revert or overwrite them without explicit permission.
- Use .NET 10 and central package management for server projects; use the existing npm scripts for the plugin.
- After a substantive change, run appropriate builds and tests. Documentation changes require at least
  `git diff --check`.
- When a change affects a transport, the bridge protocol, MCP tools, or the plugin, consult the
  corresponding documents above and update documentation with the code.
