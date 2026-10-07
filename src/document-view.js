import { linkKind } from "./links.js";
import { adjustMathTagLayout } from "./math-layout.js";
import { chooseAnchor } from "./position.js";
import {
  activeProofFoldIndex,
  buildProofFoldMap,
  createProofFoldIndex,
  findFold,
  resolveProofFoldTarget,
} from "./proof-fold.js";
import { announce, elements, post, waitForPaint } from "./ui.js";

const SVG_NAMESPACE = "http://www.w3.org/2000/svg";
const SOURCE_BLOCK_SELECTOR = "[data-source-start-line][data-source-end-line]";

let rendererPromise = null;
let renderGeneration = 0;
let renderInstance = 0;
let pendingOpen = null;
let contextId = null;
let unresolved = false;
let unresolvedRequestPending = false;
let proofFold = null;
let expandedFoldKeys = new Set();
const foldLoaders = new WeakMap();
let activeFoldForCollapse = null;
let collapseFrame = null;
let mathLayoutFrame = null;
let onProofFoldMap = () => {};

function loadRenderer() {
  rendererPromise ??= import("./renderer.js");
  return rendererPromise;
}

// Rendering --------------------------------------------------------------------------

/** Shows a document the host opened, optionally at a remembered reading position. */
export function openDocument({ source, name, contextId: nextContextId, unresolved: isUnresolved, restorePosition, proofFold: payload }) {
  if (typeof source !== "string") return;
  contextId = Number.isInteger(nextContextId) ? nextContextId : null;
  setUnresolvedState(isUnresolved === true, true);
  pendingOpen = { restorePosition: restorePosition ?? null };
  render(source, name, payload, { reload: false, restorePosition: restorePosition ?? null });
}

/** Re-renders the open document after it changed on disk, keeping folds and position. */
export function reloadDocument({ source, name, proofFold: payload }) {
  if (typeof source !== "string") return;
  // A reload that overtakes an unfinished open replaces it, keeping where it was going.
  if (pendingOpen) {
    render(source, name, payload, { reload: false, restorePosition: pendingOpen.restorePosition });
  } else {
    render(source, name, payload, { reload: true, restorePosition: currentDocumentPosition() });
  }
}

async function render(source, name, payload, { reload, restorePosition }) {
  const generation = ++renderGeneration;
  if (!reload) {
    setLoading(true);
    await waitForPaint();
  }

  try {
    const renderer = await loadRenderer();
    if (generation !== renderGeneration) return;
    renderInto(source, typeof name === "string" ? name : "Untitled.md", renderer, payload, reload);
  } catch (error) {
    if (generation !== renderGeneration) return;
    pendingOpen = null;
    showRenderError(error);
    return;
  }

  try {
    await document.fonts?.ready;
  } catch {
    // Font readiness only refines layout.
  }
  await waitForPaint();
  if (generation !== renderGeneration) return;

  pendingOpen = null;
  setLoading(false);
  if (restorePosition) restoreDocumentPosition(restorePosition);
}

function renderInto(source, name, { renderMarkdown, foldLinkTargets }, payload, reload) {
  const nextProofFold = createProofFoldIndex(payload);
  if (!reload || nextProofFold?.entry !== proofFold?.entry) expandedFoldKeys = new Set();
  proofFold = nextProofFold;
  activeFoldForCollapse = null;

  const product = proofFold ? "ProofFold" : "ProofMD";
  elements.brandName.textContent = product;
  setEmptyStateVisible(false);
  const entryId = proofFold?.entry ?? null;
  elements.preview.innerHTML = renderMarkdown(source, { documentId: entryId, instanceId: ++renderInstance });
  elements.documentName.textContent = name;

  const markdownLinks = enhance(elements.preview, {
    documentId: entryId,
    ancestry: entryId ? [entryId] : [],
    keyPrefix: "",
    renderMarkdown,
  });
  if (Number.isInteger(contextId)) {
    post("document-links", {
      contextId,
      links: markdownLinks.map((link, order) => ({ href: link.getAttribute("href"), order })),
    });
  }
  markdownLinks.forEach((link, order) => {
    link.dataset.linkOrder = String(order);
  });

  document.title = `${name} — ${product}`;
  if (!reload) {
    announce(`${name} opened.`);
    window.scrollTo({ top: 0, behavior: "auto" });
  }
  onProofFoldMap(proofFold ? buildProofFoldMap(proofFold, source, foldLinkTargets) : null);
  scheduleCollapseControl();
}

