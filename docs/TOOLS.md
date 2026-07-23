# Figma Design MCP tool reference

Connector version: 0.2.0

Every document-specific call requires the lowercase canonical `connection_id` returned by
`list_figma_connections`. Call `get_figma_capabilities` after choosing a connection because beta and
permission-gated APIs can vary by Figma runtime, account, and file.

## Common conventions

- Tool inputs use `snake_case`.
- IDs are live Figma node/style/variable IDs, not REST file keys.
- Bounded mutations accept no more than 100 items unless the tool says otherwise.
- Mutation tools that accept `dry_run: true` validate and report intended targets without writing.
- Mutation tools that accept `idempotency_key` cache the first result for the current plugin
  invocation. Reusing the key returns that result with `idempotent_replay: true`.
- Page access is explicit. Call `load_figma_page` before traversing a non-current page. The connector
  never calls `loadAllPagesAsync()` implicitly.
- Binary inputs and outputs use base64 and are capped at 12 MiB. Bridge envelopes are capped at
  16 MiB.
- Errors are structured as `error.code`, `error.message`, and `error.connection_id`.
- Figma Plugin API validation still applies. For example, a node cannot be reparented out of an
  instance, text fonts must exist, and Starter-plan page/mode limits remain enforced by Figma.

## Connection and document context

| Tool                          | Input object                                                                            |
| ----------------------------- | --------------------------------------------------------------------------------------- |
| `list_figma_connections`      | None                                                                                    |
| `get_figma_document_metadata` | None beyond `connection_id`; retained as a compatibility alias for `get_figma_document` |
| `get_figma_capabilities`      | None                                                                                    |
| `get_figma_document`          | None                                                                                    |
| `list_figma_pages`            | Optional `cursor`, `limit`                                                              |
| `load_figma_page`             | `page_id`                                                                               |
| `get_figma_selection`         | None                                                                                    |
| `set_figma_selection`         | `node_ids`, optional `focus`                                                            |
| `set_figma_current_page`      | `page_id`                                                                               |
| `get_figma_document_changes`  | Optional `cursor`, `limit`; pass the returned `next_cursor` into the next poll          |

## Scene-tree reads

| Tool                      | Input object                                                                                               |
| ------------------------- | ---------------------------------------------------------------------------------------------------------- |
| `get_figma_nodes`         | `node_ids`; optional `fields`, `child_depth` from 0 through 4                                              |
| `query_figma_nodes`       | Optional `root_id`, `node_types`, `name`, `name_contains`, `visible`, `plugin_data_key`, `fields`, `limit` |
| `get_figma_node_css`      | `node_ids`                                                                                                 |
| `get_figma_node_geometry` | `node_ids`                                                                                                 |
| `get_figma_text`          | `node_ids`; optional `start`, `end`, `segment_fields`                                                      |
| `get_figma_components`    | `node_ids`                                                                                                 |
| `get_figma_prototype`     | `node_ids`                                                                                                 |
| `get_figma_plugin_data`   | `node_ids`; optional private `keys` and `shared_namespaces`                                                |
| `get_figma_dev_metadata`  | `node_ids`                                                                                                 |

Default node fields are `id`, `type`, `name`, `removed`, `parent_id`, `visible`, `locked`, `x`, `y`,
`width`, and `height`. Explicit projections can also request transforms, bounds, paints, strokes,
effects, corners, constraints, layout/grid properties, children, vector data, component properties,
reactions, annotations, and bound variables.

## Node creation and mutation

`create_figma_nodes` accepts:

```json
{
  "nodes": [
    {
      "kind": "frame",
      "parent_id": "1:2",
      "width": 320,
      "height": 200,
      "properties": {
        "name": "Card",
        "layout_mode": "VERTICAL",
        "item_spacing": 16,
        "padding_left": 24,
        "padding_right": 24,
        "padding_top": 24,
        "padding_bottom": 24
      }
    }
  ],
  "idempotency_key": "create-card-v1"
}
```

Supported Design constructors are `rectangle`, `line`, `ellipse`, `polygon`, `star`, `vector`,
`text`, `frame`, `component`, `page`, `page_divider`, `slice`, `section`, `boolean_operation`, `svg`,
and `text_path`. Text accepts `characters`; SVG accepts `svg`; text paths accept `vector_node_id`,
`start_segment`, and `start_position`.

