import {
  asApiRecord,
  callApiMethod,
  idempotentMutation,
  limit,
  objectArray,
  OperationError,
  optionalBoolean,
  optionalNumber,
  optionalString,
  requiredString,
  requireNode,
  RpcHandler,
  RpcPayload,
  RpcResult,
  stringArray,
  toSerializable,
} from "./shared";

type StyleKind = "paint" | "text" | "effect" | "grid";

const STYLE_APIS: Record<
  StyleKind,
  { list: string; create: string; moveStyle: string; moveFolder: string }
> = {
  paint: {
    list: "getLocalPaintStylesAsync",
    create: "createPaintStyle",
    moveStyle: "moveLocalPaintStyleAfter",
    moveFolder: "moveLocalPaintFolderAfter",
  },
  text: {
    list: "getLocalTextStylesAsync",
    create: "createTextStyle",
    moveStyle: "moveLocalTextStyleAfter",
    moveFolder: "moveLocalTextFolderAfter",
  },
  effect: {
    list: "getLocalEffectStylesAsync",
    create: "createEffectStyle",
    moveStyle: "moveLocalEffectStyleAfter",
    moveFolder: "moveLocalEffectFolderAfter",
  },
  grid: {
    list: "getLocalGridStylesAsync",
    create: "createGridStyle",
    moveStyle: "moveLocalGridStyleAfter",
    moveFolder: "moveLocalGridFolderAfter",
  },
};

export const styleVariableHandlers: Record<string, RpcHandler> = {
  list_figma_styles: listStyles,
  create_figma_style: (payload) =>
    idempotentMutation("create_figma_style", payload, () => createStyle(payload)),
  update_figma_style: (payload) =>
    idempotentMutation("update_figma_style", payload, () => updateStyle(payload)),
  delete_figma_style: (payload) =>
    idempotentMutation("delete_figma_style", payload, () => deleteStyle(payload)),
  reorder_figma_styles: (payload) =>
    idempotentMutation("reorder_figma_styles", payload, () => reorderStyles(payload)),
  list_figma_style_consumers: listStyleConsumers,
  list_figma_variables: listVariables,
  create_figma_variable_collection: (payload) =>
    idempotentMutation("create_figma_variable_collection", payload, () =>
      createVariableCollection(payload),
    ),
  create_figma_variable: (payload) =>
    idempotentMutation("create_figma_variable", payload, () => createVariable(payload)),
  update_figma_variable: (payload) =>
    idempotentMutation("update_figma_variable", payload, () => updateVariable(payload)),
  delete_figma_variable: (payload) =>
    idempotentMutation("delete_figma_variable", payload, () => deleteVariable(payload)),
  bind_figma_variable: (payload) =>
    idempotentMutation("bind_figma_variable", payload, () => bindVariable(payload)),
  list_figma_team_library_assets: listTeamLibraryAssets,
};

async function listStyles(payload: RpcPayload): Promise<RpcResult> {
  const kinds = requestedStyleKinds(payload);
  const resultLimit = limit(payload, 100, 500);
  const cursor = optionalNumber(payload, "cursor", { integer: true, min: 0 }) ?? 0;
  const all: RpcResult[] = [];
  for (const kind of kinds) {
    const styles = (await callApiMethod(figma, STYLE_APIS[kind].list)) as BaseStyle[];
    all.push(...styles.map((style) => projectStyle(kind, style)));
  }
  const page = all.slice(cursor, cursor + resultLimit);
  return {
    styles: page,
    next_cursor: cursor + page.length < all.length ? cursor + page.length : null,
    total: all.length,
  };
}

async function createStyle(payload: RpcPayload): Promise<RpcResult> {
  const kind = styleKind(payload);
  if (optionalBoolean(payload, "dry_run") ?? false) {
    return { dry_run: true, kind, name: requiredString(payload, "name") };
  }
  const style = (await callApiMethod(figma, STYLE_APIS[kind].create)) as BaseStyle;
  style.name = requiredString(payload, "name");
  if (payload.description !== undefined) {
    style.description = requiredString(payload, "description");
  }
  await applyStyleValue(kind, style, payload);
  return { style: projectStyle(kind, style) };
}

