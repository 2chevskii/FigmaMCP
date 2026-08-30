![FigmaMCP — A local bridge between MCP and your Figma canvas](https://raw.githubusercontent.com/2chevskii/FigmaMCP/master/assets/branding/figmamcp-banner.png)

# FigmaMCP

FigmaMCP is a local .NET tool that lets MCP clients read and edit the active Figma Design document
through the FigmaMCP Bridge plugin.

The MCP server communicates with its client over STDIO and accepts plugin connections through a
loopback-only WebSocket. Document operations stay on the local machine.

## Requirements

- Figma Desktop.
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
- The FigmaMCP Bridge plugin from the matching
  [GitHub Release](https://github.com/2chevskii/FigmaMCP/releases).

## Install

```shell
dotnet tool install --global FigmaMCP
```

Extract `figma-mcp-plugin.<version>.zip` from the matching release and import its `manifest.json` in
Figma Desktop as a development plugin.

## Configure an MCP client

Configure the client to start the installed tool:

```json
{
  "mcpServers": {
    "figma": {
      "command": "figma-mcp-server"
    }
  }
}
```

Open the Bridge plugin in the Figma document you want to use. The plugin connects to
`127.0.0.1:3846` by default. If the server selects a fallback port or is started with
`--port <1-65535>`, enter that port in the plugin.

Call `list_figma_connections`, then pass the returned `connection_id` to document-specific tools.

## Update or uninstall

```shell
dotnet tool update --global FigmaMCP
dotnet tool uninstall --global FigmaMCP
```

## Links

- [Documentation](https://2chevskii.github.io/FigmaMCP/)
- [GitHub repository](https://github.com/2chevskii/FigmaMCP)
- [Releases](https://github.com/2chevskii/FigmaMCP/releases)
- [Issues](https://github.com/2chevskii/FigmaMCP/issues)

FigmaMCP is not affiliated with Figma. The project is distributed under the
[MIT License](https://github.com/2chevskii/FigmaMCP/blob/master/LICENSE).
