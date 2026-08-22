import test from "node:test";
import assert from "node:assert/strict";
import { loadTypescriptModule } from "./helpers/load-typescript-module.mjs";

test("server URL validation accepts only HTTP loopback roots", async () => {
  const { isServerUrl } = await loadTypescriptModule("src/config.ts", { URL });

  assert.equal(isServerUrl("http://localhost:3846"), true);
  assert.equal(isServerUrl("http://127.0.0.1:3846"), true);
  assert.equal(isServerUrl("https://figma-mcp.example.com"), false);
  assert.equal(isServerUrl("ws://localhost:3846"), false);
  assert.equal(isServerUrl("http://user:secret@localhost:3846"), false);
  assert.equal(isServerUrl("http://localhost:3846/?mode=local"), false);
  assert.equal(isServerUrl("http://localhost:3846/bridge"), false);
});

test("bridge URL uses only the configured loopback server", async () => {
  const { bridgeUrl } = await loadTypescriptModule("src/config.ts", { URL });

  assert.equal(bridgeUrl("http://127.0.0.1:3846"), "ws://127.0.0.1:3846/bridge");
});
