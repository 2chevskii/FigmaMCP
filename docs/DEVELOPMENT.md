# Разработка

## Целевой состав репозитория

После переделки в репозитории остаются только исходники локального companion, Figma Bridge plugin,
тесты этих двух компонентов и документация:

```text
.
├── docs/
├── plugin/                         # пока не меняется
│   ├── src/
│   ├── tests/
│   └── dist/                       # generated, не коммитится
└── server/
    ├── src/FigmaMcp.Server/        # local STDIO MCP + loopback /bridge
    ├── tests/FigmaMcp.Server.Tests/
    └── FigmaMcp.slnx
```

`web/`, `deploy/`, Compose-файлы, отдельные hosted API, command buffer, инфраструктурные проекты и
их тесты удалены. Они не входят в целевую структуру и не должны возвращаться в репозиторий.

## Companion

Проект использует .NET 10 и central package management. Команды после очистки solution:

```powershell
cd server
dotnet restore FigmaMcp.slnx
dotnet format FigmaMcp.slnx --verify-no-changes --no-restore
dotnet build FigmaMcp.slnx --configuration Release
dotnet test --solution FigmaMcp.slnx --configuration Release
```

Во время разработки STDIO-сервер запускают через MCP-клиент или с локальным STDIO harness. Не
печатайте диагностические данные в `stdout`: этот поток принадлежит MCP-протоколу. Локальный bridge
слушает только `127.0.0.1:3846` и нужен для подключения плагина.

## Bridge plugin

Bridge plugin сохраняет свой MessagePack-протокол и настройку адреса local companion; поддержка
access token удалена. Рабочий цикл плагина остаётся прежним:

```powershell
cd plugin
npm install
npm run format:check
npm run lint
npm run typecheck
npm test
npm run build
```

Импортируйте `plugin/dist/manifest.json` как development plugin в Figma Desktop. В настройке сервера
оставьте локальный адрес companion (по умолчанию `http://127.0.0.1:3846`); плагин самостоятельно
формирует WebSocket-адрес `/bridge`.

Плагин хранит только URL loopback companion (по умолчанию `http://127.0.0.1:3846`) и формирует
WebSocket-адрес `/bridge` без query-параметров.

## Проверка локального сценария

После реализации проверка должна покрыть следующую цепочку:

1. MCP-клиент запускает companion как STDIO-процесс.
2. Плагин подключается к `ws://127.0.0.1:3846/bridge` и получает `hello_ack`.
3. `list_figma_connections` возвращает подключение плагина.
4. Инструмент с выбранным `connection_id` получает ответ от Figma через существующий bridge.
5. После закрытия плагина ожидающий запрос завершается ошибкой, а подключение пропадает из списка.

Не нужны Docker, Compose, PostgreSQL, Redis, облачные переменные окружения, токены, регистрация
пользователя или браузерные приложения.
