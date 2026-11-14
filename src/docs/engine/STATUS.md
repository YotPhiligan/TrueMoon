# Aether / Editor: состояние и точка продолжения

**2026-11-14 — ARCH3.6 завершён: managed Mod SDK, manifest и collectible loader.**
[API/manifest/DI/ownership/границы](MODS.md), [manifest/SHA](ARCH36_BASELINE.json),
[команды/приёмка](HISTORY.md#arch36-2026-11-14).

Abstractions.Mods содержит нейтральный SDK; Runtime.Mods — ModHost и один
collectible ALC на WholeGroup closure; Tooling.Mods — явный immutable publisher.
Версии/ranges/cycles, hashes/limits/paths, exact shared/private identities
проверяются до entry activation. App владеет группами, локальные фабрики/Types
не регистрируются в root DI; App/provider/bootstrap owners недоступны модам.
Частные Worlds используют настоящий ECS. Stop закрывает admission до ожидания
active startup/tick; cleanup освобождает systems до Worlds и сохраняет failed
owners для retry. Нейтральные outcomes не удерживают модовые exceptions.
Weak tickets различают unload requested и collection; оба факта проверены.

| Фактическая проверка | Результат |
| --- | --- |
| Non-incremental solution Debug/Release | 0 errors, warnings 1570/1570 |
| Полные Aether / Abstractions / Player / Editor D/R | По 3406 / 26 / 17 / 1507 passed; 4956 на configuration, failed/skipped 0 |
| Новые Mod tests | 53 на configuration: validation/dependencies/identity, startup/cancel, cleanup retry, tick fence, weak collection |
| Source/publish D/R + fresh + SDK-free, both DI | 12 managed profiles; component 7→8, private dependency, exact lifecycle и collection |
| Independent SDK/feed | 11 свежих framework packages; SDK mod получает только Abstractions + Contracts; Runtime graph без Tooling/UI |

Windows x64, SDK11.0.100, net11.0, runtime-async=on, xUnit2/VSTest.
Exact shared hashes требуют coherent rebuild/republication host/game/mod artifacts;
schema2 и прежние scene/project/control/frame/checkpoint formats не менялись.
GameBuildLoader сохранён отдельно. Stage3.5 native/GPU/GUI/configuration proofs
остаются датированной историей: этим этапом они не повторялись. Native mods,
forced abort, system hot replacement/new-World migration, другие OS/AOT/release
не приняты; они не следуют из managed unload. Package props/Skia SHA сохранены.

**2026-11-12 — TrueMoon.Kul Windows x64 AUD1 готов в описанном scope.**
[Единственный план/контракт](../audio/PLAN.md), [App/DI и native build guide](../audio/README.md),
[новая приёмка Kul](../audio/HISTORY.md#kul-rename-2026-11-12), [manifest](../audio/KUL_RENAME_RESULTS.json).
UseAudio/UseConfiguredAudio, caller-owned sessions, decoded clips/voices, music streaming,
App stop/drain и partial-start rollback реализованы на исправленном MiniAudioEx.
23tests D/R, discovery через solution; source Debug/published Release/fresh-package
по23offline+18muted actual WASAPI checks, logical owners0/files closed после Stop.
Исторический [native fix](HISTORY.md#audio-native-fix-2026-11-12) не повторялся этим срезом;
все новые profiles загрузили exact patched DLL. Нового audible audition не было.
Далее Aether ECS/captured-asset adapter по ARCH3 boundaries. В Audio AUD2–3 остаются
device loss/RT soak/queues/hard peaks/spatial/другие OS; renderer/Player очередь прежняя.

## Kton / физика — PHYS4 App/configuration/DI 2026-11-12

TrueMoon.Kton.Bepu public App integration/factories/lifecycle реализованы:
192checks/profile (96Cobalt+96MicrosoftDI), source/fresh NuGet Debug/published Release;
реальные worlds, generated constructor,9package closure и cleanup retry.
Current PHYS2–3 regression309×3. [Scope/evidence](../physics/HISTORY.md#phys4-2026-11-12),
[App guide](../physics/APP.md), [следующий PHYS5](../physics/STATUS.md).
ECS bridge ещё впереди и согласуется с ARCH3. [Единый план](../physics/PLAN.md#current-physics-queue),
[ARCH1i](PLAN.md#arch1i-physics). Engine/GPU/platform readiness и очередь не меняются.

## Следующий конкретный шаг

**ARCH3.7 — горячая замена систем со стабильными компонентами:** отдельная
candidate generation, resumable quiesce, neutral capture и один commit на tick
boundary; сохранить last-good World/system state до commit, затем retire old
callbacks/factories/caches/tasks. New-World component migration — ARCH3.8,
сквозная приёмка — ARCH3.9. ENG7b.3/release и causal drain follow-up открыты.
Audio/Kton имеют свои очереди; proposed Request/Claim/Decision не изменены.
[Предыдущий STATUS целиком](HISTORY.md#engine-status-before-arch36-2026-11-14).
