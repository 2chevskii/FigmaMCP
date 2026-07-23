import assert from "node:assert/strict";
import { build } from "esbuild";
import test from "node:test";
import vm from "node:vm";

async function loadUuidFactory() {
  const result = await build({ entryPoints: ["src/bridge/uuid.ts"], bundle: true, format: "cjs", platform: "node", write: false });
  const module = { exports: {} };
  vm.runInNewContext(result.outputFiles[0].text, { module, exports: module.exports, Uint8Array, Math });
  return module.exports.createConnectionId;
}

test("connection IDs use the native UUID method when it exists", async () => {
  const createConnectionId = await loadUuidFactory();
  assert.equal(createConnectionId({ randomUUID: () => "native-id", getRandomValues: () => { throw new Error("unexpected"); } }), "native-id");
});

test("connection IDs fall back to a UUID v4 when randomUUID is unavailable", async () => {
  const createConnectionId = await loadUuidFactory();
  const id = createConnectionId({ getRandomValues: bytes => bytes.fill(0) });
  assert.equal(id, "00000000-0000-4000-8000-000000000000");
});
