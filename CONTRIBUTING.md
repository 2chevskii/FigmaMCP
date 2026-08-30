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

Run the root targets relevant to each changed component. The server targets are `:server:format` and
`:server:test`; the plugin targets are `:plugin:format`, `:plugin:lint`, `:plugin:test`, and
`:plugin:build`. Use `--dryrun` with any target to inspect its dependency graph. On Linux and macOS,
use `bash ./build.sh` instead. Also run `git diff --check`. If the plugin, transport, or tool contract
changed, validate the scenario in Figma Desktop; synthetic bridge tests do not replace that check.

Install the repository `commit-msg` hook once after cloning:

```powershell
./build.ps1 --target :commits:hook:install
```

Every commit must follow Conventional Commits. Use `feat:` for a feature, `fix:` for a correction,
and add `!` before the colon for a breaking change. The remaining conventional types such as `docs:`,
`test:`, `build:`, and `ci:` are valid but do not advance the product version. CI validates the full
commit range independently of the local hook.

## Pull request

Describe the user goal, architectural effect, and completed validation. Do not include generated
`bin/`, `obj/`, `packages/plugin/dist/`, `packages/plugin/node_modules/`, personal IDE settings, or
secrets. Pull request titles are free-form. The repository uses squash merges, so set the final squash
commit message to a valid Conventional Commit before merging. Work in any short-lived branch; only
`master` is long-lived.
