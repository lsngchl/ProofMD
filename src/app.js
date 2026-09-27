import "./styles.css";
import "katex/dist/katex.min.css";
import {
  focusExplorationMap,
  layoutExplorationMap,
  PROOF_FOLD_MAP_GEOMETRY,
  routeExplorationMapEdges,
  unfoldExplorationMap,
} from "./map-layout.js";
import { adjustMathTagLayout } from "./math-layout.js";
import { readStoredTheme } from "./theme-storage.js";
import {
  activeProofFoldIndex,
  createProofFoldIndex,
  resolveProofFoldTarget,
} from "./proof-fold.js";

const elements = {
  brand: document.querySelector("#brand"),
  brandName: document.querySelector("#brandName"),
  documentName: document.querySelector("#documentName"),
  dropOverlay: document.querySelector("#dropOverlay"),
  emptyState: document.querySelector("#emptyState"),
  fileInput: document.querySelector("#fileInput"),
  keyGuideButton: document.querySelector("#keyGuideButton"),
  keyGuideCloseButton: document.querySelector("#keyGuideCloseButton"),
  keyGuidePanel: document.querySelector("#keyGuidePanel"),
  mapButton: document.querySelector("#mapButton"),
  mapCloseButton: document.querySelector("#mapCloseButton"),
  mapCount: document.querySelector("#mapCount"),
  mapEdgeLayer: document.querySelector("#mapEdgeLayer"),
  mapEdges: document.querySelector("#mapEdges"),
  mapEyebrow: document.querySelector("#mapEyebrow"),
  mapHelpText: document.querySelector("#mapHelpText"),
  mapNodeLayer: document.querySelector("#mapNodeLayer"),
  mapOverlay: document.querySelector("#mapOverlay"),
  mapResetButton: document.querySelector("#mapResetButton"),
  mapResetConfirmButton: document.querySelector("#mapResetConfirmButton"),
  mapResetDialog: document.querySelector("#mapResetDialog"),
  mapSurface: document.querySelector("#mapSurface"),
  mapTitle: document.querySelector("#mapTitle"),
  mapViewport: document.querySelector("#mapViewport"),
  mapZoomFitButton: document.querySelector("#mapZoomFitButton"),
  mapZoomInButton: document.querySelector("#mapZoomInButton"),
  mapZoomOutButton: document.querySelector("#mapZoomOutButton"),
  mapZoomSlider: document.querySelector("#mapZoomSlider"),
  mapZoomValue: document.querySelector("#mapZoomValue"),
  openButton: document.querySelector("#openButton"),
  preview: document.querySelector("#preview"),
  proofFoldCollapseButton: document.querySelector("#proofFoldCollapseButton"),
  status: document.querySelector("#status"),
  themeButton: document.querySelector("#themeButton"),
  topbar: document.querySelector(".topbar"),
  unresolvedButton: document.querySelector("#unresolvedButton"),
  viewerLoading: document.querySelector("#viewerLoading"),
};

const webViewHost = window.chrome?.webview;
const SVG_NAMESPACE = "http://www.w3.org/2000/svg";
const MAP_NODE_GEOMETRY = Object.freeze({
  nodeWidth: 220,
  nodeHeight: 72,
  horizontalStep: 300,
  verticalStep: 104,
});
const MAP_MIN_ZOOM = 0.05;
const MAP_MAX_ZOOM = 2;
const MAP_ZOOM_STEP = 0.1;
const MAP_OVERVIEW_ENTER_ZOOM = 0.4;
const MAP_OVERVIEW_EXIT_ZOOM = 0.48;
const SOURCE_BLOCK_SELECTOR =
  "[data-source-start-line][data-source-end-line]";
let renderGeneration = 0;
let documentRenderInstance = 0;
let rendererPromise;
let renderedMapLayout = null;
let renderedMapGeometry = MAP_NODE_GEOMETRY;
let renderedCurrentOccurrenceId = null;
let mapPanPointer = null;
let mapOverviewMode = false;
let mapOverviewForced = false;
let preferredMapDocumentPath = null;
let expandedMapOccurrenceKeys = new Set();
let currentDocumentContextId = null;
let currentDocumentUnresolved = false;
let unresolvedRequestPending = false;
let currentProofFold = null;
let expandedProofFoldPaths = new Set();
let activeProofFoldForCollapse = null;
let proofFoldCollapseAnimationFrame = null;
let mathTagLayoutAnimationFrame = null;
let mapCamera = {
  sessionId: null,
  x: 0,
  y: 0,
  zoom: 1,
  initialized: false,
};
let mapState = {
  format: "markdown",
  sessionId: null,
  root: null,
  current: null,
  previous: null,
  nodes: [],
  edges: [],
};

function loadRenderer() {
  rendererPromise ??= import("./renderer.js");
  return rendererPromise;
}

function isExternalWebHref(href) {
  return typeof href === "string" && /^https?:\/\//iu.test(href.trim());
}

function appendExternalLinkIndicator(link) {
  const icon = document.createElementNS(SVG_NAMESPACE, "svg");
  icon.classList.add("external-link-icon");
  icon.setAttribute("viewBox", "0 0 16 16");
  icon.setAttribute("aria-hidden", "true");
  icon.setAttribute("focusable", "false");
  const path = document.createElementNS(SVG_NAMESPACE, "path");
  path.setAttribute(
    "d",
    "M9.5 2.5h4v4M13.25 2.75 7.5 8.5M12.5 9v3.5a1 1 0 0 1-1 1h-8a1 1 0 0 1-1-1v-8a1 1 0 0 1 1-1H7",
  );
  icon.append(path);

  const hint = document.createElement("span");
  hint.className = "visually-hidden";
  hint.textContent = " (opens in an external browser)";
  link.append(" ", icon, hint);
}

function isProofFoldLink(link) {
  return link.getAttribute("title")?.trim().toLowerCase() === "fold";
}

