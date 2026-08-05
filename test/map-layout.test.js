import assert from "node:assert/strict";
import test from "node:test";

import {
  focusExplorationMap,
  layoutExplorationMap,
  PROOF_FOLD_MAP_GEOMETRY,
  routeExplorationMapEdges,
  unfoldExplorationMap,
} from "../src/map-layout.js";

const options = {
  minimumWidth: 0,
  minimumHeight: 0,
  paddingX: 0,
  paddingY: 0,
};

function node(id, order) {
  return { id, order };
}

function edge(from, to, order) {
  return Number.isFinite(order) ? { from, to, order } : { from, to };
}

test("unfolds shared DAG descendants into synchronized document occurrences", () => {
  const nodes = [
    node("root", 0),
    node("A", 1),
    node("B", 2),
    { ...node("C", 3), label: "?", unexplored: true },
    node("D", 4),
  ];
  const edges = [
    edge("root", "A", 0),
    edge("root", "B", 1),
    edge("A", "C", 0),
    edge("B", "C", 0),
    edge("C", "D", 0),
  ];
  const unfolded = unfoldExplorationMap(nodes, edges, "root");

  assert.equal(unfolded.nodes.length, 7);
  assert.equal(unfolded.edges.length, 6);
  assert.equal(
    unfolded.nodes.filter((occurrence) => occurrence.documentId === "C").length,
    2,
  );
  assert.equal(
    unfolded.nodes.filter((occurrence) => occurrence.documentId === "D").length,
    2,
  );
  assert.ok(
    unfolded.nodes
      .filter((occurrence) => occurrence.documentId === "C")
      .every((occurrence) => occurrence.label === "?" && occurrence.unexplored),
  );
  assert.ok(
    unfolded.nodes
      .filter((occurrence) => occurrence.documentId === "C")
      .some(
        (occurrence) =>
          occurrence.documentPath.join("/") === "root/A/C",
      ),
  );

  const incomingCounts = new Map();
  for (const unfoldedEdge of unfolded.edges) {
    incomingCounts.set(
      unfoldedEdge.to,
      (incomingCounts.get(unfoldedEdge.to) ?? 0) + 1,
    );
  }
  assert.ok([...incomingCounts.values()].every((count) => count === 1));
});

test("focuses the detail map on the current path and one child level", () => {
  const nodes = ["root", "A", "B", "C", "D", "E", "F", "G"].map(node);
  const edges = [
    edge("root", "A", 0),
    edge("root", "B", 1),
    edge("A", "C", 0),
    edge("A", "D", 1),
    edge("B", "E", 0),
    edge("C", "F", 0),
    edge("C", "G", 1),
  ];
  const unfolded = unfoldExplorationMap(nodes, edges, "root");
  const focused = focusExplorationMap(unfolded, "C", ["root", "A", "C"]);
  const visibleDocuments = new Set(
    focused.nodes.map((occurrence) => occurrence.documentId),
  );

  assert.deepEqual(visibleDocuments, new Set(["root", "A", "B", "C", "F", "G"]));
  assert.equal(
    focused.nodes.find((occurrence) => occurrence.documentId === "A")
      .hiddenChildCount,
    1,
  );
  assert.equal(
    focused.nodes.find((occurrence) => occurrence.documentId === "B")
      .hiddenChildCount,
    1,
  );

  const branchB = unfolded.nodes.find(
    (occurrence) => occurrence.documentPath.join("/") === "root/B",
  );
  const expanded = focusExplorationMap(
    unfolded,
    "C",
    ["root", "A", "C"],
    new Set([branchB.occurrenceKey]),
  );
  assert.ok(
    expanded.nodes.some((occurrence) => occurrence.documentId === "E"),
  );
});

test("uses the preferred duplicate occurrence as the active detail path", () => {
  const nodes = ["root", "A", "B", "C"].map(node);
  const edges = [
    edge("root", "A", 0),
    edge("root", "B", 1),
    edge("A", "C", 0),
    edge("B", "C", 0),
  ];
  const unfolded = unfoldExplorationMap(nodes, edges, "root");
  const focused = focusExplorationMap(
    unfolded,
    "C",
    ["root", "B", "C"],
  );
  const currentOccurrence = focused.nodes.find(
    (occurrence) => occurrence.id === focused.currentOccurrenceId,
  );

  assert.deepEqual(currentOccurrence.documentPath, ["root", "B", "C"]);
  assert.equal(
    focused.nodes.filter((occurrence) => occurrence.documentId === "C").length,
    1,
  );
});

test("uses source-link order instead of discovery order for siblings", () => {
  const nodes = [node("root", 0), node("lower", 1), node("upper", 2)];
  const edges = [edge("root", "lower", 1), edge("root", "upper", 0)];
  const layout = layoutExplorationMap(nodes, edges, "root", options);

  assert.ok(layout.positions.get("upper").y < layout.positions.get("lower").y);
});

