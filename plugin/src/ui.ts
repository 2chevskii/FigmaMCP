import { Envelope, now, pack, PROTOCOL_VERSION, unpack } from "./bridge/protocol";
import { createConnectionId } from "./bridge/uuid";
import { BRIDGE_SUBPROTOCOL, DEFAULT_PORT, isPort, PLUGIN_VERSION } from "./config";
import { ConnectionContext, ControllerToUiMessage, UiToControllerMessage } from "./messages";

type ConnectionState =
  | "loading_config"
  | "connecting"
  | "connected"
  | "reconnecting"
  | "invalid_port"
  | "protocol_error";

const STATE_LABELS: Record<ConnectionState, string> = {
  loading_config: "Loading settings…",
  connecting: "Connecting…",
  connected: "Connected",
  reconnecting: "Reconnecting…",
  invalid_port: "Check server port",
  protocol_error: "Connection error",
};

const RECONNECT_DELAYS_MS = [500, 1000, 2000, 5000, 10000];

const portInput = requiredElement<HTMLInputElement>("#port");
const statusElement = requiredElement<HTMLDivElement>("#status");
const stateElement = requiredElement<HTMLDivElement>("#state");
const detailsElement = requiredElement<HTMLDivElement>("#details");
const errorElement = requiredElement<HTMLDivElement>("#error");
const saveButton = requiredElement<HTMLButtonElement>("#save");
const closeButton = requiredElement<HTMLButtonElement>("#close");

const connectionId = createConnectionId();
let configuredPort = DEFAULT_PORT;
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
saveButton.addEventListener("click", savePort);
closeButton.addEventListener("click", closePlugin);
portInput.addEventListener("input", clearPortError);
portInput.addEventListener("keydown", (event) => {
  if (event.key === "Enter") {
    savePort();
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
    `ws://127.0.0.1:${configuredPort}/bridge`,
  );

  const nextSocket = new WebSocket(`ws://127.0.0.1:${configuredPort}/bridge`, BRIDGE_SUBPROTOCOL);
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
        `ws://127.0.0.1:${configuredPort}/bridge`,
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
      setState("connected", "", `Local server · Port ${configuredPort}`);
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
      configuredPort = message.port;
      context = message.context;
      portInput.value = String(configuredPort);
      connect();
      break;
    case "bridge_frame":
      if (socket?.readyState === WebSocket.OPEN) {
        socket.send(message.bytes);
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

function savePort(): void {
  if (!isPort(portInput.value)) {
    setState("invalid_port", "Enter a decimal port from 1 through 65535.");
    portInput.setAttribute("aria-invalid", "true");
    portInput.focus();
    return;
  }

  portInput.removeAttribute("aria-invalid");
  configuredPort = Number(portInput.value);
  postToController({ type: "set_port", port: configuredPort });
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
    target.send(pack(envelope));
  }
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
      return `Local server · Port ${configuredPort}`;
    case "connecting":
      return "Opening a local connection";
    case "reconnecting":
      return `Retry attempt ${reconnectAttempt + 1}`;
    case "invalid_port":
      return "Update the connection setting below";
    case "protocol_error":
      return "The server returned an unexpected response";
    case "loading_config":
      return "Preparing the connector";
  }
}

function clearPortError(): void {
  portInput.removeAttribute("aria-invalid");

  if (statusElement.dataset.state === "invalid_port") {
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
