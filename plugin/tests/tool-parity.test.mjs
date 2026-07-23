import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const operationFiles = [
  "src/operations/read.ts",
  "src/operations/nodes.ts",
  "src/operations/text-components.ts",
  "src/operations/styles-variables.ts",
  "src/operations/assets-editor.ts",
  "src/operations/metadata-motion.ts",
];

test("every registered document tool has a plugin handler", async () => {
  const [registrationTest, ...operationSources] = await Promise.all([
    readFile("../server/tests/FigmaMcp.Server.Tests/Mcp/FigmaToolsRegistrationTests.cs", "utf8"),
    ...operationFiles.map((path) => readFile(path, "utf8")),
  ]);

  const registeredNames = [...registrationTest.matchAll(/"([a-z][a-z0-9_]+)"/g)].map(
    (match) => match[1],
  );
  const handlers = operationSources.join("\n");

  for (const name of registeredNames) {
    if (name === "list_figma_connections") {
      continue;
    }
    const bridgeName = name === "get_figma_document_metadata" ? "get_document_metadata" : name;
    assert.match(
      handlers,
      new RegExp(`\\b${bridgeName}\\s*:`),
      `missing plugin handler for ${name}`,
    );
  }
});

test("the Design connector does not register editor-specific product tools", async () => {
  const registrationTest = await readFile(
    "../server/tests/FigmaMcp.Server.Tests/Mcp/FigmaToolsRegistrationTests.cs",
    "utf8",
  );

  assert.doesNotMatch(registrationTest, /\b(?:figjam|buzz|slide|codegen|textreview|payments)\b/i);
});

test("documentchange is registered only after all pages are loaded", async () => {
  const [readOperations, main] = await Promise.all([
    readFile("src/operations/read.ts", "utf8"),
    readFile("src/main.ts", "utf8"),
  ]);

  assert.match(
    readOperations,
    /await figma\.loadAllPagesAsync\(\);[\s\S]*figma\.on\(["']documentchange["']/,
  );
  assert.match(main, /try \{\s*await changeJournalReady;\s*const payload/);
});
