import {
  focusExplorationMap,
  layoutExplorationMap,
  PROOF_FOLD_MAP_GEOMETRY,
  routeExplorationMapEdges,
  unfoldExplorationMap,
} from "./map-layout.js";
import { elements, post } from "./ui.js";

const SVG_NAMESPACE = "http://www.w3.org/2000/svg";
const EXPLORATION_GEOMETRY = Object.freeze({ nodeWidth: 220, nodeHeight: 72, horizontalStep: 300, verticalStep: 104 });
const MIN_ZOOM = 0.05;
const MAX_ZOOM = 2;
const ZOOM_STEP = 0.1;
// Overview mode starts at 40% and ends above 48%, so it does not flicker at one zoom level.
const OVERVIEW_ENTER_ZOOM = 0.4;
const OVERVIEW_EXIT_ZOOM = 0.48;

let explorationMap = { sessionId: null, root: null, current: null, previous: null, nodes: [], edges: [] };
let proofFoldMap = null;
let camera = { key: null, x: 0, y: 0, zoom: 1, initialized: false };
let overview = false;
let overviewForced = false;
let preferredPath = null;
let expandedKeys = new Set();
let rendered = null;
let panPointer = null;
let getPosition = () => null;
let revealFold = () => {};

const isOpen = () => !elements.mapOverlay.hidden;

/** The map for the open document: its fold structure, or the documents explored so far. */
function activeMap() {
  return proofFoldMap
    ? { ...proofFoldMap, kind: "prooffold", current: proofFoldMap.root, previous: null, key: `fold:${proofFoldMap.root}` }
    : { ...explorationMap, kind: "exploration", key: `exploration:${explorationMap.sessionId}` };
}

export function setExplorationMap(state) {
  if (!Array.isArray(state?.nodes) || !Array.isArray(state.edges)) return;
  const next = {
    sessionId: state.sessionId,
    root: typeof state.root === "string" ? state.root : null,
    current: typeof state.current === "string" ? state.current : null,
    previous: typeof state.previous === "string" ? state.previous : null,
    nodes: state.nodes,
    edges: state.edges,
  };
  preferredPath = next.sessionId === explorationMap.sessionId ? continuedPath(next) : null;
  explorationMap = next;
  refresh();
}

export function setProofFoldMap(map) {
  proofFoldMap = map;
  refresh();
}

/**
 * Keeps the highlighted occurrence of a document that appears more than once when the
 * reader moves along the branch they were following.
 */
function continuedPath(next) {
  if (!next.current || !Array.isArray(preferredPath)) return null;
  const index = preferredPath.lastIndexOf(next.current);
  if (index >= 0) return preferredPath.slice(0, index + 1);
  const continues = next.previous === preferredPath[preferredPath.length - 1] &&
    next.edges.some((edge) => edge?.from === next.previous && edge?.to === next.current);
  return continues ? [...preferredPath, next.current] : null;
}

function refresh() {
  const map = activeMap();
  const isFoldMap = map.kind === "prooffold";
  const count = map.nodes.length;
  elements.mapButton.disabled = count === 0;
  elements.mapCount.textContent = String(count);
  elements.mapResetButton.hidden = isFoldMap;
  elements.mapResetButton.disabled = count === 0;
  elements.mapEyebrow.textContent = isFoldMap ? "ProofFold map" : "Exploration map";
  elements.mapTitle.textContent = isFoldMap ? "Complete fold structure" : "Structure you have revealed";

  if (camera.key !== map.key) {
    camera = { key: map.key, x: 0, y: 0, zoom: 1, initialized: false };
    overview = false;
    overviewForced = false;
    expandedKeys = new Set();
  }
  if (count === 0 && isOpen()) closeMap();
  if (isOpen()) {
    renderMap();
    if (!camera.initialized) window.requestAnimationFrame(initializeCamera);
  }
}

