import assert from "node:assert/strict";
import test from "node:test";
import { renderMarkdown } from "../src/renderer.js";

const render = (lines) => renderMarkdown(Array.isArray(lines) ? lines.join("\n") : lines);
const mathCount = (html) => (html.match(/class="math-(?:inline|display)"/gu) ?? []).length;

test("renders display math that ends a sentence or sits inside a paragraph", () => {
  const ending = render(["The bound", "\\[", "x = 1", "\\].", "", "follows."]);
  assert.equal(mathCount(ending), 1);
  assert.match(ending, /class="math-display"/u);
  assert.match(ending, /<\/span>\.<\/p>/u);

  const inline = render("Hence \\[ a = b \\] holds.");
  assert.equal(mathCount(inline), 1);
  assert.doesNotMatch(inline, /\[ a = b \]/u);
});

test("keeps an unpaired \\[ as an escaped bracket", () => {
  const html = render("See \\[1 for details.");
  assert.equal(mathCount(html), 0);
  assert.match(html, /See \[1 for details\./u);
});

test("does not let an unclosed display opener swallow the document", () => {
  const html = render(["$$ typo", "", "Some paragraph.", "", "# Heading", "", "$$", "b", "$$"]);
  assert.match(html, /<p[^>]*>Some paragraph\.<\/p>/u);
  assert.match(html, /<h1[^>]*>Heading<\/h1>/u);
  assert.equal(mathCount(html), 1);

  const fenced = render(["\\[ a", "", "```", "code $$", "```"]);
  assert.match(fenced, /<code[^>]*>code \$\$\n<\/code>/u);
});

test("ends display math with the list item that contains it", () => {
  const html = render(["- item", "  $$", "  x", "", "$$", "", "after"]);
  assert.match(html, /<p[^>]*>after<\/p>/u);
});

test("treats dollars that cannot pair as currency", () => {
  const prices = render("Price $5 and also $x$ is variable.");
  assert.equal(mathCount(prices), 1);
  assert.match(prices, /Price \$5 and also <span class="math-inline">/u);

  assert.equal(mathCount(render("$x $ and $ y$")), 0);
  assert.equal(mathCount(render("It costs $5.\nLater $x$ holds.")), 1);
  assert.equal(mathCount(render("costs $5 and $10")), 0);
});

test("scans many unpaired dollars in linear time", () => {
  const started = performance.now();
  render("$5 ".repeat(20_000));
  assert.ok(performance.now() - started < 1_000);
});

test("keeps an anchor line inside its paragraph", () => {
  const html = render(["Some text", '<a id="x"></a>', "more text"]);
  assert.equal((html.match(/<p\b/gu) ?? []).length, 1);
  assert.match(html, /<p[^>]*>Some text\n<a class="document-anchor"[^>]*><\/a>\nmore text<\/p>/u);
});

test("hides HTML comments and shows unclosed ones as text", () => {
  const html = render([
    "Before <!-- inline note --> after.",
    "",
    "<!-- pending:",
    "  details -->",
    "",
    "Next.",
    "",
    "Unclosed <!-- comment",
  ]);
  assert.doesNotMatch(html, /note|pending|details/u);
  assert.match(html, /Before after\./u);
  assert.match(html, /<p[^>]*>Next\.<\/p>/u);
  assert.match(html, /Unclosed &lt;!-- comment/u);
});
