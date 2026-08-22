# Контекст проекта для агентов

## Проект

`figma-mcp` переделывается из hosted-системы в локальный companion для Figma. MCP-клиент запускает
один .NET-процесс и общается с ним через STDIO. Этот процесс одновременно обслуживает только
loopback WebSocket `/bridge` для существующего Figma Bridge plugin.

Bridge plugin меняется только в явно запрошенном объёме. В этой миграции из него удалена поддержка
access token; не менять его source, manifest, UI, настройки или `figma-mcp-bridge.v2` сверх этого
без явного указания пользователя.

Не возвращать в проект hosted-возможности: HTTP `/mcp`, public ingress, web/API-приложения, command
buffer, PostgreSQL/Redis, Docker/Compose, Kubernetes/cloud deployment, account/token auth и их
инфраструктуру. Удаление существующих hosted-артефактов — часть текущей миграции, когда это входит в
задачу пользователя.

## Обязательное чтение

Читайте документы в этом порядке перед архитектурными или серверными изменениями:

1. [`.agents/SPEC.md`](.agents/SPEC.md) — нормативная целевая спецификация и scope миграции.
2. [`docs/README.md`](docs/README.md) — назначение продукта и порядок работ.
3. [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — транспорты, bridge lifecycle, state и security
   boundary.
4. [`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md) — целевая структура проекта, команды сборки и
   проверка.
5. [`docs/TOOLS.md`](docs/TOOLS.md) — текущий MCP tool contract: схемы, `connection_id`, лимиты и
   ошибки.
6. [`docs/PLUGIN_API_TOOL_COVERAGE.md`](docs/PLUGIN_API_TOOL_COVERAGE.md) — покрытие Figma Plugin
   API, отложенные возможности и ограничения manifest.

Публичные документы в корне репозитория дополняют эту техническую документацию:

- [`README.md`](README.md) — краткое описание, быстрый запуск и состояние готовности;
- [`CONTRIBUTING.md`](CONTRIBUTING.md) — правила подготовки изменений;
- [`.github/SECURITY.md`](.github/SECURITY.md) — порядок сообщения об уязвимостях.

При противоречии используйте следующий приоритет:
последняя явная инструкция пользователя → `AGENTS.md` → верхняя нормативная часть
`.agents/SPEC.md` → `docs/ARCHITECTURE.md` → остальные документы → существующий код. Архивная
hosted-часть внизу `SPEC.md` не является архитектурным прецедентом.

## Локальные agent skills

- [`.agents/skills/dotnet-csharpier/SKILL.md`](.agents/skills/dotnet-csharpier/SKILL.md) — форматирование
  и проверка C#/XML server-проекта через локальный .NET tool CSharpier. Используйте для задач, которые
  явно затрагивают форматирование в `server/`.
- [`.agents/skills/plugin-prettier/SKILL.md`](.agents/skills/plugin-prettier/SKILL.md) — форматирование
  и проверка Figma plugin через локальный npm-пакет Prettier. Используйте для задач форматирования в
  `plugin/`.
- [`.agents/skills/repository-commits/SKILL.md`](.agents/skills/repository-commits/SKILL.md) — подготовка
  и создание Conventional Commits с разбиением несвязанных изменений на логические и временные группы.
  Используйте только когда пользователь явно просит создать коммиты.

## Инварианты реализации

- MCP использует STDIO: читать протокол из `stdin`, писать протокол только в `stdout`, а логи и
  диагностику — в `stderr`.
- Bridge остаётся WebSocket/MessagePack `figma-mcp-bridge.v2` на `127.0.0.1:3846/bridge`; не
  привязывать его к внешней сети.
- Все document-specific MCP-инструменты требуют живой явный `connection_id`.
- Реестр plugin-подключений и pending RPC живут в памяти; запросы для одного connection выполняются
  последовательно, а stale socket не удаляет replacement connection.
- Сохранять bounded typed payload, лимиты размера, 30-секундный deadline bridge RPC и
  идемпотентность мутаций.
- Не добавлять БД, сторонние сервисы, пакеты, тестовую инфраструктуру или конфигурацию вне явно
  запрошенного scope.

## Рабочие правила

- Перед изменением смотрите `git status --short`: working tree может содержать несвязанные изменения
  пользователя. Не отменяйте и не перезаписывайте их без явного разрешения.
- Для server-проектов используйте .NET 10 и central package management; для plugin используйте
  существующие npm scripts.
- После содержательного изменения запускайте уместные сборку и тесты. Для документации минимум —
  `git diff --check`.
- Если изменение затрагивает transport, bridge protocol, MCP tools или plugin, сначала сверяйтесь с
  соответствующим документом из списка выше и обновляйте документацию вместе с кодом.
