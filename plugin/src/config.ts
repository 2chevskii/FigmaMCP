export const BRIDGE_SUBPROTOCOL = "figma-mcp-bridge.v2";
export const DEFAULT_PORT = 3846;
export const PLUGIN_VERSION = "0.2.0";
export const PORT_STORAGE_KEY = "figma-mcp-port";

export function isPort(value: string): boolean {
  return /^(?:[1-9]\d{0,4})$/.test(value) && Number(value) <= 65535;
}
