import test from "node:test";
import assert from "node:assert/strict";
test("port validation accepts the configured range", () => {
  const valid = (value) => /^(?:[1-9]\d{0,4})$/.test(value) && Number(value) <= 65535;
  assert.equal(valid("1"), true);
  assert.equal(valid("65535"), true);
  assert.equal(valid("0"), false);
  assert.equal(valid("65536"), false);
});
