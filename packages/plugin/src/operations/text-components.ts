import {
  asApiRecord,
  asRecord,
  callApiMethod,
  idempotentMutation,
  limit,
  nodeSummary,
  objectArray,
  OperationError,
  optionalBoolean,
  optionalNumber,
  optionalString,
  requiredString,
  requireNode,
  requireSceneNode,
  RpcHandler,
  RpcPayload,
  RpcResult,
  toSerializable,
} from "./shared";

const TEXT_RANGE_METHODS: Record<string, string> = {
  font_name: "setRangeFontName",
  font_size: "setRangeFontSize",
  text_case: "setRangeTextCase",
  text_decoration: "setRangeTextDecoration",
  letter_spacing: "setRangeLetterSpacing",
  line_height: "setRangeLineHeight",
  hyperlink: "setRangeHyperlink",
  fills: "setRangeFills",
  list_options: "setRangeListOptions",
  indentation: "setRangeIndentation",
  paragraph_indent: "setRangeParagraphIndent",
  paragraph_spacing: "setRangeParagraphSpacing",
  open_type_features: "setRangeOpenTypeFeatures",
};

export const textComponentHandlers: Record<string, RpcHandler> = {
  list_figma_fonts: listFonts,
  update_figma_text: (payload) =>
    idempotentMutation("update_figma_text", payload, () => updateText(payload, false)),
  update_figma_text_path: (payload) =>
    idempotentMutation("update_figma_text_path", payload, () => updateText(payload, true)),
  create_figma_component_instance: (payload) =>
    idempotentMutation("create_figma_component_instance", payload, () =>
      createComponentInstance(payload),
    ),
  update_figma_component: (payload) =>
    idempotentMutation("update_figma_component", payload, () => updateComponent(payload)),
  update_figma_instance: (payload) =>
    idempotentMutation("update_figma_instance", payload, () => updateInstance(payload)),
  update_figma_slot: (payload) =>
    idempotentMutation("update_figma_slot", payload, () => updateSlot(payload)),
  list_figma_component_instances: listComponentInstances,
};

async function listFonts(payload: RpcPayload): Promise<RpcResult> {
  const fonts = await figma.listAvailableFontsAsync();
  const resultLimit = limit(payload, 500, 5_000);
  const cursor = optionalNumber(payload, "cursor", { integer: true, min: 0 }) ?? 0;
  const family = optionalString(payload, "family")?.toLocaleLowerCase();
  const filtered = family
    ? fonts.filter((font) => font.fontName.family.toLocaleLowerCase().includes(family))
    : fonts;
  const page = filtered.slice(cursor, cursor + resultLimit);
  return {
    fonts: page.map((font) => ({ font_name: font.fontName })),
    next_cursor: cursor + page.length < filtered.length ? cursor + page.length : null,
    total: filtered.length,
  };
}

async function updateText(payload: RpcPayload, requireTextPath: boolean): Promise<RpcResult> {
  const items = objectArray(payload, "items", { required: true, maxItems: 100 });
  if (optionalBoolean(payload, "dry_run") ?? false) {
    return { dry_run: true, would_update: items };
  }
  const changed: RpcResult[] = [];
  for (const item of items) {
    const node = await requireNode(requiredString(item, "node_id"));
    if (
      (requireTextPath && node.type !== "TEXT_PATH") ||
      (!requireTextPath && node.type !== "TEXT" && node.type !== "TEXT_PATH")
    ) {
      throw new OperationError(
        "invalid_node_type",
        `${node.id} is not a ${requireTextPath ? "text-path" : "text"} node.`,
      );
    }
    const textNode = node as TextNode | TextPathNode;

    await loadCurrentFonts(textNode);
    for (const font of objectArray(item, "font_names", { maxItems: 100 })) {
      await figma.loadFontAsync({
        family: requiredString(font, "family"),
        style: requiredString(font, "style"),
      });
    }

    const operation = optionalString(item, "operation") ?? "replace";
    const start =
      optionalNumber(item, "start", {
        integer: true,
        min: 0,
        max: textNode.characters.length,
      }) ?? 0;
    const end =
      optionalNumber(item, "end", {
        integer: true,
        min: start,
        max: textNode.characters.length,
      }) ?? textNode.characters.length;
    const characters = optionalString(item, "characters") ?? "";
    if (operation === "replace") {
      textNode.deleteCharacters(start, end);
      textNode.insertCharacters(start, characters, "BEFORE");
    } else if (operation === "insert") {
      textNode.insertCharacters(start, characters, "BEFORE");
    } else if (operation === "delete") {
      textNode.deleteCharacters(start, end);
    } else if (operation === "set_all") {
      textNode.characters = characters;
    } else if (operation !== "format") {
      throw new OperationError("invalid_argument", `Unknown text operation ${operation}.`);
    }

    const rangeEnd = Math.min(
      optionalNumber(item, "format_end", { integer: true, min: start }) ??
        (operation === "insert" || operation === "replace"
          ? start + characters.length
          : textNode.characters.length),
      textNode.characters.length,
    );
    const properties = item.properties ? asRecord(item.properties, "properties") : {};
    await applyTextProperties(textNode, start, rangeEnd, properties);

    if (node.type === "TEXT_PATH") {
      const path = asApiRecord(node);
      for (const [input, property] of [
        ["path_alignment", "pathAlignment"],
        ["paragraph_spacing", "paragraphSpacing"],
        ["paragraph_indent", "paragraphIndent"],
      ]) {
        if (item[input] !== undefined && property in path) {
          path[property] = item[input];
        }
      }
    }

    changed.push({
      ...nodeSummary(node),
      characters: textNode.characters,
      has_missing_font: textNode.hasMissingFont,
    });
  }
  return { changed };
}