test("keeps source order when a link is inserted between revealed siblings", () => {
  const nodes = [
    node("root", 0),
    node("lower", 1),
    node("upper", 2),
    node("middle", 3),
  ];
  const edges = [
    edge("root", "lower", 2),
    edge("root", "upper", 0),
    edge("root", "middle", 1),
  ];
  const layout = layoutExplorationMap(nodes, edges, "root", options);

  assert.ok(layout.positions.get("upper").y < layout.positions.get("middle").y);
  assert.ok(layout.positions.get("middle").y < layout.positions.get("lower").y);
});

test("keeps sibling subtrees in order when a middle branch grows", () => {
  const originalNodes = [
    node("A", 0),
    node("B", 1),
    node("C", 2),
    node("D", 3),
    ...[1, 2, 3, 4].map((index) => node(`B${index}`, 3 + index)),
    ...[1, 2, 3, 4, 5].map((index) => node(`D${index}`, 7 + index)),
  ];
  const originalEdges = [
    edge("A", "B"),
    edge("A", "C"),
    edge("A", "D"),
    ...[1, 2, 3, 4].map((index) => edge("B", `B${index}`)),
    ...[1, 2, 3, 4, 5].map((index) => edge("D", `D${index}`)),
  ];
  const before = layoutExplorationMap(originalNodes, originalEdges, "A", options);
  const expandedNodes = [
    ...originalNodes,
    node("C1", 13),
    node("C2", 14),
    node("C3", 15),
  ];
  const expandedEdges = [
    ...originalEdges,
    edge("C", "C1"),
    edge("C", "C2"),
    edge("C", "C3"),
  ];
  const after = layoutExplorationMap(expandedNodes, expandedEdges, "A", options);

  assert.ok(after.positions.get("B4").y < after.positions.get("C1").y);
  assert.ok(after.positions.get("C3").y < after.positions.get("D1").y);
  assert.ok(after.positions.get("B").y < after.positions.get("C").y);
  assert.ok(after.positions.get("C").y < after.positions.get("D").y);
  assert.equal(after.positions.get("B1").y, before.positions.get("B1").y);
  assert.ok(after.positions.get("D1").y > before.positions.get("D1").y);
  assert.notEqual(after.positions.get("A").y, before.positions.get("A").y);
});

test("assigns ordered, separate ports to child arrows", () => {
  const nodes = [node("A", 0), node("B", 1), node("C", 2), node("D", 3)];
  const edges = [edge("A", "B"), edge("A", "C"), edge("A", "D")];
  const layout = layoutExplorationMap(nodes, edges, "A", options);
  const routes = routeExplorationMapEdges(edges, layout);

  assert.deepEqual(
    routes.map((route) => route.sourceY),
    [...routes.map((route) => route.sourceY)].sort((a, b) => a - b),
  );
  assert.equal(new Set(routes.map((route) => route.sourceY)).size, routes.length);
  assert.deepEqual(
    routes.map((route) => route.targetY),
    [...routes.map((route) => route.targetY)].sort((a, b) => a - b),
  );
});

test("keeps the ProofFold tree compact across and spacious down the page", () => {
  const nodes = [
    node("root", 0),
    node("left", 1),
    node("middle", 2),
    node("right", 3),
    ...[1, 2, 3, 4].map((index) => node(`deep-${index}`, 3 + index)),
  ];
  const edges = [
    edge("root", "left", 0),
    edge("root", "middle", 1),
    edge("root", "right", 2),
    edge("middle", "deep-1"),
    edge("deep-1", "deep-2"),
    edge("deep-2", "deep-3"),
    edge("deep-3", "deep-4"),
  ];
  const layout = layoutExplorationMap(
    nodes,
    edges,
    "root",
    PROOF_FOLD_MAP_GEOMETRY,
  );

  assert.equal(
    layout.positions.get("middle").x - layout.positions.get("left").x,
    PROOF_FOLD_MAP_GEOMETRY.horizontalStep,
  );
  assert.equal(
    layout.positions.get("deep-4").y - layout.positions.get("deep-3").y,
    PROOF_FOLD_MAP_GEOMETRY.verticalStep,
  );
  assert.ok(layout.height > layout.width);
});

test("routes ProofFold edges as shared tree branches without arrow geometry", () => {
  const nodes = [node("root", 0), node("left", 1), node("right", 2)];
  const edges = [edge("root", "left", 0), edge("root", "right", 1)];
  const layout = layoutExplorationMap(
    nodes,
    edges,
    "root",
    PROOF_FOLD_MAP_GEOMETRY,
  );
  const routes = routeExplorationMapEdges(
    edges,
    layout,
    PROOF_FOLD_MAP_GEOMETRY,
  );

  assert.equal(new Set(routes.map((route) => route.sourceX)).size, 1);
  assert.ok(routes.every((route) => /^M .* V .* H .* V .*$/u.test(route.path)));
  assert.ok(routes.every((route) => !route.path.includes(" C ")));
});
