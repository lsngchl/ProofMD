/**
 * Picks the rendered block to scroll to for a remembered source line: the block whose
 * source range contains the line, otherwise the block starting closest to it.
 * Each candidate is `{ range: { startLine, endLine }, ... }`.
 */
export function chooseAnchor(candidates, sourceLine) {
  let closest = null;
  for (const candidate of candidates) {
    const { startLine, endLine } = candidate.range;
    if (startLine <= sourceLine && endLine >= sourceLine) return candidate;
    if (!closest ||
        Math.abs(startLine - sourceLine) < Math.abs(closest.range.startLine - sourceLine)) {
      closest = candidate;
    }
  }
  return closest;
}