function isStandaloneParagraphLink(link) {
  const paragraph = link.parentElement;
  if (paragraph?.tagName !== "P") return false;

  const meaningfulNodes = [...paragraph.childNodes].filter(
    (node) => node.nodeType !== Node.TEXT_NODE || node.textContent.trim(),
  );
  return meaningfulNodes.length === 1 && meaningfulNodes[0] === link;
}

function copySourceRange(source, target) {
  const range = sourceRangeForElement(source);
  if (!range) return;

  target.dataset.sourceStartLine = String(range.startLine);
  target.dataset.sourceEndLine = String(range.endLine);
}

function appendFoldMessage(content, message) {
  const paragraph = document.createElement("p");
  paragraph.className = "proof-fold-message";
  paragraph.textContent = message;
  content.append(paragraph);
}

function replaceProofFoldLink(
  link,
  sourceDocumentId,
  renderMarkdown,
  ancestry,
) {
  const href = link.getAttribute("href");
  const targetId = resolveProofFoldTarget(sourceDocumentId, href);
  const fragment = targetId ? currentProofFold.folds.get(targetId) : null;
  const isCycle = targetId ? ancestry.includes(targetId) : false;
  const isPending = /^fold\s*\(pending\)\s*:/iu.test(link.textContent.trim());
  const label = link.textContent.trim() || "Fold";

  const details = document.createElement("details");
  details.className = "proof-fold";
  if (targetId) details.dataset.proofFoldTarget = targetId;
  if (isPending) details.classList.add("is-pending");
  if (!targetId || !fragment || isCycle) details.classList.add("is-error");

  const summary = document.createElement("summary");
  summary.className = "proof-fold-summary";
  summary.append(...[...link.childNodes].map((node) => node.cloneNode(true)));
  details.append(summary);

  const content = document.createElement("div");
  content.className = "proof-fold-content";
  content.setAttribute("role", "region");
  content.setAttribute("aria-label", label);
  details.append(content);

  const replacementTarget = isStandaloneParagraphLink(link)
    ? link.parentElement
    : link;
  copySourceRange(replacementTarget, details);
  replacementTarget.replaceWith(details);

  let loaded = false;
  const loadContent = () => {
    if (loaded) return;
    loaded = true;

    if (!targetId || !fragment) {
      details.classList.add("is-error");
      appendFoldMessage(content, "This fold target is missing or outside the ProofFold document.");
      return;
    }
    if (isCycle) {
      details.classList.add("is-error");
      appendFoldMessage(content, "This fold would create a recursive expansion cycle.");
      return;
    }
    if (isPending) {
      appendFoldMessage(content, "This fold is pending.");
      return;
    }

    content.innerHTML = renderMarkdown(fragment.source, {
      documentId: targetId,
      instanceId: ++documentRenderInstance,
    });
    enhanceRenderedContent(
      content,
      targetId,
      renderMarkdown,
      [...ancestry, targetId],
    );
  };

  details.addEventListener("toggle", () => {
    if (details.open) {
      if (targetId) expandedProofFoldPaths.add(targetId);
      loadContent();
      scheduleMathTagLayout();
      announce(`${label} expanded.`);
    } else {
      if (targetId) expandedProofFoldPaths.delete(targetId);
      announce(`${label} collapsed.`);
    }
    scheduleProofFoldCollapseControl();
  });

  if (targetId && expandedProofFoldPaths.has(targetId)) {
    details.open = true;
    loadContent();
  }
}

function enhanceRenderedContent(
  container,
  sourceDocumentId,
  renderMarkdown,
  ancestry,
) {
  const links = [...container.querySelectorAll("a[href]")];
  const markdownLinks = [];

  for (const link of links) {
    const href = link.getAttribute("href");
    if (sourceDocumentId) {
      link.dataset.proofFoldSource = sourceDocumentId;
    }

    if (currentProofFold && sourceDocumentId && isProofFoldLink(link)) {
      replaceProofFoldLink(link, sourceDocumentId, renderMarkdown, ancestry);
      continue;
    }

    if (isRelativeMarkdownLink(href)) {
      markdownLinks.push(link);
    }
    if (isExternalWebHref(href)) {
      link.target = "_blank";
      link.rel = "noreferrer noopener";
      link.classList.add("external-link");
      appendExternalLinkIndicator(link);
    }
  }

  scheduleMathTagLayout();
  return markdownLinks;
}

function scheduleMathTagLayout() {
  if (mathTagLayoutAnimationFrame !== null) return;
  mathTagLayoutAnimationFrame = window.requestAnimationFrame(() => {
    mathTagLayoutAnimationFrame = null;
    adjustMathTagLayout(elements.preview);
  });
}

function configureProofFold(payload, preserveState) {
  const nextProofFold = createProofFoldIndex(payload);
  const sameEntry = currentProofFold &&
    nextProofFold &&
    currentProofFold.entry === nextProofFold.entry;
  if (!preserveState || !sameEntry) {
    expandedProofFoldPaths = new Set();
  }
  currentProofFold = nextProofFold;
  activeProofFoldForCollapse = null;
}

function updateProofFoldCollapseControl() {
  proofFoldCollapseAnimationFrame = null;
  const button = elements.proofFoldCollapseButton;
  if (!currentProofFold) {
    activeProofFoldForCollapse = null;
    button.hidden = true;
    return;
  }

  const openFolds = [...elements.preview.querySelectorAll("details.proof-fold[open]")];
  const topbarBottom = elements.topbar?.getBoundingClientRect().bottom ?? 0;
  const readingLine = Math.min(
    Math.max(topbarBottom + 36, window.innerHeight * 0.36),
    Math.max(0, window.innerHeight - 36),
  );
  const activeIndex = activeProofFoldIndex(
    openFolds.map((fold) => fold.getBoundingClientRect()),
    readingLine,
    window.innerHeight,
  );
  activeProofFoldForCollapse = activeIndex >= 0 ? openFolds[activeIndex] : null;
  if (!activeProofFoldForCollapse) {
    button.hidden = true;
    return;
  }

  const summary = activeProofFoldForCollapse.querySelector(
    ":scope > .proof-fold-summary",
  );
  const label = summary?.textContent.trim() || "current fold";
  button.hidden = false;
  button.title = `Collapse ${label}`;
  button.setAttribute("aria-label", `Collapse ${label}`);
}