async function applyTextProperties(
  node: TextNode | TextPathNode,
  start: number,
  end: number,
  properties: RpcPayload,
): Promise<void> {
  for (const [input, value] of Object.entries(properties)) {
    const method = TEXT_RANGE_METHODS[input];
    if (!method) {
      throw new OperationError("invalid_argument", `Text property ${input} is not writable.`);
    }
    if (input === "font_name") {
      const font = asRecord(value, "font_name");
      const fontName: FontName = {
        family: requiredString(font, "family"),
        style: requiredString(font, "style"),
      };
      await figma.loadFontAsync(fontName);
      await callApiMethod(node, method, [start, end, fontName]);
    } else {
      await callApiMethod(node, method, [start, end, value]);
    }
  }
}

async function loadCurrentFonts(node: TextNode | TextPathNode): Promise<void> {
  const fonts = node.getRangeAllFontNames(0, node.characters.length);
  for (const font of fonts) {
    await figma.loadFontAsync(font);
  }
  if (fonts.length === 0 && node.fontName !== figma.mixed) {
    await figma.loadFontAsync(node.fontName);
  }
}

async function createComponentInstance(payload: RpcPayload): Promise<RpcResult> {
  const operation = optionalString(payload, "operation") ?? "create_instance";
  if (optionalBoolean(payload, "dry_run") ?? false) {
    return { dry_run: true, operation };
  }
  if (operation === "create_instance") {
    const component = await requireNode(requiredString(payload, "component_id"));
    if (component.type !== "COMPONENT") {
      throw new OperationError("invalid_node_type", "component_id must refer to a component.");
    }
    const instance = component.createInstance();
    const parentId = optionalString(payload, "parent_id");
    if (parentId) {
      await reparent(
        instance,
        parentId,
        optionalNumber(payload, "index", { integer: true, min: 0 }),
      );
    }
    return { instance: nodeSummary(instance) };
  }
  if (operation === "component_from_node") {
    const source = await requireSceneNode(requiredString(payload, "node_id"));
    return { component: nodeSummary(figma.createComponentFromNode(source)) };
  }
  throw new OperationError(
    "invalid_argument",
    `Unknown component creation operation ${operation}.`,
  );
}

async function updateComponent(payload: RpcPayload): Promise<RpcResult> {
  const items = objectArray(payload, "items", { required: true, maxItems: 100 });
  if (optionalBoolean(payload, "dry_run") ?? false) {
    return { dry_run: true, would_update: items };
  }
  const changed: RpcResult[] = [];
  for (const item of items) {
    const node = await requireNode(requiredString(item, "node_id"));
    if (node.type !== "COMPONENT" && node.type !== "COMPONENT_SET") {
      throw new OperationError("invalid_node_type", `${node.id} is not a component.`);
    }
    const api = asApiRecord(node);
    for (const field of ["name", "description", "documentationLinks"]) {
      const input = toSnakeCase(field);
      if (item[input] !== undefined && field in api) {
        api[field] = item[input];
      }
    }
    for (const action of objectArray(item, "property_actions", { maxItems: 100 })) {
      const operation = requiredString(action, "operation");
      if (operation === "add") {
        const options = action.options ? asRecord(action.options, "options") : undefined;
        await callApiMethod(node, "addComponentProperty", [
          requiredString(action, "name"),
          requiredString(action, "type"),
          action.default_value,
          options,
        ]);
      } else if (operation === "edit") {
        await callApiMethod(node, "editComponentProperty", [
          requiredString(action, "name"),
          asRecord(action.patch, "patch"),
        ]);
      } else if (operation === "delete") {
        await callApiMethod(node, "deleteComponentProperty", [requiredString(action, "name")]);
      } else {
        throw new OperationError(
          "invalid_argument",
          `Unknown component property action ${operation}.`,
        );
      }
    }
    changed.push({
      ...nodeSummary(node),
      component_property_definitions: toSerializable(api.componentPropertyDefinitions),
    });
  }
  return { changed };
}

