import assert from "node:assert/strict";
import test from "node:test";
import { readStoredTheme } from "../src/theme-storage.js";

function storage(values) {
  const data = new Map(Object.entries(values));
  return {
    getItem: (key) => data.get(key) ?? null,
    setItem: (key, value) => data.set(key, value),
  };
}

test("migrates the saved LeanMD theme", () => {
  const saved = storage({ "leanmd-theme": "dark" });
  assert.equal(readStoredTheme(saved), "dark");
  assert.equal(saved.getItem("proofmd-theme"), "dark");
});

test("keeps an existing ProofMD preference on subsequent launches", () => {
  const saved = storage({ "leanmd-theme": "dark", "proofmd-theme": "light" });
  assert.equal(readStoredTheme(saved), "light");
  assert.equal(saved.getItem("proofmd-theme"), "light");
});

test("ignores invalid preferences and retains a read-only legacy preference", () => {
  assert.equal(readStoredTheme(storage({ "leanmd-theme": "invalid" })), null);
  const saved = storage({ "leanmd-theme": "dark" });
  saved.setItem = () => { throw new Error("Storage is read-only"); };
  assert.equal(readStoredTheme(saved), "dark");
});
