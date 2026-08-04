import {
  existsSync,
  mkdirSync,
  readFileSync,
  readdirSync,
  writeFileSync,
} from "node:fs";
import path from "node:path";

const documentSetArgument = process.argv
  .slice(2)
  .find((argument) => !argument.startsWith("--"));
if (!documentSetArgument) {
  throw new Error(
    "Usage: node DocumentFormats/LeanMD/validate-why-dag.js <document-set-directory> [--write]",
  );
}

const documentSetDirectory = path.resolve(process.cwd(), documentSetArgument);
const rootDocument = "root.md";
const rootDocumentPath = path.join(documentSetDirectory, rootDocument);
const nodesDirectory = path.join(documentSetDirectory, "nodes");
const metadataDirectory = path.join(documentSetDirectory, ".leanmd");
const dependencyPath = path.join(metadataDirectory, "dependencies.json");
const shouldWrite = process.argv.includes("--write");
const maximumPortableDocumentPathLength = 180;
const nodeFilenamePattern = /^[a-z0-9][a-z0-9_-]*\.(?:md|markdown)$/u;

function fail(message) {
  throw new Error(`Invalid LeanMD why DAG: ${message}`);
}

function readNormalizedText(filePath) {
  return readFileSync(filePath, "utf8").replace(/\r\n?/gu, "\n");
}

function normalizedRelativePath(value) {
  return value.replaceAll("\\", "/");
}

if (!existsSync(nodesDirectory)) {
  fail(`nodes directory is missing: ${nodesDirectory}`);
}
if (!existsSync(rootDocumentPath)) {
  fail(`root document is missing: ${rootDocumentPath}`);
}

const nodeEntries = readdirSync(nodesDirectory, { withFileTypes: true });
const nestedDirectories = nodeEntries
  .filter((entry) => entry.isDirectory())
  .map((entry) => entry.name);
if (nestedDirectories.length > 0) {
  fail(`nodes must be flat; nested directories found: ${nestedDirectories.join(", ")}`);
}

const unexpectedFiles = nodeEntries
  .filter(
    (entry) =>
      entry.isFile() &&
      !nodeFilenamePattern.test(entry.name) &&
      !entry.name.endsWith(".unresolved"),
  )
  .map((entry) => entry.name);
if (unexpectedFiles.length > 0) {
  fail(`unexpected files in nodes: ${unexpectedFiles.join(", ")}`);
}

const documents = new Set([
  rootDocument,
  ...nodeEntries
    .filter((entry) => entry.isFile() && nodeFilenamePattern.test(entry.name))
    .map((entry) => `nodes/${entry.name}`)
    .sort(),
]);
if (documents.size === 1) {
  fail("expected at least one Markdown document directly under nodes");
}

for (const document of documents) {
  if (document.length > maximumPortableDocumentPathLength) {
    fail(
      `document path exceeds the ${maximumPortableDocumentPathLength}-character portability budget: ${document}`,
    );
  }
}

const unresolvedMarkers = nodeEntries
  .filter((entry) => entry.isFile() && entry.name.endsWith(".unresolved"))
  .map((entry) => entry.name);
for (const marker of unresolvedMarkers) {
  const basename = marker.slice(0, -".unresolved".length);
  if (!documents.has(`nodes/${basename}.md`) && !documents.has(`nodes/${basename}.markdown`)) {
    fail(`orphan unresolved marker: nodes/${marker}`);
  }
}