function renderMap() {
  const map = activeMap();
  const isFoldMap = map.kind === "prooffold";
  const unfolded = unfoldExplorationMap(map.nodes, map.edges, map.root);
  const focused = focusExplorationMap(unfolded, map.current, preferredPath, expandedKeys);
  if (!isFoldMap) preferredPath = focused.currentDocumentPath;
  const showAll = isFoldMap || overview;
  const displayed = showAll ? unfolded : focused;
  const geometry = isFoldMap ? PROOF_FOLD_MAP_GEOMETRY : EXPLORATION_GEOMETRY;
  const layout = layoutExplorationMap(displayed.nodes, displayed.edges, displayed.root, geometry);
  const currentKey = Array.isArray(focused.currentDocumentPath) ? JSON.stringify(focused.currentDocumentPath) : null;
  rendered = {
    layout,
    geometry,
    currentId: showAll
      ? unfolded.nodes.find((node) => node.occurrenceKey === currentKey)?.id ?? unfolded.root
      : focused.currentOccurrenceId,
  };

  elements.mapHelpText.textContent = [
    isFoldMap ? "Select a fold to open it in place" : "+N expands hidden branches · Overview shows the full structure",
    unfolded.truncated ? "Showing part of a very large map" : null,
  ].filter(Boolean).join(" · ");
  elements.mapSurface.classList.toggle("is-overview", overview);
  elements.mapSurface.classList.toggle("is-proof-fold", isFoldMap);
  elements.mapSurface.style.width = `${layout.width}px`;
  elements.mapSurface.style.height = `${layout.height}px`;
  elements.mapEdges.setAttribute("width", String(layout.width));
  elements.mapEdges.setAttribute("height", String(layout.height));
  elements.mapEdges.setAttribute("viewBox", `0 0 ${layout.width} ${layout.height}`);

  const paths = routeExplorationMapEdges(displayed.edges, layout, geometry).map((route) => {
    const path = document.createElementNS(SVG_NAMESPACE, "path");
    path.setAttribute("d", route.path);
    if (!isFoldMap) path.setAttribute("marker-end", "url(#mapArrow)");
    return path;
  });
  elements.mapEdgeLayer.replaceChildren(...paths);

  const nodes = [...displayed.nodes]
    .filter((node) => layout.positions.has(node.id))
    .sort((a, b) => {
      const first = layout.positions.get(a.id);
      const second = layout.positions.get(b.id);
      return first.x - second.x || first.y - second.y || a.order - b.order;
    })
    .map((node) => renderNode(node, map, layout.positions.get(node.id), geometry));
  elements.mapNodeLayer.replaceChildren(...nodes);

  constrainCamera();
  applyCamera();
}

function renderNode(node, map, position, geometry) {
  const isFoldMap = map.kind === "prooffold";
  const isRoot = node.documentId === map.root;
  const isCurrent = node.documentId === map.current;
  const isPrevious = node.documentId === map.previous && !overview;
  const isUnresolved = node.unresolved === true;

  const button = document.createElement("button");
  button.type = "button";
  button.className = "map-node";
  button.classList.toggle("is-root", isRoot);
  button.classList.toggle("is-current", isCurrent);
  button.classList.toggle("is-previous", isPrevious);
  button.classList.toggle("is-unresolved", isUnresolved);
  const states = [
    isCurrent && !isFoldMap ? "Current document." : null,
    isPrevious ? "Previous document." : null,
    isUnresolved ? "Unresolved document." : null,
  ].filter(Boolean);
  button.title = [isFoldMap ? node.label : null, isUnresolved ? "Unresolved" : null, isPrevious ? "Previous" : null, node.detail]
    .filter(Boolean)
    .join(" · ");
  button.setAttribute("aria-label", [`${node.label}.`, ...states, node.detail].join(" "));

  const label = document.createElement("strong");
  label.textContent = node.label;
  const detail = document.createElement("span");
  detail.className = "map-node-detail";
  detail.textContent = node.detail;
  const glyph = document.createElement("span");
  glyph.className = "map-node-glyph";
  glyph.setAttribute("aria-hidden", "true");
  glyph.textContent = isUnresolved ? "!" : isRoot ? "◆" : "●";
  button.append(label, detail, glyph);
  button.addEventListener("click", () => {
    closeMap();
    if (isFoldMap) {
      revealFold(node.documentPath.slice(1));
    } else if (!isCurrent) {
      preferredPath = node.documentPath;
      post("open-map-node", { id: node.documentId, position: getPosition() });
    }
  });

  const shell = document.createElement("div");
  shell.className = "map-node-shell";
  shell.classList.toggle("is-current", isCurrent);
  shell.style.width = `${geometry.nodeWidth}px`;
  shell.style.height = `${geometry.nodeHeight}px`;
  shell.style.transform = `translate(${position.x}px, ${position.y}px)`;
  shell.append(button);

  if (!overview && (node.hiddenChildCount > 0 || node.manuallyExpanded)) {
    const expand = document.createElement("button");
    expand.type = "button";
    expand.className = "map-node-expand-button";
    expand.textContent = node.manuallyExpanded ? "−" : `+${node.hiddenChildCount}`;
    expand.setAttribute(
      "aria-label",
      node.manuallyExpanded ? `Collapse ${node.label}` : `Show ${node.hiddenChildCount} hidden children of ${node.label}`,
    );
    expand.addEventListener("click", (event) => {
      event.stopPropagation();
      if (node.manuallyExpanded) expandedKeys.delete(node.occurrenceKey);
      else expandedKeys.add(node.occurrenceKey);
      renderMap();
    });
    shell.append(expand);
  }
  return shell;
}

