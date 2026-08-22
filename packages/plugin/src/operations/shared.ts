export type RpcPayload = Record<string, unknown>;
export type RpcResult = Record<string, unknown>;
export type RpcHandler = (payload: RpcPayload) => Promise<RpcResult> | RpcResult;

const idempotentResults = new Map<string, RpcResult>();

export class OperationError extends Error {
  constructor(
    public readonly code: string,
    message: string,
  ) {
    super(message);
  }
}

export function asRecord(value: unknown, field = "payload"): RpcPayload {
  if (value === null || typeof value !== "object" || Array.isArray(value)) {
    throw new OperationError("invalid_argument", `${field} must be an object.`);
  }

  return value as RpcPayload;
}

export function optionalString(
  input: RpcPayload,
  key: string,
  options: { maxLength?: number } = {},
): string | undefined {
  const value = input[key];
  if (value === undefined || value === null) {
    return undefined;
  }

  if (typeof value !== "string" || value.length > (options.maxLength ?? 100_000)) {
    throw new OperationError("invalid_argument", `${key} must be a string within the limit.`);
  }

  return value;
}

export function requiredString(
  input: RpcPayload,
  key: string,
  options: { maxLength?: number } = {},
): string {
  const value = optionalString(input, key, options);
  if (!value) {
    throw new OperationError("invalid_argument", `${key} is required.`);
  }

  return value;
}

export function optionalBoolean(input: RpcPayload, key: string): boolean | undefined {
  const value = input[key];
  if (value === undefined || value === null) {
    return undefined;
  }

  if (typeof value !== "boolean") {
    throw new OperationError("invalid_argument", `${key} must be a boolean.`);
  }

  return value;
}

export function optionalNumber(
  input: RpcPayload,
  key: string,
  options: { integer?: boolean; min?: number; max?: number } = {},
): number | undefined {
  const value = input[key];
  if (value === undefined || value === null) {
    return undefined;
  }

  const valid =
    typeof value === "number" &&
    Number.isFinite(value) &&
    (!options.integer || Number.isInteger(value)) &&
    (options.min === undefined || value >= options.min) &&
    (options.max === undefined || value <= options.max);
  if (!valid) {
    throw new OperationError("invalid_argument", `${key} must be a number within the limit.`);
  }

  return value;
}

export function stringArray(
  input: RpcPayload,
  key: string,
  options: { required?: boolean; maxItems?: number } = {},
): string[] {
  const value = input[key];
  if (value === undefined || value === null) {
    if (options.required) {
      throw new OperationError("invalid_argument", `${key} is required.`);
    }
    return [];
  }

  const maxItems = options.maxItems ?? 500;
  if (
    !Array.isArray(value) ||
    (options.required && value.length === 0) ||
    value.length > maxItems ||
    value.some((item) => typeof item !== "string" || item.length === 0)
  ) {
    throw new OperationError(
      "invalid_argument",
      `${key} must contain at most ${maxItems} non-empty strings.`,
    );
  }

  return value;
}

export function objectArray(
  input: RpcPayload,
  key: string,
  options: { required?: boolean; maxItems?: number } = {},
): RpcPayload[] {
  const value = input[key];
  if (value === undefined || value === null) {
    if (options.required) {
      throw new OperationError("invalid_argument", `${key} is required.`);
    }
    return [];
  }

  const maxItems = options.maxItems ?? 100;
  if (
    !Array.isArray(value) ||
    (options.required && value.length === 0) ||
    value.length > maxItems
  ) {
    throw new OperationError("invalid_argument", `${key} must contain at most ${maxItems} items.`);
  }

  return value.map((item, index) => asRecord(item, `${key}[${index}]`));
}

export function limit(input: RpcPayload, fallback = 100, maximum = 500): number {
  return optionalNumber(input, "limit", { integer: true, min: 1, max: maximum }) ?? fallback;
}

export async function requireNode(nodeId: string): Promise<BaseNode> {
  const node = await figma.getNodeByIdAsync(nodeId);
  if (!node || node.type === "DOCUMENT") {
    throw new OperationError("node_not_found", `No accessible node exists for id ${nodeId}.`);
  }

  return node;
}

export async function requireSceneNode(nodeId: string): Promise<SceneNode> {
  const node = await requireNode(nodeId);
  if (!isSceneNode(node)) {
    throw new OperationError("invalid_node_type", `${nodeId} is not a scene node.`);
  }

  return node;
}

