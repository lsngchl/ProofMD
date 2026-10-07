import assert from "node:assert/strict";
import test from "node:test";
import { renderMarkdown } from "../src/renderer.js";

function anchorIds(html) {
  return [...html.matchAll(/<a\b[^>]*\bid="(proofmd-anchor-[^"]+)"[^>]*><\/a>/gu)]
    .map((match) => match[1]);
}

test("renders a standalone anchor without an empty paragraph or disrupting nearby math", () => {
  const html = renderMarkdown([
    "[Jump](#descendant-limits)",
    "",
    '<a id="descendant-limits"></a>',
    "## Descendant limits",
    "",
    "Because $x > 0$.",
  ].join("\n"));
  const [id] = anchorIds(html);

  assert.ok(id);
  assert.ok(html.includes(`href="#${id}">Jump</a>`));
  assert.match(html, /<a class="document-anchor" data-source-start-line="3" data-source-end-line="3" id="[^"]+"><\/a>\n<h2/);
  assert.match(html, /<h2 data-source-start-line="4" data-source-end-line="4">Descendant limits<\/h2>/);
  assert.match(html, /class="math-inline"/);
  assert.doesNotMatch(html, /<p[^>]*><a class="document-anchor"/);
  assert.doesNotMatch(html, /&lt;a id=/);
});

test("accepts inline and heading anchors with either quote style and HTML tag casing", () => {
  const html = renderMarkdown([
    "## Title <a id='heading'></a>",
    "",
    'Before <A\tID = "inline" > \t</A > after.',
    "",
    "[Heading](#heading) [Inline](#inline)",
  ].join("\n"));
  const ids = anchorIds(html);

  assert.equal(ids.length, 2);
  assert.match(html, /<h2[^>]*>Title <a[^>]+><\/a><\/h2>/);
  assert.match(html, /Before <a[^>]+><\/a> after\./);
  for (const id of ids) assert.ok(html.includes(`href="#${id}"`));
});

test("resolves reference and table links to anchors declared later", () => {
  const html = renderMarkdown([
    "[Jump][target]",
    "",
    "| Link |",
    "| --- |",
    "| [Jump](#later) |",
    "",
    "[target]: #later",
    "",
    '<a id="later"></a>',
  ].join("\n"));
  const [id] = anchorIds(html);

  assert.ok(id);
  assert.equal(html.split(`href="#${id}"`).length - 1, 2);
});

