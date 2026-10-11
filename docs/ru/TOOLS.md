---
title: Справочник инструментов MCP
description: Контракты инструментов Figma MCP, входные данные, ограничения и ошибки.
---

# Справочник инструментов Figma MCP

Каждый вызов инструмента для работы с документом требует канонический `connection_id` в нижнем регистре, возвращаемый `list_figma_connections`. После выбора подключения вызовите `get_figma_capabilities`: бета-возможности и API с разрешениями зависят от среды Figma, аккаунта и файла.

## Общие правила

- Входные поля инструментов используют `snake_case`.
- Идентификаторы относятся к текущим узлам, стилям и переменным Figma, а не к REST-ключам файла.
- Изменяющие данные пакетные операции ограничены 100 элементами, если для инструмента не указано иное.
- Инструменты с `dry_run: true` проверяют данные и показывают предполагаемые цели, не записывая изменения.
- Инструменты с `idempotency_key` кэшируют первый результат на время текущего запуска плагина. Повторный ключ возвращает его с `idempotent_replay: true`.
- При инициализации коннектор один раз вызывает `loadAllPagesAsync()`: Figma требует этого до регистрации `documentchange` в режиме dynamic-page. Bridge принимает операции после завершения инициализации.
- Журнал изменений фиксирует все изменения документа, включая ручное редактирование холста, а также события выбора, страницы и стилей.
- Двоичные данные на входе и выходе передаются в base64 и ограничены 12 МиБ. Конверты Bridge ограничены 16 МиБ.
- Структура ошибки: `error.code`, `error.message`, `error.connection_id`.
- Продолжают действовать проверки Plugin API Figma. Например, узел нельзя вынести из instance, используемые шрифты должны существовать, а лимиты страниц и режимов зависят от плана Figma.

## Подключение и контекст документа

| Инструмент                    | Входные данные                                                               |
| ----------------------------- | ---------------------------------------------------------------------------- |
| `list_figma_connections`      | Нет                                                                          |
| `get_figma_document_metadata` | Только `connection_id`; псевдоним `get_figma_document`                       |
| `get_figma_capabilities`      | Нет                                                                          |
| `get_figma_document`          | Нет                                                                          |
| `list_figma_pages`            | Необязательные `cursor`, `limit`                                             |
| `load_figma_page`             | `page_id`                                                                    |
| `get_figma_selection`         | Нет                                                                          |
| `set_figma_selection`         | `node_ids`, необязательный `focus`                                           |
| `set_figma_current_page`      | `page_id`                                                                    |
| `get_figma_document_changes`  | Необязательные `cursor`, `limit`; передайте `next_cursor` в следующий запрос |

## Чтение дерева документа

| Инструмент                | Входные данные                                                                                                   |
| ------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| `get_figma_nodes`         | `node_ids`; необязательные `fields`, `child_depth` от 0 до 4                                                     |
| `query_figma_nodes`       | Необязательные `root_id`, `node_types`, `name`, `name_contains`, `visible`, `plugin_data_key`, `fields`, `limit` |
| `get_figma_node_css`      | `node_ids`                                                                                                       |
| `get_figma_node_geometry` | `node_ids`                                                                                                       |
| `get_figma_text`          | `node_ids`; необязательные `start`, `end`, `segment_fields`                                                      |
| `get_figma_components`    | `node_ids`                                                                                                       |
| `get_figma_prototype`     | `node_ids`                                                                                                       |
| `get_figma_plugin_data`   | `node_ids`; необязательные приватные `keys` и `shared_namespaces`                                                |
| `get_figma_dev_metadata`  | `node_ids`                                                                                                       |

По умолчанию возвращаются поля узла `id`, `type`, `name`, `removed`, `parent_id`, `visible`, `locked`, `x`, `y`, `width` и `height`. Явная проекция также может запросить трансформации, границы, заливки, обводки, эффекты, углы, ограничения, свойства layout и grid, дочерние узлы, векторные данные, свойства компонентов, реакции, аннотации и связанные переменные.

## Создание и изменение узлов

Инструмент `create_figma_nodes` принимает, например:

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

Поддерживаемые конструкторы Design: `rectangle`, `line`, `ellipse`, `polygon`, `star`, `vector`, `text`, `frame`, `component`, `page`, `page_divider`, `slice`, `section`, `boolean_operation`, `svg` и `text_path`. Для текста используется `characters`, для SVG — `svg`, для текстовых кривых — `vector_node_id`, `start_segment` и `start_position`.

`update_figma_nodes` принимает `updates: [{ node_id, properties }]`. Разрешённые поля включают идентификацию, видимость и блокировку, позицию, поворот и прозрачность, маски и смешивание, эффекты, заливки и обводки, углы и ограничения, auto/grid layout, минимальные и максимальные размеры, обрезку, параметры экспорта, прототип, а также данные специфичных для фигуры точек и дуг и статус Dev Mode.

