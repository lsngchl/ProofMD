import MarkdownIt from "markdown-it";
import katex from "katex";
import footnotePlugin from "markdown-it-footnote";
import { mathPlugin } from "./math-plugin.js";
import { sourceMapPlugin } from "./source-map-plugin.js";

const markdown = new MarkdownIt({
  html: false,
  linkify: true,
  typographer: false,
}).use(mathPlugin, {
  engine: katex,
  katexOptions: {
    strict: false,
  },
})
  .use(footnotePlugin)
  .use(sourceMapPlugin);

function footnoteDocumentId(documentId) {
  if (typeof documentId !== "string" || !documentId) return undefined;

  return Array.from(documentId, (character) =>
    character.codePointAt(0).toString(16),
  ).join("-");
}

export function renderMarkdown(source, { documentId } = {}) {
  const docId = footnoteDocumentId(documentId);
  return markdown.render(source, docId ? { docId } : {});
}
