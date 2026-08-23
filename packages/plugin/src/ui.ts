import { Envelope, now, pack, PROTOCOL_VERSION, unpack } from "./bridge/protocol";
import { createConnectionId } from "./bridge/uuid";
import {
  BRIDGE_SUBPROTOCOL,
  bridgeUrl,
  DEFAULT_SERVER_PORT,
  parseServerPort,
  PLUGIN_VERSION,
} from "./config";
import { ConnectionContext, ControllerToUiMessage, UiToControllerMessage } from "./messages";

type ConnectionState =
  | "loading_config"
  | "connecting"
  | "connected"
  | "reconnecting"
  | "invalid_server_port"
  | "protocol_error";

const STATE_LABELS: Record<ConnectionState, string> = {
  loading_config: "Loading settings…",
  connecting: "Connecting…",
  connected: "Connected",
  reconnecting: "Reconnecting…",
  invalid_server_port: "Check server port",
  protocol_error: "Connection error",
};

const RECONNECT_DELAYS_MS = [500, 1000, 2000, 5000, 10000];

const serverPortInput = requiredElement<HTMLInputElement>("#server-port");
const statusElement = requiredElement<HTMLDivElement>("#status");
const stateElement = requiredElement<HTMLDivElement>("#state");
const detailsElement = requiredElement<HTMLDivElement>("#details");
const errorElement = requiredElement<HTMLDivElement>("#error");
const saveButton = requiredElement<HTMLButtonElement>("#save");
const closeButton = requiredElement<HTMLButtonElement>("#close");

const connectionId = createConnectionId();
let configuredServerPort = DEFAULT_SERVER_PORT;
let context: ConnectionContext = {
  plugin_version: PLUGIN_VERSION,
  editor_type: "figma",
  mode: "default",
  document_name: "Loading…",
  current_page: { id: "", name: "Loading…" },
};
let socket: WebSocket | undefined;
let reconnectAttempt = 0;
let reconnectTimer: number | undefined;
let stopped = false;

window.onmessage = handleControllerMessage;
saveButton.addEventListener("click", saveConnectionSettings);
closeButton.addEventListener("click", closePlugin);
serverPortInput.addEventListener("input", clearServerPortError);
serverPortInput.addEventListener("keydown", (event) => {
  if (event.key === "Enter") {
    saveConnectionSettings();
  }
});
function connect(): void {
  if (stopped) {
    return;
  }

  clearReconnectTimer();
  setState(
    reconnectAttempt > 0 ? "reconnecting" : "connecting",
    "",
    bridgeUrl(configuredServerPort),
  );

  const nextSocket = new WebSocket(bridgeUrl(configuredServerPort), BRIDGE_SUBPROTOCOL);
  nextSocket.binaryType = "arraybuffer";
  socket = nextSocket;

  nextSocket.onopen = () => {
    sendToSocket(nextSocket, {
      type: "hello",
      protocol_version: PROTOCOL_VERSION,
      connection_id: connectionId,
      sent_at: now(),
      payload: context,
    });
  };

  nextSocket.onmessage = (event) => {
    if (socket !== nextSocket) {
      return;
    }

    if (typeof event.data === "string") {
      closeForProtocolError(nextSocket, "Bridge sent a text frame.");
      return;
    }

    try {
      const bytes = new Uint8Array(event.data as ArrayBuffer);
      receive(unpack(bytes), bytes);
    } catch {
      closeForProtocolError(nextSocket, "Invalid bridge message.");
    }
  };

  nextSocket.onerror = () => {
    if (socket === nextSocket) {
      setState(
        "reconnecting",
        "Unable to reach the companion server. Retrying automatically.",
        bridgeUrl(configuredServerPort),
      );
    }
  };

  nextSocket.onclose = () => {
    if (socket === nextSocket) {
      socket = undefined;
      scheduleReconnect();
    }
  };
}

function receive(envelope: Envelope, bytes: Uint8Array): void {
  switch (envelope.type) {
    case "hello_ack":
      reconnectAttempt = 0;
      setState("connected", "", bridgeUrl(configuredServerPort));
      break;
    case "request":
      postToController({ type: "bridge_frame", bytes });
      break;
    case "ping":
      if (envelope.payload) {
        send({
          type: "pong",
          protocol_version: PROTOCOL_VERSION,
          connection_id: connectionId,
          sent_at: now(),
          payload: envelope.payload,
        });
      }
      break;
  }
}

