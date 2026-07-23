import { build } from "esbuild";
import { mkdir, readFile, writeFile } from "node:fs/promises";

const watch = process.argv.includes("--watch");
const outputDirectory = "dist";
const main = { entryPoints: ["src/main.ts"], bundle: true, format: "iife", target: "es2020", outfile: `${outputDirectory}/plugin.js`, platform: "browser" };
const ui = { entryPoints: ["src/ui.ts"], bundle: true, format: "iife", target: "es2020", write: false, platform: "browser" };
async function run() {
  await mkdir(outputDirectory, { recursive: true });
  await build(main);
  const result = await build(ui);
  const template = await readFile("src/ui-template.html", "utf8");
  await writeFile(`${outputDirectory}/ui.html`, template.replace("<!-- UI_SCRIPT -->", `<script>${result.outputFiles[0].text}</script>`));
}
await run();
if (watch) console.log("Initial build complete; run npm run build after source changes.");
