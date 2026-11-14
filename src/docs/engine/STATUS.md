# Aether / Editor: состояние и точка продолжения

**2026-11-15 — ARCH3.9 завершён: сквозная регрессия, единая поставка и руководство.**
[Выбор слоёв/SDK/ownership/migration](ARCHITECTURE_GUIDE.md),
[518соответствий/417sources](ARCH39_MIGRATION.csv), [graph6projects](ARCH39_PROJECT_GRAPH.json),
[baseline/SHA](ARCH39_BASELINE.json), [команды/evidence](HISTORY.md#arch39-2026-11-15).

ARCH3.1–9 закрыты в scoped Windows x64 profile: Abstractions, Runtime, headless
Tooling, Editor.UI и optional Editor.Tooling bridge; Mod SDK/loader, stable World
replacement и private-component migration. Одна immutable поставка20nupkg версии
`0.3.0.1-alpha.arch39.20261115005454` использована всеми consumers по точным SHA. Новых production
API/namespace переносов в этом этапе нет; поправлены acceptance runners и навигация.

| Проверка | Фактический результат |
| --- | --- |
| Non-incremental solution Debug/Release |0errors;1570warnings/configuration, прежний набор |
| Все14test projects Debug/Release |8906passed/configuration,28TRX, failed/skipped0 |
| SDK / Runtime / Tooling / Runtime+UI / Player |6 /8 /6 /8 /6profiles; coherent packages, isolated caches/closed feeds, оба DI |
| Common configuration/App/child |15profiles;20пакетов одной версии, фактические owned children |
| Stable reload / private-component migration |По12profiles ×20замен =480retired ALC; old collect при live current,24final contexts после App cleanup |
| Legacy mod loader |12profiles; прежние load/unload semantics сохранены |
| Full Editor |8package cases +2source GUI +2published-package GUI; Standard/Cobalt и Embedded/MS DI |
| Negative delivery / map |7invalid manifests rejected +positive exact copy;518namespace/path mappings,6project boundaries |

Source/published/fresh NuGet/SDK-free там, где предусмотрено соответствующим runner.
Повторные mod profiles сохраняют snapshots/disposed Worlds/stale contexts/guards,
проверяют IDs/relations/values/state/tick, старые commands/callbacks, owners0 и exact
disposal. Private migration:1explicit v1→v2 и19same-schema/new-CLR-generation steps.
Native Runtime+UI/Player/full Editor отдельно проверены validation0/0, resource/
handle/surface balance и child reap. Это не20GPU-mod-migrations: asset/native/GPU
migration не входит в managed profile. Unfenced side effects не получают rollback.
Другие OS/RID/AOT/trimming, physical input/DPI, device loss/K7/VRAM/long soak и
release gates не расширены. Causal drain follow-up не закрывается passing runs.

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

**ENG7b.3 — расширенная матрица Player/combined hosts:** source/published/fresh
profiles, role selection/selected content, child identity rejection, repeated Play/
Stop/crash/reap и сохранность документа. Это отдельный следующий кандидат после
завершённого ARCH3; в этом срезе не выполнялся. Functional ED/import/audio/physics/
Nom очереди и causal drain/release gates сохранены. Request/Claim/Decision не менялись.
[Предыдущий STATUS](HISTORY.md#engine-status-before-arch39-2026-11-15).
