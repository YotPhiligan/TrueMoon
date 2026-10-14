# Игровой движок и редактор: состояние

Дата: **2026-10-13 — документирование будущего направления**.
[План](PLAN.md), [этапы](PLAN.md#этапы-будущей-реализации),
[общий статус](../STATUS.md).

## Текущее состояние

**ENG0–ENG8 запланированы.** Новых engine/editor проектов, public API, 3D renderer,
frame transport или сборки/replacement Game.Logic в этой работе не создавалось.

| Область | Зафиксированное направление | Проверки собственной реализации |
| --- | --- | --- |
| Editor / Game.Runtime | Разные процессы; live world в child, authoring data в Editor; viewport получает кадры | Не выполнялись |
| Lifecycle / control | Mithril ownership + Enerit commands/events; нужна repeatable game session lifecycle | Не выполнялись; существующий App controller не является готовым session supervisor |
| Code update | Versioned game build и child replacement; checkpoint/reset/rollback; Hot Reload позже | Не выполнялись |
| Frame transport | CPU/shared RAM prototype, затем GPU candidate с capability/completion/lifetime checks | Не выполнялись; текущий Alloy lease внутри одного процесса |
| 3D game | Компактная сцена определяет engine, assets/physics/audio и инструменты | Жанр, платформа, hardware/FPS и точный scope ещё не выбраны |

Существующие результаты Core/DI, Enerit, Mithril и UI имеют собственный scope
в [общем статусе](../STATUS.md); они не подтверждают engine/editor readiness.
План не меняет общий порядок работы над framework и UI K1–K9/PA1–PA10.
Сверка2026-10-14: MIT4/ENE4 bounded App/IPC/restart proof выполнен отдельно;
ENG0–ENG8 по-прежнему запланированы, frame transport/hot reload не реализованы.

## Ограничения и решения, требующие проверки

- Предлагаемый baseline — Windows x64/net10.0; graphics API/platform matrix не
  утверждены. CPU fallback/GPU sharing policy определяется в ENG0.
- Editor остаётся запущен при обычном child failure; общий GPU/driver fault не
  считается изолированным только благодаря process boundary.
- C# update требует сборки/deltas; отсутствие паузы game process и восстановление
  всего runtime state не обещаются. Snapshot migrations выбираются явно.
- Enerit payload1MiB/same-build scope и sequential in-process Vulkan UI contracts
  требуют отдельных integration decisions. Shared RAM frames не закрывают ENE3.

## Следующий шаг при начале реализации

**ENG0**, затем **ENG1–ENG3**: выбрать маленький game scenario, определить session/
control/frame contracts и построить Editor/child probe с frame pattern и
replacement игровой сборки. Сначала подтвердить ownership/readiness/compile failure/
crash recovery и сохранение Editor state; затем ENG4 GPU path и ENG5 3D-сцена.

## Проверки текущей документационной работы

Сверены общие правила и существующие module contracts; документы добавлены в
общий план/индекс. Проверки links/anchors/fences/whitespace и итоговый diff
фиксируются по фактическому запуску ниже. Code/build/unit/process/GPU проверки
не запускались; эта работа не создаёт новых passing результатов модулей.

Фактически выполнено2026-10-13: 42 Markdown documents, 447 local links, 107 heading links;0problems. Проверка fences/conflict markers/NUL и git diff --check passed. Артефакты ignored TestResults/EnginePlanning. Новые files проверены отдельно на whitespace и наличие всех ENG0–ENG8.