/** Adds link behavior and folds to freshly rendered Markdown; returns its Markdown links. */
function enhance(container, { documentId, ancestry, keyPrefix, renderMarkdown }) {
  const markdownLinks = [];
  let foldOrdinal = 0;
  for (const link of container.querySelectorAll("a[href]")) {
    if (documentId) link.dataset.sourceDocument = documentId;
    if (proofFold && documentId && link.getAttribute("title")?.trim().toLowerCase() === "fold") {
      replaceFoldLink(link, {
        documentId,
        ancestry,
        key: `${keyPrefix}/${foldOrdinal++}`,
        renderMarkdown,
      });
      continue;
    }

    const kind = linkKind(link.getAttribute("href"));
    if (kind === "markdown") markdownLinks.push(link);
    if (kind === "external") appendExternalLinkIcon(link);
  }

  scheduleMathTagLayout();
  return markdownLinks;
}

function appendExternalLinkIcon(link) {
  const icon = document.createElementNS(SVG_NAMESPACE, "svg");
  icon.classList.add("external-link-icon");
  icon.setAttribute("viewBox", "0 0 16 16");
  icon.setAttribute("aria-hidden", "true");
  icon.setAttribute("focusable", "false");
  const path = document.createElementNS(SVG_NAMESPACE, "path");
  path.setAttribute("d", "M9.5 2.5h4v4M13.25 2.75 7.5 8.5M12.5 9v3.5a1 1 0 0 1-1 1h-8a1 1 0 0 1-1-1v-8a1 1 0 0 1 1-1H7");
  icon.append(path);

  const hint = document.createElement("span");
  hint.className = "visually-hidden";
  hint.textContent = " (opens in an external browser)";
  link.append(" ", icon, hint);
}

// Folds ------------------------------------------------------------------------------

/**
 * Replaces a fold link with a disclosure whose content is rendered on first opening.
 * `key` identifies this occurrence (its position among its parent's folds), so a fold
 * linked from two places opens and closes independently.
 */
function replaceFoldLink(link, { documentId, ancestry, key, renderMarkdown }) {
  const targetId = resolveProofFoldTarget(documentId, link.getAttribute("href"));
  const fold = findFold(proofFold, targetId);
  const isCycle = fold !== null &&
    ancestry.some((id) => id.toLowerCase() === fold.path.toLowerCase());
  const isPending = /^fold\s*\(pending\)\s*:/iu.test(link.textContent.trim());
  const label = link.textContent.trim() || "Fold";

  const details = document.createElement("details");
  details.className = "proof-fold";
  details.dataset.foldTarget = fold?.path ?? targetId ?? "";
  if (isPending) details.classList.add("is-pending");
  if (!fold || isCycle) details.classList.add("is-error");

  const summary = document.createElement("summary");
  summary.className = "proof-fold-summary";
  summary.append(...[...link.childNodes].map((node) => node.cloneNode(true)));
  const content = document.createElement("div");
  content.className = "proof-fold-content";
  content.setAttribute("role", "region");
  content.setAttribute("aria-label", label);
  details.append(summary, content);

  const paragraph = link.parentElement;
  const standalone = paragraph?.tagName === "P" &&
    [...paragraph.childNodes].every((node) => node === link || (node.nodeType === Node.TEXT_NODE && !node.textContent.trim()));
  const replaced = standalone ? paragraph : link;
  for (const attribute of ["sourceStartLine", "sourceEndLine"]) {
    if (replaced.dataset?.[attribute]) details.dataset[attribute] = replaced.dataset[attribute];
  }
  replaced.replaceWith(details);

  let loaded = false;
  const load = () => {
    if (loaded) return;
    loaded = true;
    const message = !fold
      ? "This fold target is missing or outside the ProofFold document."
      : isCycle
        ? "This fold would create a recursive expansion cycle."
        : isPending
          ? "This fold is pending."
          : null;
    if (message) {
      const paragraphElement = document.createElement("p");
      paragraphElement.className = "proof-fold-message";
      paragraphElement.textContent = message;
      content.append(paragraphElement);
      return;
    }

    content.innerHTML = renderMarkdown(fold.source, { documentId: fold.path, instanceId: ++renderInstance });
    enhance(content, { documentId: fold.path, ancestry: [...ancestry, fold.path], keyPrefix: key, renderMarkdown });
  };
  foldLoaders.set(details, load);

  details.addEventListener("toggle", () => {
    const silent = details.dataset.silent === "true";
    delete details.dataset.silent;
    if (details.open) {
      expandedFoldKeys.add(key);
      load();
      scheduleMathTagLayout();
    } else {
      expandedFoldKeys.delete(key);
    }
    if (!silent) announce(`${label} ${details.open ? "expanded" : "collapsed"}.`);
    scheduleCollapseControl();
  });

  if (expandedFoldKeys.has(key)) {
    details.dataset.silent = "true";
    details.open = true;
    load();
  }
}

