import {
  asApiRecord,
  callApiMethod,
  isSceneNode,
  limit,
  nodeSummary,
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

const DEFAULT_NODE_FIELDS = [
  "id",
  "type",
  "name",
  "removed",
  "parent_id",
  "visible",
  "locked",
  "x",
  "y",
  "width",
  "height",
];

const NODE_FIELDS = new Set([
  ...DEFAULT_NODE_FIELDS,
  "absolute_bounding_box",
  "absolute_render_bounds",
  "absolute_transform",
  "annotations",
  "arc_data",
  "attached_connectors",
  "backgrounds",
  "blend_mode",
  "bound_variables",
  "children",
  "clips_content",
  "component_properties",
  "component_property_definitions",
  "constraints",
  "corner_radius",
  "counter_axis_align_content",
  "counter_axis_align_items",
  "counter_axis_sizing_mode",
  "dash_pattern",
  "description",
  "documentation_links",
  "effects",
  "expanded",
  "export_settings",
  "fills",
  "fill_geometry",
  "fill_style_id",
  "grid_child_horizontal_align",
  "grid_child_vertical_align",
  "grid_column_anchor_index",
  "grid_column_count",
  "grid_column_gap",
  "grid_column_span",
  "grid_columns_sizing",
  "grid_row_anchor_index",
  "grid_row_count",
  "grid_row_gap",
  "grid_row_span",
  "grid_rows_sizing",
  "guides",
  "horizontal_padding",
  "inferred_auto_layout",
  "is_asset",
  "is_mask",
  "item_reverse_z_index",
  "item_spacing",
  "layout_align",
  "layout_grow",
  "layout_grids",
  "layout_mode",
  "layout_positioning",
  "main_axis_align_items",
  "main_axis_sizing_mode",
  "mask_type",
  "max_height",
  "max_width",
  "min_height",
  "min_width",
  "number_of_fixed_children",
  "opacity",
  "overflow_direction",
  "overlay_background",
  "overlay_background_interaction",
  "overlay_position_type",
  "padding_bottom",
  "padding_left",
  "padding_right",
  "padding_top",
  "paragraph_indent",
  "paragraph_spacing",
  "reactions",
  "relative_transform",
  "rotation",
  "strokes",
  "stroke_align",
  "stroke_bottom_weight",
  "stroke_cap",
  "stroke_geometry",
  "stroke_join",
  "stroke_left_weight",
  "stroke_miter_limit",
  "stroke_right_weight",
  "stroke_style_id",
  "stroke_top_weight",
  "stroke_weight",
  "stuck_nodes",
  "text_auto_resize",
  "text_style_id",
  "top_left_radius",
  "top_right_radius",
  "bottom_left_radius",
  "bottom_right_radius",
  "vector_network",
  "vector_paths",
  "vertical_padding",
]);

const FIELD_PROPERTY_NAMES: Record<string, string> = {
  absolute_bounding_box: "absoluteBoundingBox",
  absolute_render_bounds: "absoluteRenderBounds",
  absolute_transform: "absoluteTransform",
  arc_data: "arcData",
  attached_connectors: "attachedConnectors",
  background_color: "backgroundColor",
  blend_mode: "blendMode",
  bound_variables: "boundVariables",
  clips_content: "clipsContent",
  component_properties: "componentProperties",
  component_property_definitions: "componentPropertyDefinitions",
  corner_radius: "cornerRadius",
  counter_axis_align_content: "counterAxisAlignContent",
  counter_axis_align_items: "counterAxisAlignItems",
  counter_axis_sizing_mode: "counterAxisSizingMode",
  dash_pattern: "dashPattern",
  documentation_links: "documentationLinks",
  export_settings: "exportSettings",
  fill_geometry: "fillGeometry",
  fill_style_id: "fillStyleId",
  grid_child_horizontal_align: "gridChildHorizontalAlign",
  grid_child_vertical_align: "gridChildVerticalAlign",
  grid_column_anchor_index: "gridColumnAnchorIndex",
  grid_column_count: "gridColumnCount",
  grid_column_gap: "gridColumnGap",
  grid_column_span: "gridColumnSpan",
  grid_columns_sizing: "gridColumnsSizing",
  grid_row_anchor_index: "gridRowAnchorIndex",
  grid_row_count: "gridRowCount",
  grid_row_gap: "gridRowGap",
  grid_row_span: "gridRowSpan",
  grid_rows_sizing: "gridRowsSizing",
  horizontal_padding: "horizontalPadding",
  inferred_auto_layout: "inferredAutoLayout",
  is_asset: "isAsset",
  is_mask: "isMask",
  item_reverse_z_index: "itemReverseZIndex",
  item_spacing: "itemSpacing",
  layout_align: "layoutAlign",
  layout_grow: "layoutGrow",
  layout_grids: "layoutGrids",
  layout_mode: "layoutMode",
  layout_positioning: "layoutPositioning",
  main_axis_align_items: "mainAxisAlignItems",
  main_axis_sizing_mode: "mainAxisSizingMode",
  mask_type: "maskType",
  max_height: "maxHeight",
  max_width: "maxWidth",
  min_height: "minHeight",
  min_width: "minWidth",
  number_of_fixed_children: "numberOfFixedChildren",
  overflow_direction: "overflowDirection",
  overlay_background: "overlayBackground",
  overlay_background_interaction: "overlayBackgroundInteraction",
  overlay_position_type: "overlayPositionType",
  padding_bottom: "paddingBottom",
  padding_left: "paddingLeft",
  padding_right: "paddingRight",
  padding_top: "paddingTop",
  paragraph_indent: "paragraphIndent",
  paragraph_spacing: "paragraphSpacing",
  relative_transform: "relativeTransform",
  stroke_align: "strokeAlign",
  stroke_bottom_weight: "strokeBottomWeight",
  stroke_cap: "strokeCap",
  stroke_geometry: "strokeGeometry",
  stroke_join: "strokeJoin",
  stroke_left_weight: "strokeLeftWeight",
  stroke_miter_limit: "strokeMiterLimit",
  stroke_right_weight: "strokeRightWeight",
  stroke_style_id: "strokeStyleId",
  stroke_top_weight: "strokeTopWeight",
  stroke_weight: "strokeWeight",
  stuck_nodes: "stuckNodes",
  text_auto_resize: "textAutoResize",
  text_style_id: "textStyleId",
  top_left_radius: "topLeftRadius",
  top_right_radius: "topRightRadius",
  bottom_left_radius: "bottomLeftRadius",
  bottom_right_radius: "bottomRightRadius",
  vector_network: "vectorNetwork",
  vector_paths: "vectorPaths",
  vertical_padding: "verticalPadding",
};

type JournalEntry = {
  cursor: number;
  type: string;
  occurred_at: string;
  detail: unknown;
};

const journal: JournalEntry[] = [];
let nextCursor = 1;

export function startChangeJournal(): void {
  figma.on("documentchange", (event) => {
    appendChange("documentchange", {
      changes: event.documentChanges.map((change) => toSerializable(change)),
    });
  });
  figma.on("selectionchange", () => {
    appendChange("selectionchange", {
      node_ids: figma.currentPage.selection.map((node) => node.id),
    });
  });
  figma.on("currentpagechange", () => {
    appendChange("currentpagechange", {
      page: { id: figma.currentPage.id, name: figma.currentPage.name },
    });
  });
  figma.on("stylechange", (event) => {
    appendChange("stylechange", toSerializable(event));
  });
}

function appendChange(type: string, detail: unknown): void {
  journal.push({
    cursor: nextCursor,
    type,
    occurred_at: new Date().toISOString(),
    detail,
  });
  nextCursor += 1;
  if (journal.length > 500) {
    journal.splice(0, journal.length - 500);
  }
}

export const readHandlers: Record<string, RpcHandler> = {
  get_document_metadata: readDocument,
  get_figma_capabilities: getCapabilities,
  get_figma_document: readDocument,
  list_figma_pages: listPages,
  load_figma_page: loadPage,
  get_figma_selection: getSelection,
  set_figma_selection: setSelection,
  set_figma_current_page: setCurrentPage,
  get_figma_document_changes: getDocumentChanges,
  get_figma_nodes: getNodes,
  query_figma_nodes: queryNodes,
  get_figma_node_css: getNodeCss,
  get_figma_node_geometry: getNodeGeometry,
  get_figma_text: getText,
  get_figma_components: getComponents,
  get_figma_prototype: getPrototype,
  get_figma_plugin_data: getPluginData,
  get_figma_dev_metadata: getDevMetadata,
};

function getCapabilities(): RpcResult {
  const api = asApiRecord(figma);
  return {
    editor_type: figma.editorType,
    mode: figma.mode,
    api_version: "1.0.0",
    document_access: "dynamic-page",
    write_available: true,
    supported_node_types: [
      "BOOLEAN_OPERATION",
      "COMPONENT",
      "COMPONENT_SET",
      "ELLIPSE",
      "FRAME",
      "GROUP",
      "INSTANCE",
      "LINE",
      "PAGE",
      "POLYGON",
      "RECTANGLE",
      "SECTION",
      "SLICE",
      "STAR",
      "TEXT",
      "VECTOR",
    ],
    optional_apis: {
      annotations: typeof api.annotations === "object",
      motion: typeof api.motion === "object",
      team_library: typeof api.teamLibrary === "object",
      variables: typeof api.variables === "object",
      shaders: typeof api.getAvailableShadersAsync === "function",
      brushes: typeof api.loadBrushAsync === "function",
    },
    limits: {
      bridge_message_bytes: 16 * 1024 * 1024,
      node_batch: 100,
      query_results: 500,
      change_journal: 500,
    },
    exclusions: ["figjam", "slides", "buzz", "codegen", "textreview", "payments", "private_api"],
  };
}

async function readDocument(): Promise<RpcResult> {
  return {
    document: {
      id: figma.root.id,
      name: figma.root.name,
      type: figma.root.type,
      color_profile: figma.root.documentColorProfile,
      page_count: figma.root.children.length,
      pages: figma.root.children.map((page) => ({
        id: page.id,
        name: page.name,
        type: page.type,
      })),
    },
    current_page: {
      id: figma.currentPage.id,
      name: figma.currentPage.name,
      top_level_node_count: figma.currentPage.children.length,
    },
    selection: figma.currentPage.selection.map(nodeSummary),
    editor: {
      type: figma.editorType,
      mode: figma.mode,
    },
    file_thumbnail_node_id: (await figma.getFileThumbnailNodeAsync())?.id ?? null,
  };
}

function listPages(payload: RpcPayload): RpcResult {
  const pageLimit = limit(payload, 100, 500);
  const cursor = optionalNumber(payload, "cursor", { integer: true, min: 0 }) ?? 0;
  const pages = figma.root.children.slice(cursor, cursor + pageLimit);
  const next = cursor + pages.length;
  return {
    pages: pages.map((page) => ({
      id: page.id,
      name: page.name,
      type: page.type,
      loaded: page === figma.currentPage || page.children.length > 0,
    })),
    next_cursor: next < figma.root.children.length ? next : null,
  };
}

async function loadPage(payload: RpcPayload): Promise<RpcResult> {
  const pageId = requiredString(payload, "page_id");
  const page = figma.root.children.find((candidate) => candidate.id === pageId);
  if (!page) {
    throw new OperationError("page_not_found", `No page exists for id ${pageId}.`);
  }
  await page.loadAsync();
  return { page: { id: page.id, name: page.name }, loaded: true };
}

function getSelection(): RpcResult {
  const range = figma.currentPage.selectedTextRange;
  return {
    page_id: figma.currentPage.id,
    nodes: figma.currentPage.selection.map(nodeSummary),
    selected_text_range: range ? toSerializable(range) : null,
  };
}

async function setSelection(payload: RpcPayload): Promise<RpcResult> {
  const nodeIds = stringArray(payload, "node_ids", { required: true, maxItems: 100 });
  const nodes = await Promise.all(nodeIds.map(requireSceneNode));
  figma.currentPage.selection = nodes;
  if (optionalBoolean(payload, "focus") ?? false) {
    figma.viewport.scrollAndZoomIntoView(nodes);
  }
  return { selected_node_ids: nodes.map((node) => node.id) };
}

async function setCurrentPage(payload: RpcPayload): Promise<RpcResult> {
  const pageId = requiredString(payload, "page_id");
  const page = figma.root.children.find((candidate) => candidate.id === pageId);
  if (!page) {
    throw new OperationError("page_not_found", `No page exists for id ${pageId}.`);
  }
  await figma.setCurrentPageAsync(page);
  return { page: { id: page.id, name: page.name } };
}

function getDocumentChanges(payload: RpcPayload): RpcResult {
  const afterCursor = optionalNumber(payload, "cursor", { integer: true, min: 0 }) ?? 0;
  const resultLimit = limit(payload, 100, 500);
  const changes = journal.filter((entry) => entry.cursor > afterCursor).slice(0, resultLimit);
  const lastCursor = changes.length > 0 ? changes[changes.length - 1].cursor : afterCursor;
  return {
    changes,
    next_cursor: lastCursor,
    has_more: journal.some((entry) => entry.cursor > lastCursor),
    oldest_available_cursor: journal[0]?.cursor ?? nextCursor,
  };
}

async function getNodes(payload: RpcPayload): Promise<RpcResult> {
  const nodeIds = stringArray(payload, "node_ids", { required: true, maxItems: 100 });
  const fields = requestedFields(payload);
  const childDepth =
    optionalNumber(payload, "child_depth", {
      integer: true,
      min: 0,
      max: 4,
    }) ?? 0;
  const nodes = await Promise.all(nodeIds.map(requireNode));
  return { nodes: nodes.map((node) => projectNode(node, fields, childDepth)) };
}

async function queryNodes(payload: RpcPayload): Promise<RpcResult> {
  const rootId = optionalString(payload, "root_id");
  const root = rootId ? await requireNode(rootId) : figma.currentPage;
  if (!("findAll" in root) || typeof root.findAll !== "function") {
    throw new OperationError("invalid_node_type", `${root.id} cannot contain child nodes.`);
  }

  const nodeTypes = new Set(stringArray(payload, "node_types", { maxItems: 50 }));
  const exactName = optionalString(payload, "name");
  const nameContains = optionalString(payload, "name_contains")?.toLocaleLowerCase();
  const visible = optionalBoolean(payload, "visible");
  const pluginDataKey = optionalString(payload, "plugin_data_key");
  const resultLimit = limit(payload, 100, 500);
  const fields = requestedFields(payload);
  const matches: SceneNode[] = [];

  root.findAll((node) => {
    if (matches.length >= resultLimit) {
      return false;
    }
    if (!isSceneNode(node)) {
      return false;
    }
    const matchesNode =
      (nodeTypes.size === 0 || nodeTypes.has(node.type)) &&
      (exactName === undefined || node.name === exactName) &&
      (nameContains === undefined || node.name.toLocaleLowerCase().includes(nameContains)) &&
      (visible === undefined || node.visible === visible) &&
      (pluginDataKey === undefined || node.getPluginData(pluginDataKey) !== "");
    if (matchesNode) {
      matches.push(node);
    }
    return false;
  });

  return {
    nodes: matches.map((node) => projectNode(node, fields, 0)),
    truncated: matches.length >= resultLimit,
  };
}

async function getNodeCss(payload: RpcPayload): Promise<RpcResult> {
  const nodes = await getSceneNodesFromPayload(payload);
  return {
    nodes: await Promise.all(
      nodes.map(async (node) => ({
        id: node.id,
        css: await callApiMethod(node, "getCSSAsync"),
      })),
    ),
  };
}

async function getNodeGeometry(payload: RpcPayload): Promise<RpcResult> {
  const fields = [
    "absolute_bounding_box",
    "absolute_render_bounds",
    "absolute_transform",
    "relative_transform",
    "x",
    "y",
    "width",
    "height",
    "rotation",
    "constraints",
    "fills",
    "strokes",
    "fill_geometry",
    "stroke_geometry",
    "vector_network",
    "vector_paths",
    "arc_data",
    "corner_radius",
    "top_left_radius",
    "top_right_radius",
    "bottom_left_radius",
    "bottom_right_radius",
    "layout_grids",
    "layout_mode",
    "layout_positioning",
    "layout_align",
    "layout_grow",
    "grid_row_count",
    "grid_column_count",
    "grid_rows_sizing",
    "grid_columns_sizing",
  ];
  const nodes = await getSceneNodesFromPayload(payload);
  return { nodes: nodes.map((node) => projectNode(node, fields, 0)) };
}

async function getText(payload: RpcPayload): Promise<RpcResult> {
  const nodeIds = stringArray(payload, "node_ids", { required: true, maxItems: 100 });
  const segmentFields = stringArray(payload, "segment_fields", { maxItems: 50 });
  const start = optionalNumber(payload, "start", { integer: true, min: 0 });
  const end = optionalNumber(payload, "end", { integer: true, min: 0 });
  const results: RpcResult[] = [];

  for (const nodeId of nodeIds) {
    const node = await requireNode(nodeId);
    if (node.type !== "TEXT") {
      throw new OperationError("invalid_node_type", `${nodeId} is not a text node.`);
    }
    const from = start ?? 0;
    const to = Math.min(end ?? node.characters.length, node.characters.length);
    const result: RpcResult = {
      id: node.id,
      characters: node.characters.slice(from, to),
      range: { start: from, end: to },
      has_missing_font: node.hasMissingFont,
      text_auto_resize: node.textAutoResize,
    };
    if (segmentFields.length > 0) {
      result.segments = toSerializable(node.getStyledTextSegments(segmentFields as never));
    }
    results.push(result);
  }
  return { nodes: results };
}

async function getComponents(payload: RpcPayload): Promise<RpcResult> {
  const nodes = await getSceneNodesFromPayload(payload);
  return {
    nodes: await Promise.all(
      nodes.map(async (node) => {
        const api = asApiRecord(node);
        const result: RpcResult = nodeSummary(node);
        for (const property of [
          "componentProperties",
          "componentPropertyDefinitions",
          "description",
          "documentationLinks",
          "exposedInstances",
          "isExposedInstance",
          "mainComponent",
          "overrides",
          "propertiesExposed",
          "remote",
          "scaleFactor",
          "variantProperties",
        ]) {
          try {
            const value = api[property];
            result[toSnakeCase(property)] = toSerializable(
              value instanceof Promise ? await value : value,
            );
          } catch {
            // A component getter can reject for an unavailable remote object.
          }
        }
        return result;
      }),
    ),
  };
}

async function getPrototype(payload: RpcPayload): Promise<RpcResult> {
  const nodes = await getSceneNodesFromPayload(payload);
  return {
    nodes: nodes.map((node) => {
      const api = asApiRecord(node);
      const result: RpcResult = nodeSummary(node);
      for (const property of [
        "reactions",
        "flowStartingPoints",
        "overflowDirection",
        "overlayBackground",
        "overlayBackgroundInteraction",
        "overlayPositionType",
        "prototypeStartNode",
      ]) {
        if (property in api) {
          result[toSnakeCase(property)] = toSerializable(api[property]);
        }
      }
      return result;
    }),
  };
}

async function getPluginData(payload: RpcPayload): Promise<RpcResult> {
  const nodeIds = stringArray(payload, "node_ids", { required: true, maxItems: 100 });
  const keys = stringArray(payload, "keys", { maxItems: 100 });
  const namespaces = stringArray(payload, "shared_namespaces", { maxItems: 20 });
  const results: RpcResult[] = [];
  for (const nodeId of nodeIds) {
    const node = await requireNode(nodeId);
    const privateKeys = keys.length > 0 ? keys : node.getPluginDataKeys();
    const shared: RpcResult = {};
    for (const namespace of namespaces) {
      const values: RpcResult = {};
      for (const key of node.getSharedPluginDataKeys(namespace)) {
        values[key] = node.getSharedPluginData(namespace, key);
      }
      shared[namespace] = values;
    }
    results.push({
      id: node.id,
      private: Object.fromEntries(privateKeys.map((key) => [key, node.getPluginData(key)])),
      shared,
    });
  }
  return { nodes: results };
}

async function getDevMetadata(payload: RpcPayload): Promise<RpcResult> {
  const nodes = await getSceneNodesFromPayload(payload);
  const results: RpcResult[] = [];
  for (const node of nodes) {
    const api = asApiRecord(node);
    const result: RpcResult = nodeSummary(node);
    for (const property of ["annotations", "devStatus", "measurements"]) {
      if (property in api) {
        result[toSnakeCase(property)] = toSerializable(api[property]);
      }
    }
    for (const method of ["getMeasurements", "getDevResourcesAsync"]) {
      if (typeof api[method] === "function") {
        try {
          result[toSnakeCase(method.replace(/Async$/, ""))] = toSerializable(
            await callApiMethod(node, method),
          );
        } catch {
          // Report available public metadata even if an optional API is denied.
        }
      }
    }
    results.push(result);
  }
  return { nodes: results };
}

function requestedFields(payload: RpcPayload): string[] {
  const fields = stringArray(payload, "fields", { maxItems: 100 });
  const requested = fields.length > 0 ? fields : DEFAULT_NODE_FIELDS;
  const unknown = requested.filter((field) => !NODE_FIELDS.has(field));
  if (unknown.length > 0) {
    throw new OperationError("invalid_argument", `Unsupported node fields: ${unknown.join(", ")}.`);
  }
  return requested;
}

function projectNode(node: BaseNode, fields: string[], childDepth: number): RpcResult {
  const api = asApiRecord(node);
  const result: RpcResult = {};
  for (const field of fields) {
    switch (field) {
      case "id":
      case "type":
      case "name":
      case "removed":
        result[field] = node[field];
        break;
      case "parent_id":
        result.parent_id = node.parent?.id ?? null;
        break;
      case "children":
        if ("children" in node) {
          result.children = node.children.map((child) =>
            childDepth > 0 ? projectNode(child, fields, childDepth - 1) : nodeSummary(child),
          );
        }
        break;
      default: {
        const property = FIELD_PROPERTY_NAMES[field] ?? field;
        if (property in api) {
          try {
            result[field] = toSerializable(api[property]);
          } catch {
            result[field] = null;
          }
        }
      }
    }
  }
  return result;
}

async function getSceneNodesFromPayload(payload: RpcPayload): Promise<SceneNode[]> {
  const nodeIds = stringArray(payload, "node_ids", { required: true, maxItems: 100 });
  return await Promise.all(nodeIds.map(requireSceneNode));
}

function toSnakeCase(value: string): string {
  return value.replace(/[A-Z]/g, (character) => `_${character.toLowerCase()}`);
}