function handleControllerMessage(
  event: MessageEvent<{ pluginMessage?: ControllerToUiMessage }>,
): void {
  const message = event.data.pluginMessage;
  if (!message) {
    return;
  }

  switch (message.type) {
    case "config_loaded":
      configuredServerPort = message.serverPort;
      context = message.context;
      serverPortInput.value = String(configuredServerPort);
      connect();
      break;
    case "bridge_frame":
      if (socket?.readyState === WebSocket.OPEN) {
        socket.send(toArrayBuffer(message.bytes));
      }
      break;
    case "context_dirty":
      context = message.context;
      send({
        type: "context_changed",
        protocol_version: PROTOCOL_VERSION,
        connection_id: connectionId,
        sent_at: now(),
        payload: context,
      });
      break;
  }
}

function saveConnectionSettings(): void {
  const serverPort = parseServerPort(serverPortInput.value.trim());
  if (serverPort === undefined) {
    setState("invalid_server_port", "Enter a port number from 1 through 65535.");
    serverPortInput.setAttribute("aria-invalid", "true");
    serverPortInput.focus();
    return;
  }

  serverPortInput.removeAttribute("aria-invalid");
  configuredServerPort = serverPort;
  postToController({
    type: "set_connection_settings",
    serverPort: configuredServerPort,
  });
  restartConnection();
}

function restartConnection(): void {
  clearReconnectTimer();
  reconnectAttempt = 0;

  const previousSocket = socket;
  socket = undefined;

  if (previousSocket && previousSocket.readyState < WebSocket.CLOSING) {
    previousSocket.onclose = connect;
    previousSocket.close(1000, "reconnect");
    return;
  }

  connect();
}

function closePlugin(): void {
  stopped = true;
  clearReconnectTimer();
  socket?.close(1000, "plugin_closed");
  postToController({ type: "close_plugin" });
}

function send(envelope: Envelope): void {
  if (socket) {
    sendToSocket(socket, envelope);
  }
}

function sendToSocket(target: WebSocket, envelope: Envelope): void {
  if (target.readyState === WebSocket.OPEN) {
    target.send(toArrayBuffer(pack(envelope)));
  }
}

function toArrayBuffer(bytes: Uint8Array): ArrayBuffer {
  return new Uint8Array(bytes).buffer;
}

function scheduleReconnect(): void {
  if (stopped) {
    return;
  }

  const delayIndex = Math.min(reconnectAttempt, RECONNECT_DELAYS_MS.length - 1);
  const baseDelay = RECONNECT_DELAYS_MS[delayIndex];
  const jitteredDelay = baseDelay * (0.8 + Math.random() * 0.4);

  reconnectAttempt += 1;
  reconnectTimer = window.setTimeout(connect, jitteredDelay);
}

function clearReconnectTimer(): void {
  clearTimeout(reconnectTimer);
  reconnectTimer = undefined;
}

function closeForProtocolError(target: WebSocket, message: string): void {
  setState("protocol_error", message);
  target.close();
}

function setState(value: ConnectionState, message = "", details?: string): void {
  statusElement.dataset.state = value;
  stateElement.textContent = STATE_LABELS[value];
  detailsElement.textContent = details ?? defaultStateDetails(value);
  errorElement.textContent = message;
}

function defaultStateDetails(value: ConnectionState): string {
  switch (value) {
    case "connected":
      return bridgeUrl(configuredServerPort);
    case "connecting":
      return "Opening a local connection";
    case "reconnecting":
      return `Retry attempt ${reconnectAttempt + 1}`;
    case "invalid_server_port":
      return "Update the connection setting below";
    case "protocol_error":
      return "The server returned an unexpected response";
    case "loading_config":
      return "Preparing the connector";
  }
}

function clearServerPortError(): void {
  serverPortInput.removeAttribute("aria-invalid");

  if (statusElement.dataset.state === "invalid_server_port") {
    setState(socket?.readyState === WebSocket.OPEN ? "connected" : "reconnecting");
  }
}

function postToController(message: UiToControllerMessage): void {
  parent.postMessage({ pluginMessage: message }, "*");
}

function requiredElement<T extends Element>(selector: string): T {
  const element = document.querySelector<T>(selector);
  if (!element) {
    throw new Error(`Missing required UI element: ${selector}`);
  }

  return element;
}
