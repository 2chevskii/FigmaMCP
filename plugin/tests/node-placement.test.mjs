import assert from "node:assert/strict";
import test from "node:test";
import { fileURLToPath } from "node:url";

import { loadTypescriptModule } from "./helpers/load-typescript-module.mjs";

const identity = () => [
  [1, 0, 0],
  [0, 1, 0],
];

const translation = (x, y) => [
  [1, 0, x],
  [0, 1, y],
];

const plain = (value) => JSON.parse(JSON.stringify(value));

function sceneNode({
  id,
  type = "FRAME",
  name = id,
  parent = null,
  relativeTransform = identity(),
  absoluteTransform = relativeTransform,
  width = 10,
  height = 10,
}) {
  let relative = structuredClone(relativeTransform);
  let absolute = structuredClone(absoluteTransform);
  const node = {
    id,
    type,
    name,
    parent,
    removed: false,
    visible: true,
    locked: false,
    width,
    height,
    children: [],
    get x() {
      return relative[0][2];
    },
    set x(value) {
      relative[0][2] = value;
      absolute[0][2] = value;
    },
    get y() {
      return relative[1][2];
    },
    set y(value) {
      relative[1][2] = value;
      absolute[1][2] = value;
    },
    get relativeTransform() {
      return structuredClone(relative);
    },
    set relativeTransform(value) {
      relative = structuredClone(value);
      absolute = structuredClone(value);
    },
    get absoluteTransform() {
      return structuredClone(absolute);
    },
    get absoluteBoundingBox() {
      return { x: absolute[0][2], y: absolute[1][2], width, height };
    },
    appendChild(child) {
      child.parent = node;
      node.children.push(child);
    },
    insertChild(index, child) {
      child.parent = node;
      node.children.splice(index, 0, child);
    },
    remove() {
      node.removed = true;
    },
  };
  return node;
}

test("matrix helpers preserve transforms through inversion", async () => {
  const entryPoint = fileURLToPath(new URL("../src/operations/nodes.ts", import.meta.url));
  const { invertTransform, multiplyTransforms } = await loadTypescriptModule(entryPoint, {
    figma: {},
  });
  const transform = [
    [0, -2, 30],
    [2, 0, 40],
  ];

  assert.deepEqual(plain(multiplyTransforms(invertTransform(transform), transform)), identity());
});

test("anchor-relative cloning targets the anchor inside a group coordinate context", async () => {
  const page = { id: "page", type: "PAGE", name: "Page", children: [] };
  const sourceFrame = sceneNode({
    id: "source-frame",
    parent: page,
    absoluteTransform: translation(100, 50),
  });
  const sourceAvatar = sceneNode({
    id: "source-avatar",
    type: "RECTANGLE",
    parent: sourceFrame,
    relativeTransform: identity(),
    absoluteTransform: translation(100, 50),
    width: 24,
    height: 24,
  });
  const sourceHeart = sceneNode({
    id: "source-heart",
    parent: sourceFrame,
    relativeTransform: translation(1, 1),
    absoluteTransform: translation(101, 51),
    width: 5,
    height: 5,
  });
  const targetFrame = sceneNode({ id: "target-frame", parent: page });
  const targetGroup = sceneNode({ id: "target-group", type: "GROUP", parent: targetFrame });
  const targetAvatar = sceneNode({
    id: "target-avatar",
    type: "RECTANGLE",
    parent: targetGroup,
    relativeTransform: translation(5, 6),
    absoluteTransform: translation(5, 6),
    width: 24,
    height: 24,
  });
  targetFrame.children.push(targetGroup);
  targetGroup.children.push(targetAvatar);

  const nodes = new Map(
    [sourceFrame, sourceAvatar, sourceHeart, targetFrame, targetGroup, targetAvatar].map((node) => [
      node.id,
      node,
    ]),
  );
  sourceHeart.clone = () => {
    const clone = sceneNode({
      id: "clone-heart",
      parent: page,
      relativeTransform: translation(1, 1),
      absoluteTransform: translation(1, 1),
      width: 5,
      height: 5,
    });
    nodes.set(clone.id, clone);
    page.children.push(clone);
    return clone;
  };

  const figma = {
    root: { id: "document", type: "DOCUMENT" },
    currentPage: page,
    getNodeByIdAsync: async (id) => nodes.get(id) ?? null,
  };
  const entryPoint = fileURLToPath(new URL("../src/operations/nodes.ts", import.meta.url));
  const { nodeMutationHandlers } = await loadTypescriptModule(entryPoint, { figma });

  const result = await nodeMutationHandlers.clone_figma_nodes({
    node_ids: ["source-heart"],
    placement: {
      mode: "preserve_relative_transform",
      source_anchor_id: "source-avatar",
      target_anchor_id: "target-avatar",
    },
  });

  assert.equal(result.parent_id, "target-group");
  assert.deepEqual(plain(result.clones[0].clone.relative_transform), translation(6, 7));
  assert.deepEqual(plain(result.clones[0].clone.absolute_transform), translation(6, 7));
  assert.equal(result.clones[0].clone.coordinate_parent_id, "target-frame");

  const moveResult = await nodeMutationHandlers.move_figma_nodes({
    moves: [{ node_id: "clone-heart", parent_id: "target-frame" }],
  });
  assert.equal(moveResult.moved[0].before.parent_id, "target-group");
  assert.equal(moveResult.moved[0].after.parent_id, "target-frame");
  assert.deepEqual(plain(moveResult.moved[0].after.absolute_bounding_box), {
    x: 6,
    y: 7,
    width: 5,
    height: 5,
  });
});
