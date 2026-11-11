# TrueMoon.Nom: состояние и точка продолжения

**2026-11-11 — CORE8.5: свежая согласованная .NET 11 поставка проверена.**
SDK 11.0.100 / net11.0 / runtime-async=on. Полные suites повторно прошли в каждой
конфигурации Debug/Release: 874; Failed 0, Skipped 0.
Runtime/API/dependencies в CORE8.5 не менялись; обновлены runners и документация.
[Поставка, команды и границы](../core/HISTORY.md#core85-delivery-2026-11-11),
[переход](../core/DOTNET11_MIGRATION.md).

Свежий package и actual App/Nom configuration/admission consumer проверены на обоих DI. Исторические VM/durability boundaries сохранены; VM verification отложена, power-off исключён. Engine g.2 следует после g.1 по согласованию.

Сквозной CORE8 завершён; далее функциональная очередь владельца модуля.
[Предыдущий STATUS](HISTORY.md#nom-status-before-core85-2026-11-11).
