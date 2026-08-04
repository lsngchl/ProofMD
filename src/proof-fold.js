function normalizedDocumentId(value) {
  if (typeof value !== "string" || !value || value.includes("\\")) return null;
  if (value.startsWith("/") || value.startsWith("//")) return null;

  const segments = [];
  for (const segment of value.split("/")) {
    if (!segment || segment === ".") continue;
    if (segment === "..") return null;
    segments.push(segment);
  }

  return segments.length ? segments.join("/") : null;
}

export function createProofFoldIndex(payload) {
  if (!payload || typeof payload !== "object") return null;

  const entry = normalizedDocumentId(payload.entry);
  if (!entry || !Array.isArray(payload.folds)) return null;

  const folds = new Map();
  for (const fold of payload.folds) {
    const path = normalizedDocumentId(fold?.path);
    if (!path || typeof fold?.source !== "string" || folds.has(path)) {
      return null;
    }
    folds.set(path, { path, source: fold.source });
  }

  return { entry, folds };
}

export function resolveProofFoldTarget(sourceDocumentId, href) {
  const source = normalizedDocumentId(sourceDocumentId);
  if (!source || typeof href !== "string" || !href) return null;
  if (href.startsWith("#") || /^[a-z][a-z\d+.-]*:/iu.test(href)) return null;

  const encodedPath = href.split(/[?#]/u, 1)[0];
  if (!encodedPath || encodedPath.startsWith("//")) return null;

  let decodedPath;
  try {
    decodedPath = decodeURIComponent(encodedPath);
  } catch {
    return null;
  }
  if (!decodedPath || decodedPath.includes("\\") || decodedPath.startsWith("/")) {
    return null;
  }

  const segments = source.split("/");
  segments.pop();
  for (const segment of decodedPath.split("/")) {
    if (!segment || segment === ".") continue;
    if (segment === "..") {
      if (!segments.length) return null;
      segments.pop();
      continue;
    }
    segments.push(segment);
  }

  const target = segments.join("/");
  return /\.(?:md|markdown)$/iu.test(target) ? target : null;
}

export function activeProofFoldIndex(rectangles, readingLine, viewportHeight) {
  if (!Array.isArray(rectangles) ||
      !Number.isFinite(readingLine) ||
      !Number.isFinite(viewportHeight) ||
      viewportHeight <= 0) {
    return -1;
  }

  const visible = rectangles
    .map((rectangle, index) => ({ rectangle, index }))
    .filter(({ rectangle }) =>
      Number.isFinite(rectangle?.top) &&
      Number.isFinite(rectangle?.bottom) &&
      rectangle.bottom > 0 &&
      rectangle.top < viewportHeight &&
      rectangle.bottom > rectangle.top,
    );
  const containingReadingLine = visible.filter(({ rectangle }) =>
    rectangle.top <= readingLine && rectangle.bottom >= readingLine,
  );
  if (containingReadingLine.length) {
    return containingReadingLine[containingReadingLine.length - 1].index;
  }

  let closest = null;
  for (const candidate of visible) {
    const distance = readingLine < candidate.rectangle.top
      ? candidate.rectangle.top - readingLine
      : readingLine - candidate.rectangle.bottom;
    if (!closest || distance <= closest.distance) {
      closest = { index: candidate.index, distance };
    }
  }
  return closest?.index ?? -1;
}
