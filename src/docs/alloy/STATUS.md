# Alloy / Argentis: текущее состояние и точка продолжения

Последняя работа — **2026-10-17, ENG2 viewport image adapter**.
Добавлен owned `SkiaImageSource.CopyBgraPixels`: tight BGRA8 premultiplied/sRGB,
checked shape/length и независимая копия pixels. Owner-thread upload/disposal
проверены новым engine Alloy/OpenGL окном. [UI запись](HISTORY.md#eng2-owned-pixel-image--2026-10-17),
[точный engine scope/evidence](../engine/HISTORY.md#eng2-cpu-frame-viewport--2026-10-17).
UI570/570 Debug/Release; два native engine viewport runs (Debug apphost/published
Release) с actual GL pixels и cleanup. Full Debug solution1368/1368. Это отдельный
bounded engine proof; package/golden/native RichTextEditor matrix в этом шаге
не повторялись, K3/K7/K8/VRAM/final alpha не закрыты. Следующий общий этап ENG3.

Отдельный [план улучшений RichTextEditor](RICH_TEXT_EDITOR_PLAN.md) по решению
пользователя остаётся отложенным; implementation улучшений не начиналась.

Предыдущая UI реализация и запуски — **2026-10-17, RichTextEditor перед ENG1**.
Реализованы immutable document/style/options, editing/selection/clipboard/history
в Argentis и Topten.RichTextKit0.6.170 adapter в Rendering.Skia; raster/OpenGL/Vulkan
используют один layout/painting path. [Контракт PA7a](PLAN.md#pa7a-richtexteditor-перед-eng1--2026-10-17),
[API/options/fonts/binding](RICH_TEXT_EDITOR.md),
[команды/failures/evidence](HISTORY.md#richtexteditor--2026-10-17).

Предыдущие проверки реализации2026-10-17, Windows x64/net10.0/SDK10.0.402: UI suite **563/563 отдельно
Debug и Release**, failed/skipped0;31новый case. Demo: **6/6** Debug/published
Release ×raster/OpenGL/Vulkan с formatting/edit/history/options/cleanup.
Fresh **10nupkgs,36App +4direct processes** проверили rich editor и transitive
Topten/HarfBuzz delivery, fonts/native/analyzer provenance и final fingerprints;
negative control без analyzer — ожидаемый CS0246. Read-only прежний Settings golden
**360/360**, validation включая teardown0errors/0warnings, fixtures unchanged.
Nonincremental Debug solution build: **0errors/1200warnings**; это не full-solution
test pass. Own Core/Cobalt/Enerit/Mithril suites в этом шаге не повторялись.

RichTextEditor: plain clipboard и logical grapheme navigation; IME/accessibility,
full bidi caret affinity, HTML/RTF, lists/tables и long native-memory trend вне scope.
RichTextKit process-wide shaper caches не объявляются очищенными при Dispose.

Предыдущие проверки2026-10-11: fonts/golden/package — в
[истории](HISTORY.md#k2k4-fixed-font-golden--2026-10-11).
K3 physical DPI, NVIDIA K7, K8 real loss, VRAM и final matrix сохраняются.
Driver/Windows settings не менялись; workaround search не возобновлялся.

Область: Argentis, Alloy, Rendering.Skia, Platform.Silk, Alloy.Hosting,
UI tests и consumers. [План](PLAN.md), [история](HISTORY.md), [общий статус](../STATUS.md).

## Состояние реализации

| Область | Реализация | Проверки и ограничение |
| --- | --- | --- |
| Независимые слои | Argentis без Alloy/Skia/Silk, Alloy→Argentis, отдельные renderer/platform/Hosting; legacy в Hosting.Compatibility | Разделение2.1a/b проверялось в датированных запусках; legacy window не приравнивается к современному пути |
| Дерево и динамический UI | Ownership, properties, one/two-way bindings, collections, conditional regions, retained focus/selection | Входит в последнюю UI suite; полного keyed tree reconciliation не обещается |
| Controls/input/styles | Базовые controls, RichTextEditor/options, focus/capture/keyboard/wheel, themes/density, Win32 clipboard, фабрики4.8 |2026-10-17:UI563/563 Debug/Release; rich editor6native cases и прежний Settings golden; полный OS input scope открыт |
| Rendering/HUD | Raster/OpenGL/Vulkan, borrowed raw-handle HUD, retained composition, failure/recreate, resources reuse, explicit font faces и RichTextKit adapter |2026-10-17: прежний full golden/validation и rich editor smoke прошли Debug/published Release; real loss, long VRAM/lifecycle и итоговая matrix открыты |
| Windows4.9 | Appearance/custom frame/native Snap, native DPI Scale/resize/input/min-max | HWND DPI144/UiScale1.5 проверен; physical100/200%/разные DPI мониторов не проверены |
| Consumers | AlloyTest/AlloyVulkanTest, RichTextEditorTest и независимый AlloyPackageConsumer; native build/buildTransitive delivery, bundled Cobalt analyzer, explicit fonts и packaged README | **2026-10-17:**10nupkgs/36App +4direct renderer processes с rich text, Cobalt/DI/DI без analyzer, Debug/Release ×build/publish. Windows x64/net10.0/framework-dependent без RID; другие deployment modes и final alpha открыты |
| После alpha | PA1–PA10 в UI плане | Запланировано; beta API и расширения не входят в текущий alpha проход |

Наличие реализации не означает итоговую готовность этапа. Подробные изменения и точные commands/configuration находятся в HISTORY; общий solution build не является UI или full-solution test pass.

## Текущие проверки — WSI stages2026-10-10

[Полные результаты и команды](HISTORY.md#wsi-stages-raw-win32-и-driver-screen-dc--2026-10-10). Финальная matrix:по198windows в Debug/published Release (8×24cycles +6cycles с4swapchain repeats); controls без validation/acquire/submit/present, Completed означает только diagnostic completion.

- Capabilities, formats count/data, present-modes: GDI14→14/USER15→15; swapchain stage GDI15→38/USER15→15 в обоих configurations. Queries не воспроизводят GDI trend; handles/private всё ещё меняются и общей stability не доказано.
- Raw Win32/Vulkan без GLFW/production VulkanDevice/Skia: formats GDI5→5, swapchain GDI6→29 за24cycles. Surface-first и device-first дают одинаковый trend; USER2→2, после HWND cleanup census0. По4create/destroy на surface: GDI6→11 за6windows, растёт только при первом create на каждой новой surface/window.
- Native debugger на3raw cycles: GetDC(NULL) из `nvoglv64.dll+0xDE1BCA` в vkCreateSwapchain path;3distinct returned HDC, последующего ReleaseDC/DeleteDC для них в trace нет, GDI6→8. Formats-only negative control: этот callsite0hits/GDI5→5. Это локализованный ICD allocation/lifetime на RTX5070Ti/32.0.16.1742; не verdict для других drivers и не объяснение private/handles growth.
- Raw thread-exit control24cycles в Debug/Release: GDI5→5/USER2→2 после join; не production workaround, прежний WGL/new-thread USER и shared GLFW scheduling contract сохраняются. Второй установленный GPU AMD Radeon Graphics32.0.21043.5001 проверен optional raw vendor selector:по24cycles Debug/Release **AMD GDI5→5, NVIDIA6→29**. AMD raw stability не означает full Skia/presentation/K7 pass; production GPU selection не менялась.
- Production `--hosted-dispatcher --validation` повторён Debug/published Release: GL/GL, Vulkan/Vulkan, mixed pairs, native refresh/command context, cancellation/fault/shutdown/cleanup passed, registry/hooks0 и validation0errors/0warnings. Full240-window resource check, callback/chrome/controlled failure, physical DPI и unit suite не повторялись.

Артефакты ignored `TestResults/AlloyWsi`: финальная matrix/JSON/logs, Debug build/Release publish, raw repro source, CDB scripts/traces, source/binary/driver/tool provenance. Debugger извлечён из Microsoft-signed package локально; system install/settings/driver changes не выполнялись. HDC из ICD не освобождались приложением; supported Vulkan API не передаёт их ownership TrueMoon.

## Предыдущие проверки — Hosting dispatcher2026-10-10

[Команды, scope и артефакты](HISTORY.md#hosting-owner-dispatcher-и-vulkan-wsi-lifetime--2026-10-10). Process owner thread сохраняется idle; закрытие app scope освобождает его окна/сессии/devices, но не вызывает global GLFW termination. Контракт ограничен Windows Hosting; direct Silk hosts на другом потоке одновременно с ним не поддержаны.

- UI suite488/488; shared owner, sibling continuation, startup/tick/cleanup first-fault isolation, Close-before-Start, draining shutdown и отсутствие retention последнего callback target проверены8 cases. Последний native retention дефект idle frame исправлен; наличие только managed implementation не считается native proof.
- В Debug и published Release прошли GL/GL, Vulkan/Vulkan и GL/Vulkan concurrent windows: distinct HWND/same owner, own/sibling WM_PAINT refresh, close/recreate, независимые app scopes, cancellation только ожидания, fault isolation и cleanup. Callback regression в каждом configuration:6direct+12hosted failures+12healthy recreations. Chrome и controlled rendering failure также exit0; Vulkan validation0errors/0warnings. App/DI OpenGL/Vulkan Debug smoke прошёл по40frames.
- Полные240-window Debug/Release прогоны с прежними USER warm+2/GDI warm+4 лимитами **failed**. USER warm12→240 **26→26** в обоих; GDI **23→137** в обоих. По720resize/240minimize,1680tracked UI WeakReferences/retained0, registry/subscriptions/hooks0, validation0/0. Эти full runs предшествуют финальным Close-before-Start/reentrancy/task-reference guards; финальные short12-window Debug/Release checks подтвердили cleanup/validation, но не длительную resource stability.
- Native24-cycle controls без validation: Vulkan device и Skia session без WSI presentation GDI14→14; raw presenter и capabilities/formats/swapchain create/destroy без Skia GDI15→38. Retained Vulkan-loader control не убрал mixed trend. Локализован WSI путь, но точный вызов/allocator/owner не установлен; driver причиной не объявлен. Private/handles growth и VRAM остаются открытыми.

Артефакты ignored `TestResults/AlloyDispatcher`: TRX, build/publish, functional/short/full logs и JSON, stage-specific native controls. Full K7, physical DPI, fixed-font/full golden, real loss, VRAM и independent package consumer не объявлены завершёнными.

## Предыдущие проверки — WGL/thread lifetime2026-10-10

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
| K1. Ядро без GPU | **2026-10-17:563/563 Debug и Release, failed/skipped0.** Current UI suite; native CPU raster/font cases используют bundled Skia. Не full solution tests. |
| K2. Raster и GPU | **2026-10-17:** read-only fixed-font Settings golden360/360 Debug/published Release, source fixtures unchanged; прежние30PNG/3scales и tolerances сохранены. Rich editor проверен отдельно6native cases, не новыми golden fixtures. Windows x64/NVIDIA, SDK10.0.402. |
| K3. Windows и DPI | Native DPI contract реализован2026-10-09:HWND DPI144/UiScale1.5, logical resize/input/min-max, minimize/zero-size/restore и Debug/Release alpha/Snap/physical input проверены. Pure/directed100/150/200% и headless Scale-only state tests прошли. Реальные100/200%/разные DPI мониторов недоступны в текущем окружении, settings off/Aero Shake и часть physical cancel проверок впереди. |
| K4. Одинаковое поведение бекендов | **2026-10-17:** общий40-step Settings script raster/OpenGL/Vulkan на3scales,180comparisons/configuration; bounds/model/input/focus/selection/scroll/redraw и full pixels прошли с прежними допусками. Rich editor demo6/6 отдельно. Final supported matrix открыта. |
| K5. HUD-ввод и ресурсы | Shared Settings HUD2026-10-09 Debug/Release:90compositions/36draws/2sessions/5readbacks, outside passthrough/capture/focus/alpha/dynamic content, resize/minimize/restore, новая UI-сессия из модели и host-owned device проверены при native Scale1.5. Итоговый supported configuration проход остаётся. |
| K6. Vulkan validation | **2026-10-17 Settings golden Debug/published Release:** validation включая teardown0errors/0warnings; bounded offscreen/readback scope, без WSI soak. Rich editor window smoke не является validation run. Physical DPI/real loss/final matrix открыты. |
| K7. Повторные сессии | Shared owner реализован. Предыдущий full240-window Debug/Release **failed**: USER26→26/GDI23→137,1680WeakReferences/retained0. Текущие raw/stage Debug/Release controls и Debug stack локализуют screen HDC allocation до NVIDIA ICD32.0.16.1742; тот же raw control на AMD сохраняет GDI5→5. Safe app-side fix не установлен; другая NVIDIA версия, full UI на AMD, private/handles/VRAM и полный финальный K7 остаются. |
| K8. Потеря устройства | **Предыдущий controlled failure/recreate2026-10-10 Debug/published Release**: error→host, terminal stop, cleanup, новая session/tree из прежней модели на здоровом device. **Physical device/context loss**, accepted DeviceLost abandonment и lost-device teardown не индуцировались; полный K8 не закрыт. |
| K9. Производительность | **Baseline, allocation/CPU profile и одна подтверждённая reuse оптимизация выполнены2026-10-07/08.** Shared Settings static/property/list; cached paint/FIFO16fonts, до/после24000rawframes. Static0layout/draw/Update allocations. Managed Update allocations уменьшились~120→46KB/property и~140→66KB/list. Native/VRAM/GPU timestamps не измерены; performance budget не согласован. |
| 5.4/5.5. Подготовка поставки | **2026-10-17:**10fresh packages,36App Cobalt/DI/DI-without-analyzer +4direct renderer processes с RichTextEditor/Topten/HarfBuzz, fonts/OFL/native/analyzer provenance и cleanup. Debug/Release ×build/publish, Windows x64/net10.0 framework-dependent без RID. Alpha/прочие deployment modes не подтверждены. |

Alpha K1–K9 не объявляется завершённой. Требования и окончательная matrix остаются в [плане](PLAN.md#воспроизводимый-итоговый-проход-alpha); readiness table содержит датированные результаты, а не запуск документационной правки.

## Блокеры и ограничения

1. Physical100/200% и разные DPI мониторов отсутствуют в текущем проверенном окружении. Native DPI contract реализован, полный K3/4.9d не закрыт.
2. Settings off/Aero Shake и часть physical cancel/release-outside ещё нужны. Длительный lifecycle4.9 с dispatcher failed на GDI росте Vulkan WSI при стабильном USER. Native-origin callback faults/DPI subclass error проверены2026-10-10; искусственные Chrome/GDI/RemoveWindowSubclass failures не проверены. Alt+Space без PowerToys interception — отдельная проверка. Старые opacity/legacy capture failures не объявлены исправленными текущей alpha matrix.
3. На текущем NVIDIA ICD K7 GDI trend локализован до screen HDC allocation внутри vkCreateSwapchain; safe app-side fix не установлен, поиск workaround пока прекращён по решению пользователя. AMD raw control стабилен по GDI, но full UI configuration на AMD и другая NVIDIA версия не проверены; private/handles growth этим HDC trace не объясняется. Fixed-font full golden и package consumer повторены2026-10-17 в указанном bounded scope; VRAM и real loss остаются. Shared owner устраняет прежний USER trend в предыдущем full run; raw WGL allocator отдельно не установлен.
4. Исторический standalone Name binding mismatch остаётся без доказанной причины. Отдельный control-only TextBox defect исправлен, но текущий GLFW фильтрует WM_CHAR13 до UiInput, поэтому этот fix не объявляется объяснением старого Vulkan failure. В20повторах native input не наблюдался; новый trace различает unexpected-value от binding-out-of-sync. При повторе сохранять trace, не заменять failure retry-успехом.
5. Core/DI failures и full solution tests учитываются в общем и соответствующих модульных статусах, а не как самостоятельные UI задачи. При влиянии на UI Hosting указывать точный integration blocker.

## Следующий конкретный шаг

Улучшения RichTextEditor отложены: при возвращении к UI открыть
[отдельный план](RICH_TEXT_EDITOR_PLAN.md) и начать с RTE0 — сверки текущих
contracts и выбора ограниченного этапа. Эти задачи не добавлены в ENG1.

**RichTextEditor/PA7a реализован и проверен2026-10-17** перед ENG1 по запросу
пользователя; это ограниченное расширение, не закрытие PA7/beta.
Ранее2026-10-15 пользователь выбрал ограниченный
[ENG0 scope](../engine/PLAN.md#eng0-принятый-scope-первого-прототипа--2026-10-15)
с открытыми UI рисками. Headless ENG1 реализован/проверен2026-10-17 в своём
[engine scope](../engine/STATUS.md); ENG2 CPU viewport реализован в своём bounded scope; следующий общий шаг — ENG3 replacement.
В ENG1 повторена UI Debug suite563, новые UI native/package/golden runs не выполнялись.
Остальные PA1–PA10 и массовая provider
migration этим решением не начинаются; physical UI acceptance требуют своего run.
K7 не снимается; acceptance не подменяется успешными40package processes.

Для UI остаётся подготовленный physical4.9d/K3 desktop и оставшиеся OS input/chrome checks, real device/context loss/K8, VRAM evidence и полный финальный alpha проход после снятия K7. RichTextKit shaping не означает полную поддержку всех emoji; fixed-font missing-glyph symbols не выдаются за emoji rendering.

Подготовка поставки5.4/5.5 повторена2026-10-17 на Windows x64/net10.0/framework-dependent build/publish без RID; пакеты не публиковались. Physical4.9d/K3 требует подготовленного desktop100/150/200% и разных DPI мониторов, остальная4.9matrix/real loss/VRAM также остаются. Затем — итоговый UI alpha проход; PA1/минимальный PA2 остаются после alpha.

**Внешний K7 блокер:** дальнейший поиск app-side workaround текущего NVIDIA GDI defect пока прекращён по решению пользователя. Возврат — при supported driver/ICD решении либо отдельно согласованной и полностью проверенной alternate GPU configuration; raw AMD сравнение уже выполнено, full UI на AMD не подтверждён. После решения повторить полный240-window GL/Vulkan×appearance/chrome/minimize check в Debug/published Release и callback/shutdown regressions на финальном source с прежними warm+2/+4 limits. Private/handles growth расследовать отдельно. Пользовательские driver/Windows settings не менять без соответствующего запроса. Для standalone Name повторов включать `--smoke-trace`.

## Последнее изменение формата — 2026-10-09

UI PLAN явно ограничен Alloy/Argentis и интеграциями, все согласованные цели/архитектура/этапы/K1–K9/PA1–PA10/net10.0/xUnit сохранены. Полный прежний STATUS перенесён без потери текста в HISTORY; здесь оставлена текущая сводка. Общие вопросы вынесены в общий/Core/Cobalt/Enerit/Mithril документы и точки входа AGENTS.

Проверки текущей работы прошли: чтение исходников/project references; полный прежний STATUS сохранён verbatim, K1–K9 table rows идентичны, PA1–PA10/net10.0/xUnit сохранены. Общая документальная сверка —17planning/AGENTS документов,270local links/21anchors, Markdown fences/whitespace/conflicts/NUL; scoped git diff --check прошёл. Source worktree status при финальной сверке не изменился, staged entries0. Артефакты — ignored TestResults/PlanningFormat. Новые code/build/tests/GPU/publish не выполнялись.
