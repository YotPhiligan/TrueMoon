# Enerit: состояние и точка продолжения

Дата: **2026-10-11 — ENE1/ENE2 bounded payload и own-process pipes baseline**.
[План и контракт](PLAN.md#ene1-ene2-контракт--2026-10-11),
[команды и история](HISTORY.md#ene1-ene2-pipes-baseline--2026-10-11), [общий статус](../STATUS.md).

## Реализация и текущие проверки

| Область | Реализация | Текущий запуск |
| --- | --- | --- |
| Runtime | Payload length до1MiB; collection count до65 536; validation до decode/rental; полный frame под write gate | **71passed/0failed/0skipped** отдельно Debug и Release; сохранены прежние30 cases и добавлен41 case |
| Generated contract / ENE1 | Nullable strings/collections/bytes исправлены; TME001 для unsupported shapes; unknown method/trailing request; byte method-code limit | **34passed/0failed/0skipped** отдельно Debug и Release;25 новых compile/emit/diagnostic/boundary cases плюс прежние9 |
| Own-process pipes / ENE2 | Реальные generated peers, уникальные endpoints, explicit name overloads; typed payload/concurrent/cancel/remote errors/disconnect/dispose/rebind | **12/12** Debug и published Release apphost, Completed=true;6 hosted process cases также в каждой runtime suite |
| Lifecycle | Void/Task acknowledgement; local cancellation drains late response; original disconnect errors; existing client reconnect; owned shutdown | Pending map=0, оба client workers completed;12 owned child exit0/handles released; cooperative server rebind прошёл |
| Full solution | Non-incremental Debug build, включая обновлённый MithrilTest interface | Exit0, **0errors/1242warnings**; другие модульные suites/UI/runtime examples не запускались |
| MMF / ENE3 | Implementation существует, tests закомментированы | Не запускался; readiness не подтверждена |

Windows x64 build26300, net10.0, SDK10.0.401, xUnit2.9.3/VSTest.
Manual summary UTC2026-10-11T15:42:42; source hashes проверяются отдельно.
ENE1/ENE2 выполнены в принятом bounded scope; это не общая production readiness.

## Ограничения и изменения совместимости

- Только peers одинаковой сборки/generator. Native scalar layouts и Roslyn method
  order не имеют schema/version negotiation. Void/Task теперь требуют ack;
  nullable strings используют один marker. Старые peers с новыми несовместимы.
- Unsupported payloads дают TME001; object/tuple/custom-struct experiments старого
  MithrilTest исключены из generated interface, обычные implementation methods
  сохранены. Caller-visible supported набор задан в PLAN; расширение требует codec tests.
- Cancellation/50s timeout локальные, отдельного cancel frame нет. Already-started
  handler может завершиться позже; reconnect не повторяет failed invocation.
- Nested ReadBytes rentals принадлежат handler/caller. Автоматический возврат после
  partial nested decode и response trailing-byte validation пока открыты.
- Queues без backpressure; uncooperative handler может удерживать synchronous
  Dispose. Bounded process proof использует cooperative handlers и конечные deadlines.
- Старые PipesTests и четыре исходных generator tests имеют слабые assertions;
  вывод о новом контракте опирается на compile/emit и поведенческие regressions/probe.
- App/AddUnit/controller/parent-exit/restart, MMF, independent Enerit packages и
  performance не подтверждены. Mithril Never handle используется только для запуска
  owned child; это не ENE4 composition proof. XML/nullability/RS1041 warnings остаются.

## Следующий конкретный шаг

Общий G3: **MIT4/ENE4** — App/AddUnit + generated IPC + restart composition. Mithril MIT3 bounded handle/controller restart проверен2026-10-12 отдельно. MIT1/MIT2 App/controller/parent lifecycle проверены отдельно: [Mithril scope](../mithril/STATUS.md). Затем MIT4/ENE4 общий App+IPC proof. Локально Enerit: schema/versioning,
response validation и nested-buffer cleanup/backpressure до расширения readiness;
MMF ENE3 и package/performance ENE5 имеют отдельные критерии.

Последующий MIT1/MIT2 run2026-10-11 повторил Enerit runtime Debug71/71 и Release71/71 как регрессию launcher. Generator34/34 и manual12/12 выше принадлежат предыдущему ENE1/ENE2 run; они не повторены. [Команды последующего запуска](../mithril/HISTORY.md#mit1mit2-app-composition--2026-10-11).

MIT3 run2026-10-12 повторил Enerit runtime Debug71/71 и Release71/71; generator/manual выше не повторялись. [Текущие команды](../mithril/HISTORY.md#mit3-bounded-restart--2026-10-12).
