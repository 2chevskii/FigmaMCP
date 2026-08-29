import { defineConfig } from "vitepress";
import { diagramPlugin } from "vitepress-plugin-mermaid-diagram";

export default defineConfig({
  base: process.env.DOCS_BASE ?? "/",
  title: "Figma MCP",
  description: "A local MCP companion for Figma documents.",
  cleanUrls: true,
  markdown: {
    config(md) {
      md.use(diagramPlugin, { preview: true });
    },
  },
  themeConfig: {
    nav: [
      { text: "Guide", link: "/" },
      { text: "Tool reference", link: "/tools" },
      { text: "API coverage", link: "/plugin-api-tool-coverage" },
    ],
    sidebar: [
      {
        text: "Figma MCP",
        items: [
          { text: "Overview", link: "/" },
          { text: "Architecture", link: "/architecture" },
          { text: "Development", link: "/development" },
        ],
      },
      {
        text: "Reference",
        items: [
          { text: "MCP tools", link: "/tools" },
          { text: "Plugin API coverage", link: "/plugin-api-tool-coverage" },
        ],
      },
    ],
    search: { provider: "local" },
    socialLinks: [{ icon: "github", link: "https://github.com/2chevskii/figma-mcp" }],
    footer: {
      message: "Released under the MIT License.",
      copyright: "Copyright © 2026 2CHEVSKII",
    },
  },
});
