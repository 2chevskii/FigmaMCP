declare module "vitepress-plugin-mermaid-diagram/DiagramPreview.vue" {
  import type { DefineComponent } from "vue";

  const DiagramPreview: DefineComponent;
  export default DiagramPreview;
}

declare module "*.vue" {
  import type { DefineComponent } from "vue";

  const component: DefineComponent;
  export default component;
}

declare module "vitepress-plugin-mermaid-diagram/diagram-dark.css";
declare module "*.css";
