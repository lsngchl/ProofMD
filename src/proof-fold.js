/**
 * Fold ids are paths relative to the ProofFold folder, as the host reports them. Windows
 * file names ignore case, so lookups do too.
 */
export function createProofFoldIndex(payload) {
  if (typeof payload?.entry !== "string" || !Array.isArray(payload.folds)) return null;

  const folds = new Map();
  for (const fold of payload.folds) {
    if (typeof fold?.path === "string" && typeof fold.source === "string") {
      folds.set(fold.path.toLowerCase(), { path: fold.path, source: fold.source });
    }
  }
  return { entry: payload.entry, folds };
}

export function findFold(index, id) {
  return typeof id === "string" ? index?.folds.get(id.toLowerCase()) ?? null : null;
}

/** Resolves a fold link's href, written in `sourceDocumentId`, to a fold id. */
export function resolveProofFoldTarget(sourceDocumentId, href) {
  if (typeof sourceDocumentId !== "string" || typeof href !== "string") return null;

  let decodedPath;
  try {
    decodedPath = decodeURIComponent(href.split(/[?#]/u, 1)[0]);
  } catch {
    return null;
  }
  if (!decodedPath || /^[a-z][a-z\d+.-]*:/iu.test(decodedPath) || decodedPath.startsWith("/")) {
    return null;
  }

  const segments = sourceDocumentId.split("/");
  segments.pop();
  for (const segment of decodedPath.split("/")) {
    if (!segment || segment === ".") continue;
    if (segment === "..") {
      if (!segments.length) return null;
      segments.pop();
    } else {
      segments.push(segment);
    }
  }

  const target = segments.join("/");
  return /\.(?:md|markdown)$/iu.test(target) ? target : null;
}

function foldLabel(id) {
  const name = id.split("/").pop() ?? id;
  return name.replace(/\.(?:md|markdown)$/iu, "").replace(/[_-]/gu, " ");
}

/**
 * Builds the fold map from the same parse the viewer renders: `foldLinks(source)` returns
 * the hrefs of a document's fold links in order. Folds that link to each other in a cycle
 * keep the back edge; the map layout unfolds it safely.
 */
export function buildProofFoldMap(index, entrySource, foldLinks) {
  const entry = index.entry;
  const nodes = [{ id: entry, label: foldLabel(entry), detail: "ProofFold entry", order: 0 }];
  const edges = [];
  const known = new Set([entry.toLowerCase()]);
  const pending = [{ id: entry, source: entrySource }];

  while (pending.length) {
    const { id, source } = pending.shift();
    const targets = new Set();
    for (const href of foldLinks(source)) {
      const fold = findFold(index, resolveProofFoldTarget(id, href));
      if (!fold || targets.has(fold.path.toLowerCase())) continue;
      targets.add(fold.path.toLowerCase());
      edges.push({ from: id, to: fold.path, order: edges.length });
      if (!known.has(fold.path.toLowerCase())) {
        known.add(fold.path.toLowerCase());
        nodes.push({ id: fold.path, label: foldLabel(fold.path), detail: fold.path, order: nodes.length });
        pending.push({ id: fold.path, source: fold.source });
      }
    }
  }

  return { root: entry, nodes, edges };
}

/** Chooses the open fold the reader is looking at, for the collapse button. */
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
