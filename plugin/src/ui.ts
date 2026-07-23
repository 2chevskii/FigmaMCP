import { Envelope, isPort, now, pack, PROTOCOL_VERSION, unpack } from "./bridge/protocol";
type State = "loading_config" | "disconnected" | "connecting" | "connected" | "reconnecting" | "invalid_port" | "protocol_error";
const port = document.querySelector<HTMLInputElement>("#port")!; const state = document.querySelector<HTMLDivElement>("#state")!; const details = document.querySelector<HTMLDivElement>("#details")!; const error = document.querySelector<HTMLDivElement>("#error")!;
let socket: WebSocket | undefined; let stopped = false; let reconnect = 0; let timer: number | undefined; const connectionId = crypto.randomUUID(); let configuredPort = 3846; let summary: Record<string, unknown> = { plugin_version: "0.1.0", editor_type: "figma", mode: "default", document_name: "Loading…", current_page: { id: "", name: "Loading…" } };
function post(message: object): void { parent.postMessage({ pluginMessage: message }, "*"); }
function setState(value: State, message = ""): void { state.textContent = value; error.textContent = message; }
function connect(): void {
  if (stopped) return; clearTimeout(timer); setState(reconnect ? "reconnecting" : "connecting");
  socket = new WebSocket(`ws://127.0.0.1:${configuredPort}/bridge`, "figma-mcp-bridge.v1"); socket.binaryType = "arraybuffer";
  socket.onopen = () => send({ type: "hello", protocol_version: PROTOCOL_VERSION, connection_id: connectionId, sent_at: now(), payload: context() });
  socket.onmessage = event => { if (typeof event.data === "string") { setState("protocol_error", "Bridge sent a text frame."); socket?.close(); return; } try { const bytes = new Uint8Array(event.data as ArrayBuffer); const envelope = unpack(bytes); receive(envelope, bytes); } catch { setState("protocol_error", "Invalid bridge message."); socket?.close(); } };
  socket.onerror = () => { error.textContent = "Unable to connect to the companion server."; };
  socket.onclose = () => schedule();
}
function receive(envelope: Envelope, bytes: Uint8Array): void {
  if (envelope.type === "hello_ack") { reconnect = 0; setState("connected"); details.textContent = `Connection: ${connectionId}`; return; }
  if (envelope.type === "request") { post({ type: "bridge_frame", bytes }); return; }
  if (envelope.type === "ping" && envelope.payload) send({ type: "pong", protocol_version: PROTOCOL_VERSION, connection_id: connectionId, sent_at: now(), payload: envelope.payload });
}
function send(envelope: Envelope): void { if (socket?.readyState === WebSocket.OPEN) socket.send(pack(envelope)); }
function schedule(): void { if (stopped) return; const delays = [500, 1000, 2000, 5000, 10000]; const base = delays[Math.min(reconnect++, delays.length - 1)]; const jitter = base * (0.8 + Math.random() * 0.4); timer = window.setTimeout(connect, jitter); }
function context(): Record<string, unknown> { return summary; }
window.onmessage = event => { const message = event.data.pluginMessage as { type: string; port?: number; bytes?: Uint8Array; context?: Record<string, unknown> }; if (message.type === "config_loaded" && message.port) { configuredPort = message.port; summary = message.context ?? summary; port.value = String(configuredPort); connect(); } else if (message.type === "bridge_frame" && message.bytes) { if (socket?.readyState === WebSocket.OPEN) socket.send(message.bytes); } else if (message.type === "context_dirty" && message.context) { summary = message.context; send({ type: "context_changed", protocol_version: PROTOCOL_VERSION, connection_id: connectionId, sent_at: now(), payload: summary }); } };
document.querySelector("#save")!.addEventListener("click", () => { if (!isPort(port.value)) { setState("invalid_port", "Enter a decimal port from 1 through 65535."); return; } configuredPort = Number(port.value); post({ type: "set_port", port: configuredPort }); socket?.close(1000, "reconnect"); reconnect = 0; connect(); });
document.querySelector("#close")!.addEventListener("click", () => { stopped = true; clearTimeout(timer); socket?.close(1000, "plugin_closed"); post({ type: "close_plugin" }); });
