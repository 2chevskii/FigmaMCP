<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useData } from "vitepress";

interface ReleaseAsset {
  name: string;
  browser_download_url: string;
}

interface GitHubRelease {
  tag_name: string;
  html_url: string;
  assets: ReleaseAsset[];
}

const release = ref<GitHubRelease>();
const error = ref(false);
const { lang } = useData();
const isRussian = computed(() => lang.value.startsWith("ru"));

const packageVersion = computed(() => release.value?.tag_name.replace(/^v/, ""));
const serverArchives = computed(() =>
  release.value?.assets.filter(
    (asset) => asset.name.startsWith("figma-mcp-server-") && asset.name.endsWith(".zip"),
  ),
);
const pluginArchives = computed(() =>
  release.value?.assets.filter(
    (asset) => asset.name.startsWith("figma-mcp-plugin.") && asset.name.endsWith(".zip"),
  ),
);

onMounted(async () => {
  try {
    const response = await fetch(
      "https://api.github.com/repos/2chevskii/FigmaMCP/releases/latest",
      {
        headers: { Accept: "application/vnd.github+json" },
      },
    );

    if (!response.ok) {
      throw new Error(`GitHub API request failed with status ${response.status}`);
    }

    release.value = (await response.json()) as GitHubRelease;
  } catch {
    error.value = true;
  }
});
</script>

<template>
  <section class="latest-release" aria-live="polite">
    <p v-if="release">
      {{ isRussian ? "Последний выпуск:" : "Latest release:" }}
      <a :href="release.html_url"
        ><strong>{{ release.tag_name }}</strong></a
      >
    </p>
    <p v-else-if="error">
      {{
        isRussian
          ? "Не удалось загрузить последний выпуск с GitHub. Откройте"
          : "Could not load the latest release from GitHub. Visit the"
      }}
      <a href="https://github.com/2chevskii/FigmaMCP/releases/latest">{{
        isRussian ? "страницу последнего выпуска" : "latest release page"
      }}</a
      >.
    </p>
    <p v-else>
      {{
        isRussian
          ? "Загружаем данные о последнем выпуске с GitHub…"
          : "Loading latest release from GitHub…"
      }}
    </p>

    <template v-if="release && packageVersion">
      <h3>{{ isRussian ? "Запуск через" : "Run with" }} <code>dnx</code></h3>
      <pre><code>dnx FigmaMCP@{{ packageVersion }}</code></pre>
      <p>
        {{
          isRussian
            ? "Укажите эту версию выпуска в конфигурации MCP-клиента:"
            : "Configure an MCP client to start that exact release:"
        }}
      </p>
      <pre><code>{
  "mcpServers": {
    "figma": {
      "command": "dnx",
      "args": ["FigmaMCP@{{ packageVersion }}"]
    }
  }
}</code></pre>

      <h3>
        {{ isRussian ? "Установка глобального инструмента .NET" : "Install as a global .NET tool" }}
      </h3>
      <pre><code>dotnet tool install --global FigmaMCP --version {{ packageVersion }}</code></pre>

      <h3>{{ isRussian ? "Скачать архивы выпуска" : "Download release archives" }}</h3>
      <ul>
        <li v-for="asset in serverArchives" :key="asset.name">
          <a :href="asset.browser_download_url">{{ asset.name }}</a>
        </li>
        <li v-for="asset in pluginArchives" :key="asset.name">
          <a :href="asset.browser_download_url">{{ asset.name }}</a>
        </li>
      </ul>
    </template>
  </section>
</template>

<style scoped>
.latest-release {
  margin: 1.5rem 0;
  padding: 1rem 1.25rem;
  border: 1px solid var(--vp-c-divider);
  border-radius: 8px;
  background: var(--vp-c-bg-soft);
}

.latest-release pre {
  overflow-x: auto;
  padding: 0.75rem 1rem;
  border-radius: 6px;
  background: var(--vp-c-bg-alt);
}

.latest-release h3 {
  margin-top: 1.5rem;
}
</style>
