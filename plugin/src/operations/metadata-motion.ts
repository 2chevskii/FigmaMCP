import {
  asApiRecord,
  asRecord,
  callApiMethod,
  idempotentMutation,
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
  stringArray,
  toSerializable,
} from "./shared";

export const metadataMotionHandlers: Record<string, RpcHandler> = {
  set_figma_plugin_data: (payload) =>
    idempotentMutation("set_figma_plugin_data", payload, () => setPluginData(payload)),
  list_figma_annotation_categories: listAnnotationCategories,
  create_figma_annotation_category: (payload) =>
    idempotentMutation("create_figma_annotation_category", payload, () =>
      createAnnotationCategory(payload),
    ),
  set_figma_annotations: (payload) =>
    idempotentMutation("set_figma_annotations", payload, () => setAnnotations(payload)),
  manage_figma_measurements: (payload) =>
    idempotentMutation("manage_figma_measurements", payload, () => manageMeasurements(payload)),
  manage_figma_dev_resources: (payload) =>
    idempotentMutation("manage_figma_dev_resources", payload, () => manageDevResources(payload)),
  set_figma_dev_status: (payload) =>
    idempotentMutation("set_figma_dev_status", payload, () => setDevStatus(payload)),
  list_figma_animation_styles: listAnimationStyles,
  get_figma_motion: getMotion,
  update_figma_motion: (payload) =>
    idempotentMutation("update_figma_motion", payload, () => updateMotion(payload)),
};

async function setPluginData(payload: RpcPayload): Promise<RpcResult> {
  const items = objectArray(payload, "items", { required: true, maxItems: 100 });
  if (optionalBoolean(payload, "dry_run") ?? false) {
    return { dry_run: true, would_update: items };
  }
  const changed: string[] = [];
  for (const item of items) {
    const node = await requireNode(requiredString(item, "node_id"));
    for (const entry of objectArray(item, "private", { maxItems: 100 })) {
      node.setPluginData(
        requiredString(entry, "key", { maxLength: 1_000 }),
        optionalString(entry, "value", { maxLength: 100_000 }) ?? "",
      );
    }
    for (const entry of objectArray(item, "shared", { maxItems: 100 })) {
      node.setSharedPluginData(
        requiredString(entry, "namespace", { maxLength: 1_000 }),
        requiredString(entry, "key", { maxLength: 1_000 }),
        optionalString(entry, "value", { maxLength: 100_000 }) ?? "",
      );
    }
    if (item.relaunch_data !== undefined) {
      await callApiMethod(node, "setRelaunchData", [asRecord(item.relaunch_data, "relaunch_data")]);
    }
    changed.push(node.id);
  }
  return { changed_node_ids: changed };
}

async function listAnnotationCategories(payload: RpcPayload): Promise<RpcResult> {
  const categoryId = optionalString(payload, "category_id");
  if (categoryId) {
    return {
      category: toSerializable(await figma.annotations.getAnnotationCategoryByIdAsync(categoryId)),
    };
  }
  return {
    categories: toSerializable(await figma.annotations.getAnnotationCategoriesAsync()),
  };
}

async function createAnnotationCategory(payload: RpcPayload): Promise<RpcResult> {
  if (optionalBoolean(payload, "dry_run") ?? false) {
    return { dry_run: true, label: requiredString(payload, "label") };
  }
  const category = await figma.annotations.addAnnotationCategoryAsync({
    label: requiredString(payload, "label", { maxLength: 200 }),
    color: requiredString(payload, "color") as AnnotationCategoryColor,
  });
  return { category: toSerializable(category) };
}

async function setAnnotations(payload: RpcPayload): Promise<RpcResult> {
  const items = objectArray(payload, "items", { required: true, maxItems: 100 });
  if (optionalBoolean(payload, "dry_run") ?? false) {
    return { dry_run: true, would_update: items };
  }
  const changed: RpcResult[] = [];
  for (const item of items) {
    const node = await requireSceneNode(requiredString(item, "node_id"));
    if (!("annotations" in node)) {
      throw new OperationError("invalid_node_type", `${node.id} does not support annotations.`);
    }
    node.annotations = (item.annotations ?? []) as Annotation[];
    changed.push({ ...nodeSummary(node), annotations: toSerializable(node.annotations) });
  }
  return { changed };
}

