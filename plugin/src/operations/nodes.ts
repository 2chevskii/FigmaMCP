import {
  asApiRecord,
  asRecord,
  callApiMethod,
  idempotentMutation,
  isSceneNode,
  nodeSummary,
  objectArray,
  OperationError,
  optionalBoolean,
  optionalNumber,
  optionalString,
  requiredString,
  requireNode,
  requireSceneNode,
  RpcHandler,
  RpcPayload,
  RpcResult,
  stringArray,
  toSerializable,
} from "./shared";

const PROPERTY_MAP: Record<string, string> = {
  name: "name",
  visible: "visible",
  locked: "locked",
  x: "x",
  y: "y",
  rotation: "rotation",
  opacity: "opacity",
  blend_mode: "blendMode",
  is_mask: "isMask",
  mask_type: "maskType",
  effects: "effects",
  fills: "fills",
  strokes: "strokes",
  stroke_weight: "strokeWeight",
  stroke_align: "strokeAlign",
  stroke_cap: "strokeCap",
  stroke_join: "strokeJoin",
  stroke_miter_limit: "strokeMiterLimit",
  dash_pattern: "dashPattern",
  corner_radius: "cornerRadius",
  top_left_radius: "topLeftRadius",
  top_right_radius: "topRightRadius",
  bottom_left_radius: "bottomLeftRadius",
  bottom_right_radius: "bottomRightRadius",
  constraints: "constraints",
  layout_grids: "layoutGrids",
  layout_mode: "layoutMode",
  layout_wrap: "layoutWrap",
  primary_axis_sizing_mode: "primaryAxisSizingMode",
  counter_axis_sizing_mode: "counterAxisSizingMode",
  primary_axis_align_items: "primaryAxisAlignItems",
  counter_axis_align_items: "counterAxisAlignItems",
  counter_axis_align_content: "counterAxisAlignContent",
  item_spacing: "itemSpacing",
  counter_axis_spacing: "counterAxisSpacing",
  padding_left: "paddingLeft",
  padding_right: "paddingRight",
  padding_top: "paddingTop",
  padding_bottom: "paddingBottom",
  layout_align: "layoutAlign",
  layout_grow: "layoutGrow",
  layout_positioning: "layoutPositioning",
  min_width: "minWidth",
  max_width: "maxWidth",
  min_height: "minHeight",
  max_height: "maxHeight",
  clips_content: "clipsContent",
  expanded: "expanded",
  export_settings: "exportSettings",
  overflow_direction: "overflowDirection",
  reactions: "reactions",
  overlay_position_type: "overlayPositionType",
  overlay_background: "overlayBackground",
  overlay_background_interaction: "overlayBackgroundInteraction",
  point_count: "pointCount",
  inner_radius: "innerRadius",
  arc_data: "arcData",
  handle_mirroring: "handleMirroring",
  winding_rule: "windingRule",
  grid_row_count: "gridRowCount",
  grid_column_count: "gridColumnCount",
  grid_row_gap: "gridRowGap",
  grid_column_gap: "gridColumnGap",
  grid_rows_sizing: "gridRowsSizing",
  grid_columns_sizing: "gridColumnsSizing",
  grid_row_anchor_index: "gridRowAnchorIndex",
  grid_column_anchor_index: "gridColumnAnchorIndex",
  grid_row_span: "gridRowSpan",
  grid_column_span: "gridColumnSpan",
  grid_child_horizontal_align: "gridChildHorizontalAlign",
  grid_child_vertical_align: "gridChildVerticalAlign",
  section_contents_hidden: "contentsHidden",
  dev_status: "devStatus",
};

export const nodeMutationHandlers: Record<string, RpcHandler> = {
  create_figma_nodes: (payload) =>
    idempotentMutation("create_figma_nodes", payload, () => createNodes(payload)),
  update_figma_nodes: (payload) =>
    idempotentMutation("update_figma_nodes", payload, () => updateNodes(payload)),
  clone_figma_nodes: (payload) =>
    idempotentMutation("clone_figma_nodes", payload, () => cloneNodes(payload)),
  move_figma_nodes: (payload) =>
    idempotentMutation("move_figma_nodes", payload, () => moveNodes(payload)),
  delete_figma_nodes: (payload) =>
    idempotentMutation("delete_figma_nodes", payload, () => deleteNodes(payload)),
  resize_figma_nodes: (payload) =>
    idempotentMutation("resize_figma_nodes", payload, () => resizeNodes(payload)),
  combine_figma_nodes: (payload) =>
    idempotentMutation("combine_figma_nodes", payload, () => combineNodes(payload)),
  set_figma_vector_network: (payload) =>
    idempotentMutation("set_figma_vector_network", payload, () => setVectorNetwork(payload)),
};

