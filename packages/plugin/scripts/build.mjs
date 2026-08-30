import { execFile, spawn } from "node:child_process";
import { copyFile, mkdir, readFile, writeFile } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import { promisify } from "node:util";
import { rolldown, watch as watchRolldown } from "rolldown";

const OUTPUT_DIRECTORY = "dist";
const COMPILED_DIRECTORY = ".build";
const UI_SCRIPT_PLACEHOLDER = "<!-- UI_SCRIPT -->";
const UI_BUNDLE_PATH = `${COMPILED_DIRECTORY}/ui.bundle.js`;
const watch = process.argv.includes("--watch");
const execFileAsync = promisify(execFile);
const packageMetadata = JSON.parse(await readFile("package.json", "utf8"));
const productVersion = process.env.FIGMA_MCP_VERSION ?? packageMetadata.version;

const encodingPolyfill = await readFile("src/figma-encoding-polyfill.js", "utf8");
const mainOptions = {
  input: `./${COMPILED_DIRECTORY}/main.js`,
  transform: {
    define: {
      FIGMA_MCP_PRODUCT_VERSION: JSON.stringify(productVersion),
    },
  },
};
const uiOptions = {
  input: `./${COMPILED_DIRECTORY}/ui.js`,
  transform: {
    define: {
      FIGMA_MCP_PRODUCT_VERSION: JSON.stringify(productVersion),
    },
  },
};

await prepareOutputDirectory();

if (watch) {
  await watchBundles();
} else {
  await buildBundles();
}

async function prepareOutputDirectory() {
  await mkdir(OUTPUT_DIRECTORY, { recursive: true });
  await Promise.all([copyManifest(), writeVersionMetadata()]);
}

async function buildBundles() {
  const [, uiResult] = await Promise.all([
    writeBundle(mainOptions, {
      file: `${OUTPUT_DIRECTORY}/plugin.js`,
      format: "iife",
      intro: encodingPolyfill,
    }),
    generateBundle(uiOptions),
  ]);
  await writeUiHtml(uiResult.output[0].code);
}

async function watchBundles() {
  await compile();
  await buildBundles();

  const compiler = startCompilerWatch();
  const watcher = watchRolldown([
    {
      ...mainOptions,
      output: {
        file: `${OUTPUT_DIRECTORY}/plugin.js`,
        format: "iife",
        intro: encodingPolyfill,
      },
    },
    {
      ...uiOptions,
      output: {
        file: UI_BUNDLE_PATH,
        format: "iife",
      },
    },
  ]);

  watcher.on("event", async (event) => {
    if (event.code === "BUNDLE_END") {
      if (event.output.some((output) => output.endsWith("ui.bundle.js"))) {
        await writeUiHtml(await readFile(UI_BUNDLE_PATH, "utf8"));
      }

      await event.result.close();
    }
  });

  const stop = async () => {
    compiler.kill();
    await watcher.close();
  };
  process.once("SIGINT", stop);
  process.once("SIGTERM", stop);
  console.log(`Watching plugin sources; output is written to ${OUTPUT_DIRECTORY}/.`);
}

async function generateBundle(input) {
  const bundle = await rolldown(input);
  try {
    return await bundle.generate({ format: "iife" });
  } finally {
    await bundle.close();
  }
}

async function writeBundle(input, output) {
  const bundle = await rolldown(input);
  try {
    await bundle.write(output);
  } finally {
    await bundle.close();
  }
}

async function writeUiHtml(script) {
  const template = await readFile("src/ui-template.html", "utf8");
  if (!template.includes(UI_SCRIPT_PLACEHOLDER)) {
    throw new Error(`UI template is missing ${UI_SCRIPT_PLACEHOLDER}.`);
  }

  const html = template.replace(UI_SCRIPT_PLACEHOLDER, `<script>${script}</script>`);
  await writeFile(`${OUTPUT_DIRECTORY}/ui.html`, html);
}

function copyManifest() {
  return copyFile("manifest.json", `${OUTPUT_DIRECTORY}/manifest.json`);
}

function writeVersionMetadata() {
  return writeFile(
    `${OUTPUT_DIRECTORY}/version.json`,
    `${JSON.stringify({ version: productVersion }, null, 2)}\n`,
  );
}

function startCompilerWatch() {
  return spawn(process.execPath, [getTscPath(), "--watch"], {
    stdio: "inherit",
  });
}

function compile() {
  return execFileAsync(process.execPath, [getTscPath()]);
}

function getTscPath() {
  return fileURLToPath(new URL("../node_modules/@typescript/native/bin/tsc", import.meta.url));
}
