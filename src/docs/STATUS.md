# TrueMoon: общий статус

**2026-11-15 — Kul ARCH1h.2a подключён к Aether через общий scene-subsystem слой.**
[Контракт](audio/ECS_BRIDGE.md), [engine задача](engine/PLAN.md#arch1h2a-kul-session),
[результаты/команды](engine/HISTORY.md#kul-ecs-2026-11-15), [manifest/SHA](audio/KUL_ECS_RESULTS.json).
Запрошенный порядок ARCH1j → Kton PHYS5 → Kul завершён в своих bounded profiles.
Optional neutral SDK/Runtime adapter; captured input до tick, silent preparation,
session-owned playback и cleanup. Preview/device включаются явно в composition root.

## Состояние модулей

| Направление | Проверенный scope | Что дальше |
| --- | --- | --- |
| [Core/Cobalt/Enerit/Mithril](core/STATUS.md) | Прежние CORE7/8/IPC/lifecycle proofs сохраняют даты; новая общая конфигурация Kul bridge | Новые заявки/приёмка у владельцев |
| [Alloy/Argentis](alloy/STATUS.md) | Прежний native/UI scope сохранён; Kul не добавляет GPU/physical input acceptance | PA8c/input/DPI/K7/K8/release |
| [Aether/Editor](engine/STATUS.md) | ARCH3.1–9/ARCH1j/PHYS5 и Kul ARCH1h.2a scoped complete | Прежний ENG7b.3 кандидат; ED/import отдельно |
| [Kton](physics/STATUS.md) | PHYS0–5; текущая regression68D/R и same-entity Kul/Kton both-DI proof | PHYS6/PHYS8 отдельно; ragdoll необязателен |
| [Kul](audio/STATUS.md) | Windows x64 AUD1 +ARCH1h.2a ECS/captured-content;103adapter+27native tests D/R, offline/muted WASAPI delivery | AUD2–3 RT/async/device loss/OS; spatial/import/Nom отдельно |
| [Nom](nom/STATUS.md) | Прежние NOM0–NOM8 storage/buffer proofs сохраняют даты | Engine g.2 после g.1; VM verification отложена |

## Проверки и ограничения

Текущий scoped run:103adapter+27Kul+104common+68PHYS5 tests каждый D/R,
0failed/0skipped; exact slnx discovery103cases. Non-incremental solution D/R0errors/
1570existing warnings; финальные incremental builds0errors/0warnings. Independent
4offline reports×59checks и4muted actual WASAPI reports×43checks, оба DI;
final native owners/resident bytes0.3package metadata/payload checks и13archive
closed feed/profile,18-library runtime closure, exact managed/native SHA.

Это не новый full framework/UI/Editor regression и не единая поставка всех пакетов.
Прежние ARCH3.9/ARCH1j/PHYS5 counts сохраняются как исторические:
[предыдущий общий STATUS](HISTORY.md#common-status-before-kul-ecs-2026-11-15).
Spatial/listener, Editor import/Nom, fixed GameBuildArtifact ABI/ALC extension,
native-state reload/migration, device loss/long soak/RT/peak budgets/other OS,
физическое прослушивание, G4/K7/G6/R1–R6 и общий release не закрыты.
Причина исторического process-output drain timeout остаётся открыта.
Raw artifacts локальны, versioned HISTORY/manifest сохраняют команды, итог и scope.