function markdownLinks(sourceDocument) {
  const sourcePath = path.join(documentSetDirectory, sourceDocument);
  const source = readFileSync(sourcePath, "utf8");
  const links = [];
  const linkPattern =
    /\]\(([^\s)#?]+\.(?:md|markdown))(?:[?#][^\s)]*)?(?:\s+(?:"([^"]*)"|'([^']*)'))?\)/giu;

  for (const match of source.matchAll(linkPattern)) {
    let decodedTarget;
    try {
      decodedTarget = decodeURIComponent(match[1]);
    } catch {
      fail(`invalid percent-encoded link in ${sourceDocument}: ${match[1]}`);
    }
    const absoluteTarget = path.resolve(path.dirname(sourcePath), decodedTarget);
    links.push({
      source: sourceDocument,
      target: normalizedRelativePath(path.relative(documentSetDirectory, absoluteTarget)),
      role: (match[2] ?? match[3] ?? "").trim().toLowerCase(),
    });
  }

  return links;
}

const allLinks = [...documents].flatMap(markdownLinks);
const whyOrderByDocument = new Map();
const whyLinks = allLinks
  .filter(({ role }) => role === "why")
  .map((link) => {
    const order = whyOrderByDocument.get(link.source) ?? 0;
    whyOrderByDocument.set(link.source, order + 1);
    return { ...link, order };
  });

for (const link of whyLinks) {
  if (!documents.has(link.target)) {
    fail(`why link leaves the flat node set: ${link.source} -> ${link.target}`);
  }
}

const outgoing = new Map([...documents].map((document) => [document, []]));
const incomingCount = new Map([...documents].map((document) => [document, 0]));
const whyEdgeKeys = new Set();
const whyEdges = [];
for (const link of whyLinks) {
  if (link.source === link.target) {
    fail(`self-dependency is not allowed: ${link.source}`);
  }

  const key = `${link.source}\u0000${link.target}`;
  if (whyEdgeKeys.has(key)) continue;
  whyEdgeKeys.add(key);
  whyEdges.push({
    from: link.source,
    to: link.target,
    kind: "why",
    order: link.order,
  });
  outgoing.get(link.source).push(link.target);
  incomingCount.set(link.target, incomingCount.get(link.target) + 1);
}

const state = new Map([...documents].map((document) => [document, "unvisited"]));
const activePath = [];
function checkForCycles(document) {
  state.set(document, "active");
  activePath.push(document);

  for (const target of outgoing.get(document)) {
    if (state.get(target) === "active") {
      const cycleStart = activePath.indexOf(target);
      fail(`cycle found: ${[...activePath.slice(cycleStart), target].join(" -> ")}`);
    }
    if (state.get(target) === "unvisited") checkForCycles(target);
  }

  activePath.pop();
  state.set(document, "complete");
}

for (const document of documents) {
  if (state.get(document) === "unvisited") checkForCycles(document);
}

const roots = [...incomingCount]
  .filter(([, count]) => count === 0)
  .map(([document]) => document);
if (roots.length !== 1) {
  fail(`expected one root, found ${roots.length}: ${roots.join(", ")}`);
}
const [root] = roots;
if (root !== rootDocument) {
  fail(`root.md must be the single DAG root, found ${root}`);
}

const reachable = new Set();
const depth = new Map([[root, 0]]);
function visit(document) {
  if (reachable.has(document)) return;
  reachable.add(document);
  for (const target of outgoing.get(document)) {
    depth.set(target, Math.min(depth.get(target) ?? Infinity, depth.get(document) + 1));
    visit(target);
  }
}
visit(root);

const unreachable = [...documents].filter((document) => !reachable.has(document));
if (unreachable.length > 0) {
  fail(`documents are unreachable from ${root}: ${unreachable.join(", ")}`);
}

whyEdges.sort(
  (left, right) =>
    depth.get(left.from) - depth.get(right.from) ||
    left.from.localeCompare(right.from) ||
    left.order - right.order ||
    left.to.localeCompare(right.to),
);

const generated = `${JSON.stringify(
  {
    formatVersion: 2,
    layout: "flat",
    root,
    edges: whyEdges,
  },
  null,
  2,
)}\n`;

if (shouldWrite) {
  mkdirSync(metadataDirectory, { recursive: true });
  if (!existsSync(dependencyPath) || readNormalizedText(dependencyPath) !== generated) {
    writeFileSync(dependencyPath, generated, "utf8");
    console.log("Updated LeanMD dependencies.json from Markdown why links.");
  } else {
    console.log("LeanMD dependencies.json is already up to date.");
  }
} else if (!existsSync(dependencyPath)) {
  fail("dependencies.json is missing; run with --write to generate it");
} else if (readNormalizedText(dependencyPath) !== generated) {
  fail("dependencies.json is stale; run with --write to regenerate it");
}

const sharedDocuments = [...incomingCount]
  .filter(([, count]) => count > 1)
  .map(([document, count]) => `${document} (${count} incoming why edges)`);

console.log(
  `Valid LeanMD DAG: ${documents.size} flat documents, ${whyEdges.length} why edges, root ${root}.`,
);
console.log(
  sharedDocuments.length > 0
    ? `Shared documents: ${sharedDocuments.join(", ")}.`
    : "Shared documents: none.",
);
