# TrueMoon: общий статус

Последняя работа: **2026-10-10 — исправление выявленных проблем Core/DI и зависимого Enerit cleanup**. [Общий план](PLAN.md), [правила ведения](PLANNING.md), [результаты Core](core/STATUS.md), [команды/TRX](core/HISTORY.md#исправление-проблем-ядра--2026-10-10). Полная non-incremental solution сборка: exit0/errors0/warnings1271. Итоговый test run: **643passed/0failed/0skipped**, Mithril discovery0 отдельно. AppCreate failure устранён, contracts зафиксированы в модульных PLAN; NU1506/NU1507 устранены локальными package/source настройками.

Предыдущая UI работа2026-10-10: native WGL/thread lifetime investigation и shared-loop prototype поверх `9d7090a`. USER growth воспроизведён raw WGL без GLFW/Silk/UI; diagnostic reuse/shared-loop результаты поддерживают следующий Hosting dispatcher шаг. Production UI scheduling ещё не изменён; прежний failed full lifecycle result и G4/K7 остаются открыты. В текущем общем run повторена только unit suite480/480; native/physical probes не повторялись.

## Состояние модулей

| Модуль | Реализация и область вывода | Проверки и продолжение |
| --- | --- | --- |
| [Core](core/STATUS.md) | Исправлены runner/rollback/error/cancellation, lifetime/console ownership, providers/diagnostics, scheduler/pools | TrueMoon.Tests81/81, Failed0/Skipped0, discovery81; AppCreate Passed. Следом CORE5 package/headless consumer |
| [Cobalt / DI](cobalt/STATUS.md) | Exact runtime graph, Composite/generics, containers, singleton/owned disposal и adapter wrapper | Runtime37/37, generator6/6; оба providers проверены в Core. Следом COB4 analyzer/package consumer |
| [Enerit](enerit/STATUS.md) | Inherited mapping сохранён; исправлены pools/worker/handler ownership, partial/EOF reads и pending responses | Runtime30/30, generator9/9, Failed0/Skipped0. Новые20 runtime cases in-process; MMF отключён. Следом ENE1/ENE2 own-process |
| [Mithril](mithril/STATUS.md) | UnitsController, policies и process handles; production не менялся | Текущий solution discovery0/TRX total0; runtime/manual consumer собраны. Successful process checks не получены |
| [Alloy / Argentis](alloy/STATUS.md) | TextBox/input diagnostics, cleanup callback roots, native WGL/thread controls и manual shared owner loop. Alpha частична | Unit suite480/480 повторена в solution. Предыдущие raw WGL/reuse/shared-loop probes не повторены; прежний полный mixed resource check **failed**. Production dispatcher/physical DPI/package/VRAM остаются |

Наличие source/test projects означает наличие реализации или сценариев, а не успешное прохождение. Подробности и датированные результаты принадлежат локальным STATUS/HISTORY.

## Общие этапы

| Этап | Состояние |
| --- | --- |
| G1. Исходное состояние | Актуальные solution build/discovery/test результаты получены по всем7 test projects; Mithril tests0 явно выделен |
| G2. Core и DI | Известные defects исправлены; component/integration lifecycle на обоих providers проходит. Package/independent headless proof ещё нужен |
| G3. IPC и units | Реализация существует, сквозной отказ/restart/shutdown proof не выполнен этой работой |
| G4. UI alpha | Частично; UI критерии и собственный следующий шаг — в Alloy плане |
| G5. Сквозные examples | ManualTests существуют; весь заявленный набор общего плана ещё не подтверждён |
| G6. Общая поставка | Общие R1–R6 не закрыты; версия0.2.2.7-alpha не является readiness свидетельством |

## Общие проверки — датированные сведения

**Итог исправлений Core/DI2026-10-10:** Windows/Debug/net10.0, SDK10.0.401, xUnit2.9.3/VSTest. `dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet`: exit0/0errors/1271warnings. Стандартный solution test с no-build/no-restore и отдельная list-tests discovery: exit0. Core81, Cobalt37, Cobalt.Generator6, Enerit30, Enerit.Generator9, Alloy480 — все Passed, Failed0/Skipped0. Mithril TRX total0 не считается passing suite. [Команды, requirement matrix, TRX и ограничения](core/HISTORY.md#исправление-проблем-ядра--2026-10-10); локальные детали — [Cobalt](cobalt/HISTORY.md), [Enerit](enerit/HISTORY.md#совместимость-pools-и-завершение-pipes--2026-10-10). Release/pack/own-process/native UI не запускались.

**Предыдущий Core review2026-10-10:** TrueMoon.Tests **Passed3/Failed1/Skipped0**, discovered4; AppCreate падал на `ServiceResolvingException<IAppLifetime>` через чужой LifeTimeExecutor resolver. TrueMoon.Core incremental build exit0/errors0. [Команды, scope и выводы](core/HISTORY.md#ревью-ядра-и-core0-baseline--2026-10-10). Два passing RunAsync теста тогда содержали Assert.True(true); в текущем исправлении заменены behavioral assertions.

Предыдущая UI investigation: consumer Debug build/published Release, native WGL/thread240-cycle comparisons и shared-loop prototype с двумя live окнами/close/recreate. [Команды и ограничения](alloy/HISTORY.md#wgl-thread-lifetime-и-shared-owner-loop--2026-10-10). Production dispatcher и полный mixed resource result этим шагом не исправлены; unit suite/общий baseline/другие модули той UI работой не запускались заново.

Предыдущая lifecycle работа2026-10-10: UI suite **480passed/0failed/0skipped**, short/callback/retirement proofs и два полных240-window Debug/published Release прогона. Оба полных window resource results **failed** на USER growth при нулевых hooks/registry/subscriptions/retained UI roots. [Команды и ограничения](alloy/HISTORY.md#lifecycle-resize-soak-и-native-resource-growth--2026-10-10). Предыдущие TextBox/Name20repeats и backend comparison — [отдельный запуск](alloy/HISTORY.md#textbox-control-text-и-name-smoke--2026-10-10).

**Интеграция2026-10-10, main `d56f217`:** Windows/Debug/net10.0, SDK10.0.401, xUnit2.9.3/VSTest. Enerit runtime **10passed**, generator **9passed**, Alloy UI **469passed**, во всех трёх проектах Failed0/Skipped0. AlloyVulkanTest consumer build exit0, **0errors/227warnings**. Debug native callback probe exit0:6direct+12hosted failures+12healthy recreations, HWND/tree/registry/hooks cleanup, Vulkan validation0errors/0warnings. Команды и локальный scope — [Enerit история](enerit/HISTORY.md#интеграция-в-main--2026-10-10), [UI история](alloy/HISTORY.md#интеграция-в-main--2026-10-10). Артефакты — ignored `TestResults/AgentMerge` в основном checkout. Общий solution build/tests, Release publish и physical DPI matrix при интеграции не повторялись; их исторические результаты не являются новым запуском.

Из записи предыдущего UI code run **2026-10-09** сохранено: non-incremental TrueMoon.slnx build **0errors/1341warnings**, SDK10.0.401/net10.0; локальные Release publish AlloyTest/AlloyVulkanTest exit0. Подробные команды, configuration и native ограничения — [UI история](alloy/HISTORY.md#49d--k3-native-windows-dpi--реализация-и-запуск-2026-10-09).

Это предыдущий общий build и два UI consumers, не successful full solution test suite и не проверка package consumers остальных модулей. При реорганизации2026-10-09 и интеграции2026-10-10 эти команды не повторялись.

Из прежних записей **2026-09-27**: AppCreate failure и отсутствие найденных Mithril tests. AppCreate отдельно воспроизведён review2026-10-10, затем исправлен и проверен текущим run; Mithril discovery0 подтверждён текущим запуском. Старые index/Enerite записи сохранены в архиве UI документа как исторический контекст, не являются текущим blocker roadmap.

## Общие блокеры и неопределённости

- Mithril test project имеет current discovery0; process startup/restart/shutdown/parent-exit не подтверждены. Passing других модулей не закрывает MIT0–MIT3.
- G2 package/headless consumer и generator delivery ещё нужны. Cooperative shutdown не прерывает user Task, игнорирующий cancellation; Microsoft DI alias disposal отличается от Cobalt. Контракты/limitations закреплены в локальных PLAN.
- Политика поддержанных configuration/SDK/OS, warnings и package consumer matrix общего выпуска требует отдельной фиксации.
- UI physical DPI matrix ограничена доступным150% монитором. Это ограничение конкретной проверки UI, не препятствие работе над Core/DI/IPC/units.
- UI K7: прежний полный resource check failed. USER рост локализован до raw WGL на новых потоках без GLFW/Silk/UI; exact allocator и mixed private/handles growth остаются открытыми. Diagnostic reuse/shared-loop normal path проверен; следующим локальным этапом предлагается Hosting owner dispatcher и повтор полной matrix. G4 не закрыт.
- Общий lifecycle/ownership/error integration proof и независимая поставка generators ещё не подтверждены.

## Следующий конкретный шаг

1. G2/CORE5/COB4: независимый headless package consumer на Cobalt/adapter, analyzer delivery, diagnostics, cancellation/error и cleanup. Текущий solution baseline не заменяет package proof.
2. G3: Enerit ENE1/ENE2 serialization и own-process pipes; Mithril MIT0 bounded own-child startup/exit/cleanup. Current discovery0 требует собственных executable process scenarios.
3. UI: подготовить physical100/150/200%/разные DPI мониторов и продолжить локальную alpha matrix; пользовательские Windows settings без соответствующего запроса не менять.

## Изменения формата — 2026-10-09

Созданы общий PLAN/STATUS, правила и четыре модульных PLAN/STATUS. UI PLAN явно ограничен Alloy/Argentis и интеграциями; его цели/архитектура/идентификаторы/критерии сохраняются. Текущий UI STATUS сокращён; полный прежний документ сохранён в HISTORY. AGENTS указывает общую и локальные точки входа.

Проверки текущей правки прошли: чтение исходников/ProjectReference;17planning/AGENTS документов,270local links/21anchors, Markdown fences/whitespace/conflicts/NUL; полный прежний UI STATUS сохранён verbatim, K1–K9 table rows идентичны, PA1–PA10/net10.0/xUnit сохранены, пять модульных PLAN/STATUS пар присутствуют. Scoped git diff --check прошёл, source worktree status при финальной сверке не изменился, staged entries0. Артефакты документальной проверки — ignored TestResults/PlanningFormat. Новых passing build/tests/GPU/publish результатов эта запись не создаёт.