test("scopes targets and links to each document and repeated fold occurrence", () => {
  const source = '<a id="same"></a>\n\n[Jump](#same) Note[^n].\n\n[^n]: Detail.';
  const contexts = [
    {},
    { documentId: "main.md", instanceId: 1 },
    { documentId: "folds/first.md", instanceId: 2 },
    { documentId: "folds/second.md", instanceId: 3 },
    { documentId: "folds/first.md", instanceId: 4 },
  ];
  const allIds = [];
  for (const context of contexts) {
    const html = renderMarkdown(source, context);
    const [id] = anchorIds(html);
    assert.ok(id);
    assert.ok(html.includes(`href="#${id}">Jump</a>`));
    allIds.push(...[...html.matchAll(/\bid="([^"]+)"/gu)].map((match) => match[1]));
  }
  assert.equal(new Set(allIds).size, allIds.length);
});

test("keeps duplicate declarations unique and links to the first occurrence", () => {
  const html = renderMarkdown([
    '<a id="same"></a>',
    '<a id="same"></a>',
    '<a id="same--2"></a>',
    "",
    "[Jump](#same)",
  ].join("\n"));
  const ids = anchorIds(html);

  assert.equal(ids.length, 3);
  assert.equal(new Set(ids).size, 3);
  assert.ok(html.includes(`href="#${ids[0]}">Jump</a>`));
});

test("keeps code examples and escaped anchors literal", () => {
  const html = renderMarkdown([
    '`<a id="inline-code"></a>`',
    "",
    "```html",
    '<a id="fenced-code"></a>',
    "```",
    "",
    '    <a id="indented-code"></a>',
    "",
    '\\<a id="escaped"></a>',
    "",
    "[Example](#inline-code)",
  ].join("\n"));

  assert.deepEqual(anchorIds(html), []);
  assert.match(html, /<code>&lt;a id=&quot;inline-code&quot;&gt;&lt;\/a&gt;<\/code>/);
  assert.match(html, /&lt;a id=&quot;fenced-code&quot;&gt;/);
  assert.match(html, /&lt;a id=&quot;indented-code&quot;&gt;/);
  assert.match(html, /&lt;a id=&quot;escaped&quot;&gt;/);
  assert.match(html, /href="#inline-code"/);
});

test("keeps raw HTML disabled while rendering Markdown and math in the surrounding text", () => {
  const html = renderMarkdown([
    "<div>",
    "**bold** and $x+1$",
    '[Details](folds/detail.md "fold")',
    "</div>",
    "",
    "<style>.topbar { display: none; }</style>",
    '<img src="missing.png" onerror="alert(1)">',
    "<script>alert(1)</script>",
  ].join("\n"));

  assert.match(html, /&lt;div&gt;/);
  assert.match(html, /<strong>bold<\/strong>/);
  assert.match(html, /class="math-inline"/);
  assert.match(html, /<a href="folds\/detail.md" title="fold">Details<\/a>/);
  assert.doesNotMatch(html, /<(?:div|style|img|script)\b/u);
});

test("accepts only empty anchors with a single quoted id attribute", () => {
  const rejected = [
    '<a id="x" onclick="alert(1)"></a>',
    '<a onclick="alert(1)" id="x"></a>',
    '<a id="x" style="display:block"></a>',
    '<a id="x" href="javascript:alert(1)"></a>',
    '<a id="x" class="proof-fold"></a>',
    '<a id="x" id="y"></a>',
    '<a id="x">text</a>',
    '<a id="x"><b></b></a>',
    '<a id="x"/>',
    '<a id="x">',
    '<a name="x"></a>',
    "<a id=x></a>",
    '<a id=""></a>',
    '<a id="two words"></a>',
    '<a id="two&#32;words"></a>',
    '<a id="bad\u0001value"></a>',
  ];
  for (const source of rejected) {
    const html = renderMarkdown(source);
    assert.deepEqual(anchorIds(html), [], source);
    assert.doesNotMatch(html, /<a\b/u, source);
  }
});

test("matches Unicode, entity-escaped and URL-encoded ids without emitting their raw values", () => {
  const cases = [
    { markup: "하위-제한", id: "하위-제한" },
    { markup: "part:1.2", id: "part:1.2" },
    { markup: "a&amp;b", id: "a&b" },
    { markup: "part&#45;one", id: "part-one" },
    { markup: "a\\-b", id: "a\\-b" },
    { markup: "100%", id: "100%" },
    { markup: "&quot;&gt;&lt;img&gt;", id: '\"><img>' },
  ];
  for (const { markup, id } of cases) {
    const html = renderMarkdown(`<a id="${markup}"></a>\n\n[Jump](#${encodeURIComponent(id)})`);
    const ids = anchorIds(html);
    assert.equal(ids.length, 1, markup);
    assert.ok(html.includes(`href="#${ids[0]}">Jump</a>`), markup);
    assert.doesNotMatch(html, /<img\b/u);
  }
});

test("preserves unrelated fragments, document links, external links and footnotes", () => {
  const html = renderMarkdown([
    '<a id="fn1"></a>',
    '<a id="preview"></a>',
    "",
    "[Custom](#fn1) [Missing](#missing) [Document](other.md#fn1)",
    "[External](https://example.com/#fn1) [Malformed](#%ZZ) Note[^n].",
    "",
    "[^n]: Detail.",
  ].join("\n"));
  const ids = anchorIds(html);

  assert.equal(ids.length, 2);
  assert.ok(html.includes(`href="#${ids[0]}">Custom</a>`));
  assert.match(html, /href="#missing"/);
  assert.match(html, /href="other.md#fn1"/);
  assert.match(html, /href="https:\/\/example.com\/#fn1"/);
  assert.match(html, /<sup class="footnote-ref"><a href="#fn-document-1" id="fnref-document-1">/);
  assert.match(html, /<li id="fn-document-1" class="footnote-item">/);
  assert.doesNotMatch(html, /id="preview"/);
});

test("supports standalone anchors in blockquotes and lists and links inside footnotes", () => {
  const html = renderMarkdown([
    '> <a id="quoted"></a>',
    "> Quoted text.",
    "",
    '- <a id="listed"></a>',
    "  Listed text.",
    "",
    "Note[^n].",
    "",
    "[^n]: [Quote](#quoted) [List](#listed)",
  ].join("\n"));
  const ids = anchorIds(html);

  assert.equal(ids.length, 2);
  for (const id of ids) assert.ok(html.includes(`href="#${id}"`));
  assert.match(html, /<blockquote[^>]*>\n<a class="document-anchor"/);
  assert.match(html, /<li[^>]*>\n<a class="document-anchor"/);
  assert.match(html, /data-source-start-line="1" data-source-end-line="1"/);
  assert.match(html, /data-source-start-line="4" data-source-end-line="4"/);
});

test("does not create targets inside Markdown links or image alt text", () => {
  const html = renderMarkdown([
    '[<a id="link-label"></a>](https://example.com)',
    '![<a id="image-alt"></a>](image.png)',
    "[Label](#link-label) [Alt](#image-alt)",
  ].join("\n"));

  assert.deepEqual(anchorIds(html), []);
  assert.match(html, /href="https:\/\/example.com">&lt;a id=&quot;link-label&quot;/);
  assert.match(html, /alt="&lt;a id=&quot;image-alt&quot;&gt;&lt;\/a&gt;"/);
  assert.match(html, /href="#link-label"/);
  assert.match(html, /href="#image-alt"/);
});