async function createNodes(payload: RpcPayload): Promise<RpcResult> {
  const specs = objectArray(payload, "nodes", { required: true, maxItems: 100 });
  const dryRun = optionalBoolean(payload, "dry_run") ?? false;
  if (dryRun) {
    return {
      dry_run: true,
      would_create: specs.map((spec) => ({
        kind: requiredString(spec, "kind"),
        parent_id: optionalString(spec, "parent_id") ?? figma.currentPage.id,
      })),
    };
  }

  const created: RpcResult[] = [];
  for (const spec of specs) {
    const node = await createNode(spec);
    const properties = spec.properties ? asRecord(spec.properties, "properties") : {};
    await applyProperties(node, properties);
    const width = optionalNumber(spec, "width", { min: 0.01, max: 1_000_000 });
    const height = optionalNumber(spec, "height", { min: 0.01, max: 1_000_000 });
    if (width !== undefined || height !== undefined) {
      if (!("resize" in node)) {
        throw new OperationError("invalid_node_type", `${node.type} cannot be resized.`);
      }
      node.resize(width ?? node.width, height ?? node.height);
    }
    const parentId = optionalString(spec, "parent_id");
    if (parentId && isSceneNode(node)) {
      const parent = await requireContainer(parentId);
      appendChild(parent, node);
    }
    created.push(nodeSummary(node));
  }
  return { created };
}

async function createNode(spec: RpcPayload): Promise<BaseNode> {
  const kind = requiredString(spec, "kind").toLowerCase();
  switch (kind) {
    case "rectangle":
      return figma.createRectangle();
    case "line":
      return figma.createLine();
    case "ellipse":
      return figma.createEllipse();
    case "polygon":
      return figma.createPolygon();
    case "star":
      return figma.createStar();
    case "vector":
      return figma.createVector();
    case "text": {
      const node = figma.createText();
      const characters = optionalString(spec, "characters");
      if (characters !== undefined) {
        await loadFontsForNode(node);
        node.characters = characters;
      }
      return node;
    }
    case "frame":
      return figma.createFrame();
    case "component":
      return figma.createComponent();
    case "page":
      return figma.createPage();
    case "page_divider":
      return figma.createPageDivider(optionalString(spec, "divider_name"));
    case "slice":
      return figma.createSlice();
    case "section":
      return figma.createSection();
    case "boolean_operation":
      return figma.createBooleanOperation();
    case "svg":
      return figma.createNodeFromSvg(requiredString(spec, "svg", { maxLength: 1_000_000 }));
    case "text_path": {
      const vector = await requireNode(requiredString(spec, "vector_node_id"));
      if (vector.type !== "VECTOR") {
        throw new OperationError("invalid_node_type", "vector_node_id must refer to a vector.");
      }
      return figma.createTextPath(
        vector,
        optionalNumber(spec, "start_segment", { integer: true, min: 0 }) ?? 0,
        optionalNumber(spec, "start_position", { min: 0, max: 1 }) ?? 0,
      );
    }
    default:
      throw new OperationError(
        "unsupported_in_editor",
        `Node constructor ${kind} is not supported in Figma Design.`,
      );
  }
}

async function updateNodes(payload: RpcPayload): Promise<RpcResult> {
  const updates = objectArray(payload, "updates", { required: true, maxItems: 100 });
  const dryRun = optionalBoolean(payload, "dry_run") ?? false;
  if (dryRun) {
    return {
      dry_run: true,
      would_update: updates.map((update) => ({
        node_id: requiredString(update, "node_id"),
        properties: Object.keys(asRecord(update.properties, "properties")),
      })),
    };
  }

  const changed: RpcResult[] = [];
  for (const update of updates) {
    const node = await requireNode(requiredString(update, "node_id"));
    await applyProperties(node, asRecord(update.properties, "properties"));
    changed.push(nodeSummary(node));
  }
  return { changed };
}