async function updateStyle(payload: RpcPayload): Promise<RpcResult> {
  const style = await requireStyle(requiredString(payload, "style_id"));
  const kind = kindForStyle(style);
  if (optionalBoolean(payload, "dry_run") ?? false) {
    return { dry_run: true, style_id: style.id, kind };
  }
  if (payload.name !== undefined) {
    style.name = requiredString(payload, "name");
  }
  if (payload.description !== undefined) {
    style.description = requiredString(payload, "description");
  }
  await applyStyleValue(kind, style, payload);
  return { style: projectStyle(kind, style) };
}

async function deleteStyle(payload: RpcPayload): Promise<RpcResult> {
  const ids = stringArray(payload, "style_ids", { required: true, maxItems: 100 });
  const styles = await Promise.all(ids.map(requireStyle));
  if (optionalBoolean(payload, "dry_run") ?? false) {
    return { dry_run: true, would_delete: styles.map((style) => style.id) };
  }
  styles.forEach((style) => style.remove());
  return { deleted_style_ids: ids };
}

async function reorderStyles(payload: RpcPayload): Promise<RpcResult> {
  const kind = styleKind(payload);
  const operation = optionalString(payload, "operation") ?? "style";
  if (optionalBoolean(payload, "dry_run") ?? false) {
    return { dry_run: true, kind, operation };
  }
  if (operation === "style") {
    const target = await requireStyle(requiredString(payload, "target_style_id"));
    const referenceId = optionalString(payload, "reference_style_id");
    const reference = referenceId ? await requireStyle(referenceId) : null;
    await callApiMethod(figma, STYLE_APIS[kind].moveStyle, [target, reference]);
  } else if (operation === "folder") {
    await callApiMethod(figma, STYLE_APIS[kind].moveFolder, [
      requiredString(payload, "target_folder"),
      optionalString(payload, "reference_folder") ?? null,
    ]);
  } else {
    throw new OperationError("invalid_argument", `Unknown reorder operation ${operation}.`);
  }
  return { reordered: true, kind, operation };
}

async function listStyleConsumers(payload: RpcPayload): Promise<RpcResult> {
  const style = await requireStyle(requiredString(payload, "style_id"));
  const consumers = (await style.getStyleConsumersAsync()) as unknown[];
  const resultLimit = limit(payload, 100, 500);
  const cursor = optionalNumber(payload, "cursor", { integer: true, min: 0 }) ?? 0;
  const page = consumers.slice(cursor, cursor + resultLimit);
  return {
    style_id: style.id,
    consumers: toSerializable(page),
    next_cursor: cursor + page.length < consumers.length ? cursor + page.length : null,
    total: consumers.length,
  };
}

async function listVariables(payload: RpcPayload): Promise<RpcResult> {
  const resolvedType = optionalString(payload, "resolved_type") as
    VariableResolvedDataType | undefined;
  const variables = await figma.variables.getLocalVariablesAsync(resolvedType);
  const collections = await figma.variables.getLocalVariableCollectionsAsync();
  const resultLimit = limit(payload, 100, 500);
  const cursor = optionalNumber(payload, "cursor", { integer: true, min: 0 }) ?? 0;
  const page = variables.slice(cursor, cursor + resultLimit);
  return {
    variables: page.map(projectVariable),
    collections: collections.map(projectCollection),
    next_cursor: cursor + page.length < variables.length ? cursor + page.length : null,
    total: variables.length,
  };
}

async function createVariableCollection(payload: RpcPayload): Promise<RpcResult> {
  if (optionalBoolean(payload, "dry_run") ?? false) {
    return { dry_run: true, name: requiredString(payload, "name") };
  }
  const extendKey = optionalString(payload, "extend_collection_key");
  const collection = extendKey
    ? await figma.variables.extendLibraryCollectionByKeyAsync(
        extendKey,
        requiredString(payload, "name"),
      )
    : figma.variables.createVariableCollection(requiredString(payload, "name"));
  if (payload.hidden_from_publishing !== undefined) {
    collection.hiddenFromPublishing = optionalBoolean(payload, "hidden_from_publishing") ?? false;
  }
  const modeActions = objectArray(payload, "mode_actions", { maxItems: 100 });
  if (collection.isExtension && modeActions.length > 0) {
    throw new OperationError(
      "invalid_argument",
      "Extended collections cannot add or rename inherited modes.",
    );
  }
  for (const action of modeActions) {
    applyModeAction(collection as VariableCollection, action);
  }
  return { collection: projectCollection(collection) };
}

