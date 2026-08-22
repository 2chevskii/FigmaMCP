export const BRIDGE_SUBPROTOCOL = "figma-mcp-bridge.v2";
export const DEFAULT_SERVER_URL = "http://127.0.0.1:3846";
export const PLUGIN_VERSION = "0.2.0";
export const SERVER_URL_STORAGE_KEY = "figma-mcp-server-url";

export function isServerUrl(value: string): boolean {
  try {
    const url = new URL(value);
    return (
      url.protocol === "http:" &&
      (url.hostname === "127.0.0.1" || url.hostname === "localhost") &&
      !url.username &&
      !url.password &&
      url.pathname === "/" &&
      !url.search &&
      !url.hash
    );
  } catch {
    return false;
  }
}

export function bridgeUrl(serverUrl: string): string {
  const url = new URL("bridge", ensureTrailingSlash(serverUrl));
  return url.toString().replace(/^http/, "ws");
}

function ensureTrailingSlash(value: string): string {
  return value.endsWith("/") ? value : `${value}/`;
}
