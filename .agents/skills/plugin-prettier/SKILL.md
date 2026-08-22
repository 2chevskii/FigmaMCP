---
name: plugin-prettier
description: Format or verify formatting of the Figma plugin JavaScript and TypeScript project with its local Prettier package. Use for files under plugin/; do not use for C# server formatting.
---

# Prettier for the Figma plugin

Run formatting commands from `plugin/`. Prettier is a development dependency declared in
[`plugin/package.json`](../../../plugin/package.json); use that local package rather than installing
or invoking a global formatter.

Node.js is supplied by FNM through the user's PowerShell profile. Run npm commands in a profile-enabled
PowerShell session. Prefer the project scripts:

```powershell
# Working directory: plugin/
rtk pwsh -Command 'npm run format:check'
```

If RTK cannot start because its local configuration is unavailable, invoke the same profile-enabled
`pwsh` command directly and report the fallback. If local dependencies are absent, restore them with
`npm ci` before running Prettier.

Before applying formatting, inspect `git status --short`: formatting writes files and can include
pre-existing user edits. For an explicit request to apply formatting, run:

```powershell
# Working directory: plugin/
rtk pwsh -Command 'npm run format'
```

The existing scripts format the plugin and `docs/`; account for both locations when reviewing a
formatting diff. Do not run `format` when the user requested only validation. After applying changes,
run `npm run format:check` and `git diff --check`, then report the changed files.