export function isSceneNode(node: BaseNode): node is SceneNode {
  return node.type !== "DOCUMENT" && node.type !== "PAGE";
}

export function nodeSummary(node: BaseNode): RpcResult {
  const result: RpcResult = {
    id: node.id,
    type: node.type,
    name: node.name,
    removed: node.removed,
    parent_id: node.parent?.id ?? null,
  };

  if (isSceneNode(node)) {
    result.visible = node.visible;
    result.locked = node.locked;
    result.x = node.x;
    result.y = node.y;
    result.width = node.width;
    result.height = node.height;
  }

  return result;
}

export function nodeGeometrySummary(node: BaseNode): RpcResult {
  const result = nodeSummary(node);
  if (!isSceneNode(node)) {
    return result;
  }

  result.relative_transform = toSerializable(node.relativeTransform);
  result.absolute_transform = toSerializable(node.absoluteTransform);
  result.absolute_bounding_box = toSerializable(node.absoluteBoundingBox);
  result.coordinate_parent_id = coordinateParent(node)?.id ?? null;
  return result;
}

function coordinateParent(node: SceneNode): BaseNode | null {
  let parent = node.parent;
  while (parent?.type === "GROUP" || parent?.type === "BOOLEAN_OPERATION") {
    parent = parent.parent;
  }
  return parent;
}

export function toSerializable(value: unknown, depth = 0): unknown {
  if (depth > 12) {
    return "[depth-limit]";
  }

  if (
    value === null ||
    typeof value === "string" ||
    typeof value === "boolean" ||
    (typeof value === "number" && Number.isFinite(value))
  ) {
    return value;
  }

  if (value === undefined || typeof value === "function" || typeof value === "symbol") {
    return undefined;
  }

  if (value instanceof Uint8Array) {
    return bytesToBase64(value);
  }

  if (Array.isArray(value)) {
    return value.map((item) => toSerializable(item, depth + 1));
  }

  if (typeof value === "object") {
    const result: RpcPayload = {};
    for (const [key, item] of Object.entries(value as RpcPayload)) {
      if (key === "parent") {
        continue;
      }

      const serialized = toSerializable(item, depth + 1);
      if (serialized !== undefined) {
        result[key] = serialized;
      }
    }
    return result;
  }

  return String(value);
}

export function bytesToBase64(bytes: Uint8Array): string {
  const chunkSize = 32_768;
  let binary = "";
  for (let offset = 0; offset < bytes.length; offset += chunkSize) {
    const chunk = bytes.subarray(offset, Math.min(offset + chunkSize, bytes.length));
    binary += String.fromCharCode(...chunk);
  }
  return btoa(binary);
}

export function base64ToBytes(value: string): Uint8Array {
  let binary: string;
  try {
    binary = atob(value);
  } catch {
    throw new OperationError("invalid_argument", "data_base64 is not valid base64.");
  }

  const bytes = new Uint8Array(binary.length);
  for (let index = 0; index < binary.length; index += 1) {
    bytes[index] = binary.charCodeAt(index);
  }
  return bytes;
}

export function asApiRecord(value: unknown): Record<string, unknown> {
  return value as Record<string, unknown>;
}

export async function callApiMethod(
  target: unknown,
  method: string,
  args: unknown[] = [],
  unavailableCode = "unsupported_in_editor",
): Promise<unknown> {
  const candidate = asApiRecord(target)[method];
  if (typeof candidate !== "function") {
    throw new OperationError(unavailableCode, `${method} is unavailable in this Figma editor.`);
  }

  return await Reflect.apply(candidate, target, args);
}

export async function idempotentMutation(
  operation: string,
  payload: RpcPayload,
  execute: () => Promise<RpcResult> | RpcResult,
): Promise<RpcResult> {
  const key = optionalString(payload, "idempotency_key", { maxLength: 200 });
  if (key) {
    const cacheKey = `${operation}:${key}`;
    const cached = idempotentResults.get(cacheKey);
    if (cached) {
      return { ...cached, idempotent_replay: true };
    }

    const result = await execute();
    idempotentResults.set(cacheKey, result);
    while (idempotentResults.size > 200) {
      const oldest = idempotentResults.keys().next().value;
      if (typeof oldest === "string") {
        idempotentResults.delete(oldest);
      }
    }
    return result;
  }

  return await execute();
}
