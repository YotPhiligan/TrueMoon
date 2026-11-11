# Core: состояние и точка продолжения

**2026-11-11 — CORE8.5 и миграция CORE8 завершены в согласованном профиле.**
[План](PLAN.md#core8-dotnet11), [поставка, команды и ограничения](HISTORY.md#core85-delivery-2026-11-11),
[руководство перехода](DOTNET11_MIGRATION.md), [manifest пакетов и SHA](CORE8_DELIVERY.json).

- SDK 11.0.100 / net11.0 / runtime-async=on; runtime/API/dependencies в CORE8.5 не менялись.
- 16 свежих локальных TrueMoon packages одной версии: 0.3.0.1-alpha.core85.20261111221352.
  Runtime/analyzer/buildTransitive/native payloads и dependency versions проверены.
- App/configuration: 15 source/published/fresh profiles, 690 checks / 12 children, оба DI.
- Source/published examples: 4 cases; package game: 8 cases на обоих DI; fresh native viewport: 2 profiles.
- Все 11 suites повторно прошли D/R: **Passed 8567, Failed 0, Skipped 0 на конфигурацию**.
- Обычные solution builds D/R прошли; исторический warning backlog не закрывался.

Root README, AGENTS, package instructions и независимые runners используют текущий
профиль. Remote publication не выполнялась. [Предыдущий STATUS](HISTORY.md#core-status-before-core85-2026-11-11).

## Следующий конкретный шаг

Сквозная .NET 11 миграция завершена. Следующая функциональная очередь —
[ARCH1f.4](../engine/PLAN.md#arch1f-rendering); Core/diagnostics follow-up остаётся
у своего владельца. Physical/release/performance и прежние reliability причины
не закрываются package acceptance.
