---
name: dotnet-csharpier
description: Format or verify formatting of the repository's .NET server with the local CSharpier .NET tool. Use for C# or XML formatting under server/; do not use for TypeScript or Markdown formatting.
---

# CSharpier for the .NET server

Use the local CSharpier tool declared in
[`server/.config/dotnet-tools.json`](../../../server/.config/dotnet-tools.json). The server is the
formatting scope; do not run CSharpier against `plugin/` or Markdown documentation.

1. Inspect `git status --short` before applying formatting. Formatting changes files, including
   pre-existing user edits.
2. Run the commands from `server/` so the local tool manifest is discovered:

   ```powershell
   rtk dotnet tool restore
   rtk dotnet csharpier check .
   ```

   If RTK cannot start because its local configuration is unavailable, invoke the same `dotnet`
   commands directly and report the fallback.
3. For a request to apply formatting, after tool restore run:

   ```powershell
   rtk dotnet csharpier format .
   ```

4. After applying formatting, review `git diff --check` and report the files changed. Do not run
   `format` when the user requested only validation or a check.

`check` verifies formatting without writing files. `format` changes C# and supported project files
to CSharpier's canonical style.
