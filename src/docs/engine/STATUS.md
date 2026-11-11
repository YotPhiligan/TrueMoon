# Aether / Editor: состояние и точка продолжения

**2026-11-11 — CORE8.5: свежая согласованная .NET 11 поставка проверена.**
SDK 11.0.100 / net11.0 / runtime-async=on. Полные suites повторно прошли в каждой
конфигурации Debug/Release: Aether 3243, Editor 1496; Failed 0, Skipped 0.
Runtime/API/dependencies в CORE8.5 не менялись; обновлены runners и документация.
[Поставка, команды и границы](../core/HISTORY.md#core85-delivery-2026-11-11),
[переход](../core/DOTNET11_MIGRATION.md).

Source/published Runtime examples и 8 independent package game cases (оба DI, DLL/apphost, build/publish) проверили compile/load/replacement/stop/reap; два fresh GPU viewport profiles прошли. F.4 mesh surface renderer/Playing fence, g.1 importer, h.0 audio и прежние reliability gates остаются отдельными. Proposed game заявка не принималась.

Сквозной CORE8 завершён; далее функциональная очередь владельца модуля.
[Предыдущий STATUS](HISTORY.md#engine-status-before-core85-2026-11-11).