/** Folds that belong directly to `container` (the document, or one fold's content). */
function ownFolds(container) {
  return [...container.querySelectorAll("details.proof-fold")].filter(
    (details) => (details.parentElement.closest(".proof-fold-content") ?? elements.preview) === container,
  );
}

/** Opens the folds along a path of fold ids from the entry and scrolls to the last one. */
export function revealFoldPath(foldIds) {
  let container = elements.preview;
  let target = null;
  for (const id of foldIds) {
    const details = ownFolds(container).find(
      (candidate) => candidate.dataset.foldTarget.toLowerCase() === id.toLowerCase(),
    );
    if (!details) break;
    foldLoaders.get(details)?.();
    details.open = true;
    target = details;
    container = details.querySelector(":scope > .proof-fold-content");
  }
  if (!target) return;

  const summary = target.querySelector(":scope > .proof-fold-summary");
  summary.focus({ preventScroll: true });
  window.requestAnimationFrame(() => summary.scrollIntoView({ block: "start", behavior: "auto" }));
}

function updateCollapseControl() {
  collapseFrame = null;
  const button = elements.proofFoldCollapseButton;
  const openFolds = proofFold ? [...elements.preview.querySelectorAll("details.proof-fold[open]")] : [];
  const topbarBottom = elements.topbar.getBoundingClientRect().bottom;
  const readingLine = Math.min(
    Math.max(topbarBottom + 36, window.innerHeight * 0.36),
    Math.max(0, window.innerHeight - 36),
  );
  const index = activeProofFoldIndex(
    openFolds.map((fold) => fold.getBoundingClientRect()),
    readingLine,
    window.innerHeight,
  );
  activeFoldForCollapse = index >= 0 ? openFolds[index] : null;
  button.hidden = !activeFoldForCollapse;
  if (!activeFoldForCollapse) return;

  const label = activeFoldForCollapse.querySelector(":scope > .proof-fold-summary")?.textContent.trim() || "current fold";
  button.title = `Collapse ${label}`;
  button.setAttribute("aria-label", `Collapse ${label}`);
}

function scheduleCollapseControl() {
  collapseFrame ??= window.requestAnimationFrame(updateCollapseControl);
}

function collapseActiveFold() {
  const details = activeFoldForCollapse;
  if (!details?.open) return;

  const summary = details.querySelector(":scope > .proof-fold-summary");
  activeFoldForCollapse = null;
  elements.proofFoldCollapseButton.hidden = true;
  details.open = false;
  summary.focus({ preventScroll: true });
  window.requestAnimationFrame(() => {
    summary.scrollIntoView({
      block: "center",
      behavior: window.matchMedia("(prefers-reduced-motion: reduce)").matches ? "auto" : "smooth",
    });
  });
}

function scheduleMathTagLayout() {
  mathLayoutFrame ??= window.requestAnimationFrame(() => {
    mathLayoutFrame = null;
    adjustMathTagLayout(elements.preview);
  });
}

// Reading position -------------------------------------------------------------------

function sourceRange(element) {
  const startLine = Number(element.dataset.sourceStartLine);
  const endLine = Number(element.dataset.sourceEndLine);
  return Number.isInteger(startLine) && Number.isInteger(endLine) && startLine >= 1 && endLine >= startLine
    ? { startLine, endLine }
    : null;
}

/**
 * The innermost blocks of the document itself. Content inside folds has line numbers of
 * its own fold file, so a fold counts as one block of the document that contains it.
 */
function documentSourceBlocks() {
  return [...elements.preview.querySelectorAll(SOURCE_BLOCK_SELECTOR)].filter(
    (element) =>
      !element.closest(".proof-fold-content") &&
      (element.matches("details.proof-fold") || !element.querySelector(SOURCE_BLOCK_SELECTOR)),
  );
}

export function currentDocumentPosition() {
  const topbarBottom = elements.topbar.getBoundingClientRect().bottom;
  for (const element of documentSourceBlocks()) {
    const bounds = element.getBoundingClientRect();
    const range = sourceRange(element);
    if (range && bounds.height > 0 && bounds.bottom > topbarBottom) {
      return { sourceLine: range.startLine, offset: bounds.top, scrollY: Math.max(0, window.scrollY) };
    }
  }
  return { sourceLine: null, offset: 0, scrollY: Math.max(0, window.scrollY) };
}

