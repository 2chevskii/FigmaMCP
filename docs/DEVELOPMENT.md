# Development

## Repository layout

```text
.
├── docs/
├── plugin/
│   ├── scripts/
│   ├── src/
│   ├── tests/
│   └── dist/              # generated
└── server/
    ├── src/
    │   └── FigmaMcp.Server/
    ├── tests/
    │   └── FigmaMcp.Server.Tests/
    └── FigmaMcp.slnx
```

All server source and test projects live under `server/`. Plugin build artifacts live under
`plugin/dist/` and are not committed.

## Plugin workflow

Run from `plugin/`:

```powershell
npm install
npm run format:check
npm run lint
npm run typecheck
npm test
npm run build
```

Use `npm run format` to apply Prettier to plugin sources and project documentation. Use
`npm run watch` during plugin development; it rebuilds both bundles and regenerates `dist/ui.html`
when UI sources change.

The controller bundle injects a small UTF-8 encoding fallback before MessagePack loads because the
Figma sandbox does not expose `TextEncoder` or `TextDecoder`. The UI UUID helper uses
`crypto.randomUUID` when available and falls back to UUID v4 generation for older Figma runtimes.

## Server workflow

Run from `server/`:

```powershell
dotnet format FigmaMcp.slnx --verify-no-changes --no-restore
dotnet build FigmaMcp.slnx --configuration Release
dotnet run --project tests/FigmaMcp.Server.Tests --configuration Release -- --no-progress
```

The solution uses the .NET 10 Microsoft.Testing.Platform runner selected in `server/global.json`.
Dependencies use central package management in `server/Directory.Packages.props`.

## Publish

Create a self-contained Windows x64 executable from `server/`:

```powershell
dotnet publish src/FigmaMcp.Server -p:PublishProfile=win-x64
```

## Formatting rules

The root `.editorconfig` defines whitespace and line-ending rules across the repository. Prettier
formats TypeScript, JavaScript, JSON, HTML, and Markdown. `dotnet format` formats C#.

Run both formatting checks before committing changes that cross the plugin/server boundary.
