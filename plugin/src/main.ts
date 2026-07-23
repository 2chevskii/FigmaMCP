import { Envelope, now, pack, PROTOCOL_VERSION, unpack } from "./bridge/protocol";
import { DEFAULT_PORT, PLUGIN_VERSION, PORT_STORAGE_KEY } from "./config";
import { ConnectionContext, ControllerToUiMessage, UiToControllerMessage } from "./messages";
import { nodeMutationHandlers } from "./operations/nodes";
import { readHandlers, startChangeJournal } from "./operations/read";
import { OperationError, RpcHandler, RpcPayload } from "./operations/shared";
import { styleVariableHandlers } from "./operations/styles-variables";
import { textComponentHandlers } from "./operations/text-components";

figma.showUI(__html__, { width: 360, height: 300, themeColors: true });

void loadConfig();
startChangeJournal();
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
      void handleBridgeFrame(message.bytes);
      break;
  }
}

async function handleBridgeFrame(bytes: Uint8Array): Promise<void> {
  let request: Envelope;

  try {
    request = unpack(bytes);
  } catch {
    return;
  }

  if (request.type !== "request" || !request.connection_id || !request.request_id) {
    return;
  }

  const handlers: Record<string, RpcHandler> = {
    ...readHandlers,
    ...nodeMutationHandlers,
    ...textComponentHandlers,
    ...styleVariableHandlers,
  };
  const handler: RpcHandler | undefined = request.method ? handlers[request.method] : undefined;
  if (!handler) {
    respondWithError(
      request,
      "method_not_found",
      `Unsupported bridge method: ${request.method ?? "(missing)"}.`,
    );
    return;
  }

  try {
    const payload = (request.payload ?? {}) as RpcPayload;
    const result = await handler(payload);
    respond({
      type: "response",
      protocol_version: PROTOCOL_VERSION,
      connection_id: request.connection_id,
      request_id: request.request_id,
      sent_at: now(),
      payload: {
        connection_id: request.connection_id,
        ...result,
      },
    });
  } catch (error) {
    if (error instanceof OperationError) {
      respondWithError(request, error.code, error.message);
      return;
    }

    const message = error instanceof Error ? error.message : "The Figma API operation failed.";
    respondWithError(request, "figma_api_error", message);
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