async function applyProperties(node: BaseNode, properties: RpcPayload): Promise<void> {
  const api = asApiRecord(node);
  for (const [inputName, value] of Object.entries(properties)) {
    const property = PROPERTY_MAP[inputName];
    if (!property) {
      throw new OperationError("invalid_argument", `Property ${inputName} is not writable.`);
    }
    if (!(property in api)) {
      throw new OperationError(
        "invalid_node_type",
        `Property ${inputName} is unavailable on ${node.type}.`,
      );
    }
    api[property] = value;
  }
}

async function cloneNodes(payload: RpcPayload): Promise<RpcResult> {
  const nodeIds = stringArray(payload, "node_ids", { required: true, maxItems: 100 });
  if (optionalBoolean(payload, "dry_run") ?? false) {
    return { dry_run: true, would_clone: nodeIds };
  }
  const clones: RpcResult[] = [];
  for (const nodeId of nodeIds) {
    const node = await requireSceneNode(nodeId);
    const clone = node.clone();
    clones.push({ source_id: node.id, clone: nodeSummary(clone) });
  }
  return { clones };
}

async function moveNodes(payload: RpcPayload): Promise<RpcResult> {
  const moves = objectArray(payload, "moves", { required: true, maxItems: 100 });
  if (optionalBoolean(payload, "dry_run") ?? false) {
    return { dry_run: true, would_move: moves };
  }
  const moved: RpcResult[] = [];
  for (const move of moves) {
    const node = await requireNode(requiredString(move, "node_id"));
    if (node.type === "DOCUMENT") {
      throw new OperationError("invalid_node_type", "The document root cannot be moved.");
    }
    const parent = await requireContainer(requiredString(move, "parent_id"));
    const index = optionalNumber(move, "index", { integer: true, min: 0 });
    if (index === undefined) {
      appendChild(parent, node);
    } else {
      const children = asApiRecord(parent).children as readonly BaseNode[];
      insertChild(parent, Math.min(index, children.length), node);
    }
    moved.push(nodeSummary(node));
  }
  return { moved };
}

async function deleteNodes(payload: RpcPayload): Promise<RpcResult> {
  const nodeIds = stringArray(payload, "node_ids", { required: true, maxItems: 100 });
  const nodes = await Promise.all(nodeIds.map(requireNode));
  if (optionalBoolean(payload, "dry_run") ?? false) {
    return { dry_run: true, would_delete: nodes.map(nodeSummary) };
  }
  for (const node of nodes) {
    node.remove();
  }
  return { deleted_node_ids: nodeIds };
}

async function resizeNodes(payload: RpcPayload): Promise<RpcResult> {
  const items = objectArray(payload, "items", { required: true, maxItems: 100 });
  if (optionalBoolean(payload, "dry_run") ?? false) {
    return { dry_run: true, would_resize: items };
  }
  const resized: RpcResult[] = [];
  for (const item of items) {
    const node = await requireSceneNode(requiredString(item, "node_id"));
    const mode = optionalString(item, "mode") ?? "resize";
    if (mode === "rescale") {
      await callApiMethod(node, "rescale", [
        optionalNumber(item, "scale", { min: 0.0001, max: 10_000 }) ?? 1,
      ]);
    } else {
      const width = optionalNumber(item, "width", { min: 0.01, max: 1_000_000 });
      const height = optionalNumber(item, "height", { min: 0.01, max: 1_000_000 });
      if (width === undefined || height === undefined) {
        throw new OperationError("invalid_argument", "width and height are required.");
      }
      await callApiMethod(
        node,
        mode === "resize_without_constraints" ? "resizeWithoutConstraints" : "resize",
        [width, height],
      );
    }
    const lockAspectRatio = optionalBoolean(item, "lock_aspect_ratio");
    if (lockAspectRatio !== undefined && "lockAspectRatio" in asApiRecord(node)) {
      asApiRecord(node).lockAspectRatio = lockAspectRatio;
    }
    resized.push(nodeSummary(node));
  }
  return { resized };
}

