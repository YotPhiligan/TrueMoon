# Alloy / Argentis: текущее состояние и точка продолжения

Последняя работа: **2026-10-10 — native callback fault boundary и teardown proof** в отдельном worktree `codex/alloy-next` от origin/main b15d85f. Проверены только локальные UI проекты и consumers; результат представлен до объединения. Alpha не завершён.

Область: Argentis, Alloy, Rendering.Skia, Platform.Silk, Alloy.Hosting, UI tests и consumers. [UI план](PLAN.md), [история](HISTORY.md), [общий план](../PLAN.md), [общий статус](../STATUS.md), [формат](../PLANNING.md).

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

## Текущие проверки — callback fault run2026-10-10

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
| K1. Ядро без GPU | Текущая UI suite **2026-10-10:469passed/0failed/0skipped**, focused callbacks6/6. Предыдущий DPI run2026-10-09:12/12. Реализация/тесты включают clipboard/rendering failure/drawing resources/window/DPI contracts. CPU raster/PNG требуют native Skia; это не полный solution test suite. |
| K2. Raster и GPU | Shared View1 повторно проверен2026-10-09 Debug/Release на raster/GL/Vulkan:3scales×40steps, logical bounds/states и selected RGBA samples с допуском1. **Fixed-font assets/full golden fixtures отсутствуют**; smoke их не заменяет. |
| K3. Windows и DPI | Native DPI contract реализован2026-10-09:HWND DPI144/UiScale1.5, logical resize/input/min-max, minimize/zero-size/restore и Debug/Release alpha/Snap/physical input проверены. Pure/directed100/150/200% и headless Scale-only state tests прошли. Реальные100/200%/разные DPI мониторов недоступны в текущем окружении, settings off/Aero Shake и часть physical cancel проверок впереди. |
| K4. Одинаковое поведение бекендов | Общие runtime/drawing/text и тот же Settings script raster/GL/Vulkan сравнили bounds/model/input/selected pixels в Debug/Release2026-10-09. Полное сравнение текста с доставляемыми fixed fonts ещё не выполнено. |
| K5. HUD-ввод и ресурсы | Shared Settings HUD2026-10-09 Debug/Release:90compositions/36draws/2sessions/5readbacks, outside passthrough/capture/focus/alpha/dynamic content, resize/minimize/restore, новая UI-сессия из модели и host-owned device проверены при native Scale1.5. Итоговый supported configuration проход остаётся. |
| K6. Vulkan validation | Текущий callback fault/chrome/controlled rendering failure run2026-10-10 Debug/published Release:0errors/0warnings включая teardown. Предыдущая DPI/window/Snap/alpha/Settings/HUD/standalone/physical input matrix2026-10-09 сообщила0/0. Другие physical DPI/OS/GPU configurations и итоговый supported configuration проход остаются. |
| K7. Повторные сессии | Текущий callback fault run2026-10-10:по12failed+12healthy hosted windows в Debug/published Release, HWND/tree/registry/все3hooks0 после teardown. Это bounded fault/recreate proof. Short retirement2026-10-08 и long200sessions2026-10-04 — исторические; длительный lifecycle/resize soak новых hooks и VRAM/driver profiling остаются. |
| K8. Потеря устройства | **Контракт и controlled failure/recreate повторно проверены2026-10-10 Debug/published Release**: error→host, terminal stop, cleanup, новая session/tree из прежней модели на здоровом device. **Physical device/context loss**, accepted DeviceLost abandonment и реальный lost-device teardown не индуцировались; полный K8 не закрыт. |
| K9. Производительность | **Baseline, allocation/CPU profile и одна подтверждённая reuse оптимизация выполнены2026-10-07/08.** Shared Settings static/property/list; cached paint/FIFO16fonts, до/после24000rawframes. Static0layout/draw/Update allocations. Managed Update allocations уменьшились~120→46KB/property и~140→66KB/list. Native/VRAM/GPU timestamps не измерены; performance budget не согласован. |

Alpha K1–K9 не объявляется завершённой. Требования и окончательная matrix остаются в [плане](PLAN.md#воспроизводимый-итоговый-проход-alpha); readiness table содержит датированные результаты, а не запуск документационной правки.

## Блокеры и ограничения

1. Physical100/200% и разные DPI мониторов отсутствуют в текущем проверенном окружении. Native DPI contract реализован, полный K3/4.9d не закрыт.
2. Settings off/Aero Shake, часть physical cancel/release-outside и долгий lifecycle4.9 ещё нужны. Native-origin callback faults/DPI subclass error проверены2026-10-10; искусственные Chrome/GDI/RemoveWindowSubclass failures не проверены. Alt+Space без PowerToys interception — отдельная проверка. Старые opacity/legacy capture failures не объявлены исправленными текущей alpha matrix.
3. Доставляемые fixed fonts/full golden, long resource/VRAM soak, real device/context loss и final independent/package consumer остаются открытыми.
4. Нерегулярный прежний standalone Name binding mismatch не получил root-cause fix; passing smoke не закрывает расследование воспроизводимости.
5. Core/DI failures и full solution tests учитываются в общем и соответствующих модульных статусах, а не как самостоятельные UI задачи. При влиянии на UI Hosting указывать точный integration blocker.

## Следующий конкретный шаг

**4.9d/K3:** подготовленный desktop100/150/200% и разные DPI мониторов; туда/обратно, GL/Vulkan ×Opaque/Opacity/PerPixel ×standard/custom. Проверить logical client size, focus/selection/capture, свежие regions/input, desktop alpha, minimize/restore и cleanup. Directed96/144/192 не заменяет физический переход; пользовательские Windows settings без соответствующего запроса не менять.

После него закрыть оставшуюся4.9matrix, fixed-font golden, long lifecycle/VRAM, real loss и consumer evidence, затем итоговый UI alpha проход. PA1/минимальный PA2 начать после alpha; общий roadmap не меняет этот локальный порядок.

## Последнее изменение формата — 2026-10-09

UI PLAN явно ограничен Alloy/Argentis и интеграциями, все согласованные цели/архитектура/этапы/K1–K9/PA1–PA10/net10.0/xUnit сохранены. Полный прежний STATUS перенесён без потери текста в HISTORY; здесь оставлена текущая сводка. Общие вопросы вынесены в общий/Core/Cobalt/Enerit/Mithril документы и точки входа AGENTS.

Проверки текущей работы прошли: чтение исходников/project references; полный прежний STATUS сохранён verbatim, K1–K9 table rows идентичны, PA1–PA10/net10.0/xUnit сохранены. Общая документальная сверка —17planning/AGENTS документов,270local links/21anchors, Markdown fences/whitespace/conflicts/NUL; scoped git diff --check прошёл. Source worktree status при финальной сверке не изменился, staged entries0. Артефакты — ignored TestResults/PlanningFormat. Новые code/build/tests/GPU/publish не выполнялись.