function scheduleProofFoldCollapseControl() {
  if (proofFoldCollapseAnimationFrame !== null) return;
  proofFoldCollapseAnimationFrame = window.requestAnimationFrame(
    updateProofFoldCollapseControl,
  );
}

function collapseActiveProofFold() {
  const details = activeProofFoldForCollapse;
  if (!details?.open) return;

  const summary = details.querySelector(":scope > .proof-fold-summary");
  activeProofFoldForCollapse = null;
  elements.proofFoldCollapseButton.hidden = true;
  details.open = false;
  if (!(summary instanceof HTMLElement)) return;

  summary.focus({ preventScroll: true });
  window.requestAnimationFrame(() => {
    summary.scrollIntoView({
      block: "center",
      behavior: window.matchMedia("(prefers-reduced-motion: reduce)").matches
        ? "auto"
        : "smooth",
    });
  });
}

function setProductBrand(name) {
  elements.brandName.textContent = name;
  elements.brand.setAttribute("aria-label", `${name} Viewer`);
}

function renderDocument(
  source,
  name,
  renderMarkdown,
  {
    resetScroll = true,
    proofFold = null,
    preserveProofFoldState = false,
  } = {},
) {
  configureProofFold(proofFold, preserveProofFoldState);
  setProductBrand(currentProofFold ? "ProofFold" : "ProofMD");
  setEmptyStateVisible(false);
  const entryId = currentProofFold?.entry ?? null;
  elements.preview.innerHTML = renderMarkdown(source, {
    documentId: entryId,
    instanceId: ++documentRenderInstance,
  });
  elements.documentName.textContent = name;

  const markdownLinks = enhanceRenderedContent(
    elements.preview,
    entryId,
    renderMarkdown,
    entryId ? [entryId] : [],
  );
  for (const [order, link] of markdownLinks.entries()) {
    link.dataset.proofmdLinkOrder = String(order);
  }

  if (webViewHost && Number.isInteger(currentDocumentContextId)) {
    webViewHost.postMessage({
      type: "document-links",
      contextId: currentDocumentContextId,
      links: markdownLinks.map((link, order) => ({
        href: link.getAttribute("href"),
        order,
      })),
    });
  }

  document.title = `${name} — ${currentProofFold ? "ProofFold" : "ProofMD"}`;
  announce(`${name} rendered.`);
  if (resetScroll) {
    window.scrollTo({ top: 0, behavior: "auto" });
  }
  scheduleProofFoldCollapseControl();
}

function sourceRangeForElement(element) {
  if (!(element instanceof Element)) return null;

  const startLine = Number(element.dataset.sourceStartLine);
  const endLine = Number(element.dataset.sourceEndLine);
  if (
    !Number.isInteger(startLine) ||
    !Number.isInteger(endLine) ||
    startLine < 1 ||
    endLine < startLine
  ) {
    return null;
  }

  return { startLine, endLine };
}

function leafSourceBlocks() {
  return [...elements.preview.querySelectorAll(SOURCE_BLOCK_SELECTOR)].filter(
    (element) => !element.querySelector(SOURCE_BLOCK_SELECTOR),
  );
}

function currentDocumentPosition() {
  const scrollY = Math.max(0, window.scrollY);
  const anchor = leafSourceBlocks()
    .map((element) => ({
      range: sourceRangeForElement(element),
      bounds: element.getBoundingClientRect(),
    }))
    .find(
      ({ range, bounds }) =>
        range && bounds.width > 0 && bounds.height > 0 && bounds.bottom > 0,
    );

  return {
    sourceLine: anchor?.range.startLine ?? null,
    offset: anchor?.bounds.top ?? 0,
    scrollY,
  };
}

function restoreDocumentPosition(position) {
  if (!position || !Number.isFinite(position.scrollY) || position.scrollY < 0) {
    return;
  }

  let targetScrollY = position.scrollY;
  if (Number.isInteger(position.sourceLine) && Number.isFinite(position.offset)) {
    const candidates = leafSourceBlocks()
      .map((element) => ({ element, range: sourceRangeForElement(element) }))
      .filter(({ range }) => range);
    const anchor =
      candidates.find(
        ({ range }) =>
          range.startLine <= position.sourceLine && range.endLine >= position.sourceLine,
      ) ??
      candidates.reduce((closest, candidate) => {
        if (!closest) return candidate;
        return Math.abs(candidate.range.startLine - position.sourceLine) <
          Math.abs(closest.range.startLine - position.sourceLine)
          ? candidate
          : closest;
      }, null);
    if (anchor) {
      targetScrollY =
        window.scrollY + anchor.element.getBoundingClientRect().top - position.offset;
    }
  }

  const maximumScroll = Math.max(
    0,
    document.documentElement.scrollHeight - window.innerHeight,
  );
  window.scrollTo({
    top: Math.min(maximumScroll, Math.max(0, targetScrollY)),
    behavior: "auto",
  });
}

