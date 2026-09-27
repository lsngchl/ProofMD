import MarkdownIt from "markdown-it";
import katex from "katex";
import footnotePlugin from "markdown-it-footnote";
import { anchorPlugin } from "./anchor-plugin.js";
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
  .use(anchorPlugin)
  .use(sourceMapPlugin);

function renderDocumentId(documentId, instanceId) {
  const docId = typeof documentId === "string" && documentId
    ? Array.from(documentId, (character) =>
      character.codePointAt(0).toString(16),
    ).join("-")
    : undefined;
  return Number.isSafeInteger(instanceId) && instanceId >= 0
    ? `${docId ?? "document"}--${instanceId}`
    : docId;
}

export function renderMarkdown(source, { documentId, instanceId } = {}) {
  const docId = renderDocumentId(documentId, instanceId);
  return markdown.render(source, docId ? { docId } : {});
}