| Инструмент                 | Входные данные                                                                                                    |
| -------------------------- | ----------------------------------------------------------------------------------------------------------------- |
| `clone_figma_nodes`        | `node_ids`; необязательные `parent_id`, `index`, размещение относительно якоря `placement`                        |
| `move_figma_nodes`         | `moves: [{ node_id, parent_id, index? }]`                                                                         |
| `delete_figma_nodes`       | `node_ids`                                                                                                        |
| `resize_figma_nodes`       | `items: [{ node_id, mode, width?, height?, scale?, lock_aspect_ratio? }]`                                         |
| `combine_figma_nodes`      | `operation`, `node_ids`, необязательные `parent_id`, `index`; для transform-group нужен `modifier` на каждый узел |
| `set_figma_vector_network` | `node_id` и/или `vector_network`, `vector_paths`                                                                  |

Операции `combine_figma_nodes`: `group`, `transform_group`, `flatten`, `ungroup`, `combine_as_variants`, `union`, `subtract`, `intersect` и `exclude`.

По умолчанию `clone_figma_nodes` действует как `clone()` Figma и создаёт копии на текущей странице. Укажите `parent_id`, чтобы клонировать непосредственно в контейнер. Для сохранения полной трансформации относительно визуального якоря при копировании задайте `placement` с `source_anchor_id` и `target_anchor_id`. Если родитель управляет размещением дочерних элементов, например через auto-layout без абсолютного позиционирования, операция завершается с `unsupported_placement` и удаляет созданные копии. Результаты клонирования и перемещения содержат `relative_transform`, `absolute_transform`, `absolute_bounding_box` и `coordinate_parent_id`; для перемещения возвращается геометрия до и после операции.

## Текст, компоненты и экземпляры

| Инструмент                        | Входные данные                                                                                                                  |
| --------------------------------- | ------------------------------------------------------------------------------------------------------------------------------- |
| `list_figma_fonts`                | Необязательные `family`, `cursor`, `limit`                                                                                      |
| `update_figma_text`               | `items` с `node_id`, `operation`, диапазонами, `characters`, необязательными `font_names` и свойствами диапазонов               |
| `update_figma_text_path`          | Та же схема, что для текста, плюс необязательные `path_alignment`, `paragraph_spacing`, `paragraph_indent`                      |
| `create_figma_component_instance` | `operation` (`create_instance` или `component_from_node`) и `component_id` либо `node_id`                                       |
| `update_figma_component`          | `items` с метаданными и `property_actions` (`add`, `edit`, `delete`)                                                            |
| `update_figma_instance`           | `items` с `swap_component`, `set_main_component`, `set_properties`, `remove_overrides`, `detach`, `set_scale` или `set_exposed` |
| `update_figma_slot`               | `operation` (`create`, `reset`, `inspect`) и `component_id` либо `slot_id`                                                      |
| `list_figma_component_instances`  | `component_id`, необязательные `cursor`, `limit`                                                                                |

Операции с текстом: `replace`, `insert`, `delete`, `set_all`, `format`. Свойства диапазона включают `font_name`, `font_size`, `text_case`, `text_decoration`, `letter_spacing`, `line_height`, `hyperlink`, `fills`, `list_options`, `indentation`, `paragraph_indent`, `paragraph_spacing` и `open_type_features`. Перед записью коннектор загружает все текущие и запрошенные шрифты.

## Стили, переменные и библиотеки

| Инструмент                         | Входные данные                                                                                   |
| ---------------------------------- | ------------------------------------------------------------------------------------------------ |
| `list_figma_styles`                | Необязательные `kinds` (`paint`, `text`, `effect`, `grid`), `cursor`, `limit`                    |
| `create_figma_style`               | `kind`, `name` и значение соответствующего типа                                                  |
| `update_figma_style`               | `style_id` и изменяемые поля                                                                     |
| `delete_figma_style`               | `style_ids`                                                                                      |
| `reorder_figma_styles`             | `kind`, `operation` (`style` или `folder`), целевые и опорные ID либо пути                       |
| `list_figma_style_consumers`       | `style_id`, необязательные `cursor`, `limit`                                                     |
| `list_figma_variables`             | Необязательные `resolved_type`, `cursor`, `limit`                                                |
| `create_figma_variable_collection` | `name`; необязательные `extend_collection_key`, `hidden_from_publishing`, `mode_actions`         |
| `create_figma_variable`            | `collection_id`, `name`, `resolved_type`; необязательные значения, aliases, scopes и code syntax |
| `update_figma_variable`            | `variable_id` и изменяемые поля                                                                  |
| `delete_figma_variable`            | `variable_ids` и/или `collection_ids`                                                            |
| `bind_figma_variable`              | `bindings` для `node_field`, `text_range`, `paint`, `effect`, `layout_grid` или `explicit_mode`  |
| `list_figma_team_library_assets`   | Операция списка/импорта и `collection_key` либо ключ ресурса `key`                               |