async function manageMeasurements(payload: RpcPayload): Promise<RpcResult> {
  const operation = optionalString(payload, "operation") ?? "list";
  const page = figma.currentPage;
  if (operation === "list") {
    const nodeId = optionalString(payload, "node_id");
    const measurements = nodeId
      ? page.getMeasurementsForNode(await requireSceneNode(nodeId))
      : page.getMeasurements();
    return { measurements: toSerializable(measurements) };
  }
  if (figma.editorType !== "dev") {
    throw new OperationError(
      "unsupported_in_editor",
      "Measurement mutations are only available when the connector runs in Dev Mode.",
    );
  }
  if (operation === "add") {
    const start = asRecord(payload.start, "start");
    const end = asRecord(payload.end, "end");
    const measurement = page.addMeasurement(
      {
        node: await requireSceneNode(requiredString(start, "node_id")),
        side: requiredString(start, "side") as MeasurementSide,
      },
      {
        node: await requireSceneNode(requiredString(end, "node_id")),
        side: requiredString(end, "side") as MeasurementSide,
      },
      payload.options as never,
    );
    return { measurement: toSerializable(measurement) };
  }
  if (operation === "edit") {
    return {
      measurement: toSerializable(
        page.editMeasurement(
          requiredString(payload, "measurement_id"),
          asRecord(payload.patch, "patch") as never,
        ),
      ),
    };
  }
  if (operation === "delete") {
    page.deleteMeasurement(requiredString(payload, "measurement_id"));
    return { deleted: true };
  }
  throw new OperationError("invalid_argument", `Unknown measurement operation ${operation}.`);
}

async function manageDevResources(payload: RpcPayload): Promise<RpcResult> {
  const node = await requireSceneNode(requiredString(payload, "node_id"));
  const operation = optionalString(payload, "operation") ?? "list";
  if (operation === "list") {
    return {
      resources: toSerializable(
        await callApiMethod(node, "getDevResourcesAsync", [
          { includeChildren: optionalBoolean(payload, "include_children") ?? false },
        ]),
      ),
    };
  }
  if (operation === "add") {
    await callApiMethod(node, "addDevResourceAsync", [
      requiredString(payload, "url"),
      optionalString(payload, "name"),
    ]);
  } else if (operation === "edit") {
    await callApiMethod(node, "editDevResourceAsync", [
      requiredString(payload, "current_url"),
      asRecord(payload.patch, "patch"),
    ]);
  } else if (operation === "delete") {
    await callApiMethod(node, "deleteDevResourceAsync", [requiredString(payload, "url")]);
  } else {
    throw new OperationError("invalid_argument", `Unknown dev-resource operation ${operation}.`);
  }
  return {
    resources: toSerializable(await callApiMethod(node, "getDevResourcesAsync")),
  };
}

async function setDevStatus(payload: RpcPayload): Promise<RpcResult> {
  const node = await requireSceneNode(requiredString(payload, "node_id"));
  const api = asApiRecord(node);
  if (!("devStatus" in api)) {
    throw new OperationError("invalid_node_type", `${node.id} does not support dev status.`);
  }
  if (payload.status !== undefined) {
    api.devStatus = payload.status;
  }
  return { node_id: node.id, dev_status: toSerializable(api.devStatus) };
}

function listAnimationStyles(payload: RpcPayload): RpcResult {
  const spring = payload.physical_spring
    ? asRecord(payload.physical_spring, "physical_spring")
    : undefined;
  return {
    beta: true,
    styles: toSerializable(figma.motion.figmaAnimationStyles()),
    normalized_bounce: spring
      ? figma.motion.physicalSpringToNormalized(spring as unknown as PhysicalSpring)
      : undefined,
  };
}

async function getMotion(payload: RpcPayload): Promise<RpcResult> {
  const nodeIds = stringArray(payload, "node_ids", { required: true, maxItems: 100 });
  const nodes = await Promise.all(nodeIds.map(requireSceneNode));
  return {
    beta: true,
    nodes: nodes.map((node) => ({
      ...nodeSummary(node),
      animation_styles: toSerializable(node.animationStyles),
      animations: toSerializable(node.animations),
      manual_keyframe_tracks: toSerializable(node.manualKeyframeTracks),
      timelines: toSerializable(node.timelines),
    })),
  };
}

async function updateMotion(payload: RpcPayload): Promise<RpcResult> {
  const items = objectArray(payload, "items", { required: true, maxItems: 100 });
  if (optionalBoolean(payload, "dry_run") ?? false) {
    return { beta: true, dry_run: true, would_update: items };
  }
  const changed: RpcResult[] = [];
  for (const item of items) {
    const node = await requireSceneNode(requiredString(item, "node_id"));
    const operation = requiredString(item, "operation");
    let result: unknown;
    if (operation === "apply_style") {
      result = node.applyAnimationStyle(
        requiredString(item, "style_id"),
        item.configuration as AnimationStyleConfiguration | undefined,
      );
    } else if (operation === "remove_style") {
      node.removeAnimationStyle(requiredString(item, "applied_style_id"));
    } else if (operation === "apply_manual_track") {
      node.applyManualKeyframeTrack(
        asRecord(item.field, "field") as KeyframeField,
        asRecord(item.track, "track") as unknown as ManualKeyframeTrackInput,
      );
    } else if (operation === "remove_manual_track") {
      node.removeManualKeyframeTrack(asRecord(item.field, "field") as KeyframeField);
    } else if (operation === "set_timeline_duration") {
      node.setTimelineDuration(
        requiredString(item, "timeline_id"),
        optionalNumber(item, "duration", { min: 0.001, max: 86_400 }) ?? 1,
      );
    } else {
      throw new OperationError("invalid_argument", `Unknown Motion operation ${operation}.`);
    }
    changed.push({ node_id: node.id, operation, result: toSerializable(result) });
  }
  return { beta: true, changed };
}