async function combineNodes(payload: RpcPayload): Promise<RpcResult> {
  const operation = requiredString(payload, "operation").toLowerCase();
  const nodeIds = stringArray(payload, "node_ids", { required: true, maxItems: 100 });
  const nodes = await Promise.all(nodeIds.map(requireSceneNode));
  if (optionalBoolean(payload, "dry_run") ?? false) {
    return { dry_run: true, operation, node_ids: nodeIds };
  }

  if (operation === "ungroup") {
    const ungrouped = nodes.flatMap((node) =>
      (callSynchronous("ungroup", [node]) as SceneNode[]).map(nodeSummary),
    );
    return { operation, nodes: ungrouped };
  }

  const parent = await requireContainer(
    optionalString(payload, "parent_id") ?? nodes[0]?.parent?.id ?? figma.currentPage.id,
  );
  const index = optionalNumber(payload, "index", { integer: true, min: 0 });
  if (operation === "transform_group") {
    const children = asApiRecord(parent).children as readonly SceneNode[];
    const modifiers = payload.modifiers;
    if (!Array.isArray(modifiers) || modifiers.length !== nodes.length) {
      throw new OperationError(
        "invalid_argument",
        "transform_group requires one modifier per node.",
      );
    }
    const result = callSynchronous("transformGroup", [
      nodes,
      parent,
      Math.min(index ?? children.length, children.length),
      modifiers,
    ]) as BaseNode;
    return { operation, node: nodeSummary(result) };
  }
  const args: unknown[] = [nodes, parent];
  if (index !== undefined && operation === "group") {
    args.push(index);
  }

  const method: Record<string, string> = {
    group: "group",
    flatten: "flatten",
    combine_as_variants: "combineAsVariants",
    union: "union",
    subtract: "subtract",
    intersect: "intersect",
    exclude: "exclude",
  };
  const methodName = method[operation];
  if (!methodName) {
    throw new OperationError("invalid_argument", `Unknown combine operation ${operation}.`);
  }
  const result = callSynchronous(methodName, args) as BaseNode;
  return { operation, node: nodeSummary(result) };
}

async function setVectorNetwork(payload: RpcPayload): Promise<RpcResult> {
  const node = await requireNode(requiredString(payload, "node_id"));
  if (node.type !== "VECTOR") {
    throw new OperationError("invalid_node_type", "node_id must refer to a vector.");
  }
  if (optionalBoolean(payload, "dry_run") ?? false) {
    return { dry_run: true, node_id: node.id };
  }
  if (payload.vector_network !== undefined) {
    await node.setVectorNetworkAsync(payload.vector_network as VectorNetwork);
  }
  if (payload.vector_paths !== undefined) {
    node.vectorPaths = payload.vector_paths as VectorPaths;
  }
  return {
    node: nodeSummary(node),
    vector_network: toSerializable(node.vectorNetwork),
    vector_paths: toSerializable(node.vectorPaths),
  };
}

async function requireContainer(nodeId: string): Promise<BaseNode> {
  if (nodeId === figma.root.id) {
    return figma.root;
  }
  const node = await requireNode(nodeId);
  if (!("children" in node) || !("appendChild" in node)) {
    throw new OperationError("invalid_node_type", `${nodeId} cannot contain child nodes.`);
  }
  return node;
}

function appendChild(parent: BaseNode, node: BaseNode): void {
  const method = asApiRecord(parent).appendChild;
  if (typeof method !== "function") {
    throw new OperationError("invalid_node_type", `${parent.id} cannot contain child nodes.`);
  }
  Reflect.apply(method, parent, [node]);
}

function insertChild(parent: BaseNode, index: number, node: BaseNode): void {
  const method = asApiRecord(parent).insertChild;
  if (typeof method !== "function") {
    throw new OperationError("invalid_node_type", `${parent.id} cannot contain child nodes.`);
  }
  Reflect.apply(method, parent, [index, node]);
}

async function loadFontsForNode(node: TextNode | TextPathNode): Promise<void> {
  const fontName = node.fontName;
  if (fontName !== figma.mixed) {
    await figma.loadFontAsync(fontName);
  }
}

function callSynchronous(method: string, args: unknown[]): unknown {
  const candidate = asApiRecord(figma)[method];
  if (typeof candidate !== "function") {
    throw new OperationError("unsupported_in_editor", `${method} is unavailable.`);
  }
  return Reflect.apply(candidate, figma, args);
}
