import MarkdownIt from "markdown-it";
import katex from "katex";
import footnotePlugin from "markdown-it-footnote";
import { anchorPlugin } from "./anchor-plugin.js";
import { commentPlugin } from "./comment-plugin.js";
import { mathPlugin } from "./math-plugin.js";
import { sourceMapPlugin } from "./source-map-plugin.js";

const markdown = new MarkdownIt({ html: false, linkify: true })
  .use(mathPlugin, { engine: katex, katexOptions: { strict: false } })
  .use(footnotePlugin)
  .use(anchorPlugin)
  .use(commentPlugin)
  .use(sourceMapPlugin);

// Footnote and anchor ids embed this value unescaped, so it must stay attribute-safe:
// hex code points of the document id, plus a counter that differs for every render.
function renderDocumentId(documentId, instanceId) {
  const encoded = typeof documentId === "string" && documentId
    ? Array.from(documentId, (character) => character.codePointAt(0).toString(16)).join("-")
    : "document";
  return Number.isSafeInteger(instanceId) && instanceId >= 0 ? `${encoded}--${instanceId}` : encoded;
}

export function renderMarkdown(source, { documentId, instanceId } = {}) {
  return markdown.render(source, { docId: renderDocumentId(documentId, instanceId) });
}

/** The hrefs of links titled "fold", in reading order, exactly as rendering sees them. */
export function foldLinkTargets(source) {
  const targets = [];
  const visit = (tokens) => {
    for (const token of tokens) {
      if (token.type === "link_open" && token.attrGet("title")?.trim().toLowerCase() === "fold") {
        targets.push(token.attrGet("href"));
      }
      if (token.children) visit(token.children);
    }
  };
  visit(markdown.parse(source, {}));
  return targets;
}
