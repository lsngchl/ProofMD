import assert from "node:assert/strict";
import test from "node:test";
import {
  activeProofFoldIndex,
  buildProofFoldMap,
  createProofFoldIndex,
  findFold,
  resolveProofFoldTarget,
} from "../src/proof-fold.js";
import { foldLinkTargets, renderMarkdown } from "../src/renderer.js";

test("indexes fold sources and finds them regardless of case", () => {
  const index = createProofFoldIndex({
    entry: "main.md",
    folds: [
      { path: "folds/First.md", source: "First" },
      { path: "folds/nested/second.md", source: "Second" },
    ],
  });

  assert.equal(index.entry, "main.md");
  assert.equal(findFold(index, "folds/first.md").path, "folds/First.md");
  assert.equal(findFold(index, "FOLDS/NESTED/SECOND.MD").source, "Second");
  assert.equal(findFold(index, "folds/missing.md"), null);
  assert.equal(createProofFoldIndex(null), null);
});

test("resolves fold targets relative to their containing document", () => {
  assert.equal(resolveProofFoldTarget("main.md", "./folds/first.md"), "folds/first.md");
  assert.equal(resolveProofFoldTarget("folds/first.md", "./nested/second.md"), "folds/nested/second.md");
  assert.equal(resolveProofFoldTarget("folds/nested/second.md", "../shared.md#calculation"), "folds/shared.md");
  assert.equal(resolveProofFoldTarget("main.md", "folds/space%20name.md"), "folds/space name.md");
});

test("rejects fold targets that escape the document folder or are not Markdown", () => {
  assert.equal(resolveProofFoldTarget("main.md", "../outside.md"), null);
  assert.equal(resolveProofFoldTarget("main.md", "https://example.com/fold.md"), null);
  assert.equal(resolveProofFoldTarget("main.md", "./folds/data.json"), null);
  assert.equal(resolveProofFoldTarget("main.md", "%ZZ.md"), null);
});

test("finds fold links exactly where rendering does", () => {
  const source = [
    "[Plain](folds/a.md) [Wrapped",
    "label](folds/b.md \"fold\") and [$[0,1]$ bound](folds/c.md 'Fold').",
    "",
    "`[Code](folds/d.md \"fold\")`",
    "",
    "    [Indented](folds/e.md \"fold\")",
    "",
    "[Reference][r]",
    "",
    "[r]: folds/f%20g.md \"fold\"",
  ].join("\n");

  assert.deepEqual(foldLinkTargets(source), ["folds/b.md", "folds/c.md", "folds/f%20g.md"]);
});

test("builds the fold map from the entry in link order", () => {
  const index = createProofFoldIndex({
    entry: "main.md",
    folds: [
      { path: "folds/a.md", source: "[Shared](shared.md \"fold\")" },
      { path: "folds/b.md", source: "[Shared](shared.md \"fold\") [Back](a.md \"fold\")" },
      { path: "folds/shared.md", source: "Leaf" },
      { path: "folds/unlinked.md", source: "Never reached" },
    ],
  });
  const map = buildProofFoldMap(
    index,
    "[B](folds/B.md \"fold\")\n\n[A](folds/a.md \"fold\") [A again](folds/a.md \"fold\")",
    foldLinkTargets,
  );

  assert.equal(map.root, "main.md");
  assert.deepEqual(map.nodes.map((node) => node.id), ["main.md", "folds/b.md", "folds/a.md", "folds/shared.md"]);
  assert.deepEqual(
    map.edges.map((edge) => `${edge.from}>${edge.to}`),
    ["main.md>folds/b.md", "main.md>folds/a.md", "folds/b.md>folds/shared.md", "folds/b.md>folds/a.md", "folds/a.md>folds/shared.md"],
  );
  assert.equal(map.nodes[1].label, "b");
  assert.equal(map.nodes[0].detail, "ProofFold entry");
});

test("keeps footnote anchors unique across independently rendered folds", () => {
  const source = "Text[^note].\n\n[^note]: Detail.";
  const first = renderMarkdown(source, { documentId: "folds/first.md", instanceId: 1 });
  const second = renderMarkdown(source, { documentId: "folds/first.md", instanceId: 2 });

  assert.notEqual(first.match(/id="(fnref-[^"]+)"/u)[1], second.match(/id="(fnref-[^"]+)"/u)[1]);
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
