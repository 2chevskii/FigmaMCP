# Figma MCP

Локальный MCP companion для работы с открытым документом Figma через Figma Bridge plugin.
MCP-клиент запускает .NET-процесс и общается с ним по STDIO; plugin подключается к этому же
процессу только по loopback WebSocket. В проекте нет hosted-сервиса, аккаунтов, токенов или
внешней инфраструктуры.

```text
MCP client ── stdin/stdout ──> local companion ── ws://127.0.0.1:3846/bridge ──> Figma plugin
```

## Возможности

- MCP-over-STDIO: протокол использует только `stdin` и `stdout`; диагностика уходит в `stderr`.
- Figma Bridge на MessagePack WebSocket `figma-mcp-bridge.v2`, доступный только на loopback.
- Явный `connection_id` для инструментов, работающих с документом.
- In-memory registry подключений, последовательные RPC для одного подключения и bounded payload.
- Локальные инструменты для чтения и редактирования Figma Design-документов.

Подробный контракт инструментов — в [docs/TOOLS.md](docs/TOOLS.md), ограничения Plugin API — в
[docs/PLUGIN_API_TOOL_COVERAGE.md](docs/PLUGIN_API_TOOL_COVERAGE.md).

## Требования

- Windows x64;
- .NET SDK 10 (версия зафиксирована в [server/global.json](server/global.json));
- Node.js и npm для сборки Figma plugin и запуска MCP Inspector;
- Figma Desktop для ручной проверки plugin в реальном документе.

## Быстрый локальный запуск

1. Соберите companion:

   ```powershell
   dotnet build .\server\FigmaMcp.slnx --configuration Release
   ```

2. Соберите Bridge plugin:

   ```powershell
   npm ci --prefix .\plugin
   npm run build --prefix .\plugin
   ```

3. В Figma Desktop импортируйте `plugin/dist/manifest.json` как development plugin и откройте его в
   нужном документе. Оставьте адрес companion `http://127.0.0.1:3846`.

4. Для проверки MCP-сессии запустите Inspector. Скрипт соберёт сервер и передаст его Inspector как
   STDIO-процесс:

   ```powershell
   .\Start-McpInspector.ps1
   ```

После подключения plugin сначала вызовите `list_figma_connections`, затем передавайте полученный
`connection_id` в document-specific инструменты.

## Подключение к MCP-клиенту

Для самостоятельного бинарника опубликуйте server под Windows:

```powershell
dotnet publish .\server\src\FigmaMcp.Server\FigmaMcp.Server.csproj `
  --configuration Release `
  -p:PublishProfile=win-x64
```

Затем укажите созданный `figma-mcp-server.exe` как команду MCP-сервера в настройках клиента. Не
перенаправляйте его `stdout`: это исключительно поток MCP-протокола.

## Структура

```text
plugin/  Figma Bridge plugin
server/  .NET 10 MCP companion и тесты
docs/    архитектура, разработка и контракт инструментов
```

## Документация

- [Архитектура](docs/ARCHITECTURE.md)
- [Разработка и проверки](docs/DEVELOPMENT.md)
- [Нормативная спецификация](.agents/SPEC.md)
- [Вклад в проект](CONTRIBUTING.md)
- [Безопасность](.github/SECURITY.md)

## Состояние и границы

Сервер и синтетические bridge-проверки запускаются локально. Полный сценарий всё ещё требует
ручной проверки в Figma Desktop: импорт development plugin, подключение к bridge и выполнение
вызова к реальному документу.

Проект не аффилирован с Figma и распространяется по [лицензии MIT](LICENSE).
