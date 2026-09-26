const BRACKET_INLINE = { open: "\\(", close: "\\)", display: false };
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
    // Dollar signs are ordinary Markdown text when they do not form a pair.
    if (delimiter === DOLLAR_INLINE) return false;

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
    if (delimiter === DOLLAR_INLINE) return false;
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
  if (delimiter !== DOLLAR_INLINE) {
    return findUnescaped(source, delimiter.close, from);
  }

  return findUnescaped(source, delimiter.close, from, (index) => {
    const before = source[index - 1];
    const after = source[index + delimiter.close.length];
    return !/\s/u.test(before) && !/\d/u.test(after ?? "");
  });
}

function findUnescaped(source, delimiter, from, isValid = () => true) {
  let index = source.indexOf(delimiter, from);

  while (index >= 0) {
    if (!isEscaped(source, index) && isValid(index)) return index;
    index = source.indexOf(delimiter, index + delimiter.length);
  }

  return -1;
}

function isEscaped(source, index) {
  let backslashCount = 0;
  for (let cursor = index - 1; cursor >= 0 && source[cursor] === "\\"; cursor -= 1) {
    backslashCount += 1;
  }
  return backslashCount % 2 === 1;
}
