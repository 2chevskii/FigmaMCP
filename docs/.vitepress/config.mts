import { defineConfig } from "vitepress";
import { diagramPlugin } from "vitepress-plugin-mermaid-diagram";

const releaseTag = process.env.DOCS_VERSION?.trim();
if (releaseTag && !/^v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$/.test(releaseTag)) {
  throw new Error(
    `DOCS_VERSION must use the stable release tag format vMAJOR.MINOR.PATCH, received: ${releaseTag}`,
  );
}

const documentationVersion = releaseTag ?? "development";
const documentationBase = process.env.DOCS_BASE ?? "/";
const versionLink = releaseTag
  ? `https://github.com/2chevskii/figma-mcp/releases/tag/${releaseTag}`
  : "https://github.com/2chevskii/figma-mcp";

export default defineConfig({
  base: documentationBase,
  title: "Figma MCP",
  description: "A local MCP companion for Figma documents.",
  head: [
    [
      "link",
      {
        rel: "icon",
        type: "image/png",
        href: `${documentationBase}branding/figmamcp-icon.png`,
      },
    ],
  ],
  cleanUrls: true,
  markdown: {
    config(md) {
      md.use(diagramPlugin, { preview: true });
    },
  },
  themeConfig: {
    logo: { src: "/branding/figmamcp-icon.png", alt: "FigmaMCP" },
    nav: [
      { text: "Guide", link: "/" },
      { text: "Tool reference", link: "/TOOLS" },
      { text: "API coverage", link: "/plugin-api-tool-coverage" },
      { text: `Version: ${documentationVersion}`, link: versionLink },
    ],
    sidebar: [
      {
        text: "Figma MCP",
        items: [
          { text: "Overview", link: "/" },
          { text: "Architecture", link: "/ARCHITECTURE" },
          { text: "Development", link: "/DEVELOPMENT" },
        ],
      },
      {
        text: "Reference",
        items: [
          { text: "MCP tools", link: "/TOOLS" },
          { text: "Plugin API coverage", link: "/plugin-api-tool-coverage" },
        ],
      },
    ],
    search: { provider: "local" },
    socialLinks: [{ icon: "github", link: "https://github.com/2chevskii/figma-mcp" }],
    footer: {
      message: `Documentation for Figma MCP ${documentationVersion}. Released under the MIT License.`,
      copyright: "Copyright © 2026 2CHEVSKII",
    },
  },
});