// Camera -----------------------------------------------------------------------------

const clampZoom = (zoom) => Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, zoom));

function constrainCamera() {
  const width = elements.mapViewport.clientWidth;
  const height = elements.mapViewport.clientHeight;
  if (!rendered || width === 0 || height === 0) return;

  const margin = Math.min(100, width * 0.25, height * 0.25);
  camera.x = Math.min(width - margin, Math.max(margin - rendered.layout.width * camera.zoom, camera.x));
  camera.y = Math.min(height - margin, Math.max(margin - rendered.layout.height * camera.zoom, camera.y));
}

function applyCamera() {
  elements.mapSurface.style.transform = `translate(${camera.x}px, ${camera.y}px) scale(${camera.zoom})`;
  const percentage = Math.round(camera.zoom * 100);
  elements.mapZoomSlider.value = String(percentage);
  elements.mapZoomValue.textContent = `${percentage}%`;
  elements.mapZoomOutButton.disabled = camera.zoom <= MIN_ZOOM;
  elements.mapZoomInButton.disabled = camera.zoom >= MAX_ZOOM;
  elements.mapZoomFitButton.classList.toggle("is-active", overview);
  elements.mapZoomFitButton.setAttribute("aria-pressed", String(overview));
}

function setZoom(nextZoom, anchorX, anchorY) {
  const zoom = clampZoom(nextZoom);
  if (zoom === camera.zoom) return;

  const fixedX = Number.isFinite(anchorX) ? anchorX : elements.mapViewport.clientWidth / 2;
  const fixedY = Number.isFinite(anchorY) ? anchorY : elements.mapViewport.clientHeight / 2;
  camera.x = fixedX - ((fixedX - camera.x) / camera.zoom) * zoom;
  camera.y = fixedY - ((fixedY - camera.y) / camera.zoom) * zoom;
  camera.zoom = zoom;
  camera.initialized = true;

  if (overviewForced && zoom >= OVERVIEW_EXIT_ZOOM) overviewForced = false;
  const nextOverview = overviewForced || (overview ? zoom < OVERVIEW_EXIT_ZOOM : zoom <= OVERVIEW_ENTER_ZOOM);
  // The fold map always shows everything, so only the exploration map changes layout.
  if (nextOverview !== overview) {
    overview = nextOverview;
    renderMap();
    if (activeMap().kind !== "prooffold") window.requestAnimationFrame(centerCurrentNode);
    return;
  }
  constrainCamera();
  applyCamera();
}

function centerCurrentNode() {
  const position = rendered?.layout.positions.get(rendered.currentId);
  if (!position) return;
  camera.x = elements.mapViewport.clientWidth / 2 - (position.x + rendered.geometry.nodeWidth / 2) * camera.zoom;
  camera.y = elements.mapViewport.clientHeight / 2 - (position.y + rendered.geometry.nodeHeight / 2) * camera.zoom;
  camera.initialized = true;
  constrainCamera();
  applyCamera();
}

function fitToViewport() {
  const width = elements.mapViewport.clientWidth;
  const height = elements.mapViewport.clientHeight;
  if (!rendered || width === 0 || height === 0) return;

  camera.zoom = clampZoom(Math.min((width - 64) / rendered.layout.width, (height - 64) / rendered.layout.height));
  camera.x = (width - rendered.layout.width * camera.zoom) / 2;
  camera.y = (height - rendered.layout.height * camera.zoom) / 2;
  camera.initialized = true;
  constrainCamera();
  applyCamera();
}

