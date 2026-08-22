import assert from "node:assert/strict";
import test from "node:test";
import { loadTypescriptModule } from "./helpers/load-typescript-module.mjs";

test("connection IDs use the native UUID method when it exists", async () => {
  const { createConnectionId } = await loadTypescriptModule("src/bridge/uuid.ts");
  assert.equal(
    createConnectionId({
      randomUUID: () => "native-id",
      getRandomValues: () => {
        throw new Error("unexpected");
      },
    }),
    "native-id",
  );
});

test("connection IDs fall back to a UUID v4 when randomUUID is unavailable", async () => {
  const { createConnectionId } = await loadTypescriptModule("src/bridge/uuid.ts");
  const id = createConnectionId({ getRandomValues: (bytes) => bytes.fill(0) });
  assert.equal(id, "00000000-0000-4000-8000-000000000000");
});
