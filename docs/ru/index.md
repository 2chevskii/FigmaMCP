---
layout: home

hero:
  name: Figma MCP
  text: Локальный компаньон для документов Figma
  tagline: Подключает MCP-клиенты к открытому документу Figma через локальный bridge, доступный только на этом компьютере.
  image:
    src: /branding/figmamcp-icon.png
    alt: Знак FigmaMCP
  actions:
    - theme: brand
      text: Начать работу
      link: ./INSTALLATION
    - theme: alt
      text: Справочник инструментов
      link: ./TOOLS

features:
  - title: Локальная работа
    details: Компаньон запускается на вашем компьютере и обменивается данными с плагином Bridge через локальный WebSocket.
  - title: Явный доступ к документу
    details: Каждый инструмент для работы с документом использует активный connection_id, выбранный из подключений плагина Figma.
  - title: Типизированные и ограниченные данные
    details: Bridge использует типизированные контракты MessagePack, ограниченный размер данных, последовательные вызовы и контролируемые изменения.
---

![FigmaMCP — локальный bridge между MCP и холстом Figma](/branding/figmamcp-banner.png){.brand-banner}

## Обзор

Figma MCP — локальный компаньон для документов Figma, в которых открыт плагин Figma Bridge. MCP-клиент запускает процесс .NET и обменивается с ним протокольными сообщениями через STDIO.

```mermaid
sequenceDiagram
    participant Client as MCP-клиент
    participant Companion as Компаньон
    participant Registry as Реестр подключений
    participant Plugin as Плагин Bridge
    participant Figma as API Figma

    Client->>Companion: Запустить дочерний процесс и установить MCP через STDIO
    Plugin->>Companion: Открыть локальный WebSocket с figma-mcp-bridge.v2
    Companion->>Registry: Проверить hello и зарегистрировать connection_id
    Companion-->>Plugin: Подтвердить регистрацию сообщением hello_ack
    Client->>Companion: Вызвать инструмент документа с выбранным connection_id
    Companion->>Registry: Найти активное подключение и создать request_id
    Registry-->>Companion: Вернуть активное подключение плагина
    Companion->>Plugin: Отправить ограниченный запрос Bridge в MessagePack
    Plugin->>Figma: Прочитать или изменить активный документ Figma
    Figma-->>Plugin: Вернуть результат операции
    Plugin-->>Companion: Отправить ответ MessagePack с request_id
    Companion-->>Client: Вернуть соответствующий результат MCP через stdout
```

Компаньон работает с двумя транспортами:

- STDIO обслуживает MCP. Протокольные сообщения передаются через `stdin` и `stdout`, диагностика — через `stderr`.
- Локальная конечная точка WebSocket `/bridge` обслуживает плагин Figma Bridge.

Сервер, плагин и bridge используют явные типизированные контракты. Инструменты для работы с документом получают действующий `connection_id`; операции bridge используют ограниченные данные, тайм-аут 30 секунд и ключи идемпотентности для изменений, где это применимо.

## Установка и подключение

Установите компаньон и плагин Bridge, затем настройте MCP-клиент по [руководству по установке](./INSTALLATION). Клиент запускает компаньон через STDIO, а плагин подключается к локальной конечной точке Bridge.

## Документация

- [Архитектура](./ARCHITECTURE): транспорт, жизненный цикл, состояние и границы безопасности.
- [Установка](./INSTALLATION): установка компаньона, подключение плагина и настройка MCP-клиента.
- [Разработка](./DEVELOPMENT): структура репозитория, команды сборки и локальные проверки.
- [Справочник инструментов](./TOOLS): контракт MCP-инструментов.
- [Покрытие Plugin API](./plugin-api-tool-coverage): поддерживаемые и отложенные области API Figma.
