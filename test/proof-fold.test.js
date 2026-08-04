import assert from "node:assert/strict";
import test from "node:test";
import {
  activeProofFoldIndex,
  createProofFoldIndex,
  resolveProofFoldTarget,
} from "../src/proof-fold.js";
import { renderMarkdown } from "../src/renderer.js";

test("indexes a valid ProofFold payload", () => {
  const index = createProofFoldIndex({
    entry: "main.md",
    folds: [
      { path: "folds/first.md", source: "First" },
      { path: "folds/nested/second.md", source: "Second" },
    ],
  });

  assert.equal(index.entry, "main.md");
  assert.equal(index.folds.size, 2);
  assert.equal(index.folds.get("folds/nested/second.md").source, "Second");
});

test("rejects malformed or duplicate ProofFold documents", () => {
  assert.equal(createProofFoldIndex(null), null);
  assert.equal(createProofFoldIndex({ entry: "../main.md", folds: [] }), null);
  assert.equal(createProofFoldIndex({
    entry: "main.md",
    folds: [
      { path: "folds/a.md", source: "A" },
      { path: "folds/a.md", source: "B" },
    ],
  }), null);
});

test("resolves fold targets relative to their containing document", () => {
  assert.equal(
    resolveProofFoldTarget("main.md", "./folds/first.md"),
    "folds/first.md",
  );
  assert.equal(
    resolveProofFoldTarget("folds/first.md", "./nested/second.md"),
    "folds/nested/second.md",
  );
  assert.equal(
    resolveProofFoldTarget("folds/nested/second.md", "../shared.md#calculation"),
    "folds/shared.md",
  );
  assert.equal(
    resolveProofFoldTarget("main.md", "folds/space%20name.md"),
    "folds/space name.md",
  );
});

test("rejects fold targets that escape the document root or are not Markdown", () => {
  assert.equal(resolveProofFoldTarget("main.md", "../outside.md"), null);
  assert.equal(resolveProofFoldTarget("main.md", "https://example.com/fold.md"), null);
  assert.equal(resolveProofFoldTarget("main.md", "./folds/data.json"), null);
  assert.equal(resolveProofFoldTarget("main.md", "%ZZ.md"), null);
});

test("keeps footnote anchors unique across independently rendered folds", () => {
  const source = "Text[^note].\n\n[^note]: Detail.";
  const first = renderMarkdown(source, { documentId: "folds/first.md" });
  const second = renderMarkdown(source, { documentId: "folds/second.md" });

  assert.match(first, /id="fnref-[^"]+-1"/u);
  assert.match(second, /id="fnref-[^"]+-1"/u);
  assert.notEqual(
    first.match(/id="(fnref-[^"]+-1)"/u)[1],
    second.match(/id="(fnref-[^"]+-1)"/u)[1],
  );
});

test("selects the deepest open fold crossing the reading line", () => {
  assert.equal(activeProofFoldIndex([
    { top: 80, bottom: 900 },
    { top: 220, bottom: 720 },
    { top: 760, bottom: 980 },
  ], 360, 800), 1);
});

test("falls back to the nearest visible fold", () => {
  assert.equal(activeProofFoldIndex([
    { top: 40, bottom: 120 },
    { top: 500, bottom: 650 },
    { top: 900, bottom: 980 },
  ], 340, 800), 1);
  assert.equal(activeProofFoldIndex([], 340, 800), -1);
});
