import assert from "node:assert/strict";
import test from "node:test";
import { linkKind } from "../src/links.js";
import { chooseAnchor } from "../src/position.js";

test("classifies links by what ProofMD does with them", () => {
  const cases = {
    "#limits": "fragment",
    "notes.md": "markdown",
    "../Notes%20Two.MARKDOWN#part": "markdown",
    "./sub/a.md?view=1": "markdown",
    "https://example.org/a.md": "external",
    "HTTP://example.org": "external",
    "mailto:someone@example.org": "external",
    "paper.pdf": "other",
    "folder/": "other",
    "?q": "other",
    "/root.md": "other",
    "//host/a.md": "other",
    "file:///C:/a.md": "other",
    "ms-settings:display": "other",
    "%ZZ.md": "other",
    "": "other",
  };
  for (const [href, kind] of Object.entries(cases)) {
    assert.equal(linkKind(href), kind, href);
  }
});

test("restores to the block containing the remembered line, else the nearest one", () => {
  const blocks = [
    { name: "a", range: { startLine: 1, endLine: 3 } },
    { name: "b", range: { startLine: 5, endLine: 9 } },
    { name: "c", range: { startLine: 14, endLine: 14 } },
  ];
  assert.equal(chooseAnchor(blocks, 7).name, "b");
  assert.equal(chooseAnchor(blocks, 12).name, "c");
  assert.equal(chooseAnchor(blocks, 4).name, "b");
  assert.equal(chooseAnchor([], 4), null);
});
