# Alloy / Argentis: текущее состояние и точка продолжения

Последняя работа: **2026-10-10 — native WGL/thread lifetime investigation и shared-loop prototype** в основном checkout поверх `9d7090a`. USER growth воспроизведён raw Win32/WGL без GLFW/Silk/Skia/UI; reuse ограничил рост в диагностических OpenGL controls. Проверены два одновременно живых окна на одном owner loop. Production Hosting scheduling не изменён; K7/alpha и историческая причина Name mismatch остаются открытыми.

Область: Argentis, Alloy, Rendering.Skia, Platform.Silk, Alloy.Hosting, UI tests и consumers. [UI план](PLAN.md), [история](HISTORY.md), [общий план](../PLAN.md), [общий статус](../STATUS.md), [формат](../PLANNING.md).

Общая проверка после Core/DI fixes2026-10-10: TrueMoon.Alloy.Tests повторно **480passed/0failed/0skipped**, discovery480, Windows/Debug/net10.0/SDK10.0.401. [Solution commands/TRX](../core/HISTORY.md#исправление-проблем-ядра--2026-10-10). UI consumers собраны в non-incremental solution build; native/physical/soak проверки не повторены, прежние K7/alpha ограничения сохраняются.

## Состояние реализации

| Область | Реализация | Проверки и ограничение |
| --- | --- | --- |
| Независимые слои | Argentis без Alloy/Skia/Silk, Alloy→Argentis, отдельные renderer/platform/Hosting; legacy в Hosting.Compatibility | Разделение2.1a/b проверялось в датированных запусках; legacy window не приравнивается к современному пути |
| Дерево и динамический UI | Ownership, properties, one/two-way bindings, collections, conditional regions, retained focus/selection | Входит в последнюю UI suite; полного keyed tree reconciliation не обещается |
| Controls/input/styles | Базовые controls, focus/capture/keyboard/wheel, themes/density, Win32 clipboard, фабрики4.8 | Общая Settings form и функциональные checks есть; fixed-font/full golden и полный OS input scope открыты |
| Rendering/HUD | Raster/OpenGL/Vulkan, borrowed raw-handle HUD, retained composition, failure/recreate и resources reuse | GPU/validation/selected-pixel baseline проверялись; real loss, long VRAM/lifecycle и full golden открыты |
| Windows4.9 | Appearance/custom frame/native Snap, native DPI Scale/resize/input/min-max | HWND DPI144/UiScale1.5 проверен; physical100/200%/разные DPI мониторов не проверены |
| Consumers | AlloyTest/AlloyVulkanTest, shared Settings model/script и Debug/Release smoke | Package/independent final consumer delivery остаётся alpha задачей |
| После alpha | PA1–PA10 в UI плане | Запланировано; beta API и расширения не входят в текущий alpha проход |

Наличие реализации не означает итоговую готовность этапа. Подробные изменения и точные commands/configuration находятся в HISTORY; общий solution build не является UI или full-solution test pass.

## Текущие проверки — WGL/thread lifetime2026-10-10

[Команды, таблицы и scope](HISTORY.md#wgl-thread-lifetime-и-shared-owner-loop--2026-10-10). Debug build и published Release exit0. Изменения этого шага относятся к manual diagnostics; unit suite и другие модули не запускались заново.

- Raw Win32/WGL на новых потоках, по240contexts Debug/Release: USER warm12 **18→246** в обоих, GDI7→7; после join и фиксированной1s quiescence USER246 сохраняется. Raw Win32 без GL24cycles USER1→1; Vulkan-window без device24cycles USER3→3. USER growth не требует GLFW/Silk/Skia/UI. Фактический GL renderer — NVIDIA RTX5070Ti; точный allocator/тип USER object ещё не установлен. Census top-level/message-only HWND не показывает сопоставимого роста, но не перечисляет все USER objects.
- Прямой Skia/OpenGL UI control, по240windows Debug/Release на новых потоках: USER21→246/234. При reuse одного worker26→26/26; двух32→32/33. Private bytes и handles — наблюдения, не VRAM proof и не утверждение об устранении прежнего GL/Vulkan mixed роста. Workers в этом control работают serial round-robin; это не multi-window concurrency proof.
- Финальный diagnostic source отдельно повторён с двумя workers по240UI windows Debug/Release; hooks/registry/UI WeakReferences проверяются после каждого окна. Shared owner loop отдельно прошёл по20rounds/60HWNDs/1200frames/180resize в каждой configuration: два live окна, close/recreate первого при продолжающемся sibling, корректные context/owner checks, registry/hooks/300tracked WeakReferences retained0. USER warm round4→20:25→25(Debug),24→24(Release), GDI17→17; прежний warm+2/+4 лимит сохранён.
- GLFW требует window creation/event processing на одном main thread. Serial worker reuse — диагностическая гипотеза; направление для Hosting — dispatcher с единым owner event loop на время жизни приложения. Его production lifecycle, command/cancellation/fault semantics и Vulkan integration ещё не реализованы. Shared-loop control не заменяет полный K7/chrome/alpha/minimize/VRAM проход.

Артефакты ignored `TestResults/AlloyNativeThreads`: isolated controls, 240-cycle matrix и source/binary provenance, final shared-loop/reuse reports, logs и publish. Старый failed полный window soak не объявлен исправленным.

## Предыдущие проверки — lifecycle/resize2026-10-10

[Команды, воспроизведения и ограничения](HISTORY.md#lifecycle-resize-soak-и-native-resource-growth--2026-10-10). UI suite повторена после production cleanup fixes: **480passed/0failed/0skipped**. Debug consumer build и Release publish exit0; общие contracts/build/package настройки не менялись.

- Полные Debug/published Release прогоны выполнили по **240HWNDs,720resize,240minimize/restore**:20повторов GL/Vulkan×Opaque/Opacity/PerPixel×standard/custom. После каждого окна registry/subscriptions/все3hooks0;1680tracked WeakReferences на прогон, retained0. Resize сохранил editor value/selection/focus; zero-size suspend и active capture cancellation прошли; validation0/0 включая teardown.
- **Оба полных прогона failed** на прежнем USER/GDI warm+2/+4 лимите: USER16→128(Debug)/130(Release), GDI15→15. Handles551→896/894; private memory~201→639MiB/~197→638MiB. Managed bytes включают metadata самого probe; private memory не является измерением VRAM. Нулевые UI references не доказывают общую resource stability.
- Native-only controls без Skia/UI/VulkanDevice воспроизвели USER рост в GL/new-thread, включая direct Silk без TrueMoon host/adapters/input; GL на одном потоке и Vulkan/new-thread стабильны. Точный native allocator/owner и причина роста private/handles ещё не установлены; GPU driver причиной не объявлен.
- Финальная короткая matrix **12windows Debug +12published Release**:235/239frames, по36resize/12minimize,84WeakReferences/retained0, validation0/0. Callback regression — в каждом configuration6direct+12hosted failures+12healthy recreations, first cause/cleanup/Completion identity и hooks/registry0.
- Actual Vulkan retirement — Debug/Release×KHR present fences/forced legacy:4×200sessions/2000frames/400created=destroyed swapchains/10minimize. Peak2generations, confirmed proofs2000(fences)/399(legacy); owned objects/registry/subscriptions/retained UI/hooks0, validation0/0. Два первоначально ошибочно помеченных legacy запуска фактически были KHR; они не засчитаны в legacy scope и повторены с literal flag. Passing retirement не заменяет failed длительный window resource check.

Артефакты ignored `TestResults/AlloyLifecycle`: исходные retention failures, root-chain evidence, short/full JSON/logs, native controls, retirement/callback logs, текущий TRX, publish и provenance. Physical DPI transitions, полный solution, VRAM/driver profile, real device loss и package consumer не проверялись.

## Предыдущие проверки — TextBox/Name smoke2026-10-10

[Команды и область вывода](HISTORY.md#textbox-control-text-и-name-smoke--2026-10-10). Focused regression до исправления:7failed/4passed; после: **11passed/0failed/0skipped**. UI suite **480passed/0failed/0skipped**. Debug build/Release publish standalone и compile-linked consumer build exit0.

- **20/20 standalone runs**:5×Debug/Release×OpenGL/Vulkan, по40–41frames, stage2 model/editor совпали, trace≤128. Native input в этих20runs не наблюдался; повторный успех не является root-cause fix исторического failure.
- Финальная диагностика проверена owned directed WM_CHAR: `X` вызвал ожидаемый negative smoke failure и trace с одинаковыми изменёнными model/editor (`reason=unexpected-value`); WM_CHAR13 не дошёл до UiInput в текущем GLFW, smoke завершился успешно. Отказ записи trace не маскирует первичную ошибку. Это3bounded proofs, включая2ожидаемых child failures, не3passing smoke runs.
- Debug shared raster/OpenGL/Vulkan сравнение:3logical scales×40steps, bounds/model/input и selected RGBA совпали; Vulkan validation0errors/0warnings. Это не physical DPI/full golden.

Артефакты — ignored `TestResults/AlloyNameBinding` в основном checkout: red/green/final TRX/logs, standalone trace JSON, run summary, owned native proof script/logs, provenance и publish. Полный solution, физическая DPI matrix и long soak/VRAM не запускались.

## Предыдущие проверки — интеграция в main2026-10-10

[Команды и scope](HISTORY.md#интеграция-в-main--2026-10-10): Windows/Debug/net10.0, SDK10.0.401. UI suite **469passed/0failed/0skipped**; AlloyVulkanTest build exit0, **0errors/227warnings**. Debug callback probe — **6direct +12hosted failures +12healthy recreations**, first cause/cleanup/Completion/StopAsync, HWND/tree/registry/все3hooks0, Vulkan validation0errors/0warnings. Артефакты — ignored `TestResults/AgentMerge` в основном checkout. Published Release, прежние chrome/rendering-failure probes и physical DPI matrix при интеграции не повторялись.

## Предыдущие проверки — агентный callback run2026-10-10

[Изменения, команды и scope](HISTORY.md#49--k7-callback-fault-boundary-и-cleanup-2026-10-10).

- Platform.Silk сохраняет первую ошибку native-origin mouse/key/text/focus/render handler, проверяет pending Win32 hook fault до пользовательского handler и передаёт причину через VerifyWindowAccess/Run вне native dispatch. Последующие handlers подавляются; ownership и teardown порядок сохранены.
- Focused WindowCallbackBoundaryTests **6passed/0failed/0skipped**; финальная UI suite **469passed/0failed/0skipped**, SDK10.0.401/net10.0/xUnit2.9.3/VSTest. Это локальная UI suite, не общий baseline.
- Native callback probe **Debug и published Release**, каждый: **6direct cases +12hosted failures +12healthy recreations**. Native mouse/key/text/focus, directed DPI subclass failure и managed Render dispatch; OpenGL/Vulkan×Opaque/Opacity/PerPixel. First cause/stack, original+cleanup errors, Completion/StopAsync identity, tree/focus/registry/HWND и все3hooks после каждой сессии проверены; Vulkan validation **0errors/0warnings**. Полный pending-hook guard включён в оба финальных запуска.
- Existing `--window-chrome` и `--settings-failure` повторены в Debug/published Release — **4probe runs exit0**, validation0/0 включая teardown. Локальный UI consumer build и Release publish exit0; другие модули и solution tests не запускались.
- Первый probe attempt без process VK_LAYER_PATH завершился missing validation layer после успешной direct/OpenGL части. Полные финальные проверки выполнены с копией установленного validation tooling в этом worktree, без глобальных environment/settings изменений.

Артефакты: ignored TestResults/AlloyCallbacks — TRX, logs, callbacks-debug/release.json, provenance hashes, локальная validation tooling copy и published consumer. Направленные сообщения не являются physical DPI/device loss; длительный soak/VRAM и принудительные failures RemoveWindowSubclass/GDI/Chrome hooks этим проходом не подтверждены.

## Предыдущие UI проверки — code run2026-10-09

Это предыдущий code run перед текущей callback правкой. [Полная запись DPI](HISTORY.md#49d--k3-native-windows-dpi--реализация-и-запуск-2026-10-09).

- Focused WindowDpiTests **12/12**, UI suite **463/463**, failed/skipped0, SDK10.0.401/net10.0/xUnit2.9.3/VSTest. Это не full solution suite.
- Final Debug/published Release regression **32/32 actual scenarios**: DPI/chrome/Snap/alpha/compare/HUD/controlled failure и16standalone smoke. Ошибка runner flags обнаружена и10cases повторены; evidence определяется actual scope reports, не одним exit0.
- Native DPI — по12samples/backend/appearance/custom combinations на **одном physical DPI144(150%)**, Scale1.5; по36directed96/144/192requests, **0physical DPI transitions**, unavailable96/192. Resize/client click/limits/retained focus/hooks прошли. Pure/directed tests100/200% не выдаются за physical matrix.
- Chrome — по8windows/116directed hits, limits/work area/negative/stale geometry/Hosting cleanup. Directed Snap — по8cases; validation0/0, hooks0.
- Physical input Debug/Release — по6cases/150checks, failed0,6skipped Alt+Space из-за PowerToys interception, settings unchanged/hooks0/validation0. Drag/resize/maximize/edges/corners/hover/Win+Z прошли;24context PNG визуально сверены. Snap bar последний раз проверялся2026-10-08, текущий DPI run его не повторяет.
- Desktop alpha: custom/native Snap — по6cases/80samples/48targets; standard decorated/borderless — по12cases/160samples/96targets. Matches/source/resource/validation checks прошли на двух фонах при150%.
- Shared raster/GL/Vulkan comparison —3scales×40steps; HUD по90compositions/36UI draws/2sessions/5readbacks. Controlled failure/recreate прошёл; physical device/context loss не индуцирован.
- Release publish обоих UI examples exit0. Общий solution build и ограничения всех модулей — в [общем статусе](../STATUS.md#общие-проверки--датированные-сведения); это не новый результат реорганизации.

Артефакты этого code run: ignored TestResults/AlloyDpi — TRX/logs/JSON/PNG и verification. Исторический K9 baseline/profile/reuse2026-10-07/08 — [PERFORMANCE_BASELINE](PERFORMANCE_BASELINE.md); измерения не повторялись при новой структуре документов.

## Проверки готовности

| Критерий | Состояние на дату сверки |
| --- | --- |
| K1. Ядро без GPU | Предыдущая UI suite после lifecycle fixes **2026-10-10:480passed/0failed/0skipped**; при native thread investigation не повторялась. Focused control-text11/11, callback6/6 и DPI2026-10-09:12/12 — предыдущие отдельные runs. CPU raster/PNG требуют native Skia; это не полный solution test suite. |
| K2. Raster и GPU | Предыдущий Name run2026-10-10 проверил Debug shared View1 на raster/GL/Vulkan:3scales×40steps, bounds/states/selected RGBA с допуском1. При lifecycle работе comparison не повторён. Предыдущая Release comparison —2026-10-09. **Fixed-font assets/full golden fixtures отсутствуют**. |
| K3. Windows и DPI | Native DPI contract реализован2026-10-09:HWND DPI144/UiScale1.5, logical resize/input/min-max, minimize/zero-size/restore и Debug/Release alpha/Snap/physical input проверены. Pure/directed100/150/200% и headless Scale-only state tests прошли. Реальные100/200%/разные DPI мониторов недоступны в текущем окружении, settings off/Aero Shake и часть physical cancel проверок впереди. |
| K4. Одинаковое поведение бекендов | Предыдущий Name run2026-10-10 сравнил Settings script raster/GL/Vulkan bounds/model/input/selected pixels в Debug. Lifecycle не повторяет pixel comparison. Предыдущий Release comparison —2026-10-09. Полное сравнение текста с fixed fonts ещё не выполнено. |
| K5. HUD-ввод и ресурсы | Shared Settings HUD2026-10-09 Debug/Release:90compositions/36draws/2sessions/5readbacks, outside passthrough/capture/focus/alpha/dynamic content, resize/minimize/restore, новая UI-сессия из модели и host-owned device проверены при native Scale1.5. Итоговый supported configuration проход остаётся. |
| K6. Vulkan validation | Предыдущие lifecycle/short/callback/actual retirement2026-10-10 Debug/published Release:0errors/0warnings включая teardown; full lifecycle всё равно failed на USER resource check. Native thread investigation не создаёт VulkanDevice и не повторяет validation. Chrome/controlled rendering failure2026-10-10 и DPI/window/Settings/HUD2026-10-09 — предыдущие runs. Другие physical DPI/OS/GPU configurations и final supported matrix остаются. |
| K7. Повторные сессии | Предыдущий full lifecycle2026-10-10 по240windows Debug/Release **failed** на USER/handles/private growth при нулевых retained UI. Текущий raw WGL воспроизвёл USER growth без GLFW/Silk/UI; serial reuse и shared owner loop с двумя live GL окнами проверены отдельно. Production dispatcher и повтор полного mixed resource check впереди; exact allocator/VRAM остаются, K7 не закрыт. |
| K8. Потеря устройства | **Предыдущий controlled failure/recreate2026-10-10 Debug/published Release**: error→host, terminal stop, cleanup, новая session/tree из прежней модели на здоровом device. При lifecycle это не повторялось. **Physical device/context loss**, accepted DeviceLost abandonment и lost-device teardown не индуцировались; полный K8 не закрыт. |
| K9. Производительность | **Baseline, allocation/CPU profile и одна подтверждённая reuse оптимизация выполнены2026-10-07/08.** Shared Settings static/property/list; cached paint/FIFO16fonts, до/после24000rawframes. Static0layout/draw/Update allocations. Managed Update allocations уменьшились~120→46KB/property и~140→66KB/list. Native/VRAM/GPU timestamps не измерены; performance budget не согласован. |

Alpha K1–K9 не объявляется завершённой. Требования и окончательная matrix остаются в [плане](PLAN.md#воспроизводимый-итоговый-проход-alpha); readiness table содержит датированные результаты, а не запуск документационной правки.

## Блокеры и ограничения

1. Physical100/200% и разные DPI мониторов отсутствуют в текущем проверенном окружении. Native DPI contract реализован, полный K3/4.9d не закрыт.
2. Settings off/Aero Shake и часть physical cancel/release-outside ещё нужны. Длительный lifecycle4.9 выполнен, но resource check failed на USER росте. Native-origin callback faults/DPI subclass error проверены2026-10-10; искусственные Chrome/GDI/RemoveWindowSubclass failures не проверены. Alt+Space без PowerToys interception — отдельная проверка. Старые opacity/legacy capture failures не объявлены исправленными текущей alpha matrix.
3. Доставляемые fixed fonts/full golden, native GL/new-thread USER growth и наблюдаемый private/handles growth, VRAM/driver profile, real device/context loss и final independent/package consumer остаются открытыми. Два managed callback roots устранены; это не исправление всех native allocations.
4. Исторический standalone Name binding mismatch остаётся без доказанной причины. Отдельный control-only TextBox defect исправлен, но текущий GLFW фильтрует WM_CHAR13 до UiInput, поэтому этот fix не объявляется объяснением старого Vulkan failure. В20повторах native input не наблюдался; новый trace различает unexpected-value от binding-out-of-sync. При повторе сохранять trace, не заменять failure retry-успехом.
5. Core/DI failures и full solution tests учитываются в общем и соответствующих модульных статусах, а не как самостоятельные UI задачи. При влиянии на UI Hosting указывать точный integration blocker.

## Следующий конкретный шаг

**4.9d/K3:** подготовленный desktop100/150/200% и разные DPI мониторов; туда/обратно, GL/Vulkan ×Opaque/Opacity/PerPixel ×standard/custom. Проверить logical client size, focus/selection/capture, свежие regions/input, desktop alpha, minimize/restore и cleanup. Directed96/144/192 не заменяет физический переход; пользовательские Windows settings без соответствующего запроса не менять.

После него закрыть оставшуюся4.9matrix, fixed-font golden, long lifecycle/VRAM, real loss и consumer evidence, затем итоговый UI alpha проход. PA1/минимальный PA2 начать после alpha; общий roadmap не меняет этот локальный порядок.

Пока физическое DPI окружение недоступно, ближайший доступный шаг — реализовать app-lifetime owner dispatcher/event loop в Hosting вместо нового потока на каждое окно. Перед интеграцией уточнить lifetime/shutdown, concurrent windows, command routing, cancellation и first-fault isolation; сохранить session/device ownership и cleanup-before-context-destroy. Manual shared-loop prototype подтверждает только Windows/OpenGL normal path. Затем повторить прежний240-window GL/Vulkan×appearance/chrome/minimize resource check в Debug/published Release и callback/shutdown regressions с прежними лимитами. Точный WGL/OS/ICD allocator и mixed private/handles growth остаются отдельными вопросами; глобальная GLFW termination не принята. Independent package consumer остаётся следующим независимым deliverable. Для standalone Name повторов включать `--smoke-trace`.

## Последнее изменение формата — 2026-10-09

UI PLAN явно ограничен Alloy/Argentis и интеграциями, все согласованные цели/архитектура/этапы/K1–K9/PA1–PA10/net10.0/xUnit сохранены. Полный прежний STATUS перенесён без потери текста в HISTORY; здесь оставлена текущая сводка. Общие вопросы вынесены в общий/Core/Cobalt/Enerit/Mithril документы и точки входа AGENTS.

Проверки текущей работы прошли: чтение исходников/project references; полный прежний STATUS сохранён verbatim, K1–K9 table rows идентичны, PA1–PA10/net10.0/xUnit сохранены. Общая документальная сверка —17planning/AGENTS документов,270local links/21anchors, Markdown fences/whitespace/conflicts/NUL; scoped git diff --check прошёл. Source worktree status при финальной сверке не изменился, staged entries0. Артефакты — ignored TestResults/PlanningFormat. Новые code/build/tests/GPU/publish не выполнялись.
