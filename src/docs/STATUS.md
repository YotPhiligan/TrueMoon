# TrueMoon: общий статус

Последняя работа: **2026-10-10 — объединение агентных веток Enerit и Alloy/Argentis в main и интеграционные проверки**. [Общий план](PLAN.md), [правила ведения](PLANNING.md). Объединены `6e554e3` и `cbae354`; состояние исходников при проверках — `d56f217`. Общие contracts/build/package настройки не менялись. Проверены затронутые модули, не весь solution.

## Состояние модулей

| Модуль | Реализация и область вывода | Проверки и продолжение |
| --- | --- | --- |
| [Core](core/STATUS.md) | Найдены App.Builder/DefaultApp, lifecycle, configuration providers, diagnostics, Contracts/Core utilities | Чтение исходников; новый baseline не запускался. Исторический AppCreate failure требует текущего воспроизведения |
| [Cobalt / DI](cobalt/STATUS.md) | Найдены resolver/registration/lifetime/disposal, generator и Microsoft DI adapter | Есть runtime/generator test projects, но их успешный run этой работой не подтверждён. Следом baseline обоих DI путей |
| [Enerit](enerit/STATUS.md) | ENE0 baseline выполнен; исправлены inherited generated mapping, одинаковые сигнатуры и overloads. ENE1 частично | Main2026-10-10: runtime10/10, generator9/9, Failed0/Skipped0. Pipes проверены in-process; MMF scenarios отключены. Следом serialization contract и bounded own-process pipes scenario |
| [Mithril](mithril/STATUS.md) | Найдены UnitsController, policies и process handles | В тестовом проекте среди исходников найден только Usings.cs. Новый test discovery не запускался; historical zero-tests запись не считается текущим результатом |
| [Alloy / Argentis](alloy/STATUS.md) | Добавлена защита native callbacks и передача первой ошибки вне dispatch; alpha остаётся частичной | Main2026-10-10: UI469/469, Debug callback/cleanup6direct+12hosted+12recreations, hooks0/validation0. Published Release proof относится к отдельному агентному run. Physical100/200%/mixed-monitor остаются |

Наличие source/test projects означает наличие реализации или сценариев, а не успешное прохождение. Подробности и датированные результаты принадлежат локальным STATUS/HISTORY.

## Общие этапы

| Этап | Состояние |
| --- | --- |
| G1. Исходное состояние | Документальная карта и отдельные Enerit/UI проверки выполнены. Полный baseline Core/Cobalt/Mithril и общего solution ещё не выполнен |
| G2. Core и DI | Реализация существует, контракты/проверки требуют отдельного прохода |
| G3. IPC и units | Реализация существует, сквозной отказ/restart/shutdown proof не выполнен этой работой |
| G4. UI alpha | Частично; UI критерии и собственный следующий шаг — в Alloy плане |
| G5. Сквозные examples | ManualTests существуют; весь заявленный набор общего плана ещё не подтверждён |
| G6. Общая поставка | Общие R1–R6 не закрыты; версия0.2.2.7-alpha не является readiness свидетельством |

## Общие проверки — датированные сведения

**Интеграция2026-10-10, main `d56f217`:** Windows/Debug/net10.0, SDK10.0.401, xUnit2.9.3/VSTest. Enerit runtime **10passed**, generator **9passed**, Alloy UI **469passed**, во всех трёх проектах Failed0/Skipped0. AlloyVulkanTest consumer build exit0, **0errors/227warnings**. Debug native callback probe exit0:6direct+12hosted failures+12healthy recreations, HWND/tree/registry/hooks cleanup, Vulkan validation0errors/0warnings. Команды и локальный scope — [Enerit история](enerit/HISTORY.md#интеграция-в-main--2026-10-10), [UI история](alloy/HISTORY.md#интеграция-в-main--2026-10-10). Артефакты — ignored `TestResults/AgentMerge` в основном checkout. Общий solution build/tests, Release publish и physical DPI matrix при интеграции не повторялись; их исторические результаты не являются новым запуском.

Из записи предыдущего UI code run **2026-10-09** сохранено: non-incremental TrueMoon.slnx build **0errors/1341warnings**, SDK10.0.401/net10.0; локальные Release publish AlloyTest/AlloyVulkanTest exit0. Подробные команды, configuration и native ограничения — [UI история](alloy/HISTORY.md#49d--k3-native-windows-dpi--реализация-и-запуск-2026-10-09).

Это предыдущий общий build и два UI consumers, не successful full solution test suite и не проверка package consumers остальных модулей. При реорганизации2026-10-09 и интеграции2026-10-10 эти команды не повторялись.

Из прежних записей **2026-09-27**: AppCreate failure и отсутствие найденных Mithril tests. Они перенесены как вопросы для baseline в Core/Mithril, без утверждения, что текущий код падает тем же образом. Старые index/Enerite записи сохранены в архиве UI документа как исторический контекст, не являются текущим blocker roadmap.

## Общие блокеры и неопределённости

- Нет актуального самостоятельного baseline всех модулей и результатов всего заявленного набора тестовых проектов.
- Политика поддержанных configuration/SDK/OS, warnings и package consumer matrix общего выпуска требует отдельной фиксации.
- UI physical DPI matrix ограничена доступным150% монитором. Это ограничение конкретной проверки UI, не препятствие работе над Core/DI/IPC/units.
- Общий lifecycle/ownership/error integration proof и независимая поставка generators ещё не подтверждены.

## Следующий конкретный шаг

1. G1: выполнить отдельную исходную сверку build/test discovery/results Core/Cobalt/Mithril, сохранить команды/configuration и обновить их STATUS. Enerit продолжить по ENE1/ENE2; его ENE0 baseline не заменяет own-process transport checks. Не переносить UI passing counts на другие проекты.
2. По полученным failures выбрать минимальный Core/DI contract или fix для G2; IPC/Mithril продолжать по собственным планам и доступному окружению.
3. UI: подготовить physical100/150/200%/разные DPI мониторов и продолжить локальную alpha matrix; пользовательские Windows settings без соответствующего запроса не менять.

## Изменения формата — 2026-10-09

Созданы общий PLAN/STATUS, правила и четыре модульных PLAN/STATUS. UI PLAN явно ограничен Alloy/Argentis и интеграциями; его цели/архитектура/идентификаторы/критерии сохраняются. Текущий UI STATUS сокращён; полный прежний документ сохранён в HISTORY. AGENTS указывает общую и локальные точки входа.

Проверки текущей правки прошли: чтение исходников/ProjectReference;17planning/AGENTS документов,270local links/21anchors, Markdown fences/whitespace/conflicts/NUL; полный прежний UI STATUS сохранён verbatim, K1–K9 table rows идентичны, PA1–PA10/net10.0/xUnit сохранены, пять модульных PLAN/STATUS пар присутствуют. Scoped git diff --check прошёл, source worktree status при финальной сверке не изменился, staged entries0. Артефакты документальной проверки — ignored TestResults/PlanningFormat. Новых passing build/tests/GPU/publish результатов эта запись не создаёт.
