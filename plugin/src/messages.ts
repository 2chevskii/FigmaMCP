export type ConnectionContext = {
  plugin_version: string;
  editor_type: string;
  mode: string;
  document_name: string;
  current_page: {
    id: string;
    name: string;
  };
};

export type ControllerToUiMessage =
  | { type: "config_loaded"; port: number; context: ConnectionContext }
  | { type: "bridge_frame"; bytes: Uint8Array }
  | { type: "context_dirty"; context: ConnectionContext };

export type UiToControllerMessage =
  | { type: "set_port"; port: number }
  | { type: "close_plugin" }
  | { type: "bridge_frame"; bytes: Uint8Array };
