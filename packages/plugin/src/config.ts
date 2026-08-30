export const BRIDGE_SUBPROTOCOL = "figma-mcp-bridge.v2";
export const DEFAULT_SERVER_PORT = 3846;
declare const FIGMA_MCP_PRODUCT_VERSION: string;

export const PLUGIN_VERSION =
  typeof FIGMA_MCP_PRODUCT_VERSION === "undefined" ? "1.0.0" : FIGMA_MCP_PRODUCT_VERSION;
export const SERVER_PORT_STORAGE_KEY = "figma-mcp-server-port";

export function isServerPort(value: unknown): value is number {
  return typeof value === "number" && Number.isInteger(value) && value >= 1 && value <= 65535;
}

export function parseServerPort(value: string): number | undefined {
  if (!/^\d+$/.test(value)) {
    return undefined;
  }

  const port = Number(value);
  return isServerPort(port) ? port : undefined;
}

export function bridgeUrl(port: number): string {
  return `ws://localhost:${port}/bridge`;
}
