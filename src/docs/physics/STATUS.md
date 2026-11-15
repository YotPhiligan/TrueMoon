# TrueMoon.Kton: состояние и следующий шаг

**2026-11-15 — PHYS5 ECS/session bridge реализован и проверен.**
[Контракт/API/limits](ECS_BRIDGE.md), [проверки/команды](HISTORY.md#phys5-2026-11-15),
[manifest/SHA](PHYS5_RESULTS.json), [independent consumer](../../ManualTests/KtonEcs/README.md).

`TrueMoon.Aether.Kton` отделяет neutral components/schemas/helpers от
`TrueMoon.Aether.Kton.Runtime` с App/DI/registry и session-owned physics world.
Provider выбирает composition root; основной Runtime и neutral Kton не получили
backend/bridge references. Общий ARCH1j source сохранён без изменений.

| Scope | Реализовано | Фактическая проверка15ноября |
| --- | --- | --- |
| App/DI/configuration | UseAetherKton/UseConfiguredAetherKton, exact schema Kton:Bridge:MaximumContactEvents; lazy typed factories, independent worlds |68tests D/R, Cobalt/MS DI и independent8provider executions |
| ECS/scene | Body/velocity schema v1, box/sphere/capsule, Static/Dynamic/Kinematic, root/unit-scale, explicit velocity/teleport, ExternalMotion | Real BEPU Step/writeback/rotation, schema/default/version/forwarding/filter и rejection cases |
| Phase lifecycle | Gameplay spawn/delete/attach/detach, descriptor replacement, stale/foreign generations | Same-tick reconciliation, fresh body IDs, original stale End mapping; helpers validate write declarations |
| Events | PhysicsContact transient entities после Movement, Gameplay next-tick, bounded full projection | Begin/Stay/End/retirement, mask0, exact capacity/overflow и command capacity failures |
| Ownership/failures | Scheduler systems → native world → borrowed ECS World; cleanup retry retains owner/lease | Cancel/factory/shape/Step/cleanup errors, failed candidate сохраняет old session; owners0/pools0 |
| Delivery/build |2new packages, common SDK/TFM/profile, pure SDK/explicit provider closure | Solution D/R0errors/1570existing warnings;2metadata archives; source/published/fresh42checks×4reports, closed runtime graph20libraries |

Новый xUnit suite: **68passed/0failed/0skipped каждый Debug/Release**. Точный slnx
entrypoint обнаружил68cases; common subsystem regression104D/R. Полный15-suite
run и новая native/GPU/physical/platform acceptance не выполнялись.

Ограничения: exact60Hz, root/unit-scale COM primitives, no mesh/joint ECS/controller.
Queries остаются neutral Kton API; отдельный injected game/session query reader,
physics checkpoint/native-state migration и game-artifact shared ABI не расширены.
Contact projection default64/max256, overflow terminal; Ecs command/entity/type
budgets проверяются существующим scheduler. PhysicsBody readonly pose меняется
через explicit teleport, runtime roles — через PhysicsEntityCommands.

## Следующий конкретный шаг

Общая последовательность ARCH1j → PHYS5 → [Kul ARCH1h.2a](../audio/ECS_BRIDGE.md)
завершена в своих профилях. Новая совместная same-entity проверка обоих DI и68PHYS5
regression cases каждый D/R выполнены в Kul acceptance; прежний PHYS5 manifest сохранён.
Следующий физический feature — [PHYS6 controller](PLAN.md#phys6), затем PHYS8 wider delivery/
performance по отдельному scope. PHYS7 ragdoll необязателен. Previous PHYS4 и
pre-PHYS5 STATUS сохранены в [истории](HISTORY.md#physics-status-before-phys5-2026-11-15);
старые manifests не переписывались. Claims/requests не менялись.