Операции библиотеки: `list_variable_collections`, `list_variables`, `import_variable`, `import_component`, `import_component_set` и `import_style`. Манифест запрашивает разрешение `teamlibrary`. Библиотеку нужно включить в интерфейсе Figma; Plugin API не умеет включать её самостоятельно.

## Ресурсы и экспорт

| Инструмент             | Входные данные                                                                          |
| ---------------------- | --------------------------------------------------------------------------------------- |
| `create_figma_image`   | `data_base64` или публичный URL HTTP(S); URL локальной сети отклоняются                 |
| `get_figma_image`      | `hash`                                                                                  |
| `create_figma_media`   | `kind: "video"`, `data_base64`                                                          |
| `list_figma_shaders`   | Для списка входные данные не нужны; для импорта — `import_id`                           |
| `load_figma_brushes`   | `brush_type` (`STRETCH` или `SCATTER`)                                                  |
| `export_figma_nodes`   | До 20 `node_ids`, необязательные параметры Plugin API для PNG/JPG/SVG/PDF/JSON_REST_V1  |
| `get_figma_screenshot` | `node_id`, необязательные `scale` (`0.01`–`4`) и `contents_only`; возвращает PNG inline |
| `encode_figma_binary`  | `data_base64`, необязательная операция `inspect` или `normalize_base64`                 |

`get_figma_screenshot` предназначен для визуальной проверки: результат содержит типизированный блок MCP `image`, который совместимый клиент может показать как изображение, а не как base64 в JSON. `export_figma_nodes` остаётся общим API для экспорта нескольких узлов в различных форматах.

Произвольный `figma.fetch` не должен становиться универсальным сетевым прокси. Создание изображений и медиаданных по URL может использовать его внутри инструмента с учётом allowlist манифеста, максимального размера ответа, проверки MIME-типа, тайм-аутов и правил SSRF.

## Прототип, viewport, уведомления и состояние файла

| Инструмент                      | Входные данные                                                                                                            |
| ------------------------------- | ------------------------------------------------------------------------------------------------------------------------- |
| `update_figma_prototype`        | `items: [{ node_id, properties }]` для реакций, flow, overflow и overlay                                                  |
| `get_figma_viewport`            | Нет                                                                                                                       |
| `set_figma_viewport`            | `node_ids` либо `center` и/или `zoom`                                                                                     |
| `notify_figma_user`             | `message`, необязательные `timeout_ms`, `error`                                                                           |
| `commit_figma_undo`             | Необязательная операция `commit` или `undo`                                                                               |
| `save_figma_version`            | `title`, необязательный `description`                                                                                     |
| `get_figma_file_thumbnail_node` | Нет                                                                                                                       |
| `set_figma_file_thumbnail_node` | Необязательный `node_id`; пропуск очищает значение. Figma принимает только frames, components, component sets и sections. |

## Данные плагина, аннотации и метаданные разработки

| Инструмент                         | Входные данные                                                                   |
| ---------------------------------- | -------------------------------------------------------------------------------- |
| `set_figma_plugin_data`            | `items` с `node_id`, приватными/общими записями и необязательным `relaunch_data` |
| `list_figma_annotation_categories` | Необязательный `category_id`                                                     |
| `create_figma_annotation_category` | `label`, `color`                                                                 |
| `set_figma_annotations`            | `items: [{ node_id, annotations }]`                                              |
| `manage_figma_measurements`        | Операция `list`, `add`, `edit`, `delete` и её поля                               |
| `manage_figma_dev_resources`       | `node_id`, операция `list`, `add`, `edit`, `delete`, поля URL/имени              |
| `set_figma_dev_status`             | `node_id`, необязательный статус Plugin API; если его опустить, статус читается  |

Превью Dev Resources доступны только в приватном или партнёрском API и не предоставляются. Изменение измерений доступно только в Dev Mode; коннектор Design возвращает `unsupported_in_editor`.

## Бета-версия Motion

| Инструмент                    | Входные данные                                                                                                |
| ----------------------------- | ------------------------------------------------------------------------------------------------------------- |
| `list_figma_animation_styles` | Необязательный `physical_spring` с mass, stiffness и damping                                                  |
| `get_figma_motion`            | `node_ids`                                                                                                    |
| `update_figma_motion`         | `items` с `apply_style`, `remove_style`, `apply_manual_track`, `remove_manual_track`, `set_timeline_duration` |

Результаты Motion содержат `beta: true`. Схема данных следует бета-версии Plugin API и может меняться независимо от стабильного конверта Bridge при обновлении Figma.

## Намеренно не поддерживается

Коннектор не предоставляет raw JavaScript/eval, произвольный `fetch`, управление интерфейсом плагина, `clientStorage`, платежи, `openExternal`, приватные API, партнёрские превью dev resources, FigJam, Slides, Buzz, callbacks codegen/parameters или режим text-review. Эти API либо не работают с документом Design, либо относятся к реализации самого коннектора, требуют отдельного режима запуска плагина или создают небезопасную поверхность универсального выполнения/сетевого доступа.
