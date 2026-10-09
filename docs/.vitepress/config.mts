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
  locales: {
    root: {
      label: "English",
      lang: "en-US",
      title: "Figma MCP",
      description: "A local MCP companion for Figma documents.",
    },
    ru: {
      label: "Русский",
      lang: "ru-RU",
      title: "Figma MCP",
      description: "Локальный MCP-компаньон для работы с документами Figma.",
      themeConfig: {
        nav: [
          { text: "Руководство", link: "/ru/" },
          { text: "Установка", link: "/ru/INSTALLATION" },
          { text: "Инструменты", link: "/ru/TOOLS" },
          { text: "Покрытие API", link: "/ru/plugin-api-tool-coverage" },
          { text: `Версия: ${documentationVersion}`, link: versionLink },
        ],
        sidebar: [
          {
            text: "Figma MCP",
            items: [
              { text: "Обзор", link: "/ru/" },
              { text: "Установка", link: "/ru/INSTALLATION" },
              { text: "Архитектура", link: "/ru/ARCHITECTURE" },
              { text: "Разработка", link: "/ru/DEVELOPMENT" },
            ],
          },
          {
            text: "Справочник",
            items: [
              { text: "Инструменты MCP", link: "/ru/TOOLS" },
              { text: "Покрытие Plugin API", link: "/ru/plugin-api-tool-coverage" },
            ],
          },
        ],
        search: {
          provider: "local",
          options: {
            locales: {
              ru: {
                translations: {
                  button: { buttonText: "Поиск", buttonAriaLabel: "Поиск" },
                  modal: {
                    noResultsText: "Ничего не найдено",
                    resetButtonTitle: "Очистить поиск",
                    footer: {
                      selectText: "выбрать",
                      navigateText: "перейти",
                      closeText: "закрыть",
                    },
                  },
                },
              },
            },
          },
        },
        docFooter: { prev: "Предыдущая страница", next: "Следующая страница" },
        outline: { label: "На этой странице" },
        langMenuLabel: "Выбрать язык",
        returnToTopLabel: "Наверх",
        sidebarMenuLabel: "Меню",
        darkModeSwitchLabel: "Оформление",
        lightModeSwitchTitle: "Светлая тема",
        darkModeSwitchTitle: "Тёмная тема",
        skipToContentLabel: "Перейти к содержимому",
        footer: {
          message: `Документация Figma MCP ${documentationVersion}. Распространяется по лицензии MIT.`,
          copyright: "Copyright © 2026 2CHEVSKII",
        },
      },
    },
  },
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
      { text: "Installation", link: "/INSTALLATION" },
      { text: "Tool reference", link: "/TOOLS" },
      { text: "API coverage", link: "/plugin-api-tool-coverage" },
      { text: `Version: ${documentationVersion}`, link: versionLink },
    ],
    sidebar: [
      {
        text: "Figma MCP",
        items: [
          { text: "Overview", link: "/" },
          { text: "Installation", link: "/INSTALLATION" },
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
    search: {
      provider: "local",
      options: {
        locales: {
          root: {
            translations: {
              button: { buttonText: "Search", buttonAriaLabel: "Search documentation" },
              modal: {
                noResultsText: "No results found",
                resetButtonTitle: "Clear search",
                footer: { selectText: "to select", navigateText: "to navigate", closeText: "to close" },
              },
            },
          },
        },
      },
    },
    socialLinks: [{ icon: "github", link: "https://github.com/2chevskii/figma-mcp" }],
    footer: {
      message: `Documentation for Figma MCP ${documentationVersion}. Released under the MIT License.`,
      copyright: "Copyright © 2026 2CHEVSKII",
    },
  },
});
