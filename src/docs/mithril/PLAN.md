# Mithril: план units и процессного lifecycle

Первичный roadmap2026-10-09. [Статус](STATUS.md), [общий план](../PLAN.md), [правила](../PLANNING.md). Новые guarantees/semantics ещё требуют фиксации перед реализацией.

## Границы

TrueMoon.Mithril, Mithril.Tests и ManualTests/MithrilTest. UnitsController, configuration, hosting/startup/lifetime/restart policies и handles main/child/external process.

App lifecycle/configuration принадлежат [Core](../core/PLAN.md), invocation/transport — [Enerit](../enerit/PLAN.md). У Mithril нет прямого ProjectReference на Enerit; совместный пример собирает их через приложение. Этот план не создаёт такой зависимости автоматически.

## Цель

Приложение запускает собственные units в заявленных hosting режимах, наблюдает exit/failure, применяет согласованные startup/restart policies и завершается без оставшихся принадлежащих ему процессов/handles. External process ownership описывается отдельно.

## Архитектура и решения

Сохранять IUnitsController, IUnitConfiguration и IUnitHandle boundaries, явные policy values и configuration через Contracts. Runtime зависит от Contracts/Core, не от UI.

Определить semantics Start/Spawn/Stop/disposal, startup cancellation/failure, identity конфигурации/handle и допустимого restart. Разделить owned child и borrowed/external process: действия с чужими процессами не выводятся из общего app shutdown. Правила restart limits/delay и exit во время stop согласовать до реализации гарантий.

## Этапы

| ID | Результат | Критерий завершения |
| --- | --- | --- |
| MIT0 | Сверка discovery и рабочего manual scenario | Зафиксирован фактический набор тестов, в том числе zero discovery; пример запускает только собственный процесс и сохраняет результат |
| MIT1 | Configuration/startup/hosting contract | Main/child/external modes подтверждены в объявленном scope; invalid configuration/start failure/cancel не оставляют partial handles |
| MIT2 | Shutdown и resource ownership | Stop/disposal/repeat/cancel, parent exit и cleanup соответствуют контракту; внешние процессы обрабатываются по явному ownership правилу |
| MIT3 | Restart и отказ процесса | Normal/crash exits, policy selection и остановка во время restart воспроизводимы; нет непредусмотренного повторного запуска или неконтролируемого restart loop |
| MIT4 | Интеграция с Enerit/Core | Own child unit выполняет IPC, при exit/failure канал и процесс закрываются по контрактам, app shutdown согласован |
| MIT5 | Consumer и operational diagnostics | Published/package consumer запускает/останавливает own child; policy/error/exit diagnostics и commands/configuration воспроизводимы |

MIT0 не закрывается успешным build пустого test project. MIT1–MIT3 проверять на собственных тестовых процессах с bounded временем и cleanup. Системные службы/чужие приложения не используются как тестовые units.

## Критерии готовности

- Supported hosting/startup/lifetime/restart policies имеют зафиксированный результат и поведенческие checks.
- Failure/cancellation/shutdown не оставляют owned processes, handles или subscriptions; restart не продолжается после согласованного stop.
- Сохраняются исходные exit/failure причины и принадлежность процесса, public errors доступны caller/diagnostics.
- Independent consumer доставляет нужные executable/configuration artifacts и работает без исходного solution.
- Полный process/IPC scenario подтверждён на заявленной OS/configuration; неподдержанные режимы отмечены отдельно.

## После первой рабочей проверки

Развивать наблюдаемость, управляемые политики и удобство deployment по реальным units. Distributed orchestration, remote supervisors и расширение process ownership не входят автоматически в первую рабочую версию.