`update_figma_nodes` accepts `updates: [{ node_id, properties }]`. The allowlist covers identity,
visibility/locking, position/rotation/opacity, masks/blending/effects, fills/strokes, corners,
constraints, auto/grid layout, min/max sizing, clipping, export settings, prototype properties,
shape-specific point/arc data, and dev status.

| Tool                       | Input object                                                                                                  |
| -------------------------- | ------------------------------------------------------------------------------------------------------------- |
| `clone_figma_nodes`        | `node_ids`                                                                                                    |
| `move_figma_nodes`         | `moves: [{ node_id, parent_id, index? }]`                                                                     |
| `delete_figma_nodes`       | `node_ids`                                                                                                    |
| `resize_figma_nodes`       | `items: [{ node_id, mode, width?, height?, scale?, lock_aspect_ratio? }]`                                     |
| `combine_figma_nodes`      | `operation`, `node_ids`, optional `parent_id`, `index`; transform groups also require one `modifier` per node |
| `set_figma_vector_network` | `node_id` and `vector_network` and/or `vector_paths`                                                          |

Combine operations are `group`, `transform_group`, `flatten`, `ungroup`, `combine_as_variants`,
`union`, `subtract`, `intersect`, and `exclude`.

## Text, components, and instances

| Tool                              | Input object                                                                                                                     |
| --------------------------------- | -------------------------------------------------------------------------------------------------------------------------------- |
| `list_figma_fonts`                | Optional `family`, `cursor`, `limit`                                                                                             |
| `update_figma_text`               | `items` with `node_id`, `operation`, ranges, `characters`, optional `font_names`, and range `properties`                         |
| `update_figma_text_path`          | Same item schema as text plus optional `path_alignment`, `paragraph_spacing`, `paragraph_indent`                                 |
| `create_figma_component_instance` | `operation` (`create_instance` or `component_from_node`) and `component_id` or `node_id`                                         |
| `update_figma_component`          | `items` with metadata and `property_actions` (`add`, `edit`, `delete`)                                                           |
| `update_figma_instance`           | `items` using `swap_component`, `set_main_component`, `set_properties`, `remove_overrides`, `detach`, `set_scale`, `set_exposed` |
| `update_figma_slot`               | `operation` (`create`, `reset`, `inspect`) and `component_id` or `slot_id`                                                       |
| `list_figma_component_instances`  | `component_id`, optional `cursor`, `limit`                                                                                       |

Text operations are `replace`, `insert`, `delete`, `set_all`, and `format`. Range properties include
`font_name`, `font_size`, `text_case`, `text_decoration`, `letter_spacing`, `line_height`,
`hyperlink`, `fills`, `list_options`, `indentation`, `paragraph_indent`, `paragraph_spacing`, and
`open_type_features`. The connector loads all current and requested fonts before writing.

## Styles, variables, and libraries

| Tool                               | Input object                                                                                      |
| ---------------------------------- | ------------------------------------------------------------------------------------------------- |
| `list_figma_styles`                | Optional `kinds` (`paint`, `text`, `effect`, `grid`), `cursor`, `limit`                           |
| `create_figma_style`               | `kind`, `name`, and the type-specific value                                                       |
| `update_figma_style`               | `style_id` and fields to patch                                                                    |
| `delete_figma_style`               | `style_ids`                                                                                       |
| `reorder_figma_styles`             | `kind`, `operation` (`style` or `folder`), and target/reference IDs or paths                      |
| `list_figma_style_consumers`       | `style_id`, optional `cursor`, `limit`                                                            |
| `list_figma_variables`             | Optional `resolved_type`, `cursor`, `limit`                                                       |
| `create_figma_variable_collection` | `name`; optional `extend_collection_key`, `hidden_from_publishing`, `mode_actions`                |
| `create_figma_variable`            | `collection_id`, `name`, `resolved_type`; optional values, aliases, scopes, and code syntax       |
| `update_figma_variable`            | `variable_id` and fields to patch                                                                 |
| `delete_figma_variable`            | `variable_ids` and/or `collection_ids`                                                            |
| `bind_figma_variable`              | `bindings` using `node_field`, `text_range`, `paint`, `effect`, `layout_grid`, or `explicit_mode` |
| `list_figma_team_library_assets`   | A list/import `operation` and its `collection_key` or asset `key`                                 |

