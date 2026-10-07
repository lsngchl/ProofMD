const OPEN = "<!--";
const CLOSE = "-->";

/**
 * Hides HTML comments, which authors use as invisible notes and placeholders, while raw
 * HTML stays disabled. An unclosed comment is shown as text.
 */
export function commentPlugin(markdown) {
  markdown.block.ruler.before("html_block", "proofmd_comment_block", blockCommentRule, {
    alt: ["paragraph", "reference", "blockquote", "list"],
  });
  markdown.inline.ruler.before("html_inline", "proofmd_comment", inlineCommentRule);
}

function blockCommentRule(state, startLine, endLine, silent) {
  if (state.sCount[startLine] - state.blkIndent >= 4) return false;
  const start = state.bMarks[startLine] + state.tShift[startLine];
  if (!state.src.startsWith(OPEN, start)) return false;

  for (let line = startLine; line < endLine; line += 1) {
    const lineStart = line === startLine ? start + OPEN.length : state.bMarks[line] + state.tShift[line];
    const close = state.src.slice(lineStart, state.eMarks[line]).indexOf(CLOSE);
    if (close < 0) continue;
    // Text after the comment on its closing line belongs to the document, not the comment.
    if (state.src.slice(lineStart + close + CLOSE.length, state.eMarks[line]).trim()) return false;
    if (!silent) state.line = line + 1;
    return true;
  }
  return false;
}

function inlineCommentRule(state, silent) {
  if (!state.src.startsWith(OPEN, state.pos)) return false;
  const close = state.src.indexOf(CLOSE, state.pos + OPEN.length);
  if (close < 0 || close + CLOSE.length > state.posMax) return false;

  if (!silent) state.pending = state.pending.trimEnd();
  state.pos = close + CLOSE.length;
  return true;
}
