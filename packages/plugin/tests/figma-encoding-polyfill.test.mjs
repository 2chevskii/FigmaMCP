import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import vm from "node:vm";

test("encoding fallback supports MessagePack's large UTF-8 strings", async () => {
  const source = await readFile(
    new URL("../src/figma-encoding-polyfill.js", import.meta.url),
    "utf8",
  );
  const sandbox = { Uint8Array, ArrayBuffer };
  sandbox.globalThis = sandbox;
  vm.runInNewContext(source, sandbox);

  const value = "Figma 🚀 ".repeat(80);
  const bytes = new sandbox.TextEncoder().encode(value);
  assert.equal(new sandbox.TextDecoder().decode(bytes), value);
  assert.equal(bytes.length > 200, true);
});
