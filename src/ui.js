/** The desktop host. The viewer only ever runs inside ProofMD's WebView2. */
export const host = window.chrome.webview;

const byId = (id) => document.getElementById(id);

export const elements = {
  brandName: byId("brandName"),
  documentName: byId("documentName"),
  dropOverlay: byId("dropOverlay"),
  emptyState: byId("emptyState"),
  keyGuideButton: byId("keyGuideButton"),
  keyGuideCloseButton: byId("keyGuideCloseButton"),
  keyGuidePanel: byId("keyGuidePanel"),
  mapButton: byId("mapButton"),
  mapCloseButton: byId("mapCloseButton"),
  mapCount: byId("mapCount"),
  mapEdgeLayer: byId("mapEdgeLayer"),
  mapEdges: byId("mapEdges"),
  mapEyebrow: byId("mapEyebrow"),
  mapHelpText: byId("mapHelpText"),
  mapNodeLayer: byId("mapNodeLayer"),
  mapOverlay: byId("mapOverlay"),
  mapResetButton: byId("mapResetButton"),
  mapResetConfirmButton: byId("mapResetConfirmButton"),
  mapResetDialog: byId("mapResetDialog"),
  mapSurface: byId("mapSurface"),
  mapTitle: byId("mapTitle"),
  mapViewport: byId("mapViewport"),
  mapZoomFitButton: byId("mapZoomFitButton"),
  mapZoomInButton: byId("mapZoomInButton"),
  mapZoomOutButton: byId("mapZoomOutButton"),
  mapZoomSlider: byId("mapZoomSlider"),
  mapZoomValue: byId("mapZoomValue"),
  openButton: byId("openButton"),
  preview: byId("preview"),
  proofFoldCollapseButton: byId("proofFoldCollapseButton"),
  status: byId("status"),
  themeButton: byId("themeButton"),
  topbar: document.querySelector(".topbar"),
  unresolvedButton: byId("unresolvedButton"),
  viewerLoading: byId("viewerLoading"),
};

export function post(type, fields = {}) {
  host.postMessage({ type, ...fields });
}

/** Speaks a short status through the polite live region. */
export function announce(message) {
  elements.status.textContent = "";
  window.setTimeout(() => {
    elements.status.textContent = message;
  }, 10);
}

/** Resolves after the browser has painted the current state. */
export function waitForPaint() {
  return new Promise((resolve) => {
    window.requestAnimationFrame(() => window.requestAnimationFrame(resolve));
  });
}
