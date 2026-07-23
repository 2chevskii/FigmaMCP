import { Envelope, now, pack, PROTOCOL_VERSION, unpack } from "./bridge/protocol";
import { DEFAULT_PORT, PLUGIN_VERSION, PORT_STORAGE_KEY } from "./config";
import { ConnectionContext, ControllerToUiMessage, UiToControllerMessage } from "./messages";

const GET_DOCUMENT_METADATA = "get_document_metadata";

figma.showUI(__html__, { width: 360, height: 300, themeColors: true });

void loadConfig();
figma.on("currentpagechange", notifyContextChanged);
figma.ui.onmessage = handleUiMessage;

async function loadConfig(): Promise<void> {
  const storedPort: unknown = await figma.clientStorage.getAsync(PORT_STORAGE_KEY);
  postToUi({
    type: "config_loaded",
    port: typeof storedPort === "number" ? storedPort : DEFAULT_PORT,
    context: readConnectionContext(),
  });
}

function handleUiMessage(message: UiToControllerMessage): void {
  switch (message.type) {
    case "set_port":
      void figma.clientStorage.setAsync(PORT_STORAGE_KEY, message.port);
      break;
    case "close_plugin":
      figma.closePlugin();
      break;
    case "bridge_frame":
      handleBridgeFrame(message.bytes);
      break;
  }
}

function handleBridgeFrame(bytes: Uint8Array): void {
  let request: Envelope;

  try {
    request = unpack(bytes);
  } catch {
    return;
  }

  if (request.type !== "request" || !request.connection_id || !request.request_id) {
    return;
  }

  if (request.method !== GET_DOCUMENT_METADATA) {
    respondWithError(
      request,
      "method_not_found",
      `Unsupported bridge method: ${request.method ?? "(missing)"}.`,
    );
    return;
  }

  try {
    respond({
      type: "response",
      protocol_version: PROTOCOL_VERSION,
      connection_id: request.connection_id,
      request_id: request.request_id,
      sent_at: now(),
      payload: {
        connection_id: request.connection_id,
        ...readDocumentMetadata(),
      },
    });
  } catch {
    respondWithError(request, "figma_api_error", "Unable to read document metadata.");
  }
}

function respondWithError(request: Envelope, code: string, message: string): void {
  respond({
    type: "error",
    protocol_version: PROTOCOL_VERSION,
    connection_id: request.connection_id,
    request_id: request.request_id,
    sent_at: now(),
    error: { code, message },
  });
}

function respond(envelope: Envelope): void {
  postToUi({ type: "bridge_frame", bytes: pack(envelope) });
}

function readDocumentMetadata(): Record<string, unknown> {
  const document = figma.root;
  const currentPage = figma.currentPage;

  return {
    document: {
      name: document.name,
      type: document.type,
      color_profile: document.documentColorProfile,
      page_count: document.children.length,
      pages: document.children.map((page) => ({ id: page.id, name: page.name })),
    },
    current_page: {
      id: currentPage.id,
      name: currentPage.name,
      top_level_node_count: currentPage.children.length,
    },
    selection: currentPage.selection.map((node) => ({
      id: node.id,
      name: node.name,
      type: node.type,
    })),
    editor: {
      type: figma.editorType,
      mode: figma.mode,
    },
  };
}

function readConnectionContext(): ConnectionContext {
  return {
    plugin_version: PLUGIN_VERSION,
    editor_type: figma.editorType,
    mode: figma.mode,
    document_name: figma.root.name,
    current_page: {
      id: figma.currentPage.id,
      name: figma.currentPage.name,
    },
  };
}

function notifyContextChanged(): void {
  postToUi({ type: "context_dirty", context: readConnectionContext() });
}

function postToUi(message: ControllerToUiMessage): void {
  figma.ui.postMessage(message);
}
