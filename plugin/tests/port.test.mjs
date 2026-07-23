import test from "node:test";
import assert from "node:assert/strict";
import { loadTypescriptModule } from "./helpers/load-typescript-module.mjs";

test("port validation accepts the configured range", async () => {
  const { isPort } = await loadTypescriptModule("src/config.ts");

  assert.equal(isPort("1"), true);
  assert.equal(isPort("65535"), true);
  assert.equal(isPort("0"), false);
  assert.equal(isPort("65536"), false);
});
