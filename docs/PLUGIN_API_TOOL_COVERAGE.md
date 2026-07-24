# Figma Plugin API coverage proposal

Research date: 2026-07-23

## Scope and source of truth

This proposal maps the public Figma Plugin API to an MCP surface for the local companion architecture.
The review used Figma's official Plugin API reference, including:

- [API reference overview](https://developers.figma.com/docs/plugins/api/api-reference/)
- [the `figma` global object](https://developers.figma.com/docs/plugins/api/figma/)
- [global objects](https://developers.figma.com/docs/plugins/api/global-objects/)
- [node types](https://developers.figma.com/docs/plugins/api/nodes/)
- [shared node properties](https://developers.figma.com/docs/plugins/api/node-properties/)
- [data types](https://developers.figma.com/docs/plugins/api/data-types/)
- [plugin manifest](https://developers.figma.com/docs/plugins/manifest/)
- [API errors](https://developers.figma.com/docs/plugins/api/api-errors/)
- every public sub-API linked from the global-object index: UI, utilities, constants, Motion,
  codegen, timer, viewport, client storage, parameters, text review, payments, variables, team
  library, annotations, and Buzz
- every node-type entry and the property/method families represented by the shared-property index

The official `@figma/plugin-typings` package is the machine-readable companion to the written
reference. Version `1.131.0`, currently resolved by this repository, was used to check that no public
interface family was omitted.

This is an API-family coverage proposal, not a recommendation to expose one MCP tool for every
TypeScript property. The Plugin API contains hundreds of node properties. A smaller set of
composable, typed, bounded tools gives complete practical coverage without producing an unusable MCP
tool list.

## Implementation status

The Figma Design portion of this proposal is implemented in connector version 0.2.0. The complete
registered catalog and input conventions are documented in [TOOLS.md](TOOLS.md).

The implementation intentionally excludes the editor-specific tools in section 11, the
invocation-driven/product-management APIs in section 12, private/partner-only APIs, and unrestricted
network or JavaScript execution. Measurement reads are available in Design; measurement mutations
return `unsupported_in_editor` because Figma exposes those writes only in Dev Mode.

## Design principles for the expanded surface

Every document-specific tool should keep requiring `connection_id`. In addition:

1. **Use stable composable tools.** Represent property reads and writes as typed projections and
   patches rather than generating hundreds of property-specific tools.
2. **Keep every read bounded.** Require explicit fields, depth, result limits, and continuation
   cursors. Return large exports and media as MCP resources or chunk handles, not inline tool JSON.
3. **Initialize change tracking completely.** Call `loadAllPagesAsync()` once before registering
   `documentchange`; Figma requires this in dynamic-page mode. Bridge requests wait for initialization
   so manual and connector-originated changes are both journaled.
4. **Serialize writes per connection.** Preserve the existing per-plugin request serialization and
   permit reads on other connections to proceed concurrently.
5. **Make writes auditable.** Mutating tools should support `dry_run`, `idempotency_key`, and a
   bounded batch. Report created/changed/deleted node IDs and warnings.
6. **Preserve Figma semantics.** Do not serialize raw Figma proxy objects. Project paints, effects,
   vectors, variables, styles, reactions, and other values to versioned DTOs.
7. **Advertise runtime capabilities.** The available API depends on editor type, mode, manifest
   permissions/capabilities, plugin publication status, and whether an API is public, private, or
   beta.
8. **Annotate MCP tools accurately.** Mark read-only tools with `readOnlyHint`; mark writes with
   `destructiveHint` or `idempotentHint` as appropriate.

## Proposed tool catalog

### 1. Discovery, capabilities, and document context

| Proposed tool                | Purpose                                                                                                                                                             |
| ---------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `get_figma_capabilities`     | Return editor type/mode, public API version, manifest permissions and capabilities, supported node types, optional sub-APIs, beta features, and write availability. |
| `get_figma_document`         | Read bounded document-level fields, page summaries, color profile, thumbnail node, current page, and optional user-safe editor context.                             |
| `list_figma_pages`           | Page/divider summaries with pagination and loaded-state information.                                                                                                |
| `load_figma_page`            | Explicitly load one page by ID for subsequent dynamic-page operations.                                                                                              |
| `get_figma_selection`        | Return a projected, bounded view of the current selection and selected text range.                                                                                  |
| `set_figma_selection`        | Select explicit node IDs and optionally focus/zoom them.                                                                                                            |
| `set_figma_current_page`     | Switch to one explicitly selected page through the async dynamic-page API.                                                                                          |
| `get_figma_document_changes` | Poll a bounded change journal by cursor, backed by `documentchange`, selection, page, and style events.                                                             |

This family covers document/root/current-page access, page loading, selection, editor/mode context,
node/style change events, and the public current-user/active-user fields when permissions and policy
allow them.

### 2. Scene-tree discovery and projected node reads

| Proposed tool             | Purpose                                                                                                                                                           |
| ------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `get_figma_nodes`         | Fetch one or more nodes by ID with an explicit field set and optional bounded child expansion.                                                                    |
| `query_figma_nodes`       | Search one loaded page or subtree using serializable criteria: node types, name, visibility, plugin-data key, component/instance identity, and bounded traversal. |
| `get_figma_node_css`      | Return `getCSSAsync()` output for explicit nodes.                                                                                                                 |
| `get_figma_node_geometry` | Read transforms, bounds, fills/strokes, vector paths/network, arc/point data, corner data, layout grids, constraints, and auto/grid layout fields.                |
| `get_figma_text`          | Read characters and requested text/range attributes or styled segments with pagination.                                                                           |
| `get_figma_components`    | Read component, component-set, instance, slot, variant, override, and component-property details.                                                                 |
| `get_figma_prototype`     | Read reactions, flows, overlays, transitions, connector endpoints, and prototype settings.                                                                        |
| `get_figma_plugin_data`   | Read this plugin's private/shared plugin data and relaunch data for explicit nodes/styles.                                                                        |
| `get_figma_dev_metadata`  | Read dev status, annotations, measurements, documentation links, and public dev resources when available.                                                         |

Together these tools cover all public node types in the reference: document/page; core Figma Design
shapes, frames, groups, sections, slices, vectors, text, components, instances, slots, and boolean
operations; FigJam code blocks, connectors, embeds, link previews, media, shapes with text, stamps,
stickies, tables, highlights, washi tape, and widgets; Slides nodes; Buzz-related nodes; and removed
nodes.

The generic projection schema should include the shared-property families from the official index:
identity/hierarchy, transforms/dimensions, visibility/locking, blend/effects, fills/strokes, corners,
layout/constraints/grids, export settings, prototype/reactions, variables, components, text, vectors,
motion, plugin/dev metadata, annotations, measurements, and editor-specific properties.

### 3. Core node creation, structure, and property mutation

| Proposed tool              | Purpose                                                                                                                                                        |
| -------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `create_figma_nodes`       | Create a bounded batch from discriminated DTOs for every node constructor supported by the active editor.                                                      |
| `update_figma_nodes`       | Apply typed property patches to explicit nodes, including transforms, layout, appearance, export settings, prototype data, and editor-specific properties.     |
| `clone_figma_nodes`        | Clone nodes into an optional target parent, optionally preserving their transforms between source and target anchors; return old-to-new mappings and geometry. |
| `move_figma_nodes`         | Reparent/reorder nodes using append/insert operations and explicit indices; return before/after local and absolute geometry.                                   |
| `delete_figma_nodes`       | Remove explicit nodes with dry-run support.                                                                                                                    |
| `resize_figma_nodes`       | Resize, resize without constraints, rescale, or lock/unlock aspect ratio.                                                                                      |
| `combine_figma_nodes`      | Group, transform-group, flatten, ungroup, combine as variants, or perform union/subtract/intersect/exclude boolean operations.                                 |
| `set_figma_vector_network` | Replace vector networks and related vector-specific data.                                                                                                      |

`create_figma_nodes` should discriminate constructors such as rectangle, line, ellipse, polygon,
star, vector, text, frame, component, page, page divider, slice, section, boolean operation, FigJam
nodes, Slides nodes, table, text path, and JSX/SVG-derived nodes. Unsupported constructors should
produce `unsupported_in_editor`, not a generic Plugin API exception.

### 4. Text and font operations

| Proposed tool            | Purpose                                                                                                                                                          |
| ------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `list_figma_fonts`       | List available fonts and report missing-font state.                                                                                                              |
| `update_figma_text`      | Insert/delete/replace text and update whole-node or range typography, lists, hyperlinks, fills, styles, decorations, OpenType, paragraph, and variable bindings. |
| `update_figma_text_path` | Update text-path content, alignment, vector path, and path-start data.                                                                                           |

The plugin should discover and load every required font before a write. Figma documents unloaded-font
writes as invalid operations, so font preparation belongs inside the text mutation boundary rather
than being left to an MCP caller to sequence correctly.

### 5. Components, instances, slots, and variants

| Proposed tool                     | Purpose                                                                                                               |
| --------------------------------- | --------------------------------------------------------------------------------------------------------------------- |
| `create_figma_component_instance` | Create an instance or create a component from an existing node.                                                       |
| `update_figma_component`          | Add/edit/delete component properties, descriptions, documentation links, variant metadata, and publishing visibility. |
| `update_figma_instance`           | Swap the main component, set properties, reset/remove overrides, expose nested instances, detach, or change scale.    |
| `update_figma_slot`               | Create/reset slots and inspect limit violations.                                                                      |
| `list_figma_component_instances`  | Return bounded local instance references for a component.                                                             |

### 6. Styles, variables, and libraries

| Proposed tool                      | Purpose                                                                                                                 |
| ---------------------------------- | ----------------------------------------------------------------------------------------------------------------------- |
| `list_figma_styles`                | List or fetch local paint, text, effect, and grid styles with pagination.                                               |
| `create_figma_style`               | Create any public local style type.                                                                                     |
| `update_figma_style`               | Rename, patch, bind variables, or change the style-specific value.                                                      |
| `delete_figma_style`               | Remove a local style.                                                                                                   |
| `reorder_figma_styles`             | Reorder styles or style folders.                                                                                        |
| `list_figma_style_consumers`       | Return bounded consumers for a style.                                                                                   |
| `list_figma_variables`             | List/fetch local variables and collections by ID/type with pagination.                                                  |
| `create_figma_variable_collection` | Create, extend, rename, and manage collection modes.                                                                    |
| `create_figma_variable`            | Create a variable or alias with explicit type, scopes, values, and code syntax.                                         |
| `update_figma_variable`            | Set/remove mode values and overrides, scopes, publishing state, description, and code syntax.                           |
| `delete_figma_variable`            | Remove a variable or collection.                                                                                        |
| `bind_figma_variable`              | Bind/unbind variables on nodes, text ranges, paints, effects, and layout grids; set explicit collection modes.          |
| `list_figma_team_library_assets`   | List enabled library variable collections/variables and import components, component sets, styles, or variables by key. |

The team-library tool must report that libraries can only be enabled through Figma's UI. The Plugin
API can inspect enabled libraries and import known assets; it cannot enable a library.

### 7. Images, media, shaders, brushes, and export

| Proposed tool          | Purpose                                                                                                                      |
| ---------------------- | ---------------------------------------------------------------------------------------------------------------------------- |
| `create_figma_image`   | Create an image from bounded bytes or a fetched URL and return its hash/size.                                                |
| `get_figma_image`      | Resolve an image hash and expose bytes through a bounded MCP resource.                                                       |
| `create_figma_media`   | Create supported video, GIF, link-preview, or editor-specific media nodes.                                                   |
| `list_figma_shaders`   | List available shaders and import one by ID.                                                                                 |
| `load_figma_brushes`   | Load a supported stretch/scatter brush family.                                                                               |
| `export_figma_nodes`   | Export explicit nodes as PNG/JPG/SVG/PDF/JSON_REST_V1 with scale/constraint options and resource handles for binary results. |
| `get_figma_screenshot` | Render one node as PNG and return it through a typed inline MCP image content block for direct visual inspection.            |
| `encode_figma_binary`  | Base64 encode/decode only if protocol clients cannot transport binary resource chunks directly.                              |

Arbitrary `figma.fetch` should not become a general network-proxy tool. URL-backed image/media
creation may use it internally subject to the manifest allowlist, maximum response size, MIME checks,
timeouts, and SSRF policy.

### 8. Prototyping, viewport, UI feedback, undo, and file state

| Proposed tool                   | Purpose                                                                              |
| ------------------------------- | ------------------------------------------------------------------------------------ |
| `update_figma_prototype`        | Set reactions, flows, overlay behavior, connector endpoints, and Slides transitions. |
| `get_figma_viewport`            | Read viewport center, zoom, bounds, and editor-specific grid/single view.            |
| `set_figma_viewport`            | Set center/zoom/view mode or scroll-and-zoom to explicit nodes.                      |
| `notify_figma_user`             | Show a bounded notification for user-visible agent feedback.                         |
| `commit_figma_undo`             | Commit an undo boundary or trigger undo.                                             |
| `save_figma_version`            | Save a named version-history checkpoint.                                             |
| `get_figma_file_thumbnail_node` | Read the current file-thumbnail node.                                                |
| `set_figma_file_thumbnail_node` | Set/clear the file-thumbnail node.                                                   |

`openExternal`, plugin UI show/hide/resize/reposition, UI messaging, and `closePlugin` should remain
connector implementation controls rather than general MCP tools. Exposing them adds no document
capability and lets an agent disrupt its own bridge. If remote UI control is a product requirement,
place it behind a separate `control_figma_connector_ui` tool with a narrow enum.

### 9. Plugin data, annotations, measurements, and development metadata

| Proposed tool                      | Purpose                                                                     |
| ---------------------------------- | --------------------------------------------------------------------------- |
| `set_figma_plugin_data`            | Set/delete this plugin's private/shared data and relaunch data.             |
| `list_figma_annotation_categories` | List/fetch annotation categories.                                           |
| `create_figma_annotation_category` | Add a category with label and color.                                        |
| `set_figma_annotations`            | Set annotations on supported nodes.                                         |
| `manage_figma_measurements`        | List/add/edit/delete measurements and cross-node measurements.              |
| `manage_figma_dev_resources`       | List/add/edit/delete resource links and previews when the API is available. |
| `set_figma_dev_status`             | Read or update supported dev status metadata.                               |

Some Dev Resources operations are private/partner-only. Capability discovery must distinguish a
missing public capability from a temporary runtime failure.

### 10. Motion

| Proposed tool                 | Purpose                                                                             |
| ----------------------------- | ----------------------------------------------------------------------------------- |
| `list_figma_animation_styles` | List available Motion animation styles and normalize spring parameters.             |
| `get_figma_motion`            | Read node animation styles, animations, manual keyframe tracks, and timelines.      |
| `update_figma_motion`         | Apply/remove animation styles, add/remove manual tracks, and set timeline duration. |

The official Motion API is beta. Its bridge DTOs should carry an independent schema version and the
server should advertise the feature as beta.

### 11. Editor-specific extensions

These tools should be registered only when at least one connected plugin can support the relevant
editor, or should fail with a precise capability error.

| Proposed tool              | Editor      | Coverage                                                                |
| -------------------------- | ----------- | ----------------------------------------------------------------------- |
| `get_figjam_timer`         | FigJam      | Remaining/total/state.                                                  |
| `control_figjam_timer`     | FigJam      | Start, pause, resume, and stop.                                         |
| `update_figjam_sticky`     | FigJam      | Sticky text/author visibility/width and stickable relations.            |
| `update_figjam_table`      | FigJam      | Cell access; insert/remove/move/resize rows and columns; cell text.     |
| `update_figjam_connector`  | FigJam      | Connector endpoints, caps, line type, text, and background.             |
| `update_figjam_widget`     | FigJam      | Clone widgets and get/set widget synchronized state.                    |
| `get_figma_canvas_grid`    | Slides/Buzz | Read the 2D canvas grid and focused node/slide.                         |
| `update_figma_canvas_grid` | Slides/Buzz | Set grid, create rows, and move nodes to coordinates.                   |
| `update_figma_slide`       | Slides      | Create slides/rows, transitions, skip state, and view mode.             |
| `create_figma_buzz_asset`  | Buzz        | Create Buzz frames/instances and assign asset types.                    |
| `get_figma_buzz_content`   | Buzz        | Extract text/media fields and asset type.                               |
| `update_figma_buzz_asset`  | Buzz        | Set asset type, smart-resize, text/media content, and canvas placement. |

### 12. Invocation-driven and product-management APIs

Several documented APIs are real Plugin API functionality but do not fit a request/response MCP
tool:

| API family                   | Treatment                                                                                                                                                                                                                                              |
| ---------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `figma.codegen`              | Requires a Dev Mode codegen plugin and Figma-owned `generate`/preferences callbacks with a 15-second contract. Implement as a separate codegen plugin mode, not an arbitrary MCP call. MCP may supply cached templates/configuration to that callback. |
| `figma.parameters`           | Handles Quick Actions input events during plugin launch. Keep in the plugin invocation layer, not the companion tool catalog.                                                                                                                          |
| `figma.textreview`           | Requires the text-review capability and user enable/disable flow. Implement only in a dedicated text-review plugin mode.                                                                                                                               |
| `figma.payments`             | Product monetization and user checkout, not document automation. Do not expose payment tokens or checkout as MCP tools.                                                                                                                                |
| `figma.clientStorage`        | Connector-local implementation state. Do not expose arbitrary read/write access; use it internally for port and future preferences.                                                                                                                    |
| `figma.ui` and `showUI`      | Connector transport/UI implementation. Do not expose raw message passing or arbitrary HTML.                                                                                                                                                            |
| `figma.util` and `constants` | Use internally while validating color/paint DTOs; optionally return supported palettes through `get_figma_capabilities`.                                                                                                                               |
| `openExternal`               | Do not expose by default because it causes a user-visible external side effect unrelated to document manipulation.                                                                                                                                     |
| global `fetch`               | Do not expose as an unrestricted network proxy. Use only inside bounded asset-import workflows.                                                                                                                                                        |

This treatment covers the functionality without pretending that an invocation callback, checkout
flow, local implementation store, or raw browser UI primitive is a useful agent tool.

## Manifest and deployment implications

The current manifest has `editorType: ["figma"]`, `documentAccess: "dynamic-page"`, and no proposed or
private APIs. It therefore cannot exercise all public editor-specific families.

Full public coverage requires one of these deployment models:

1. **Recommended: editor-specific connector variants sharing one bridge protocol.** Publish/build
   Design, FigJam, Slides, Buzz, Dev Mode codegen, and optional text-review variants. Each variant
   requests only its required permissions/capabilities.
2. **Single broad connector where Figma permits it.** Add every supported editor type and optional
   public capability to one manifest, then gate every call at runtime. This produces a larger
   permission surface and makes publication/review harder.

Permissions should be opt-in:

- `currentuser` only for a separately enabled user-context tool;
- `activeusers` only for FigJam collaboration context;
- `teamlibrary` for library variable discovery;
- `payments` should not be requested for this MCP connector;
- `codegen` and `textreview` only in dedicated invocation modes.

Do not enable `enablePrivatePluginApi`. Public coverage intentionally excludes `fileKey`, partner-only
Dev Resources behavior, VS Code partner integration, link-preview/auth partner modes, and any other
private Figma API. The server should report these as `private_api_unavailable`, not claim support.

## Bridge protocol changes

Protocol version 1 supports exactly `get_document_metadata`, has a 1 MiB message ceiling, and no
application chunking. The proposed surface needs a versioned protocol extension:

- Add a generic request envelope whose `method` is one of the bounded operations above.
- Define explicit MessagePack DTOs for every input/output; never pass arbitrary property names and
  values directly to a raw reflection/eval layer.
- Add response paging and continuation tokens for queries, styles, variables, instances, changes,
  and library results.
- Add resource/chunk transfer for exports, images, GIF/video input, SVG, and other large binary data.
- Keep per-message and per-operation byte/item/depth limits.
- Add cancellation and progress for expensive exports or traversals.
- Return typed errors such as `page_not_loaded`, `unsupported_in_editor`, `permission_required`,
  `font_load_failed`, `mixed_value`, `invalid_node_type`, `readonly_node`,
  `instance_override_forbidden`, `payload_too_large`, and `private_api_unavailable`.

A raw tool such as `execute_figma_javascript` must not be added. It would bypass schemas, limits,
write annotations, capability checks, audit logs, and the security boundary.

## Suggested implementation order

1. **Read foundation:** capabilities, pages, page loading, projected node reads, bounded queries,
   selection, CSS, and exports.
2. **Core Design writes:** create/update/move/clone/delete, text/font handling, grouping/boolean
   operations, undo boundaries, and viewport control.
3. **Design systems:** components/instances/slots, styles, variables, team libraries, annotations,
   measurements, and plugin data.
4. **Rich assets and prototypes:** images/media, vectors, shaders/brushes, reactions, flows, and
   version history.
5. **Motion beta.**
6. **Separate editor variants:** FigJam, Slides, Buzz, and Dev Mode/codegen.

The first three phases provide the highest-value Figma Design automation while preserving the
bounded, explicit-target architecture established by the current specification.

## Coverage conclusion

The catalog above accounts for every public Plugin API family in the official reference while
avoiding a one-tool-per-property explosion. Practical full coverage is approximately a few dozen
composable MCP tools plus editor-specific variants, not hundreds of thin setters.

The most important architectural decision is to separate:

- public document/editor operations that should be MCP tools;
- invocation-driven APIs that require a dedicated plugin mode;
- connector implementation primitives that should stay internal; and
- private/partner APIs that this unofficial connector must not enable.

With that separation, the server can cover the complete public, automatable Plugin API surface
without weakening the existing explicit-connection, bounded-response, dynamic-page, or public-API
constraints.
