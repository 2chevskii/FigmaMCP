import test from "node:test";
import assert from "node:assert/strict";
import { loadTypescriptModule } from "./helpers/load-typescript-module.mjs";

test("server port validation accepts integer ports in range", async () => {
  const { isServerPort, parseServerPort } = await loadTypescriptModule("src/config.ts");

  assert.equal(isServerPort(1), true);
  assert.equal(isServerPort(65535), true);
  assert.equal(isServerPort(0), false);
  assert.equal(isServerPort(65536), false);
  assert.equal(isServerPort(3846.5), false);
  assert.equal(parseServerPort("3846"), 3846);
  assert.equal(parseServerPort(" 3846"), undefined);
  assert.equal(parseServerPort("3846.5"), undefined);
});

test("bridge URL uses the configured local port", async () => {
  const { bridgeUrl } = await loadTypescriptModule("src/config.ts");

  assert.equal(bridgeUrl(3846), "ws://localhost:3846/bridge");
});
