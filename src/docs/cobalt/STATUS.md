# Cobalt / DI: состояние и точка продолжения

**2026-11-11 — CORE8.5: свежая согласованная .NET 11 поставка проверена.**
SDK 11.0.100 / net11.0 / runtime-async=on. Полные suites повторно прошли в каждой
конфигурации Debug/Release: runtime/adapter 179, generator 96; Failed 0, Skipped 0.
Runtime/API/dependencies в CORE8.5 не менялись; обновлены runners и документация.
[Поставка, команды и границы](../core/HISTORY.md#core85-delivery-2026-11-11),
[переход](../core/DOTNET11_MIGRATION.md).

Свежие Cobalt analyzer/buildTransitive payloads и generated DI проверены у independent consumers; Cobalt и Microsoft DI adapter — на actual App/game lifecycle. Performance measurements, scopes и новая factory API не добавлялись.

Сквозной CORE8 завершён; далее функциональная очередь владельца модуля.
[Предыдущий STATUS](HISTORY.md#cobalt-status-before-core85-2026-11-11).

<a id="предыдущий-cob7-run--2026-10-15"></a>
