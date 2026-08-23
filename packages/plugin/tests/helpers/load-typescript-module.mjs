import { rolldown } from "rolldown";
import vm from "node:vm";

export async function loadTypescriptModule(entryPoint, globals = {}) {
  const bundle = await rolldown({ input: entryPoint });
  const result = await bundle.generate({ format: "cjs" });
  await bundle.close();
  const module = { exports: {} };
  const sandbox = {
    module,
    exports: module.exports,
    Uint8Array,
    Math,
    ...globals,
  };

  vm.runInNewContext(result.output[0].code, sandbox);
  return module.exports;
}
