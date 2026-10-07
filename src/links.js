/**
 * Classifies a rendered link's href:
 * - "fragment": a target in the same document
 * - "markdown": a relative Markdown file the host opens in this window
 * - "external": a web or mail address the system opens
 * - "other": anything ProofMD cannot open
 */
export function linkKind(href) {
  if (typeof href !== "string" || !href) return "other";
  if (href.startsWith("#")) return "fragment";
  if (/^(?:https?|mailto):/iu.test(href)) return "external";
  if (/^[a-z][a-z\d+.-]*:/iu.test(href) || href.startsWith("/") || href.startsWith("\\")) {
    return "other";
  }

  const path = href.split(/[?#]/u, 1)[0];
  try {
    return /\.(?:md|markdown)$/iu.test(decodeURIComponent(path)) ? "markdown" : "other";
  } catch {
    return "other";
  }
}
