import { decode, encode } from "@msgpack/msgpack";

export const PROTOCOL_VERSION = 2;
export const MAX_MESSAGE_BYTES = 16 * 1024 * 1024;

export type BridgePayload = Record<string, unknown>;

export type BridgeError = {
  code: string;
  message: string;
};

export type Envelope = {
  type: string;
  protocol_version: number;
  connection_id?: string;
  request_id?: string;
  method?: string;
  payload?: BridgePayload;
  error?: BridgeError;
  sent_at: string;
};

export function now(): string {
  return new Date().toISOString();
}

export function pack(value: Envelope): Uint8Array {
  const bytes = encode(value);
  if (bytes.length > MAX_MESSAGE_BYTES) {
    throw new Error("Bridge message exceeds 16 MiB.");
  }

  return bytes;
}

export function unpack(bytes: Uint8Array): Envelope {
  if (bytes.length === 0 || bytes.length > MAX_MESSAGE_BYTES) {
    throw new Error("Invalid bridge message length.");
  }

  const value: unknown = decode(bytes);
  if (
    value === null ||
    typeof value !== "object" ||
    !("type" in value) ||
    typeof value.type !== "string" ||
    !("protocol_version" in value) ||
    value.protocol_version !== PROTOCOL_VERSION ||
    !("sent_at" in value) ||
    typeof value.sent_at !== "string"
  ) {
    throw new Error("Invalid bridge envelope.");
  }

  return value as Envelope;
}
