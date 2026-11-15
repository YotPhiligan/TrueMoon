# Aether / Editor: состояние и точка продолжения

**2026-11-15 — Kul подключён к игровой scene session, ARCH1h.2a.**
[Контракт/API](../audio/ECS_BRIDGE.md), [единственная детальная задача](PLAN.md#arch1h2a-kul-session),
[команды/результаты](HISTORY.md#kul-ecs-2026-11-15), [manifest/SHA](../audio/KUL_ECS_RESULTS.json).

Optional neutral SDK и Runtime adapter добавляют source/command/state, явный immutable
audio capture, prepared sound/music pool и session playback. Тик выполняет controls
после primitive extraction; пауза сохраняет позиции, paused Step остаётся silent.
Кандидаты готовятся бесшумно; cleanup освобождает native session до capture pins/World.
Проверены same-entity Kton/Kul, полные phase commands, generation/rebind/EOF и cleanup retry.
Базовые Runtime/Abstractions, Kton/Bepu и26common ARCH1j hashes сохранены.

| Проверка15ноября | Результат/точный scope |
| --- | --- |
| Новый Kul bridge suite Debug/Release |103passed/config, failed/skipped0 |
| Самостоятельный Kul suite Debug/Release |27passed/config, включая4новых native flags tests |
| Common/Kton regression Debug/Release |104+68passed/config; не новый full engine/Editor suite |
| Solution |Non-incremental D/R0errors/1570existing warnings; финальные incremental D/R0errors/0warnings; exact discovery103cases |
| Independent source/published/fresh |4offline reports×59checks +4muted actual WASAPI reports×43checks, оба DI |
| Packages |3metadata/payload checks;13coherent archives/profile,18-library closure, exact managed/native SHA |

Предыдущие ARCH3.1–9/ARCH1j/PHYS5 результаты сохраняют свои даты и scope:
[предыдущий STATUS](HISTORY.md#engine-status-before-kul-ecs-2026-11-15).
Audio не подключается автоматически к Editor preview. Spatial/listener, Editor import/
Nom, fixed GameBuildArtifact ABI/ALC extension, native-state reload/migration,
device loss/soak/RT/other OS и audibility остаются отдельными задачами.

## Следующий конкретный шаг

Запрошенная последовательность ARCH1j → PHYS5 → Kul ARCH1h.2a завершена.
Следующий прежний engine кандидат — ENG7b.3, расширенная host/Player matrix;
ED/import/Nom, PHYS6 и AUD2–3 выбираются отдельно. Functional/physical/release gates
и причина исторического drain timeout сохранены. Claims/requests не менялись.