function restoreDocumentPosition(position) {
  if (!Number.isFinite(position?.scrollY) || position.scrollY < 0) return;

  let top = position.scrollY;
  if (Number.isInteger(position.sourceLine) && Number.isFinite(position.offset)) {
    const candidates = documentSourceBlocks()
      .map((element) => ({ element, range: sourceRange(element) }))
      .filter(({ range }) => range);
    const anchor = chooseAnchor(candidates, position.sourceLine);
    if (anchor) top = window.scrollY + anchor.element.getBoundingClientRect().top - position.offset;
  }

  const maximum = Math.max(0, document.documentElement.scrollHeight - window.innerHeight);
  window.scrollTo({ top: Math.min(maximum, Math.max(0, top)), behavior: "auto" });
}

// States -----------------------------------------------------------------------------

function setLoading(visible) {
  elements.viewerLoading.classList.toggle("is-visible", visible);
  elements.viewerLoading.setAttribute("aria-hidden", String(!visible));
}

function setEmptyStateVisible(visible) {
  elements.emptyState.hidden = !visible;
  elements.preview.hidden = visible;
}

export function showEmptyState() {
  ++renderGeneration;
  pendingOpen = null;
  contextId = null;
  proofFold = null;
  expandedFoldKeys = new Set();
  activeFoldForCollapse = null;
  elements.proofFoldCollapseButton.hidden = true;
  elements.brandName.textContent = "ProofMD";
  setUnresolvedState(false, false);
  elements.preview.replaceChildren();
  elements.documentName.textContent = "No document open";
  document.title = "ProofMD";
  setEmptyStateVisible(true);
  setLoading(false);
  onProofFoldMap(null);
  window.scrollTo({ top: 0, behavior: "auto" });
}

function showRenderError(error) {
  setEmptyStateVisible(false);
  const message = document.createElement("p");
  message.className = "render-error";
  message.textContent = "This document could not be rendered.";
  elements.preview.replaceChildren(message);
  setLoading(false);
  announce(error instanceof Error ? error.message : "Rendering failed.");
}

// Unresolved marker ------------------------------------------------------------------

function setUnresolvedState(isUnresolved, enabled) {
  unresolved = isUnresolved;
  unresolvedRequestPending = false;
  const button = elements.unresolvedButton;
  button.disabled = !enabled || !Number.isInteger(contextId);
  button.setAttribute("aria-pressed", String(unresolved));
  button.title = unresolved ? "Mark this document as resolved" : "Mark this document as unresolved";
}

/** Applies the host's answer about the open document's unresolved marker. */
export function applyUnresolvedState(message) {
  if (message.contextId !== contextId) return;
  setUnresolvedState(message.unresolved === true, message.enabled !== false);
  if (typeof message.error === "string" && message.error) announce(message.error);
}

// Links ------------------------------------------------------------------------------

/**
 * Every link click is handled here: the WebView itself only ever shows the viewer page.
 * Same-document fragments scroll, Markdown files open through the host, and web and mail
 * addresses open in the system's apps.
 */
function handleLinkClick(event) {
  const link = event.target instanceof Element ? event.target.closest("a[href]") : null;
  if (!link || !elements.preview.contains(link) || event.defaultPrevented) return;

  const href = link.getAttribute("href");
  const kind = linkKind(href);
  const primary = event.type === "click" && event.button === 0;
  if (kind === "fragment" && primary) {
    let target = null;
    try {
      target = document.getElementById(decodeURIComponent(href.slice(1)));
    } catch {
      // A malformed fragment has no target.
    }
    if (target && elements.preview.contains(target)) return;
    event.preventDefault();
    announce("This link's target is not in the document.");
    return;
  }

  event.preventDefault();
  if (kind === "external") {
    post("open-external-link", { href });
  } else if (kind === "markdown" && primary) {
    const order = Number(link.dataset.linkOrder);
    post("open-markdown-link", {
      href,
      sourceDocument: link.dataset.sourceDocument ?? null,
      order: Number.isInteger(order) ? order : null,
      position: currentDocumentPosition(),
    });
  } else if (kind === "other" && primary) {
    announce("ProofMD cannot open this kind of link.");
  }
}

export function initDocumentView({ onProofFoldMapChange }) {
  onProofFoldMap = onProofFoldMapChange;
  elements.preview.addEventListener("click", handleLinkClick);
  elements.preview.addEventListener("auxclick", handleLinkClick);
  elements.proofFoldCollapseButton.addEventListener("click", collapseActiveFold);
  elements.unresolvedButton.addEventListener("click", () => {
    if (unresolvedRequestPending || !Number.isInteger(contextId)) return;
    unresolvedRequestPending = true;
    elements.unresolvedButton.disabled = true;
    post("set-document-unresolved", { contextId, unresolved: !unresolved });
  });
  window.addEventListener("scroll", scheduleCollapseControl, { passive: true });
  window.addEventListener("resize", () => {
    scheduleCollapseControl();
    scheduleMathTagLayout();
  });
  document.fonts?.ready.then(scheduleMathTagLayout);
}
