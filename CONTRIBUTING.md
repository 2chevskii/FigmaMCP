# Вклад в Figma MCP

## Перед началом работы

- Ознакомьтесь с [README.md](README.md) и [нормативной спецификацией](.agents/SPEC.md).
- Для изменений transport, bridge или MCP tools сначала прочитайте документы в порядке, указанном в
  [AGENTS.md](AGENTS.md).
- Не возвращайте hosted-компоненты, публичные HTTP MCP-endpoint, токены, базы данных, Docker или
  cloud-конфигурацию.

## Границы изменений

- MCP-протокол использует только STDIO: никакого текста или логов в `stdout`.
- Bridge слушает только `127.0.0.1:3846/bridge` и использует `figma-mcp-bridge.v2`.
- Все document-specific MCP calls требуют активный `connection_id`.
- Изменения plugin должны сохранять typed MessagePack protocol, лимиты payload и Figma Plugin API
  ограничения.

## Проверка перед pull request

```powershell
Push-Location .\server
dotnet format FigmaMcp.slnx --verify-no-changes --no-restore
dotnet build FigmaMcp.slnx --configuration Release
dotnet test --solution FigmaMcp.slnx --configuration Release
Pop-Location

npm ci --prefix .\plugin
npm run format:check --prefix .\plugin
npm run lint --prefix .\plugin
npm run typecheck --prefix .\plugin
npm test --prefix .\plugin
npm run build --prefix .\plugin
```

Также выполните `git diff --check`. Если менялся plugin, transport или tool contract, проверьте
сценарий в Figma Desktop; синтетические тесты bridge не заменяют этот запуск.

## Pull request

Опишите пользовательскую цель, архитектурный эффект и выполненные проверки. Не включайте
сгенерированные `bin/`, `obj/`, `plugin/dist/`, `plugin/node_modules/`, персональные настройки IDE
или секреты.
