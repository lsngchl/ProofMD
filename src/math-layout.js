const DEFAULT_HORIZONTAL_GAP = 6;
const DEFAULT_VERTICAL_GAP = 4;

function rectanglesConflict(formulaRect, tagRect, horizontalGap) {
  const overlapsVertically =
    formulaRect.top < tagRect.bottom && formulaRect.bottom > tagRect.top;
  const isTooCloseHorizontally =
    formulaRect.left < tagRect.right + horizontalGap &&
    formulaRect.right > tagRect.left - horizontalGap;
  return overlapsVertically && isTooCloseHorizontally;
}

export function calculateMathTagOffset(
  formulaRects,
  tagRect,
  containerRect,
  {
    horizontalGap = DEFAULT_HORIZONTAL_GAP,
    verticalGap = DEFAULT_VERTICAL_GAP,
  } = {},
) {
  const conflictingRects = formulaRects.filter((rect) =>
    rectanglesConflict(rect, tagRect, horizontalGap),
  );
  if (conflictingRects.length === 0) return null;

  const formulaBottom = Math.max(...conflictingRects.map((rect) => rect.bottom));
  const shift = Math.ceil(Math.max(0, formulaBottom - tagRect.top + verticalGap));
  const extraSpace = Math.ceil(
    Math.max(0, tagRect.bottom + shift - containerRect.bottom),
  );

  return { shift, extraSpace };
}

function unionRect(rects) {
  if (rects.length === 0) return null;

  return {
    top: Math.min(...rects.map((rect) => rect.top)),
    right: Math.max(...rects.map((rect) => rect.right)),
    bottom: Math.max(...rects.map((rect) => rect.bottom)),
    left: Math.min(...rects.map((rect) => rect.left)),
  };
}

function visibleRect(element) {
  const rect = element.getBoundingClientRect();
  return rect.width > 0 && rect.height > 0 ? rect : null;
}

function visibleTagRect(tag) {
  const contentRects = [...tag.children]
    .filter((child) => !child.classList.contains("strut"))
    .map(visibleRect)
    .filter(Boolean);
  return unionRect(contentRects) ?? visibleRect(tag);
}

export function adjustMathTagLayout(container) {
  const displays = container.querySelectorAll(
    ".katex-display > .katex > .katex-html",
  );

  for (const mathHtml of displays) {
    const display = mathHtml.parentElement?.parentElement;
    const tag = mathHtml.querySelector(":scope > .tag");
    if (!display || !tag || display.getClientRects().length === 0) continue;

    display.classList.remove("math-tag-below");
    display.style.removeProperty("--math-tag-shift");
    display.style.removeProperty("--math-tag-extra-space");

    const formulaRects = [...mathHtml.children]
      .filter((child) => child !== tag)
      .map(visibleRect)
      .filter(Boolean);
    const tagRect = visibleTagRect(tag);
    if (!tagRect || formulaRects.length === 0) continue;

    const layout = calculateMathTagOffset(
      formulaRects,
      tagRect,
      mathHtml.getBoundingClientRect(),
    );
    if (!layout) continue;

    display.style.setProperty("--math-tag-shift", `${layout.shift}px`);
    display.style.setProperty(
      "--math-tag-extra-space",
      `${layout.extraSpace}px`,
    );
    display.classList.add("math-tag-below");
  }
}
