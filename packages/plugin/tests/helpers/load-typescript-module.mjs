import { build } from "esbuild";
import vm from "node:vm";

export async function loadTypescriptModule(entryPoint, globals = {}) {
  const result = await build({
    entryPoints: [entryPoint],
    bundle: true,
    format: "cjs",
    platform: "node",
    write: false,
  });
  const module = { exports: {} };
  const sandbox = {
    module,
    exports: module.exports,
    Uint8Array,
    Math,
    ...globals,
  };

  vm.runInNewContext(result.outputFiles[0].text, sandbox);
  return module.exports;
}