function isRelativeMarkdownLink(href) {
  if (!href || href.startsWith("#") || /^[a-z][a-z\d+.-]*:/i.test(href)) {
    return false;
  }

  const path = href.split(/[?#]/u, 1)[0];
  if (!path || path.startsWith("//")) return false;

  try {
    return /\.(?:md|markdown)$/iu.test(decodeURIComponent(path));
  } catch {
    return false;
  }
}

function reconciledPreferredMapPath(nextState) {
  const current =
    typeof nextState.current === "string" ? nextState.current : null;
  if (!current || !Array.isArray(preferredMapDocumentPath)) return null;

  const currentIndex = preferredMapDocumentPath.lastIndexOf(current);
  if (currentIndex >= 0) {
    return preferredMapDocumentPath.slice(0, currentIndex + 1);
  }

  const previous =
    typeof nextState.previous === "string" ? nextState.previous : null;
  const lastDocument =
    preferredMapDocumentPath[preferredMapDocumentPath.length - 1] ?? null;
  const continuesPath =
    previous === lastDocument &&
    nextState.edges.some(
      (edge) => edge?.from === previous && edge?.to === current,
    );
  return continuesPath ? [...preferredMapDocumentPath, current] : null;
}

function setMapState(nextState) {
  if (!nextState || !Array.isArray(nextState.nodes) || !Array.isArray(nextState.edges)) {
    return;
  }

  if (mapCamera.sessionId !== nextState.sessionId) {
    mapCamera = {
      sessionId: nextState.sessionId,
      x: 0,
      y: 0,
      zoom: 1,
      initialized: false,
    };
    mapOverviewMode = false;
    mapOverviewForced = false;
    preferredMapDocumentPath = null;
    expandedMapOccurrenceKeys = new Set();
  } else {
    preferredMapDocumentPath = reconciledPreferredMapPath(nextState);
  }

  mapState = {
    format: nextState.format === "prooffold" ? "prooffold" : "markdown",
    sessionId: nextState.sessionId,
    root: typeof nextState.root === "string" ? nextState.root : null,
    current: typeof nextState.current === "string" ? nextState.current : null,
    previous: typeof nextState.previous === "string" ? nextState.previous : null,
    nodes: nextState.nodes,
    edges: nextState.edges,
  };

  const count = mapState.nodes.length;
  const isProofFoldMap = mapState.format === "prooffold";
  elements.mapButton.disabled = count === 0;
  elements.mapCount.textContent = String(count);
  elements.mapResetButton.disabled = count === 0;
  elements.mapResetButton.hidden = isProofFoldMap;
  elements.mapEyebrow.textContent = isProofFoldMap
    ? "ProofFold map"
    : "Exploration map";
  elements.mapTitle.textContent = isProofFoldMap
    ? "Complete fold structure"
    : "Structure you have revealed";
  elements.mapHelpText.textContent = isProofFoldMap
    ? "Icon nodes show every reachable fold · Hover for names"
    : "+N expands hidden branches · Overview shows the full structure";

  if (count === 0 && !elements.mapOverlay.hidden) {
    closeMap();
  }

  renderMap();

  if (!elements.mapOverlay.hidden && !mapCamera.initialized) {
    window.requestAnimationFrame(initializeOpenMapCamera);
  }
}

function renderMap() {
  const isProofFoldMap = mapState.format === "prooffold";
  const unfoldedMap = unfoldExplorationMap(
    mapState.nodes,
    mapState.edges,
    mapState.root,
  );
  const focusedMap = focusExplorationMap(
    unfoldedMap,
    mapState.current,
    preferredMapDocumentPath,
    expandedMapOccurrenceKeys,
  );
  preferredMapDocumentPath = focusedMap.currentDocumentPath;
  const currentOccurrenceKey = Array.isArray(preferredMapDocumentPath)
    ? JSON.stringify(preferredMapDocumentPath)
    : null;
  const overviewCurrentOccurrence = unfoldedMap.nodes.find(
    (node) => node.occurrenceKey === currentOccurrenceKey,
  );
  const displayedMap = isProofFoldMap || mapOverviewMode
    ? unfoldedMap
    : focusedMap;
  const mapGeometry = isProofFoldMap
    ? PROOF_FOLD_MAP_GEOMETRY
    : MAP_NODE_GEOMETRY;
  const layout = layoutExplorationMap(
    displayedMap.nodes,
    displayedMap.edges,
    displayedMap.root,
    mapGeometry,
  );
  renderedMapLayout = layout;
  renderedMapGeometry = mapGeometry;
  renderedCurrentOccurrenceId = mapOverviewMode && !isProofFoldMap
    ? overviewCurrentOccurrence?.id ?? null
    : focusedMap.currentOccurrenceId;
  elements.mapNodeLayer.replaceChildren();
  elements.mapEdgeLayer.replaceChildren();
  elements.mapSurface.classList.toggle("is-overview", mapOverviewMode);
  elements.mapSurface.classList.toggle("is-proof-fold", isProofFoldMap);

  elements.mapSurface.style.width = `${layout.width}px`;
  elements.mapSurface.style.height = `${layout.height}px`;
  elements.mapEdges.setAttribute("width", String(layout.width));
  elements.mapEdges.setAttribute("height", String(layout.height));
  elements.mapEdges.setAttribute("viewBox", `0 0 ${layout.width} ${layout.height}`);

  const routes = routeExplorationMapEdges(
    displayedMap.edges,
    layout,
    mapGeometry,
  );
  for (const route of routes) {
    const path = document.createElementNS(SVG_NAMESPACE, "path");
    path.setAttribute("d", route.path);
    if (!isProofFoldMap) {
      path.setAttribute("marker-end", "url(#mapArrow)");
    }
    elements.mapEdgeLayer.append(path);
  }

  const displayNodes = [...displayedMap.nodes].sort((a, b) => {
    const aPosition = layout.positions.get(a.id);
    const bPosition = layout.positions.get(b.id);
    return (
      aPosition.x - bPosition.x ||
      aPosition.y - bPosition.y ||
      a.order - b.order
    );
  });
  for (const node of displayNodes) {
    const position = layout.positions.get(node.id);
    if (!position) continue;

    const documentId = node.documentId;
    const isRoot = documentId === mapState.root;
    const button = document.createElement("button");
    button.className = "map-node";
    button.type = "button";
    const isCurrent = documentId === mapState.current;
    const isPrevious = documentId === mapState.previous;
    const isUnresolved = node.unresolved === true;
    const stateDescriptions = [];
    if (isCurrent) stateDescriptions.push("Current document.");
    if (isPrevious) stateDescriptions.push("Previous document.");
    if (isUnresolved) stateDescriptions.push("Unresolved document.");
    const stateDescription = stateDescriptions.length
      ? `${stateDescriptions.join(" ")} `
      : "";
    button.title = [
      isProofFoldMap ? node.label : null,
      isUnresolved ? "Unresolved" : null,
      isPrevious ? "Previous" : null,
      node.detail,
    ]
      .filter(Boolean)
      .join(" · ");
    button.setAttribute(
      "aria-label",
      `${node.label}. ${stateDescription}${node.detail}`,
    );
    button.classList.toggle("is-root", isRoot);
    button.classList.toggle("is-current", isCurrent);
    button.classList.toggle("is-previous", isPrevious && !mapOverviewMode);
    button.classList.toggle("is-unresolved", isUnresolved);

    const label = document.createElement("strong");
    label.textContent = node.label;
    const detail = document.createElement("span");
    detail.className = "map-node-detail";
    detail.textContent = node.detail;
    const glyph = document.createElement("span");
    glyph.className = "map-node-glyph";
    glyph.setAttribute("aria-hidden", "true");
    glyph.textContent = isUnresolved
      ? "!"
      : isRoot
        ? "◆"
        : "●";
    button.append(label, detail, glyph);

    button.addEventListener("click", () => {
      if (!webViewHost) return;
      preferredMapDocumentPath = node.documentPath;
      closeMap();
      webViewHost.postMessage({
        type: "open-map-node",
        id: documentId,
        position: currentDocumentPosition(),
      });
    });

    const shell = document.createElement("div");
    shell.className = "map-node-shell";
    shell.classList.toggle("is-current", isCurrent);
    shell.dataset.occurrenceKey = node.occurrenceKey;
    shell.style.width = `${mapGeometry.nodeWidth}px`;
    shell.style.height = `${mapGeometry.nodeHeight}px`;
    shell.style.transform = `translate(${position.x}px, ${position.y}px)`;
    shell.append(button);

    if (
      !mapOverviewMode &&
      (node.hiddenChildCount > 0 || node.manuallyExpanded)
    ) {
      const expandButton = document.createElement("button");
      expandButton.className = "map-node-expand-button";
      expandButton.type = "button";
      expandButton.textContent = node.manuallyExpanded
        ? "−"
        : `+${node.hiddenChildCount}`;
      expandButton.setAttribute(
        "aria-label",
        node.manuallyExpanded
          ? `Collapse ${node.label}`
          : `Show ${node.hiddenChildCount} hidden children of ${node.label}`,
      );
      expandButton.addEventListener("click", (event) => {
        event.stopPropagation();
        if (node.manuallyExpanded) {
          expandedMapOccurrenceKeys.delete(node.occurrenceKey);
        } else {
          expandedMapOccurrenceKeys.add(node.occurrenceKey);
        }
        renderMap();
      });
      shell.append(expandButton);
    }

    elements.mapNodeLayer.append(shell);
  }

  constrainMapCamera();
  applyMapCamera();
}

function clampMapZoom(zoom) {
  return Math.min(MAP_MAX_ZOOM, Math.max(MAP_MIN_ZOOM, zoom));
}

function overviewModeForZoom(zoom) {
  if (mapOverviewForced) return true;
  return mapOverviewMode
    ? zoom < MAP_OVERVIEW_EXIT_ZOOM
    : zoom <= MAP_OVERVIEW_ENTER_ZOOM;
}

function constrainMapCamera() {
  if (!renderedMapLayout) return;

  const viewportWidth = elements.mapViewport.clientWidth;
  const viewportHeight = elements.mapViewport.clientHeight;
  if (viewportWidth === 0 || viewportHeight === 0) return;

  const visibleMargin = Math.min(100, viewportWidth * 0.25, viewportHeight * 0.25);
  const scaledWidth = renderedMapLayout.width * mapCamera.zoom;
  const scaledHeight = renderedMapLayout.height * mapCamera.zoom;
  mapCamera.x = Math.min(
    viewportWidth - visibleMargin,
    Math.max(visibleMargin - scaledWidth, mapCamera.x),
  );
  mapCamera.y = Math.min(
    viewportHeight - visibleMargin,
    Math.max(visibleMargin - scaledHeight, mapCamera.y),
  );
}

function applyMapCamera() {
  elements.mapSurface.style.transform =
    `translate(${mapCamera.x}px, ${mapCamera.y}px) scale(${mapCamera.zoom})`;
  const percentage = Math.round(mapCamera.zoom * 100);
  elements.mapZoomSlider.value = String(percentage);
  elements.mapZoomValue.textContent = `${percentage}%`;
  elements.mapZoomOutButton.disabled = mapCamera.zoom <= MAP_MIN_ZOOM;
  elements.mapZoomInButton.disabled = mapCamera.zoom >= MAP_MAX_ZOOM;
  elements.mapZoomFitButton.classList.toggle("is-active", mapOverviewMode);
  elements.mapZoomFitButton.setAttribute(
    "aria-pressed",
    String(mapOverviewMode),
  );
}

function setMapZoom(nextZoom, anchorX, anchorY) {
  const zoom = clampMapZoom(nextZoom);
  if (zoom === mapCamera.zoom) return;

  const viewportWidth = elements.mapViewport.clientWidth;
  const viewportHeight = elements.mapViewport.clientHeight;
  const fixedX = Number.isFinite(anchorX) ? anchorX : viewportWidth / 2;
  const fixedY = Number.isFinite(anchorY) ? anchorY : viewportHeight / 2;
  const contentX = (fixedX - mapCamera.x) / mapCamera.zoom;
  const contentY = (fixedY - mapCamera.y) / mapCamera.zoom;
  mapCamera.zoom = zoom;
  mapCamera.x = fixedX - contentX * zoom;
  mapCamera.y = fixedY - contentY * zoom;
  mapCamera.initialized = true;

  if (mapOverviewForced && zoom >= MAP_OVERVIEW_EXIT_ZOOM) {
    mapOverviewForced = false;
  }
  const nextOverviewMode = overviewModeForZoom(zoom);
  if (nextOverviewMode !== mapOverviewMode) {
    mapOverviewMode = nextOverviewMode;
    renderMap();
    window.requestAnimationFrame(centerCurrentMapNode);
    return;
  }

  constrainMapCamera();
  applyMapCamera();
}

function centerCurrentMapNode() {
  if (!renderedMapLayout) return;

  const position = renderedMapLayout.positions.get(renderedCurrentOccurrenceId);
  if (!position) return;

  mapCamera.x =
    elements.mapViewport.clientWidth / 2 -
    (position.x + renderedMapGeometry.nodeWidth / 2) * mapCamera.zoom;
  mapCamera.y =
    elements.mapViewport.clientHeight / 2 -
    (position.y + renderedMapGeometry.nodeHeight / 2) * mapCamera.zoom;
  mapCamera.initialized = true;
  constrainMapCamera();
  applyMapCamera();
}

function fitMapToViewport() {
  mapOverviewForced = true;
  mapOverviewMode = true;
  renderMap();
  fitRenderedMapToViewport();
}

function fitRenderedMapToViewport() {
  if (!renderedMapLayout) return;

  const viewportWidth = elements.mapViewport.clientWidth;
  const viewportHeight = elements.mapViewport.clientHeight;
  if (viewportWidth === 0 || viewportHeight === 0) return;

  mapCamera.zoom = clampMapZoom(
    Math.min(
      (viewportWidth - 64) / renderedMapLayout.width,
      (viewportHeight - 64) / renderedMapLayout.height,
    ),
  );
  mapCamera.x = (viewportWidth - renderedMapLayout.width * mapCamera.zoom) / 2;
  mapCamera.y = (viewportHeight - renderedMapLayout.height * mapCamera.zoom) / 2;
  mapCamera.initialized = true;
  constrainMapCamera();
  applyMapCamera();
}

function initializeOpenMapCamera() {
  if (mapState.format !== "prooffold") {
    centerCurrentMapNode();
    return;
  }

  if (!mapCamera.initialized) {
    mapOverviewForced = false;
    mapOverviewMode = false;
    renderMap();
    fitRenderedMapToViewport();
    return;
  }

  constrainMapCamera();
  applyMapCamera();
}

function startMapPan(event) {
  if (event.button !== 0 || event.target.closest(".map-node-shell")) return;

  event.preventDefault();
  mapPanPointer = {
    id: event.pointerId,
    x: event.clientX,
    y: event.clientY,
  };
  elements.mapViewport.setPointerCapture(event.pointerId);
  elements.mapViewport.classList.add("is-panning");
}

function moveMapPan(event) {
  if (!mapPanPointer || event.pointerId !== mapPanPointer.id) return;

  event.preventDefault();
  mapCamera.x += event.clientX - mapPanPointer.x;
  mapCamera.y += event.clientY - mapPanPointer.y;
  mapPanPointer.x = event.clientX;
  mapPanPointer.y = event.clientY;
  mapCamera.initialized = true;
  constrainMapCamera();
  applyMapCamera();
}

function endMapPan(event) {
  if (!mapPanPointer || event.pointerId !== mapPanPointer.id) return;

  if (elements.mapViewport.hasPointerCapture(event.pointerId)) {
    elements.mapViewport.releasePointerCapture(event.pointerId);
  }
  mapPanPointer = null;
  elements.mapViewport.classList.remove("is-panning");
}

function openMap() {
  if (elements.mapButton.disabled) return;

  closeKeyGuide();
  elements.mapOverlay.hidden = false;
  elements.mapButton.setAttribute("aria-expanded", "true");
  document.body.classList.add("map-is-open");
  renderMap();

  window.requestAnimationFrame(initializeOpenMapCamera);
}

function closeMap() {
  if (elements.mapResetDialog.open) elements.mapResetDialog.close();
  if (
    mapPanPointer &&
    elements.mapViewport.hasPointerCapture(mapPanPointer.id)
  ) {
    elements.mapViewport.releasePointerCapture(mapPanPointer.id);
  }
  mapPanPointer = null;
  elements.mapViewport.classList.remove("is-panning");
  elements.mapOverlay.hidden = true;
  elements.mapButton.setAttribute("aria-expanded", "false");
  document.body.classList.remove("map-is-open");
}

function toggleMap() {
  if (elements.mapButton.disabled) return;

  if (elements.mapOverlay.hidden) {
    openMap();
  } else {
    closeMap();
  }
}

function openKeyGuide() {
  elements.keyGuidePanel.hidden = false;
  elements.keyGuideButton.setAttribute("aria-expanded", "true");
}

function closeKeyGuide() {
  elements.keyGuidePanel.hidden = true;
  elements.keyGuideButton.setAttribute("aria-expanded", "false");
}

function toggleKeyGuide() {
  if (elements.keyGuidePanel.hidden) {
    openKeyGuide();
  } else {
    closeKeyGuide();
  }
}

function isEditableTarget(target) {
  return (
    target instanceof HTMLElement &&
    (target.isContentEditable || ["INPUT", "TEXTAREA", "SELECT"].includes(target.tagName))
  );
}

function setDropOverlay(visible) {
  elements.dropOverlay.classList.toggle("is-visible", visible);
  elements.dropOverlay.setAttribute("aria-hidden", String(!visible));
}

function showRenderError(error) {
  setEmptyStateVisible(false);
  elements.preview.replaceChildren();
  const message = document.createElement("p");
  message.className = "render-error";
  message.textContent = "This document could not be rendered.";
  elements.preview.append(message);
  announce(error instanceof Error ? error.message : "Rendering failed.");
}

async function openFile(file) {
  if (!file) return;

  setDocumentLoading(true);
  await waitForPaint();

  try {
    const source = await file.text();
    await renderWithLoading(source, file.name || "Untitled.md");
  } catch {
    announce("The selected file could not be read.");
    setDocumentLoading(false);
  } finally {
    elements.fileInput.value = "";
  }
}

async function renderWithLoading(
  source,
  name,
  {
    preserveScroll = false,
    showLoading = true,
    restorePosition = null,
    proofFold = null,
  } = {},
) {
  const generation = ++renderGeneration;
  const previousScrollY = preserveScroll ? window.scrollY : 0;
  if (showLoading) {
    setDocumentLoading(true);
    await waitForPaint();
  }

  try {
    const { renderMarkdown } = await loadRenderer();
    if (generation !== renderGeneration) return;
    renderDocument(source, name, renderMarkdown, {
      resetScroll: !preserveScroll,
      proofFold,
      preserveProofFoldState: preserveScroll,
    });
  } catch (error) {
    if (generation !== renderGeneration) return;
    showRenderError(error);
    setDocumentLoading(false);
    return;
  }

  try {
    await document.fonts?.ready;
  } catch {
    // Font readiness is a visual enhancement, not a rendering requirement.
  }

  await waitForPaint();
  if (generation !== renderGeneration) return;

  setDocumentLoading(false);
  if (restorePosition) {
    restoreDocumentPosition(restorePosition);
  } else if (preserveScroll) {
    const maximumScroll = Math.max(
      0,
      document.documentElement.scrollHeight - window.innerHeight,
    );
    window.scrollTo({
      top: Math.min(previousScrollY, maximumScroll),
      behavior: "auto",
    });
  }
}

function showEmptyState() {
  ++renderGeneration;
  currentDocumentContextId = null;
  currentProofFold = null;
  expandedProofFoldPaths = new Set();
  activeProofFoldForCollapse = null;
  if (proofFoldCollapseAnimationFrame !== null) {
    window.cancelAnimationFrame(proofFoldCollapseAnimationFrame);
    proofFoldCollapseAnimationFrame = null;
  }
  elements.proofFoldCollapseButton.hidden = true;
  setProductBrand("ProofMD");
  setDocumentUnresolvedState(false, false);
  elements.preview.replaceChildren();
  elements.documentName.textContent = "No document open";
  document.title = "ProofMD Viewer";
  setEmptyStateVisible(true);
  setDocumentLoading(false);
  announce("No document open. Drop a Markdown file or use Open .md.");
  window.scrollTo({ top: 0, behavior: "auto" });
}

function setEmptyStateVisible(visible) {
  elements.emptyState.hidden = !visible;
  elements.preview.hidden = visible;
}

function setDocumentLoading(visible) {
  elements.viewerLoading.classList.toggle("is-visible", visible);
  elements.viewerLoading.setAttribute("aria-hidden", String(!visible));
}

function waitForPaint() {
  return new Promise((resolve) => {
    window.requestAnimationFrame(() => {
      window.requestAnimationFrame(resolve);
    });
  });
}

function announce(message) {
  elements.status.textContent = "";
  window.setTimeout(() => {
    elements.status.textContent = message;
  }, 10);
}

function setDocumentUnresolvedState(unresolved, enabled = true) {
  currentDocumentUnresolved = unresolved === true;
  unresolvedRequestPending = false;
  elements.unresolvedButton.disabled =
    !enabled || !webViewHost || !Number.isInteger(currentDocumentContextId);
  elements.unresolvedButton.setAttribute(
    "aria-pressed",
    String(currentDocumentUnresolved),
  );
  elements.unresolvedButton.title = currentDocumentUnresolved
    ? "Mark this document as resolved"
    : "Mark this document as unresolved";
}

function setTheme(theme) {
  document.documentElement.dataset.theme = theme;
  elements.themeButton.textContent = theme === "dark" ? "Light" : "Dark";
  elements.themeButton.setAttribute(
    "aria-label",
    `Switch to ${theme === "dark" ? "light" : "dark"} theme`,
  );

  try {
    localStorage.setItem("proofmd-theme", theme);
  } catch {
    // Storage may be unavailable for a local file; the theme still works.
  }
}

function initialTheme() {
  try {
    const saved = readStoredTheme(localStorage);
    if (saved) return saved;
  } catch {
    // Fall through to the operating-system preference.
  }

  return window.matchMedia("(prefers-color-scheme: dark)").matches
    ? "dark"
    : "light";
}

function requestOpenFile() {
  if (webViewHost) {
    webViewHost.postMessage({ type: "open-file-dialog" });
    return;
  }

  elements.fileInput.click();
}

elements.fileInput.addEventListener("change", (event) => {
  openFile(event.target.files?.[0]);
});

elements.openButton.addEventListener("click", (event) => {
  if (!webViewHost) return;

  event.preventDefault();
  requestOpenFile();
});

elements.proofFoldCollapseButton.addEventListener(
  "click",
  collapseActiveProofFold,
);

elements.preview.addEventListener("click", (event) => {
  if (!webViewHost || event.defaultPrevented || event.button !== 0) return;

  const target = event.target instanceof Element ? event.target : null;
  const link = target?.closest("a[href]");
  if (!link || !elements.preview.contains(link)) return;

  const href = link.getAttribute("href");
  if (!isRelativeMarkdownLink(href)) return;

  event.preventDefault();
  const order = Number(link.dataset.proofmdLinkOrder);
  webViewHost.postMessage({
    type: "open-markdown-link",
    href,
    sourceDocument: link.dataset.proofFoldSource ?? null,
    order: Number.isInteger(order) && order >= 0 ? order : null,
    position: currentDocumentPosition(),
  });
});

elements.mapButton.addEventListener("click", toggleMap);
elements.unresolvedButton.addEventListener("click", () => {
  if (
    !webViewHost ||
    unresolvedRequestPending ||
    !Number.isInteger(currentDocumentContextId)
  ) {
    return;
  }

  unresolvedRequestPending = true;
  elements.unresolvedButton.disabled = true;
  webViewHost.postMessage({
    type: "set-document-unresolved",
    contextId: currentDocumentContextId,
    unresolved: !currentDocumentUnresolved,
  });
});
elements.mapCloseButton.addEventListener("click", closeMap);
elements.mapResetButton.addEventListener("click", () => {
  if (!elements.mapResetButton.disabled) elements.mapResetDialog.showModal();
});
elements.mapResetConfirmButton.addEventListener("click", () => {
  webViewHost?.postMessage({ type: "reset-map" });
});
elements.mapOverlay.addEventListener("click", (event) => {
  if (event.target === elements.mapOverlay) closeMap();
});
elements.mapViewport.addEventListener("pointerdown", startMapPan);
elements.mapViewport.addEventListener("pointermove", moveMapPan);
elements.mapViewport.addEventListener("pointerup", endMapPan);
elements.mapViewport.addEventListener("pointercancel", endMapPan);
elements.mapViewport.addEventListener(
  "wheel",
  (event) => {
    event.preventDefault();
    const bounds = elements.mapViewport.getBoundingClientRect();
    const zoomFactor = event.deltaY < 0 ? 1.1 : 1 / 1.1;
    setMapZoom(
      mapCamera.zoom * zoomFactor,
      event.clientX - bounds.left,
      event.clientY - bounds.top,
    );
  },
  { passive: false },
);
elements.mapZoomOutButton.addEventListener("click", () => {
  setMapZoom(mapCamera.zoom - MAP_ZOOM_STEP);
});
elements.mapZoomInButton.addEventListener("click", () => {
  setMapZoom(mapCamera.zoom + MAP_ZOOM_STEP);
});
elements.mapZoomSlider.addEventListener("input", (event) => {
  setMapZoom(Number(event.target.value) / 100);
});
elements.mapZoomFitButton.addEventListener("click", fitMapToViewport);
elements.keyGuideButton.addEventListener("click", toggleKeyGuide);
elements.keyGuideCloseButton.addEventListener("click", closeKeyGuide);

document.addEventListener("click", (event) => {
  if (elements.keyGuidePanel.hidden) return;

  const target = event.target instanceof Node ? event.target : null;
  if (
    target &&
    (elements.keyGuidePanel.contains(target) || elements.keyGuideButton.contains(target))
  ) {
    return;
  }

  closeKeyGuide();
});

elements.themeButton.addEventListener("click", () => {
  const next = document.documentElement.dataset.theme === "dark" ? "light" : "dark";
  setTheme(next);
});

document.addEventListener("keydown", (event) => {
  if (elements.mapResetDialog.open) return;

  if (event.key === "Escape" && !elements.keyGuidePanel.hidden) {
    event.preventDefault();
    closeKeyGuide();
    return;
  }

  if (event.key === "Escape" && !elements.mapOverlay.hidden) {
    event.preventDefault();
    closeMap();
    return;
  }

  if (
    !event.repeat &&
    !event.ctrlKey &&
    !event.metaKey &&
    !event.altKey &&
    event.key.toLowerCase() === "m"
  ) {
    event.preventDefault();
    closeKeyGuide();
    toggleMap();
    return;
  }

  if (
    webViewHost &&
    !event.repeat &&
    (event.key === "Backspace" || event.key === ",") &&
    !event.ctrlKey &&
    !event.metaKey &&
    !event.altKey &&
    elements.keyGuidePanel.hidden &&
    !isEditableTarget(event.target)
  ) {
    event.preventDefault();
    closeMap();
    webViewHost.postMessage({ type: "go-back" });
    return;
  }

  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "o") {
    event.preventDefault();
    requestOpenFile();
  }
});

window.addEventListener("scroll", scheduleProofFoldCollapseControl, { passive: true });
window.addEventListener("resize", scheduleProofFoldCollapseControl);
window.addEventListener("resize", scheduleMathTagLayout);
document.fonts?.ready.then(scheduleMathTagLayout);

for (const eventName of ["dragenter", "dragover"]) {
  window.addEventListener(eventName, (event) => {
    event.preventDefault();
    setDropOverlay(true);
  });
}

window.addEventListener("dragleave", (event) => {
  if (event.relatedTarget !== null) return;
  setDropOverlay(false);
});

window.addEventListener("drop", (event) => {
  event.preventDefault();
  setDropOverlay(false);
  const file = event.dataTransfer?.files?.[0];
  if (
    file &&
    webViewHost &&
    typeof webViewHost.postMessageWithAdditionalObjects === "function"
  ) {
    webViewHost.postMessageWithAdditionalObjects(
      { type: "open-dropped-file" },
      [file],
    );
    return;
  }

  openFile(file);
});

window.ProofMD = Object.freeze({
  openMarkdown(
    source,
    name = "Untitled.md",
    contextId = null,
    unresolved = false,
    restorePosition = null,
    proofFold = null,
  ) {
    if (typeof source !== "string") return;
    currentDocumentContextId = Number.isInteger(contextId) ? contextId : null;
    setDocumentUnresolvedState(unresolved, true);
    return renderWithLoading(
      source,
      typeof name === "string" ? name : "Untitled.md",
      { restorePosition, proofFold },
    );
  },
  reloadMarkdown(source, name = "Untitled.md", contextId = null, proofFold = null) {
    if (typeof source !== "string") return;
    if (Number.isInteger(contextId)) {
      currentDocumentContextId = contextId;
    }
    return renderWithLoading(
      source,
      typeof name === "string" ? name : "Untitled.md",
      { preserveScroll: true, showLoading: false, proofFold },
    );
  },
  showEmptyState,
});

setTheme(initialTheme());
showEmptyState();

if (webViewHost) {
  webViewHost.addEventListener("message", async (event) => {
    const message = event.data;
    if (message?.type === "open-markdown") {
      window.ProofMD.openMarkdown(
        message.source,
        message.name,
        message.contextId,
        message.unresolved,
        message.restorePosition,
        message.proofFold,
      );
    } else if (message?.type === "reload-markdown") {
      await window.ProofMD.reloadMarkdown(
        message.source,
        message.name,
        message.contextId,
        message.proofFold,
      );
    } else if (message?.type === "show-empty-state") {
      window.ProofMD.showEmptyState();
    } else if (message?.type === "host-window-visible") {
      await waitForPaint();
      webViewHost.postMessage({ type: "viewer-window-painted" });
    } else if (message?.type === "map-state") {
      setMapState(message);
    } else if (message?.type === "document-unresolved-state") {
      if (message.contextId !== currentDocumentContextId) return;
      setDocumentUnresolvedState(message.unresolved, message.enabled !== false);
      if (typeof message.error === "string" && message.error) announce(message.error);
    }
  });

  waitForPaint().then(() => {
    webViewHost.postMessage({ type: "viewer-shell-painted" });
  });
}
