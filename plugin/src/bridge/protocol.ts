import { decode, encode } from "@msgpack/msgpack";

export const PROTOCOL_VERSION = 1;
export const MAX_MESSAGE_BYTES = 1024 * 1024;
export type Envelope = { type: string; protocol_version: number; connection_id?: string; request_id?: string; method?: string; payload?: Record<string, unknown>; error?: { code: string; message: string }; sent_at: string };
export const now = () => new Date().toISOString();
export function pack(value: Envelope): Uint8Array { const bytes = encode(value); if (bytes.length > MAX_MESSAGE_BYTES) throw new Error("Bridge message exceeds 1 MiB."); return bytes; }
export function unpack(bytes: Uint8Array): Envelope {
  if (bytes.length === 0 || bytes.length > MAX_MESSAGE_BYTES) throw new Error("Invalid bridge message length.");
  const value = decode(bytes) as Envelope;
  if (!value || typeof value !== "object" || typeof value.type !== "string" || value.protocol_version !== PROTOCOL_VERSION || typeof value.sent_at !== "string") throw new Error("Invalid bridge envelope.");
  return value;
}
export function isPort(value: string): boolean { return /^(?:[1-9]\d{0,4})$/.test(value) && Number(value) <= 65535; }
