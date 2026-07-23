import { build, context } from "esbuild";
import { copyFile, mkdir, readFile, writeFile } from "node:fs/promises";

const OUTPUT_DIRECTORY = "dist";
const UI_SCRIPT_PLACEHOLDER = "<!-- UI_SCRIPT -->";
const watch = process.argv.includes("--watch");

const encodingPolyfill = await readFile("src/figma-encoding-polyfill.js", "utf8");
const mainOptions = {
  entryPoints: ["src/main.ts"],
  bundle: true,
  format: "iife",
  target: "es2020",
  outfile: `${OUTPUT_DIRECTORY}/plugin.js`,
  platform: "browser",
  banner: { js: encodingPolyfill },
};
const uiOptions = {
  entryPoints: ["src/ui.ts"],
  bundle: true,
  format: "iife",
  target: "es2020",
  write: false,
  platform: "browser",
};

await prepareOutputDirectory();

if (watch) {
  await watchBundles();
} else {
  await buildBundles();
}

async function prepareOutputDirectory() {
  await mkdir(OUTPUT_DIRECTORY, { recursive: true });
  await copyManifest();
}

async function buildBundles() {
  const [, uiResult] = await Promise.all([build(mainOptions), build(uiOptions)]);
  await writeUiHtml(uiResult);
}

async function watchBundles() {
  const writeUiPlugin = {
    name: "write-ui-html",
    setup(buildApi) {
      buildApi.onEnd(async (result) => {
        if (result.errors.length === 0) {
          await writeUiHtml(result);
        }
      });
    },
  };

  const [mainContext, uiContext] = await Promise.all([
    context(mainOptions),
    context({ ...uiOptions, plugins: [writeUiPlugin] }),
  ]);

  await Promise.all([mainContext.watch(), uiContext.watch()]);
  console.log(`Watching plugin sources; output is written to ${OUTPUT_DIRECTORY}/.`);
}

async function writeUiHtml(result) {
  const output = result.outputFiles?.[0];
  if (!output) {
    throw new Error("The UI bundle did not produce JavaScript output.");
  }

  const template = await readFile("src/ui-template.html", "utf8");
  if (!template.includes(UI_SCRIPT_PLACEHOLDER)) {
    throw new Error(`UI template is missing ${UI_SCRIPT_PLACEHOLDER}.`);
  }

  const html = template.replace(UI_SCRIPT_PLACEHOLDER, `<script>${output.text}</script>`);
  await writeFile(`${OUTPUT_DIRECTORY}/ui.html`, html);
}

function copyManifest() {
  return copyFile("manifest.json", `${OUTPUT_DIRECTORY}/manifest.json`);
}