async function createVariable(payload: RpcPayload): Promise<RpcResult> {
  const collection = await requireCollection(requiredString(payload, "collection_id"));
  if (optionalBoolean(payload, "dry_run") ?? false) {
    return {
      dry_run: true,
      name: requiredString(payload, "name"),
      collection_id: collection.id,
    };
  }
  const variable = figma.variables.createVariable(
    requiredString(payload, "name"),
    collection,
    requiredString(payload, "resolved_type") as VariableResolvedDataType,
  );
  await patchVariable(variable, payload);
  return { variable: projectVariable(variable) };
}

async function updateVariable(payload: RpcPayload): Promise<RpcResult> {
  const variable = await requireVariable(requiredString(payload, "variable_id"));
  if (optionalBoolean(payload, "dry_run") ?? false) {
    return { dry_run: true, variable_id: variable.id };
  }
  await patchVariable(variable, payload);
  return { variable: projectVariable(variable) };
}

async function patchVariable(variable: Variable, payload: RpcPayload): Promise<void> {
  if (payload.name !== undefined) {
    variable.name = requiredString(payload, "name");
  }
  if (payload.description !== undefined) {
    variable.description = requiredString(payload, "description");
  }
  if (payload.hidden_from_publishing !== undefined) {
    variable.hiddenFromPublishing = optionalBoolean(payload, "hidden_from_publishing") ?? false;
  }
  if (payload.scopes !== undefined) {
    variable.scopes = stringArray(payload, "scopes", { maxItems: 50 }) as VariableScope[];
  }
  for (const value of objectArray(payload, "values", { maxItems: 100 })) {
    let nextValue = value.value as VariableValue;
    const aliasVariableId = optionalString(value, "alias_variable_id");
    if (aliasVariableId) {
      nextValue = figma.variables.createVariableAlias(await requireVariable(aliasVariableId));
    }
    variable.setValueForMode(requiredString(value, "mode_id"), nextValue);
  }
  for (const platform of objectArray(payload, "code_syntax", { maxItems: 3 })) {
    const value = optionalString(platform, "value");
    const key = requiredString(platform, "platform") as CodeSyntaxPlatform;
    if (value === undefined || value === "") {
      variable.removeVariableCodeSyntax(key);
    } else {
      variable.setVariableCodeSyntax(key, value);
    }
  }
  for (const modeId of stringArray(payload, "remove_override_mode_ids", { maxItems: 100 })) {
    variable.removeOverrideForMode(modeId);
  }
}

async function deleteVariable(payload: RpcPayload): Promise<RpcResult> {
  const variableIds = stringArray(payload, "variable_ids", { maxItems: 100 });
  const collectionIds = stringArray(payload, "collection_ids", { maxItems: 100 });
  if (variableIds.length + collectionIds.length === 0) {
    throw new OperationError("invalid_argument", "variable_ids or collection_ids is required.");
  }
  if (optionalBoolean(payload, "dry_run") ?? false) {
    return { dry_run: true, variable_ids: variableIds, collection_ids: collectionIds };
  }
  for (const id of variableIds) {
    (await requireVariable(id)).remove();
  }
  for (const id of collectionIds) {
    (await requireCollection(id)).remove();
  }
  return { deleted_variable_ids: variableIds, deleted_collection_ids: collectionIds };
}

