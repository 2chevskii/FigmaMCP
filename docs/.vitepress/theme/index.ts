import DefaultTheme from "vitepress/theme";
import type { Theme } from "vitepress";
import DiagramPreview from "vitepress-plugin-mermaid-diagram/DiagramPreview.vue";
import "vitepress-plugin-mermaid-diagram/diagram-dark.css";
import "./custom.css";

export default {
  extends: DefaultTheme,
  enhanceApp({ app }) {
    app.component("DiagramPreview", DiagramPreview);
  },
} satisfies Theme;
