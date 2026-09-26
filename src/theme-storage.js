export function readStoredTheme(storage) {
  const saved = storage.getItem("proofmd-theme");
  if (saved === "light" || saved === "dark") return saved;

  const legacy = storage.getItem("leanmd-theme");
  if (legacy !== "light" && legacy !== "dark") return null;

  try {
    storage.setItem("proofmd-theme", legacy);
  } catch {
    // The existing preference still applies when storage is read-only.
  }
  return legacy;
}
