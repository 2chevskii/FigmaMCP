# Figma MCP — спецификация локального STDIO companion

**Статус:** локальная архитектура реализована; требуется проверка полного сценария с Figma Desktop.
**Платформа:** Windows x64.
**Runtime:** .NET 10.
**MCP-транспорт:** STDIO.
**Транспорт Bridge:** бинарный WebSocket с MessagePack, `figma-mcp-bridge.v2`.

> Важно: разделы, которые начинаются ниже с «Historical hosted specification», сохранены только как
> история предыдущего решения. Они **не нормативны**, не должны использоваться для реализации и не
> могут переопределять эту верхнюю спецификацию, `docs/ARCHITECTURE.md` или явное указание
> пользователя.

## Нормативная целевая архитектура

Figma MCP — один локальный companion для документов, в которых пользователь открыл существующий
Figma Bridge plugin. MCP-клиент запускает companion как дочерний процесс. MCP-сообщения проходят
только через STDIO; companion не является hosted- или multi-tenant-сервисом.

```text
MCP client ── stdin/stdout ──> local companion ── ws://127.0.0.1:3846/bridge ──> Bridge plugin
                                                                                         │
                                                                                  Figma Plugin API
```

### Входит в целевую версию

- Один .NET-процесс `FigmaMcp.Server`.
- Официальный MCP SDK со STDIO transport.
- Локальный loopback WebSocket endpoint `/bridge` для плагина.
- In-memory registry живых plugin-подключений и pending RPC.
- Текущий MCP tool contract: явный `connection_id`, bounded typed payload, ограничения размера,
  30-секундный bridge deadline и идемпотентность мутаций.
- Bridge plugin без изменения протокола; его access token удалён из UI, storage и bridge URL.

### Исключено и подлежит удалению

- Streamable HTTP `/mcp`, HTTP/SSE, bearer/query tokens и публичный ingress.
- User/admin web-приложения и API, command buffer, межсервисные API и очереди.
- PostgreSQL, Redis, Docker Compose, Kubernetes и cloud/deployment-конфигурация.
- Регистрация, browser sessions, CAPTCHA, роли, personal access tokens и hosted-аутентификация.
- Hosted-тесты, документация эксплуатации и зависимости, нужные только перечисленным компонентам.

### STDIO-контракт

Companion читает MCP-протокол из `stdin` и пишет протокольные ответы **только** в `stdout`. Логи,
диагностика и startup/errors выводятся в `stderr`. Нельзя добавлять человеческий текст, логгер или
баннер в stdout.

MCP-клиент конфигурирует исполняемый файл, например:

```json
{
  "mcpServers": {
    "figma": { "command": "C:\\path\\to\\figma-mcp-server.exe" }
  }
}
```

### Неизменяемый Bridge

До отдельного указания пользователя не менять `plugin/`: source, manifest, UI, настройки и protocol.
Companion обязан быть совместим с текущим bridge:

- путь `/bridge`, подпротокол `figma-mcp-bridge.v2`;
- один бинарный WebSocket frame содержит один MessagePack map;
- envelopes `hello`, `hello_ack`, `context_changed`, `request`, `response`, `error`, `ping`, `pong`;
- канонические UUID в нижнем регистре и UTC ISO-8601 timestamps;
- общий лимит сообщения 16 MiB и base64 binary payload до 12 MiB;
- только allowlisted typed operations, без JavaScript execution или произвольной reflection.

Bridge plugin настраивает только адрес локального companion. Он не хранит и не передаёт access token;
не добавлять новую аутентификацию без отдельного решения.

### Локальный bridge boundary

- Слушать только `127.0.0.1:3846/bridge` по умолчанию; не использовать `0.0.0.0`, LAN или IPv6-any.
- Принимать только `127.0.0.1:<port>` и `localhost:<port>` в Host.
- Принимать только WebSocket upgrade с `figma-mcp-bridge.v2`, бинарные frames и отсутствующий либо
  `null` Origin; отклонять text frames и иные browser origins.
- Нельзя трактовать отсутствие hosted-инфраструктуры как разрешение открыть bridge в сеть.

### Connection и tool semantics

1. Plugin отправляет `hello`; companion валидирует его, сохраняет connection и отвечает `hello_ack`.
2. MCP-клиент вызывает `list_figma_connections`, затем передаёт выбранный живой `connection_id` в
   каждый document-specific tool.
3. Запросы одного connection выполняются последовательно; разные connections могут выполняться
   параллельно. Ответы сопоставляются по `request_id`.
4. `connection_id` означает plugin invocation, а не постоянный Figma-файл. Replacement connection
   устанавливается compare-and-swap: stale socket не может удалить replacement.
5. Disconnect завершает pending запросы контролируемой MCP-ошибкой. Перезапуск companion очищает
   только in-memory state; плагин должен переподключиться штатно.

Не менять существующий tool catalog из `docs/TOOLS.md` только ради смены MCP-транспорта.

### Очерёдность миграции

1. Удалены hosted-проекты, их тесты, инфраструктура и package/project references.
2. В solution оставлены local server и его тесты; server владеет registry и `/bridge`.
3. MCP HTTP transport заменён на STDIO без изменения tool contract.
4. Из plugin удалена поддержка access token без изменения bridge protocol.
5. Собрать и проверить цепочку: STDIO initialize/tool listing → plugin hello →
   `list_figma_connections` → document-specific tool → Figma response.

### Обязательная проверка

- Для server: restore, format-check, build и test.
- Для plugin: `format:check`, lint, typecheck, test и build.
- Проверить реальный STDIO-клиент, Figma Desktop и loopback-only bridge.
- Убедиться, что stdout companion содержит исключительно MCP-протокол.
- Не создавать новые hosted-тесты, сервисы, контейнеры, зависимости или deployment-конфигурацию.

### Связанные документы

- `AGENTS.md` — инструкция и карта документации для агентов.
- `docs/README.md` — обзор и план миграции.
- `docs/ARCHITECTURE.md` — детали transport, lifecycle и security boundary.
- `docs/DEVELOPMENT.md` — структура, сборка и локальный сценарий проверки.
- `docs/TOOLS.md` — нормативный MCP tool contract.
- `docs/PLUGIN_API_TOOL_COVERAGE.md` — границы покрытия Figma Plugin API.
