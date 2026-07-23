import { Envelope, now, pack, PROTOCOL_VERSION, unpack } from "./bridge/protocol";

type UiMessage = { type: "config_loaded"; port: number } | { type: "set_port"; port: number } | { type: "close_plugin" } | { type: "bridge_frame"; bytes: Uint8Array } | { type: "context_dirty" };
const key = "figma-mcp-port";
figma.showUI(__html__, { width: 360, height: 240, themeColors: true });
void loadConfig();
figma.on("currentpagechange", () => figma.ui.postMessage({ type: "context_dirty" }));
figma.on("selectionchange", () => undefined);
figma.ui.onmessage = (message: UiMessage) => {
  if (message.type === "set_port") { void figma.clientStorage.setAsync(key, message.port); return; }
  if (message.type === "close_plugin") { figma.closePlugin(); return; }
  if (message.type === "bridge_frame") { void handleRequest(message.bytes); }
};
async function loadConfig(): Promise<void> { const value = await figma.clientStorage.getAsync(key); figma.ui.postMessage({ type: "config_loaded", port: typeof value === "number" ? value : 3846 }); }
async function handleRequest(bytes: Uint8Array): Promise<void> {
  let request: Envelope;
  try { request = unpack(bytes); } catch { return; }
  if (request.type !== "request" || request.method !== "get_document_metadata" || !request.connection_id || !request.request_id) return;
  try {
    const response: Envelope = { type: "response", protocol_version: PROTOCOL_VERSION, connection_id: request.connection_id, request_id: request.request_id, sent_at: now(), payload: metadata() };
    figma.ui.postMessage({ type: "bridge_frame", bytes: pack(response) });
  } catch {
    const response: Envelope = { type: "error", protocol_version: PROTOCOL_VERSION, connection_id: request.connection_id, request_id: request.request_id, sent_at: now(), error: { code: "figma_api_error", message: "Unable to read document metadata." } };
    figma.ui.postMessage({ type: "bridge_frame", bytes: pack(response) });
  }
}
function metadata(): Record<string, unknown> {
  const root = figma.root;
  return { connection_id: "", document: { name: root.name, type: root.type, color_profile: root.documentColorProfile, page_count: root.children.length, pages: root.children.map(page => ({ id: page.id, name: page.name })) }, current_page: { id: figma.currentPage.id, name: figma.currentPage.name, top_level_node_count: figma.currentPage.children.length }, selection: figma.currentPage.selection.map(node => ({ id: node.id, name: node.name, type: node.type })), editor: { type: figma.editorType, mode: figma.mode } };
}
