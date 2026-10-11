---
title: Installation
description: Install and connect the Figma MCP companion and Bridge plugin.
---

# Installation

Figma MCP needs the companion server, the Figma Bridge plugin, and an MCP client. The server runs
locally and communicates with Figma Desktop through the plugin.

<LatestRelease />

## Install the companion

The version and asset links above are loaded from GitHub's
[`releases/latest` API](https://api.github.com/repos/2chevskii/FigmaMCP/releases/latest). Use the
displayed release version when configuring your installation.

### Run with `dnx` without installing

With the .NET 10 SDK installed, `dnx` downloads the specified tool to the NuGet cache and runs it
without a persistent tool installation. The versioned command and MCP configuration appear above.

### Install as a .NET tool

Install the .NET 10 runtime, then use the versioned command shown above. The global tool command
installs the matching release from NuGet.org.

The MCP client runs the installed `figma-mcp-server` command as a child process.

### Use a self-contained release

Choose the server archive for your platform from the versioned links above.

Extract the archive and configure the executable as the MCP server command. Use `FigmaMCP.exe` on
Windows and `FigmaMCP` on Linux or macOS. These archives do not require a .NET installation.

## Install the Figma Bridge plugin

Download the versioned plugin archive from the links above and extract it. In Figma Desktop, import
the extracted `manifest.json` as a development plugin, then open the plugin in the document you want
to use.

Keep the plugin Bridge port at `3846` unless you set another server port. The companion listens on
`127.0.0.1:3846/bridge` by default. If that port is occupied and no explicit port was set, the server
selects a subsequent available port and reports it on `stderr`; use that port in the plugin settings.
An explicit `--port <1-65535>` value is never changed.

## Connect an MCP client

Import the repository's [`mcp.json`](https://github.com/2chevskii/FigmaMCP/blob/master/mcp.json) into
an MCP client that supports server configuration imports:

```json
{
  "mcpServers": {
    "figma": {
      "command": "figma-mcp-server"
    }
  }
}
```

For a self-contained release, set `command` to the executable's full path. For example, on Windows:

```json
{
  "mcpServers": {
    "figma": {
      "command": "C:\\path\\to\\FigmaMCP.exe"
    }
  }
}
```

If the client does not support importing a file, add the matching configuration to its MCP server
settings. The client starts the companion process over STDIO; leave its standard output available for
the MCP protocol.

## Verify the connection

With the MCP client running and the plugin open in a Figma document, call `list_figma_connections`.
Use the returned `connection_id` with document-specific tools. See the [tool reference](/TOOLS) for
available operations and their requirements.

To update a .NET tool installation, run `dotnet tool update --global FigmaMCP`. For release archives,
use the latest versioned asset links above and update the executable path in the MCP client if needed.