async function bindVariable(payload: RpcPayload): Promise<RpcResult> {
  const bindings = objectArray(payload, "bindings", { required: true, maxItems: 100 });
  if (optionalBoolean(payload, "dry_run") ?? false) {
    return { dry_run: true, bindings };
  }
  const changed: RpcResult[] = [];
  for (const binding of bindings) {
    const node = await requireNode(requiredString(binding, "node_id"));
    if (node.type === "DOCUMENT" || node.type === "PAGE") {
      throw new OperationError("invalid_node_type", `${node.id} cannot consume variables.`);
    }
    const operation = optionalString(binding, "operation") ?? "node_field";
    if (operation === "explicit_mode") {
      const collection = await requireCollection(requiredString(binding, "collection_id"));
      const modeId = optionalString(binding, "mode_id");
      if (modeId) {
        node.setExplicitVariableModeForCollection(collection, modeId);
      } else {
        node.clearExplicitVariableModeForCollection(collection);
      }
    } else if (operation === "text_range") {
      const variableId = optionalString(binding, "variable_id");
      const variable = variableId ? await requireVariable(variableId) : null;
      await callApiMethod(node, "setRangeBoundVariable", [
        optionalNumber(binding, "start", { integer: true, min: 0 }) ?? 0,
        optionalNumber(binding, "end", { integer: true, min: 0 }) ?? 0,
        requiredString(binding, "field"),
        variable,
      ]);
    } else if (operation === "node_field") {
      const variableId = optionalString(binding, "variable_id");
      const variable = variableId ? await requireVariable(variableId) : null;
      await callApiMethod(node, "setBoundVariable", [requiredString(binding, "field"), variable]);
    } else if (operation === "paint" || operation === "effect" || operation === "layout_grid") {
      const property =
        operation === "paint" ? "fills" : operation === "effect" ? "effects" : "layoutGrids";
      const values = asApiRecord(node)[property];
      if (!Array.isArray(values)) {
        throw new OperationError("invalid_node_type", `${node.id} has no ${property}.`);
      }
      const index =
        optionalNumber(binding, "index", {
          integer: true,
          min: 0,
          max: values.length - 1,
        }) ?? 0;
      const variableId = optionalString(binding, "variable_id");
      const variable = variableId ? await requireVariable(variableId) : null;
      const helper =
        operation === "paint"
          ? "setBoundVariableForPaint"
          : operation === "effect"
            ? "setBoundVariableForEffect"
            : "setBoundVariableForLayoutGrid";
      const copy = await callApiMethod(figma.variables, helper, [
        values[index],
        requiredString(binding, "field"),
        variable,
      ]);
      const next = [...values];
      next[index] = copy;
      asApiRecord(node)[property] = next;
    } else {
      throw new OperationError("invalid_argument", `Unknown binding operation ${operation}.`);
    }
    changed.push({ node_id: node.id, operation });
  }
  return { changed };
}

async function listTeamLibraryAssets(payload: RpcPayload): Promise<RpcResult> {
  const operation = optionalString(payload, "operation") ?? "list_variable_collections";
  if (operation === "list_variable_collections") {
    return {
      collections: toSerializable(
        await figma.teamLibrary.getAvailableLibraryVariableCollectionsAsync(),
      ),
      note: "Libraries must be enabled in the Figma UI before they are available here.",
    };
  }
  if (operation === "list_variables") {
    return {
      variables: toSerializable(
        await figma.teamLibrary.getVariablesInLibraryCollectionAsync(
          requiredString(payload, "collection_key"),
        ),
      ),
    };
  }
  const key = requiredString(payload, "key");
  if (operation === "import_variable") {
    return { variable: projectVariable(await figma.variables.importVariableByKeyAsync(key)) };
  }
  if (operation === "import_component") {
    const node = await figma.importComponentByKeyAsync(key);
    return { component: { id: node.id, name: node.name, key: node.key } };
  }
  if (operation === "import_component_set") {
    const node = await figma.importComponentSetByKeyAsync(key);
    return { component_set: { id: node.id, name: node.name, key: node.key } };
  }
  if (operation === "import_style") {
    const style = await figma.importStyleByKeyAsync(key);
    return { style: projectStyle(kindForStyle(style), style) };
  }
  throw new OperationError("invalid_argument", `Unknown team-library operation ${operation}.`);
}

async function applyStyleValue(
  kind: StyleKind,
  style: BaseStyle,
  payload: RpcPayload,
): Promise<void> {
  const api = asApiRecord(style);
  const fields: Record<StyleKind, string[]> = {
    paint: ["paints"],
    text: [
      "fontSize",
      "fontName",
      "textDecoration",
      "letterSpacing",
      "lineHeight",
      "paragraphIndent",
      "paragraphSpacing",
      "textCase",
      "leadingTrim",
    ],
    effect: ["effects"],
    grid: ["layoutGrids"],
  };
  for (const property of fields[kind]) {
    const input = property.replace(/[A-Z]/g, (character) => `_${character.toLowerCase()}`);
    if (payload[input] !== undefined && property in api) {
      if (property === "fontName") {
        const font = payload[input] as FontName;
        await figma.loadFontAsync(font);
      }
      api[property] = payload[input];
    }
  }
}

