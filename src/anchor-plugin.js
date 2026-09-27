const EMPTY_ANCHOR = /^<a[\t ]+id[\t ]*=[\t ]*(?:"([^"\r\n]*)"|'([^'\r\n]*)')[\t ]*>[\t ]*<\/a[\t ]*>/iu;

/** Accept empty, id-only anchors while keeping markdown-it's raw HTML disabled. */
export function anchorPlugin(markdown) {
  markdown.inline.ruler.before("html_inline", "proofmd_anchor", inlineAnchorRule);
  markdown.block.ruler.before("html_block", "proofmd_anchor_block", blockAnchorRule, {
    alt: ["paragraph", "reference", "blockquote", "list"],
  });
  markdown.core.ruler.push("proofmd_anchor_links", scopeAnchorLinks);
  markdown.renderer.rules.proofmd_anchor = (tokens, index, options, env, renderer) => {
    const token = tokens[index];
    return `<a${renderer.renderAttrs(token)}></a>${token.block ? "\n" : ""}`;
  };
}

function readAnchor(source, markdown) {
  const match = EMPTY_ANCHOR.exec(source);
  if (!match) return null;

  // Decode HTML character references without treating backslashes as Markdown escapes.
  const id = (match[1] ?? match[2]).replace(
    /&([a-z#][a-z0-9]{1,31});/giu,
    (entity) => markdown.utils.unescapeAll(entity),
  );
  if (!id || /[\s\u0000-\u001f\u007f]/u.test(id)) return null;

  return { id, markup: match[0], length: match[0].length };
}

function pushAnchor(state, anchor) {
  const token = state.push("proofmd_anchor", "a", 0);
  token.content = anchor.markup;
  token.meta = { anchorId: anchor.id };
  token.attrSet("class", "document-anchor");
  return token;
}

function inlineAnchorRule(state, silent) {
  if (state.src[state.pos] !== "<" || state.linkLevel > 0) return false;
  const anchor = readAnchor(state.src.slice(state.pos, state.posMax), state.md);
  if (!anchor) return false;

  if (!silent) pushAnchor(state, anchor);
  state.pos += anchor.length;
  return true;
}

function blockAnchorRule(state, startLine, endLine, silent) {
  if (state.sCount[startLine] - state.blkIndent >= 4) return false;
  const start = state.bMarks[startLine] + state.tShift[startLine];
  if (state.src[start] !== "<") return false;
  const source = state.src.slice(start, state.eMarks[startLine]);
  const anchor = readAnchor(source, state.md);
  if (!anchor || source.slice(anchor.length).trim()) return false;
  if (silent) return true;

  const token = pushAnchor(state, anchor);
  token.block = true;
  token.map = [startLine, startLine + 1];
  state.line = startLine + 1;
  return true;
}

function scopeAnchorLinks(state) {
  const tokens = [];
  function collect(children, inImage = false) {
    for (const token of children) {
      // Image descriptions remain literal text and do not create targets.
      if (inImage && token.type === "proofmd_anchor") token.type = "text";
      if (!inImage) tokens.push(token);
      if (token.children) collect(token.children, inImage || token.type === "image");
    }
  }
  collect(state.tokens);

  const anchors = new Map();
  const counts = new Map();
  for (const token of tokens) {
    if (token.type !== "proofmd_anchor") continue;
    const originalId = token.meta.anchorId;
    const encodedId = Array.from(originalId, (character) =>
      character.codePointAt(0).toString(16),
    ).join("-");
    const count = (counts.get(originalId) ?? 0) + 1;
    counts.set(originalId, count);
    const id = `proofmd-anchor-${state.env.docId ?? "document"}--${encodedId}${count > 1 ? `--${count}` : ""}`;
    token.attrSet("id", id);
    if (!anchors.has(originalId)) anchors.set(originalId, id);
  }
  if (!anchors.size) return;

  for (const token of tokens) {
    if (token.type !== "link_open") continue;
    const href = token.attrGet("href");
    if (!href?.startsWith("#")) continue;
    let target;
    try {
      target = anchors.get(decodeURIComponent(href.slice(1)));
    } catch {
      continue;
    }
    if (target) token.attrSet("href", `#${target}`);
  }
}
