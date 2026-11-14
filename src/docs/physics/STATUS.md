# TrueMoon.Kton: состояние и следующий шаг

**2026-11-14 — предкоммитная сверка:** App192checks и provider309checks повторены
каждый в source Debug/fresh package Debug/published Release; package metadata
Kton/Kton.Bepu прошла. [Состав, команды и границы](../COMMIT_AUDIT_2026-11-14.md).
PHYS5 и wider delivery/release readiness этим не объявляются.

**2026-11-12 — PHYS4 public App/configuration/DI/lifetime реализован и проверен.**

[Единый план](PLAN.md#current-physics-queue), [App guide](APP.md), [контракт](CONTRACTS.md),
[provider](PROVIDER.md), [queries/events](QUERIES_CONTACTS.md),
[команды/границы](HISTORY.md#phys4-2026-11-12), [manifest](PHYS4_RESULTS.json),
[runner](../../ManualTests/KtonApp/README.md).

| Область | Реализация | Фактическая проверка12ноября |
| --- | --- | --- |
| App/configuration | UseBepuPhysics/UseConfiguredBepuPhysics,16fields Kton:DefaultWorld:*, validated detached startup snapshot | Все fields, source priority/provenance/callback precedence, invalid/null/duplicates/mixed registration, no backend allocation at Build/start |
| DI/factory | Existing typed/base IFactory aliases, один App-owned BepuPhysicsHost, adapters не IDisposable | **192/192checks/profile:96Cobalt+96MicrosoftDI**, source Debug/fresh NuGet Debug/published Release; real generated consumer constructor; exact9package graph/9DLL SHA+1shared-framework DLL |
| World ownership | Create() defaults, Create(options) complete arguments; successful worlds exclusively caller-owned | Независимые миры/Apps, immutable snapshots, early disposal, live world после App disposal; no alias double Dispose |
| Lifecycle/failures | Reverse consumer stop/startup rollback, permanent admission close, retained failed-candidate owner | Startup/stop/DI Dispose/cached failure, cancellation boundaries, cleanup error aggregation, explicit retained-host retry и caller-world retry after App; final owners0/pools0 |
| Provider regression | PHYS2–3 bodies/joints/commands/fixed Step/queries/contacts/generational IDs | **309/309checks×3profiles** повторены на текущем provider;5package closure/actual DLL SHA; neutral-only suite отдельно не запускалась |
| Следующие этапы | PHYS5 Aether ECS/session bridge, PHYS6 controller, PHYS8 wider delivery/performance | Full solution tests/GPU/native launch/other OS/AOT/2+workers/remote release в PHYS4 не запускались |

## Ограничения

App закрывает фабрику и очищает unpublished failed creations; сессия join/drain/dispose
своих миров. Root fallback disposal выданных миров не введён. Host.IsReleased не
утверждает PublishedWorlds=0. После финального App/DI Dispose failure новая попытка
выполняется на заранее удержанном borrowed BepuPhysicsHost: App/provider могут
вернуть закешированную ошибку. Мир с failed Dispose сохраняется самим caller.

Профиль проверки: Windows x64/.NET11/JIT/1worker. Fault injection доказывает retry
между завершёнными стадиями, не arbitrary partial unsafe BEPU.Dispose recovery.
No-arg factory defaults фиксируются до ресурсов; App configuration reload не меняет
модуль/миры. Stop cleanup синхронный и не прерывается cancellation token.
Source targeted build0errors/494existing upstream XML/nullability warnings;
Kton/provider/probe warnings0; fresh consumer Debug/Release0warnings/0errors.
Budgets не являются hard RAM/frame-time гарантиями. Предыдущий PHYS3 STATUS сохранён
в [HISTORY](HISTORY.md#physics-status-before-phys4-2026-11-12), manifests PHYS0–3 не переписаны.

## Следующий конкретный шаг

**[PHYS5](PLAN.md#phys5):** согласовать Aether Abstractions/Runtime boundary по ARCH3,
добавить ECS value components/registry, prepare/update/writeback и session ownership.
Проверить spawn/delete/teleport, pause/Stop/Replace, next-tick contact events и cleanup
без освобождения borrowed ECS World/dependencies. Текущая очередь Aether/Claims не менялась.