function projectStyle(kind: StyleKind, style: BaseStyle): RpcResult {
  const api = asApiRecord(style);
  const result: RpcResult = {
    id: style.id,
    key: style.key,
    name: style.name,
    description: style.description,
    remote: style.remote,
    kind,
  };
  for (const property of [
    "paints",
    "effects",
    "layoutGrids",
    "fontSize",
    "fontName",
    "textDecoration",
    "letterSpacing",
    "lineHeight",
    "paragraphIndent",
    "paragraphSpacing",
    "textCase",
    "leadingTrim",
    "boundVariables",
  ]) {
    if (property in api) {
      result[property.replace(/[A-Z]/g, (character) => `_${character.toLowerCase()}`)] =
        toSerializable(api[property]);
    }
  }
  return result;
}

function projectVariable(variable: Variable): RpcResult {
  return {
    id: variable.id,
    key: variable.key,
    name: variable.name,
    description: variable.description,
    remote: variable.remote,
    resolved_type: variable.resolvedType,
    collection_id: variable.variableCollectionId,
    hidden_from_publishing: variable.hiddenFromPublishing,
    scopes: variable.scopes,
    code_syntax: variable.codeSyntax,
    values_by_mode: toSerializable(variable.valuesByMode),
  };
}

function projectCollection(collection: VariableCollection | ExtendedVariableCollection): RpcResult {
  const api = asApiRecord(collection);
  return {
    id: collection.id,
    key: collection.key,
    name: collection.name,
    remote: collection.remote,
    hidden_from_publishing: collection.hiddenFromPublishing,
    default_mode_id: collection.defaultModeId,
    modes: toSerializable(collection.modes),
    variable_ids: collection.variableIds,
    is_extension: collection.isExtension,
    parent_collection_id: api.parentVariableCollectionId,
    root_collection_id: api.rootVariableCollectionId,
  };
}

function applyModeAction(collection: VariableCollection, action: RpcPayload): void {
  const operation = requiredString(action, "operation");
  if (operation === "add") {
    collection.addMode(requiredString(action, "name"));
  } else if (operation === "rename") {
    collection.renameMode(requiredString(action, "mode_id"), requiredString(action, "name"));
  } else if (operation === "remove") {
    collection.removeMode(requiredString(action, "mode_id"));
  } else {
    throw new OperationError("invalid_argument", `Unknown mode action ${operation}.`);
  }
}

async function requireStyle(id: string): Promise<BaseStyle> {
  const style = await figma.getStyleByIdAsync(id);
  if (!style) {
    throw new OperationError("style_not_found", `No local style exists for id ${id}.`);
  }
  return style;
}

async function requireVariable(id: string): Promise<Variable> {
  const variable = await figma.variables.getVariableByIdAsync(id);
  if (!variable) {
    throw new OperationError("variable_not_found", `No variable exists for id ${id}.`);
  }
  return variable;
}

async function requireCollection(id: string): Promise<VariableCollection> {
  const collection = await figma.variables.getVariableCollectionByIdAsync(id);
  if (!collection) {
    throw new OperationError(
      "variable_collection_not_found",
      `No variable collection exists for id ${id}.`,
    );
  }
  return collection;
}

function styleKind(payload: RpcPayload): StyleKind {
  const kind = requiredString(payload, "kind") as StyleKind;
  if (!(kind in STYLE_APIS)) {
    throw new OperationError("invalid_argument", `Unknown style kind ${kind}.`);
  }
  return kind;
}

function requestedStyleKinds(payload: RpcPayload): StyleKind[] {
  const values = stringArray(payload, "kinds", { maxItems: 4 });
  if (values.length === 0) {
    return ["paint", "text", "effect", "grid"];
  }
  return values.map((value) => styleKind({ kind: value }));
}

function kindForStyle(style: BaseStyle): StyleKind {
  const type = asApiRecord(style).type;
  if (type === "PAINT" || type === "TEXT" || type === "EFFECT" || type === "GRID") {
    return type.toLowerCase() as StyleKind;
  }
  throw new OperationError("invalid_argument", `Unsupported style type ${String(type)}.`);
}