Library operations are `list_variable_collections`, `list_variables`, `import_variable`,
`import_component`, `import_component_set`, and `import_style`. The manifest requests the
`teamlibrary` permission. Libraries must still be enabled in Figma's UI; the Plugin API cannot enable
them.

## Assets and export

| Tool                  | Input object                                                                         |
| --------------------- | ------------------------------------------------------------------------------------ |
| `create_figma_image`  | `data_base64` or public HTTP(S) `url`; private-network URLs are rejected             |
| `get_figma_image`     | `hash`                                                                               |
| `create_figma_media`  | `kind: "video"`, `data_base64`                                                       |
| `list_figma_shaders`  | No input to list; `import_id` to materialize one shader                              |
| `load_figma_brushes`  | `brush_type` (`STRETCH` or `SCATTER`)                                                |
| `export_figma_nodes`  | Up to 20 `node_ids`, optional Plugin API `settings` for PNG/JPG/SVG/PDF/JSON_REST_V1 |
| `encode_figma_binary` | `data_base64`, optional `operation: "inspect"` or `"normalize_base64"`               |

## Prototype, viewport, feedback, and file state

| Tool                            | Input object                                                                                               |
| ------------------------------- | ---------------------------------------------------------------------------------------------------------- |
| `update_figma_prototype`        | `items: [{ node_id, properties }]` for reactions, flows, overflow, and overlays                            |
| `get_figma_viewport`            | None                                                                                                       |
| `set_figma_viewport`            | `node_ids` or a `center` and/or `zoom`                                                                     |
| `notify_figma_user`             | `message`, optional `timeout_ms`, `error`                                                                  |
| `commit_figma_undo`             | Optional `operation` (`commit` or `undo`)                                                                  |
| `save_figma_version`            | `title`, optional `description`                                                                            |
| `get_figma_file_thumbnail_node` | None                                                                                                       |
| `set_figma_file_thumbnail_node` | Optional `node_id`; omit it to clear. Figma accepts frames, components, component sets, and sections only. |

## Plugin data, annotations, and development metadata

| Tool                               | Input object                                                                 |
| ---------------------------------- | ---------------------------------------------------------------------------- |
| `set_figma_plugin_data`            | `items` with `node_id`, private/shared entries, and optional `relaunch_data` |
| `list_figma_annotation_categories` | Optional `category_id`                                                       |
| `create_figma_annotation_category` | `label`, `color`                                                             |
| `set_figma_annotations`            | `items: [{ node_id, annotations }]`                                          |
| `manage_figma_measurements`        | `operation` (`list`, `add`, `edit`, `delete`) and operation fields           |
| `manage_figma_dev_resources`       | `node_id`, `operation` (`list`, `add`, `edit`, `delete`), URL/name fields    |
| `set_figma_dev_status`             | `node_id`, optional Plugin API `status`; omit status to read                 |

Development-resource previews are private/partner-only and are not exposed. Measurement mutations
are only available from Dev Mode; a Design connector returns `unsupported_in_editor` for them.

## Motion beta

| Tool                          | Input object                                                                                                      |
| ----------------------------- | ----------------------------------------------------------------------------------------------------------------- |
| `list_figma_animation_styles` | Optional `physical_spring` with mass, stiffness, and damping                                                      |
| `get_figma_motion`            | `node_ids`                                                                                                        |
| `update_figma_motion`         | `items` using `apply_style`, `remove_style`, `apply_manual_track`, `remove_manual_track`, `set_timeline_duration` |

Motion results include `beta: true`. Its payload schema follows the beta Plugin API and can change
with Figma releases independently of the stable bridge envelope.

## Deliberate exclusions

The connector does not expose raw JavaScript/eval, arbitrary `fetch`, plugin UI control,
`clientStorage`, payments, `openExternal`, private APIs, partner-only dev-resource previews, FigJam,
Slides, Buzz, codegen callbacks, parameter callbacks, or text-review mode. Those APIs either do not
operate on a Design document, belong to the connector implementation itself, require a distinct
plugin invocation mode, or would create an unsafe general-purpose execution/network surface.
