const BRACKET_INLINE = { open: "\\(", close: "\\)", display: false };
const BRACKET_DISPLAY = { open: "\\[", close: "\\]", display: true };
const DOLLAR_INLINE = { open: "$", close: "$", display: false };
const DOLLAR_DISPLAY = { open: "$$", close: "$$", display: true };
const BLOCK_DELIMITERS = [
  { open: "\\[", close: "\\]" },
  { open: "$$", close: "$$" },
];

/**
 * A deliberately small markdown-it plugin for bracket and dollar LaTeX
 * delimiters. It claims math tokens before Markdown's backslash escape rule
 * can remove them, while leaving code spans and fenced code blocks untouched.
 * Display math on lines of its own becomes a block; display math inside a
 * paragraph (for example `\[ x \].` ending a sentence) is rendered in place.
 */
export function mathPlugin(md, options = {}) {
  const engine = options.engine;
  const katexOptions = options.katexOptions ?? {};

  if (!engine || typeof engine.renderToString !== "function") {
    throw new TypeError("mathPlugin requires a KaTeX-compatible engine.");
  }

  md.inline.ruler.before("escape", "proofmd_math_inline", inlineMathRule);
  md.block.ruler.before("fence", "proofmd_math_block", blockMathRule, {
    alt: ["paragraph", "reference", "blockquote", "list"],
  });

  md.renderer.rules.proofmd_math_inline = (tokens, index) => {
    const rendered = renderMath(tokens[index].content, false);
    return `<span class="math-inline">${rendered}</span>`;
  };

  md.renderer.rules.proofmd_math_display_inline = (tokens, index) => {
    const rendered = renderMath(tokens[index].content, true);
    return `<span class="math-display">${rendered}</span>`;
  };

  md.renderer.rules.proofmd_math_block = (tokens, index) => {
    const rendered = renderMath(tokens[index].content, true);
    const attributes = md.renderer.renderAttrs(tokens[index]);
    return `<div class="math-display"${attributes}>${rendered}</div>\n`;
  };

  function renderMath(source, displayMode) {
    try {
      return engine.renderToString(source, {
        ...katexOptions,
        displayMode,
        throwOnError: false,
        trust: false,
      });
    } catch {
      const escaped = md.utils.escapeHtml(source);
      return `<code class="math-error" title="Unable to render this expression">${escaped}</code>`;
    }
  }
}

function inlineMathRule(state, silent) {
  let delimiter;

  if (state.src.startsWith(BRACKET_INLINE.open, state.pos)) {
    delimiter = BRACKET_INLINE;
  } else if (state.src.startsWith(BRACKET_DISPLAY.open, state.pos)) {
    delimiter = BRACKET_DISPLAY;
  } else if (state.src.startsWith(DOLLAR_DISPLAY.open, state.pos)) {
    delimiter = DOLLAR_DISPLAY;
  } else if (state.src[state.pos] === DOLLAR_INLINE.open) {
    const nextCharacter = state.src[state.pos + 1];
    if (!nextCharacter || /\s/u.test(nextCharacter)) return false;
    delimiter = DOLLAR_INLINE;
  } else {
    return false;
  }

  const contentStart = state.pos + delimiter.open.length;
  const closeAt = findClosingDelimiter(
    state.src,
    delimiter,
    contentStart,
  );

  if (closeAt < 0) {
    // An unpaired "$" or "\[" keeps its ordinary Markdown meaning ("$", "[").
    if (delimiter === DOLLAR_INLINE || delimiter === BRACKET_DISPLAY) return false;

    // Preserve unmatched bracket and double-dollar delimiters as one token so
    // a second character cannot be mistaken for a new opening delimiter.
    if (!silent) {
      const token = state.push("text", "", 0);
      token.content = delimiter.open;
    }
    state.pos = contentStart;
    return true;
  }

  const content = state.src.slice(contentStart, closeAt).trim();
  if (!content) {
    if (delimiter === DOLLAR_INLINE || delimiter === BRACKET_DISPLAY) return false;
    if (!silent) {
      const token = state.push("text", "", 0);
      token.content = delimiter.open;
    }
    state.pos = contentStart;
    return true;
  }

  if (!silent) {
    const tokenType = delimiter.display
      ? "proofmd_math_display_inline"
      : "proofmd_math_inline";
    const token = state.push(tokenType, "math", 0);
    token.content = content;
    token.markup = delimiter.open;
  }

  state.pos = closeAt + delimiter.close.length;
  return true;
}

function blockMathRule(state, startLine, endLine, silent) {
  if (state.sCount[startLine] - state.blkIndent >= 4) {
    return false;
  }

  const start = state.bMarks[startLine] + state.tShift[startLine];
  const firstLine = state.src.slice(start, state.eMarks[startLine]);

  const delimiter = BLOCK_DELIMITERS.find(({ open }) =>
    firstLine.startsWith(open),
  );
  if (!delimiter) return false;

  const contentLines = [];
  let line = startLine;
  let remainder = firstLine.slice(delimiter.open.length);
  let closeAt = findUnescaped(remainder, delimiter.close, 0);

  if (closeAt >= 0) {
    if (remainder.slice(closeAt + delimiter.close.length).trim() !== "") {
      return false;
    }
    contentLines.push(remainder.slice(0, closeAt));
  } else {
    contentLines.push(remainder);

    for (line = startLine + 1; line < endLine; line += 1) {
      // Display math never spans a blank line or leaves its container (a list item,
      // a quote); an unclosed opener must not swallow the rest of the document.
      if (state.isEmpty(line) || state.sCount[line] < state.blkIndent) return false;

      const lineStart = state.bMarks[line] + state.tShift[line];
      remainder = state.src.slice(lineStart, state.eMarks[line]);
      closeAt = findUnescaped(remainder, delimiter.close, 0);

      if (closeAt < 0) {
        contentLines.push(remainder);
        continue;
      }

      if (remainder.slice(closeAt + delimiter.close.length).trim() !== "") {
        return false;
      }

      contentLines.push(remainder.slice(0, closeAt));
      break;
    }

    if (closeAt < 0) {
      return false;
    }
  }

  const content = contentLines.join("\n").trim();
  if (!content) return false;

  if (silent) {
    return true;
  }

  state.line = line + 1;
  const token = state.push("proofmd_math_block", "math", 0);
  token.block = true;
  token.content = content;
  token.map = [startLine, state.line];
  token.markup = delimiter.open;
  return true;
}

function findClosingDelimiter(source, delimiter, from) {
  const index = findUnescaped(source, delimiter.close, from);
  if (delimiter !== DOLLAR_INLINE || index < 0) return index;

  // A single dollar pairs only with the next one, and only if that one can close:
  // in "costs $5 and $x$", the "$" of "$5" stays text instead of pairing with "x".
  const before = source[index - 1];
  const after = source[index + 1];
  return !/\s/u.test(before) && !/\d/u.test(after ?? "") ? index : -1;
}

function findUnescaped(source, delimiter, from) {
  let index = source.indexOf(delimiter, from);
  while (index >= 0 && isEscaped(source, index)) {
    index = source.indexOf(delimiter, index + delimiter.length);
  }
  return index;
}

function isEscaped(source, index) {
  let backslashCount = 0;
  for (let cursor = index - 1; cursor >= 0 && source[cursor] === "\\"; cursor -= 1) {
    backslashCount += 1;
  }
  return backslashCount % 2 === 1;
}