function toggleOverview() {
  if (overviewForced) {
    overviewForced = false;
    overview = false;
    camera.zoom = 1;
    renderMap();
    centerCurrentNode();
    return;
  }
  overviewForced = true;
  overview = true;
  renderMap();
  fitToViewport();
}

function initializeCamera() {
  if (activeMap().kind === "prooffold") fitToViewport();
  else centerCurrentNode();
}

// Opening and input ------------------------------------------------------------------

export function openMap() {
  if (elements.mapButton.disabled) return;
  elements.mapOverlay.hidden = false;
  elements.mapButton.setAttribute("aria-expanded", "true");
  document.body.classList.add("map-is-open");
  renderMap();
  window.requestAnimationFrame(() => {
    if (!camera.initialized) initializeCamera();
    elements.mapCloseButton.focus({ preventScroll: true });
  });
}

export function closeMap() {
  if (!isOpen()) return;
  if (elements.mapResetDialog.open) elements.mapResetDialog.close();
  endPan();
  const focusWasInside = elements.mapOverlay.contains(document.activeElement);
  elements.mapOverlay.hidden = true;
  elements.mapButton.setAttribute("aria-expanded", "false");
  document.body.classList.remove("map-is-open");
  if (focusWasInside) elements.mapButton.focus({ preventScroll: true });
}

export function toggleMap() {
  if (isOpen()) closeMap();
  else openMap();
}

export function isMapOpen() {
  return isOpen();
}

function endPan(event) {
  if (!panPointer || (event && event.pointerId !== panPointer.id)) return;
  if (elements.mapViewport.hasPointerCapture(panPointer.id)) {
    elements.mapViewport.releasePointerCapture(panPointer.id);
  }
  panPointer = null;
  elements.mapViewport.classList.remove("is-panning");
}

/** Converts a wheel event to a zoom factor, treating line and page scrolling like pixels. */
function wheelZoomFactor(event) {
  const unit = event.deltaMode === WheelEvent.DOM_DELTA_LINE ? 16 : event.deltaMode === WheelEvent.DOM_DELTA_PAGE ? 400 : 1;
  return Math.exp(-event.deltaY * unit * 0.0015);
}

export function initMapView({ currentPosition, revealFoldPath }) {
  getPosition = currentPosition;
  revealFold = revealFoldPath;

  elements.mapButton.addEventListener("click", toggleMap);
  elements.mapCloseButton.addEventListener("click", closeMap);
  elements.mapOverlay.addEventListener("click", (event) => {
    if (event.target === elements.mapOverlay) closeMap();
  });
  elements.mapResetButton.addEventListener("click", () => elements.mapResetDialog.showModal());
  elements.mapResetConfirmButton.addEventListener("click", () => post("reset-map"));
  elements.mapZoomOutButton.addEventListener("click", () => setZoom(camera.zoom - ZOOM_STEP));
  elements.mapZoomInButton.addEventListener("click", () => setZoom(camera.zoom + ZOOM_STEP));
  elements.mapZoomSlider.addEventListener("input", (event) => setZoom(Number(event.target.value) / 100));
  elements.mapZoomFitButton.addEventListener("click", toggleOverview);

  const viewport = elements.mapViewport;
  viewport.addEventListener("pointerdown", (event) => {
    if (event.button !== 0 || event.target.closest(".map-node-shell")) return;
    event.preventDefault();
    panPointer = { id: event.pointerId, x: event.clientX, y: event.clientY };
    viewport.setPointerCapture(event.pointerId);
    viewport.classList.add("is-panning");
  });
  viewport.addEventListener("pointermove", (event) => {
    if (!panPointer || event.pointerId !== panPointer.id) return;
    event.preventDefault();
    camera.x += event.clientX - panPointer.x;
    camera.y += event.clientY - panPointer.y;
    panPointer.x = event.clientX;
    panPointer.y = event.clientY;
    camera.initialized = true;
    constrainCamera();
    applyCamera();
  });
  viewport.addEventListener("pointerup", endPan);
  viewport.addEventListener("pointercancel", endPan);
  viewport.addEventListener("wheel", (event) => {
    event.preventDefault();
    if (event.deltaY === 0) return;
    const bounds = viewport.getBoundingClientRect();
    setZoom(camera.zoom * wheelZoomFactor(event), event.clientX - bounds.left, event.clientY - bounds.top);
  }, { passive: false });
}