async function updateInstance(payload: RpcPayload): Promise<RpcResult> {
  const items = objectArray(payload, "items", { required: true, maxItems: 100 });
  if (optionalBoolean(payload, "dry_run") ?? false) {
    return { dry_run: true, would_update: items };
  }
  const changed: RpcResult[] = [];
  for (const item of items) {
    const node = await requireNode(requiredString(item, "node_id"));
    if (node.type !== "INSTANCE") {
      throw new OperationError("invalid_node_type", `${node.id} is not an instance.`);
    }
    const operation = requiredString(item, "operation");
    if (operation === "swap_component" || operation === "set_main_component") {
      const component = await requireNode(requiredString(item, "component_id"));
      if (component.type !== "COMPONENT") {
        throw new OperationError("invalid_node_type", "component_id must refer to a component.");
      }
      if (operation === "swap_component") {
        node.swapComponent(component);
      } else {
        node.mainComponent = component;
      }
    } else if (operation === "set_properties") {
      node.setProperties(asRecord(item.properties, "properties") as never);
    } else if (operation === "remove_overrides") {
      node.removeOverrides();
    } else if (operation === "detach") {
      const frame = node.detachInstance();
      changed.push({ detached_from: node.id, frame: nodeSummary(frame) });
      continue;
    } else if (operation === "set_scale") {
      node.scaleFactor =
        optionalNumber(item, "scale_factor", { min: 0.0001, max: 10_000 }) ?? node.scaleFactor;
    } else if (operation === "set_exposed") {
      node.isExposedInstance = optionalBoolean(item, "is_exposed") ?? false;
    } else {
      throw new OperationError("invalid_argument", `Unknown instance operation ${operation}.`);
    }
    changed.push({
      ...nodeSummary(node),
      component_properties: toSerializable(node.componentProperties),
      overrides: toSerializable(node.overrides),
    });
  }
  return { changed };
}

async function updateSlot(payload: RpcPayload): Promise<RpcResult> {
  const operation = requiredString(payload, "operation");
  if (optionalBoolean(payload, "dry_run") ?? false) {
    return { dry_run: true, operation };
  }
  if (operation === "create") {
    const component = await requireNode(requiredString(payload, "component_id"));
    if (component.type !== "COMPONENT") {
      throw new OperationError("invalid_node_type", "component_id must refer to a component.");
    }
    const slot = component.createSlot();
    return { slot: nodeSummary(slot), limit_violations: slot.limitViolations };
  }
  const slot = await requireNode(requiredString(payload, "slot_id"));
  if (slot.type !== "SLOT") {
    throw new OperationError("invalid_node_type", "slot_id must refer to a slot.");
  }
  if (operation === "reset") {
    slot.resetSlot();
  } else if (operation !== "inspect") {
    throw new OperationError("invalid_argument", `Unknown slot operation ${operation}.`);
  }
  return { slot: nodeSummary(slot), limit_violations: slot.limitViolations };
}

async function listComponentInstances(payload: RpcPayload): Promise<RpcResult> {
  const component = await requireNode(requiredString(payload, "component_id"));
  if (component.type !== "COMPONENT") {
    throw new OperationError("invalid_node_type", "component_id must refer to a component.");
  }
  const instances = await component.getInstancesAsync();
  const resultLimit = limit(payload, 100, 500);
  const cursor = optionalNumber(payload, "cursor", { integer: true, min: 0 }) ?? 0;
  const page = instances.slice(cursor, cursor + resultLimit);
  return {
    instances: page.map(nodeSummary),
    next_cursor: cursor + page.length < instances.length ? cursor + page.length : null,
    total: instances.length,
  };
}

async function reparent(node: SceneNode, parentId: string, index?: number): Promise<void> {
  const parent = await requireNode(parentId);
  const api = asApiRecord(parent);
  if (typeof api.appendChild !== "function" || typeof api.insertChild !== "function") {
    throw new OperationError("invalid_node_type", `${parentId} cannot contain scene nodes.`);
  }
  if (index === undefined) {
    Reflect.apply(api.appendChild, parent, [node]);
  } else {
    const children = api.children as readonly SceneNode[];
    Reflect.apply(api.insertChild, parent, [Math.min(index, children.length), node]);
  }
}

function toSnakeCase(value: string): string {
  return value.replace(/[A-Z]/g, (character) => `_${character.toLowerCase()}`);
}
