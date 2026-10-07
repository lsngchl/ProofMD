import "./styles.css";
import "katex/dist/katex.min.css";
import {
  applyUnresolvedState,
  currentDocumentPosition,
  initDocumentView,
  openDocument,
  reloadDocument,
  revealFoldPath,
  showEmptyState,
} from "./document-view.js";
import { closeMap, initMapView, isMapOpen, setExplorationMap, setProofFoldMap, toggleMap } from "./map-view.js";
import { elements, host, post, waitForPaint } from "./ui.js";

const THEME_STORAGE_KEY = "proofmd-theme";

function applyTheme(theme) {
  document.documentElement.dataset.theme = theme;
  elements.themeButton.textContent = theme === "dark" ? "Light" : "Dark";
  elements.themeButton.setAttribute("aria-label", `Switch to ${theme === "dark" ? "light" : "dark"} theme`);
}

// Follows the operating system until the user picks a theme explicitly.
function initialTheme() {
  const saved = localStorage.getItem(THEME_STORAGE_KEY);
  if (saved === "light" || saved === "dark") return saved;
  return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}

function setKeyGuideOpen(open) {
  elements.keyGuidePanel.hidden = !open;
  elements.keyGuideButton.setAttribute("aria-expanded", String(open));
}

function isEditable(target) {
  return target instanceof HTMLElement &&
    (target.isContentEditable || ["INPUT", "TEXTAREA", "SELECT"].includes(target.tagName));
}

function handleKey(event) {
  // Other Ctrl shortcuts (find, copy, print) belong to the WebView.
  if (elements.mapResetDialog.open || (event.ctrlKey && event.key.toLowerCase() !== "o")) return;
  const plain = !event.repeat && !event.ctrlKey && !event.metaKey && !event.altKey;

  if (event.key === "Escape" && !elements.keyGuidePanel.hidden) {
    event.preventDefault();
    setKeyGuideOpen(false);
  } else if (event.key === "Escape" && isMapOpen()) {
    event.preventDefault();
    closeMap();
  } else if (plain && event.key.toLowerCase() === "m" && !isEditable(event.target)) {
    event.preventDefault();
    setKeyGuideOpen(false);
    toggleMap();
  } else if (plain && (event.key === "Backspace" || event.key === ",") &&
      elements.keyGuidePanel.hidden && !isEditable(event.target)) {
    event.preventDefault();
    closeMap();
    post("go-back");
  } else if (event.ctrlKey && event.key.toLowerCase() === "o") {
    event.preventDefault();
    post("open-file-dialog");
  }
}

function setDropOverlay(visible) {
  elements.dropOverlay.classList.toggle("is-visible", visible);
  elements.dropOverlay.setAttribute("aria-hidden", String(!visible));
}

initDocumentView({ onProofFoldMapChange: setProofFoldMap });
initMapView({ currentPosition: currentDocumentPosition, revealFoldPath });
applyTheme(initialTheme());

elements.themeButton.addEventListener("click", () => {
  const next = document.documentElement.dataset.theme === "dark" ? "light" : "dark";
  applyTheme(next);
  localStorage.setItem(THEME_STORAGE_KEY, next);
});
elements.openButton.addEventListener("click", () => post("open-file-dialog"));
elements.keyGuideButton.addEventListener("click", () => setKeyGuideOpen(elements.keyGuidePanel.hidden));
elements.keyGuideCloseButton.addEventListener("click", () => setKeyGuideOpen(false));
document.addEventListener("click", (event) => {
  if (!elements.keyGuidePanel.hidden &&
      !elements.keyGuidePanel.contains(event.target) &&
      !elements.keyGuideButton.contains(event.target)) {
    setKeyGuideOpen(false);
  }
});
document.addEventListener("keydown", handleKey);

for (const eventName of ["dragenter", "dragover"]) {
  window.addEventListener(eventName, (event) => {
    event.preventDefault();
    setDropOverlay(true);
  });
}
window.addEventListener("dragleave", (event) => {
  if (event.relatedTarget === null) setDropOverlay(false);
});
window.addEventListener("drop", (event) => {
  event.preventDefault();
  setDropOverlay(false);
  const file = event.dataTransfer?.files?.[0];
  if (file) host.postMessageWithAdditionalObjects({ type: "open-dropped-file" }, [file]);
});

host.addEventListener("message", async ({ data: message }) => {
  switch (message?.type) {
    case "open-markdown":
      openDocument(message);
      break;
    case "reload-markdown":
      reloadDocument(message);
      break;
    case "show-empty-state":
      showEmptyState();
      break;
    case "map-state":
      setExplorationMap(message);
      break;
    case "document-unresolved-state":
      applyUnresolvedState(message);
      break;
    case "host-window-visible":
      await waitForPaint();
      post("viewer-window-painted");
      break;
  }
});

// The window stays hidden until this first paint, then shows the loading state until the
// host sends a document or asks for the empty state.
waitForPaint().then(() => post("viewer-shell-painted"));
