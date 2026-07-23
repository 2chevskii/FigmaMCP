import {
  asApiRecord,
  asRecord,
  base64ToBytes,
  bytesToBase64,
  idempotentMutation,
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

const MAX_BINARY_BYTES = 12 * 1024 * 1024;

export const assetEditorHandlers: Record<string, RpcHandler> = {
  create_figma_image: (payload) =>
    idempotentMutation("create_figma_image", payload, () => createImage(payload)),
  get_figma_image: getImage,
  create_figma_media: (payload) =>
    idempotentMutation("create_figma_media", payload, () => createMedia(payload)),
  list_figma_shaders: listShaders,
  load_figma_brushes: loadBrushes,
  export_figma_nodes: exportNodes,
  encode_figma_binary: encodeBinary,
  update_figma_prototype: (payload) =>
    idempotentMutation("update_figma_prototype", payload, () => updatePrototype(payload)),
  get_figma_viewport: getViewport,
  set_figma_viewport: (payload) =>
    idempotentMutation("set_figma_viewport", payload, () => setViewport(payload)),
  notify_figma_user: notifyUser,
  commit_figma_undo: commitUndo,
  save_figma_version: (payload) =>
    idempotentMutation("save_figma_version", payload, () => saveVersion(payload)),
  get_figma_file_thumbnail_node: getFileThumbnailNode,
  set_figma_file_thumbnail_node: (payload) =>
    idempotentMutation("set_figma_file_thumbnail_node", payload, () =>
      setFileThumbnailNode(payload),
    ),
};

async function createImage(payload: RpcPayload): Promise<RpcResult> {
  if (optionalBoolean(payload, "dry_run") ?? false) {
    return { dry_run: true, source: payload.url ? "url" : "base64" };
  }
  const url = optionalString(payload, "url");
  let image: Image;
  if (url) {
    validateAssetUrl(url);
    image = await figma.createImageAsync(url);
  } else {
    const bytes = boundedBytes(requiredString(payload, "data_base64", { maxLength: 17_000_000 }));
    image = figma.createImage(bytes);
  }
  return {
    hash: image.hash,
    size: await image.getSizeAsync(),
  };
}

async function getImage(payload: RpcPayload): Promise<RpcResult> {
  const hash = requiredString(payload, "hash");
  const image = figma.getImageByHash(hash);
  if (!image) {
    throw new OperationError("image_not_found", `No image exists for hash ${hash}.`);
  }
  const bytes = await image.getBytesAsync();
  ensureBinaryLimit(bytes);
  return {
    hash,
    size: await image.getSizeAsync(),
    byte_length: bytes.length,
    mime_type: detectImageMime(bytes),
    data_base64: bytesToBase64(bytes),
  };
}

async function createMedia(payload: RpcPayload): Promise<RpcResult> {
  const kind = requiredString(payload, "kind");
  if (kind !== "video") {
    throw new OperationError(
      "unsupported_in_editor",
      `${kind} media creation is not available in Figma Design.`,
    );
  }
  if (optionalBoolean(payload, "dry_run") ?? false) {
    return { dry_run: true, kind };
  }
  const bytes = boundedBytes(requiredString(payload, "data_base64", { maxLength: 17_000_000 }));
  const video = await figma.createVideoAsync(bytes);
  return { kind, hash: video.hash };
}

async function listShaders(payload: RpcPayload): Promise<RpcResult> {
  const importId = optionalString(payload, "import_id");
  if (importId) {
    return { shader: toSerializable(await figma.importShaderById(importId)) };
  }
  return { shaders: toSerializable(await figma.listAvailableShaders()) };
}

async function loadBrushes(payload: RpcPayload): Promise<RpcResult> {
  const brushType = requiredString(payload, "brush_type");
  if (brushType !== "STRETCH" && brushType !== "SCATTER") {
    throw new OperationError("invalid_argument", "brush_type must be STRETCH or SCATTER.");
  }
  await figma.loadBrushesAsync(brushType);
  return { loaded: true, brush_type: brushType };
}

async function exportNodes(payload: RpcPayload): Promise<RpcResult> {
  const nodeIds = stringArray(payload, "node_ids", { required: true, maxItems: 20 });
  const settings = payload.settings ? asRecord(payload.settings, "settings") : undefined;
  const exported: RpcResult[] = [];
  for (const nodeId of nodeIds) {
    const node = await requireSceneNode(nodeId);
    const result = await node.exportAsync(settings as ExportSettings | undefined);
    if (result instanceof Uint8Array) {
      ensureBinaryLimit(result);
      exported.push({
        node_id: node.id,
        byte_length: result.length,
        data_base64: bytesToBase64(result),
      });
    } else {
      exported.push({ node_id: node.id, data: toSerializable(result) });
    }
  }
  return { exports: exported };
}

function encodeBinary(payload: RpcPayload): RpcResult {
  const operation = optionalString(payload, "operation") ?? "inspect";
  const bytes = boundedBytes(requiredString(payload, "data_base64", { maxLength: 17_000_000 }));
  if (operation !== "inspect" && operation !== "normalize_base64") {
    throw new OperationError("invalid_argument", `Unknown binary operation ${operation}.`);
  }
  return {
    byte_length: bytes.length,
    data_base64: operation === "normalize_base64" ? bytesToBase64(bytes) : undefined,
  };
}

async function updatePrototype(payload: RpcPayload): Promise<RpcResult> {
  const items = objectArray(payload, "items", { required: true, maxItems: 100 });
  if (optionalBoolean(payload, "dry_run") ?? false) {
    return { dry_run: true, would_update: items };
  }
  const allowed: Record<string, string> = {
    reactions: "reactions",
    flow_starting_points: "flowStartingPoints",
    overflow_direction: "overflowDirection",
    overlay_background: "overlayBackground",
    overlay_background_interaction: "overlayBackgroundInteraction",
    overlay_position_type: "overlayPositionType",
  };
  const changed: RpcResult[] = [];
  for (const item of items) {
    const node = await requireNode(requiredString(item, "node_id"));
    const api = asApiRecord(node);
    const properties = asRecord(item.properties, "properties");
    for (const [input, value] of Object.entries(properties)) {
      const property = allowed[input];
      if (!property) {
        throw new OperationError(
          "invalid_argument",
          `Prototype property ${input} is not writable.`,
        );
      }
      if (!(property in api)) {
        throw new OperationError(
          "invalid_node_type",
          `Prototype property ${input} is unavailable on ${node.type}.`,
        );
      }
      api[property] = value;
    }
    changed.push(nodeSummary(node));
  }
  return { changed };
}

function getViewport(): RpcResult {
  return {
    center: figma.viewport.center,
    zoom: figma.viewport.zoom,
    bounds: figma.viewport.bounds,
  };
}

async function setViewport(payload: RpcPayload): Promise<RpcResult> {
  const nodeIds = stringArray(payload, "node_ids", { maxItems: 100 });
  if (nodeIds.length > 0) {
    const nodes = await Promise.all(nodeIds.map(requireSceneNode));
    figma.viewport.scrollAndZoomIntoView(nodes);
  } else {
    const center = payload.center ? asRecord(payload.center, "center") : undefined;
    if (center) {
      figma.viewport.center = {
        x: optionalNumber(center, "x") ?? figma.viewport.center.x,
        y: optionalNumber(center, "y") ?? figma.viewport.center.y,
      };
    }
    const zoom = optionalNumber(payload, "zoom", { min: 0.01, max: 256 });
    if (zoom !== undefined) {
      figma.viewport.zoom = zoom;
    }
  }
  return getViewport();
}

function notifyUser(payload: RpcPayload): RpcResult {
  const message = requiredString(payload, "message", { maxLength: 500 });
  const timeout = optionalNumber(payload, "timeout_ms", {
    integer: true,
    min: 500,
    max: 10_000,
  });
  figma.notify(message, {
    timeout,
    error: optionalBoolean(payload, "error"),
  });
  return { shown: true };
}

function commitUndo(payload: RpcPayload): RpcResult {
  const operation = optionalString(payload, "operation") ?? "commit";
  if (operation === "commit") {
    figma.commitUndo();
  } else if (operation === "undo") {
    figma.triggerUndo();
  } else {
    throw new OperationError("invalid_argument", "operation must be commit or undo.");
  }
  return { operation };
}

async function saveVersion(payload: RpcPayload): Promise<RpcResult> {
  const result = await figma.saveVersionHistoryAsync(
    requiredString(payload, "title", { maxLength: 200 }),
    optionalString(payload, "description", { maxLength: 2_000 }),
  );
  return { version: toSerializable(result) };
}

async function getFileThumbnailNode(): Promise<RpcResult> {
  const node = await figma.getFileThumbnailNodeAsync();
  return { node: node ? nodeSummary(node) : null };
}

async function setFileThumbnailNode(payload: RpcPayload): Promise<RpcResult> {
  const nodeId = optionalString(payload, "node_id");
  const candidate = nodeId ? await requireSceneNode(nodeId) : null;
  if (
    candidate &&
    candidate.type !== "FRAME" &&
    candidate.type !== "COMPONENT" &&
    candidate.type !== "COMPONENT_SET" &&
    candidate.type !== "SECTION"
  ) {
    throw new OperationError(
      "invalid_node_type",
      "The file thumbnail must be a frame, component, component set, or section.",
    );
  }
  const node = candidate as FrameNode | ComponentNode | ComponentSetNode | SectionNode | null;
  await figma.setFileThumbnailNodeAsync(node);
  return { node: node ? nodeSummary(node) : null };
}

function boundedBytes(base64: string): Uint8Array {
  const bytes = base64ToBytes(base64);
  ensureBinaryLimit(bytes);
  return bytes;
}

function ensureBinaryLimit(bytes: Uint8Array): void {
  if (bytes.length > MAX_BINARY_BYTES) {
    throw new OperationError(
      "payload_too_large",
      `Binary payloads are limited to ${MAX_BINARY_BYTES} bytes.`,
    );
  }
}

function validateAssetUrl(value: string): void {
  let url: URL;
  try {
    url = new URL(value);
  } catch {
    throw new OperationError("invalid_argument", "url must be an absolute HTTP(S) URL.");
  }
  if (url.protocol !== "https:" && url.protocol !== "http:") {
    throw new OperationError("invalid_argument", "url must use HTTP or HTTPS.");
  }
  const host = url.hostname.toLocaleLowerCase();
  if (
    host === "localhost" ||
    host === "127.0.0.1" ||
    host === "::1" ||
    host.startsWith("10.") ||
    host.startsWith("192.168.") ||
    /^172\.(?:1[6-9]|2\d|3[01])\./.test(host) ||
    host.endsWith(".local")
  ) {
    throw new OperationError("url_not_allowed", "Private-network asset URLs are not allowed.");
  }
}

function detectImageMime(bytes: Uint8Array): string {
  if (bytes[0] === 0x89 && bytes[1] === 0x50) {
    return "image/png";
  }
  if (bytes[0] === 0xff && bytes[1] === 0xd8) {
    return "image/jpeg";
  }
  if (bytes[0] === 0x47 && bytes[1] === 0x49) {
    return "image/gif";
  }
  return "application/octet-stream";
}
