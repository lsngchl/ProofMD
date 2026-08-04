import assert from "node:assert/strict";
import {
  mkdtempSync,
  readFileSync,
  rmSync,
} from "node:fs";
import { tmpdir } from "node:os";
import path from "node:path";
import { spawnSync } from "node:child_process";
import test from "node:test";

const repositoryRoot = path.resolve(import.meta.dirname, "..");

test("builds a complete read-only LeanMD document set as one HTML file", () => {
  const temporaryDirectory = mkdtempSync(path.join(tmpdir(), "leanmd-build-test-"));
  const outputPath = path.join(temporaryDirectory, "example.html");

  try {
    const result = spawnSync(
      process.execPath,
      ["leanmd-build/build.js", "DocumentFormats/LeanMD/__example", outputPath],
      { cwd: repositoryRoot, encoding: "utf8" },
    );
    assert.equal(result.status, 0, result.stderr || result.stdout);

    const html = readFileSync(outputPath, "utf8");
    const metadataMatch = html.match(
      /<script id="leanmd-build-data" type="application\/json">([^<]+)<\/script>/u,
    );
    assert.ok(metadataMatch, "build metadata should be embedded");
    const metadata = JSON.parse(metadataMatch[1]);

    assert.equal(metadata.root, "root.md");
    assert.equal(
      new Set(metadata.documents.map(({ id }) => id)).size,
      metadata.documents.length,
    );
    assert.equal(
      (html.match(/<template data-document-id=/gu) ?? []).length,
      metadata.documents.length,
    );
    assert.equal(
      (html.match(/class="map-node(?: |")/gu) ?? []).length,
      metadata.documents.length,
    );
    assert.match(html, /class="katex"/u);
    assert.match(html, /url\(data:font\/woff2;base64,/u);
    assert.doesNotMatch(html, /<script[^>]+src=/iu);
    assert.doesNotMatch(html, /<link[^>]+rel="stylesheet"/iu);
    assert.doesNotMatch(html, /url\((?:["']?)fonts\//iu);
    assert.doesNotMatch(html, /id="unresolvedButton"|set-document-unresolved/u);
  } finally {
    rmSync(temporaryDirectory, { recursive: true, force: true });
  }
});
