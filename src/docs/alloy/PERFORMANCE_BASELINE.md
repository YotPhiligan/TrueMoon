# TrueMoon.Alloy: Settings baseline K9

Актуальный profile/reuse и свежие пары до/после2026-10-08 — в разделе [«Профиль и оптимизация»](#профиль-и-оптимизация-2026-10-08). Ниже до этого раздела сохранён исходный baseline2026-10-07; driver configuration различается.

Два независимых Release-процесса **2026-10-07**, по 12 сценариев × 500 измеренных кадров: **12 000 raw samples**. Статичный UI во всех четырёх вариантах выполняет **0 Measure / 0 Arrange / 0 Render**, выделяет **0 managed bytes в Update**, сохраняет дерево и UI-поверхность. При изменениях layout выполняется от корня; неизменённые controls и строки сохраняются. Это исходная точка для последующих сравнений, без установленного performance budget или заявления о готовности alpha.

## Сцена и воспроизведение

Используется исходный [AlloyTest.View1](../../ManualTests/AlloyTest/View1.cs), compile-linked в AlloyVulkanTest, с SettingsModel/PNG и прозрачным profiling Panel. View720×1000, logical viewport900×1100, scale1, Dark/default density, Ready, без фокуса. Начальные 32 строки дают **195 элементов**, включая profiling root. Часть списка может быть clipped viewport; виртуализации нет. Режимы:

- `static`: повторный Update без изменения модели.
- `property-edit`: чередование Name «Замер А 👩‍💻» / «Замер Б 👩‍💻» одинаковой длины; модель меняется перед Update.
- `list-edit`: добавление новой строки с фиксированным именем / удаление последней; 32/33 строки и 195/200 элементов. После чётного batch исходные row models/generated controls и всё исходное дерево имеют прежнюю identity/order.

Четыре варианта: raster offscreen, OpenGL с настоящим Silk context, Vulkan offscreen с acquire/return, Vulkan HUD с настоящими compositor/swapchain/presentation. GL окно320×240 предоставляет context, измеряемая UI-поверхность900×1100; Vulkan окно900×1100. Ввод/resize/readback в измеренные кадры не входят. HUD меняет вариант сцены каждый кадр, продолжая композицию при статичном UI.

```powershell
dotnet publish ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj -c Release --no-restore -o TestResults/AlloyPerformance/publish -v quiet
./ManualTests/AlloyVulkanTest/MeasureSettings.ps1 -Executable TestResults/AlloyPerformance/publish/AlloyVulkanTest.exe -OutputDirectory TestResults/AlloyPerformance/baseline
```

Параметры [runner](../../ManualTests/AlloyVulkanTest/MeasureSettings.ps1): `Rows=32`, `Samples=500` (чётное), `Warmup=100`, `WarmupMilliseconds=500`, `Runs=2`. Каждый case прогревается до выполнения **обоих** минимумов; фактические frames/duration записываются в JSON. Перед batch, вне измерения, восстанавливаются начальные Name/row count. В текущих прогонах warmup500–1563ms; static raster выполняет около4.8млн дешёвых iterations за500ms, HUD требует100 кадров/~1.56s. Порядок backend/mode фиксированный; нагрузка фона не контролировалась. Affinity, priority, power scheme и GC настройки runner не меняет.

Для отдельной correctness проверки добавить `-Samples 4 -Warmup 2 -WarmupMilliseconds 0 -Runs 1 -Validation` после настройки VK_LAYER_PATH. В измерительных прогонах validation **выключен**; их нулевые counters не являются свидетельством включённого validation.

## Границы измерений

[SettingsPerformanceProbe](../../ManualTests/AlloyVulkanTest/SettingsPerformanceProbe.cs) использует owner thread реальной сессии, profiling root и proxy IUiRenderSurface; runtime/public API не меняются. [PerformanceReport](../../ManualTests/AlloyVulkanTest/PerformanceReport.cs) сохраняет все samples без исключения outliers и вычитания overhead. Median/p95 — nearest rank; JSON также содержит mean/min/max/population standard deviation и process-wide GC collections по batch. Это последовательный инструментированный frame baseline, без статистической confidence оценки.

`Update` включает bindings/layout/render. `MeasureCore`/`ArrangeCore` содержат обход View1, но исключают outer profiling Panel bookkeeping для margin/padding/slot. `surface-render` — весь вызов IUiRenderSurface.Render. Эти фазы вложены в Update: **время и bytes нельзя складывать с total повторно**. Mutation измеряется отдельно: property368B/frame, alternating append/remove248B/frame в обоих прогонах.

Время — CPU wall time: raster исполняется на CPU, GL Render включает Flush, Vulkan Render записывает Skia work. GL `glFinish`, Vulkan acquire/Submit(true)/WaitIdle и Return/WaitIdle измеряются как host phases; HUD composition и presentation имеют отдельные интервалы с последовательными ожиданиями. Это не GPU timestamps и не сопоставление GPU throughput между backend; различаются точки завершения работы. [Skia flush/submit API](https://api.skia.org/classGrDirectContext.html).

Аллокации — delta [GC.GetAllocatedBytesForCurrentThread](https://learn.microsoft.com/en-us/dotnet/api/system.gc.getallocatedbytesforcurrentthread?view=net-10.0): managed bytes owner thread, включая временные объекты. Native/driver/VRAM, другие потоки и live heap не измеряются. Setup, initial draw, warmup, снимки/assertions/report и teardown исключены; BatchTicks включает harness overhead. Пустой delegate с тем же Begin/End имеет median0.0µs, p950.1µs и0B в обоих процессах, Stopwatch10MHz. Значения static Update около0–1µs близки к пределу инструмента и зависят от cadence/ожиданий HUD.

## Update: два прогона

Время в **µs**, bytes — среднее на один Update; пары означают **run1 / run2**, без объединения распределений.

| Вариант | Режим | Median µs | P95 µs | Managed B/frame |
| --- | --- | ---: | ---: | ---: |
| raster | static | 0.0 / 0.0 | 0.1 / 0.1 | 0 / 0 |
| raster | property-edit | 3796.2 / 3798.1 | 4593.1 / 4668.0 | 120086.0 / 120085.6 |
| raster | list-edit | 3989.2 / 3912.5 | 6167.8 / 5254.4 | 140195.4 / 140194.0 |
| OpenGL | static | 0.0 / 0.0 | 0.1 / 0.1 | 0 / 0 |
| OpenGL | property-edit | 1202.2 / 1183.0 | 1645.1 / 1615.1 | 120072.0 / 120072.0 |
| OpenGL | list-edit | 1417.3 / 1216.5 | 2028.4 / 1633.4 | 140193.3 / 140193.3 |
| Vulkan | static | 0.1 / 0.1 | 0.3 / 0.2 | 0 / 0 |
| Vulkan | property-edit | 897.7 / 878.4 | 1356.8 / 1211.3 | 120112.0 / 120112.0 |
| Vulkan | list-edit | 940.3 / 909.8 | 1489.3 / 1262.8 | 140233.3 / 140233.7 |
| Vulkan HUD | static | 1.1 / 1.0 | 1.9 / 1.9 | 0 / 0 |
| Vulkan HUD | property-edit | 1239.3 / 1199.8 | 1707.0 / 1600.5 | 120112.0 / 120112.0 |
| Vulkan HUD | list-edit | 1293.1 / 1309.3 | 1749.8 / 1806.6 | 140233.3 / 140233.3 |

Медиана OpenGL list-edit изменилась на−14.2% относительно run1; allocations почти совпадают. Малые изменения времени на этой машине ещё нельзя считать выигрышем оптимизации. Во всех static batches GC collections0/0/0; при редактировании наблюдалось3–4 Gen0 collections и отдельные Gen1/Gen2, сохранённые в raw JSON. Samples с GC не удалялись.

## Вложенные layout/render фазы

Медианы **µs**, run1 / run2; в static все три фазы отсутствуют.

| Вариант | Режим | MeasureCore | ArrangeCore | Surface Render |
| --- | --- | ---: | ---: | ---: |
| raster | property-edit | 277.5 / 278.9 | 7.7 / 7.8 | 3505.8 / 3508.1 |
| raster | list-edit | 289.5 / 286.1 | 8.7 / 8.5 | 3639.9 / 3563.5 |
| OpenGL | property-edit | 290.7 / 289.6 | 8.1 / 8.0 | 886.8 / 870.3 |
| OpenGL | list-edit | 323.9 / 293.5 | 8.8 / 8.1 | 1024.5 / 879.9 |
| Vulkan | property-edit | 293.8 / 284.3 | 8.4 / 8.0 | 588.1 / 577.9 |
| Vulkan | list-edit | 293.9 / 290.6 | 8.2 / 7.9 | 595.8 / 582.3 |
| Vulkan HUD | property-edit | 483.5 / 452.1 | 13.1 / 11.6 | 706.0 / 712.0 |
| Vulkan HUD | list-edit | 477.5 / 467.3 | 12.5 / 13.2 | 715.6 / 740.8 |

Property-edit: Measure31240B, Arrange1640B, Render~86424–86464B. List-edit: Measure31680B, Arrange1660B, Render87608–87648B. Оставшиеся allocations Update включают binding/collection work. Фаза render даёт большую часть allocations и времени Update; конкретный stack ещё не профилировался. Следующий шаг — CPU/allocation profile `surface-render` и текстового measure/draw пути, затем одна подтверждённая оптимизация и повтор той же процедуры. Font/paint cache и dirty-subtree layout этим результатом не объявляются готовыми решениями.

## Host отдельно от UI

Медианы **µs**, run1 / run2. Raster не имеет host phases; отсутствующие фазы обозначены «—» (Calls=0).

| Вариант | Режим | Export / completion | Return | Composition | Presentation |
| --- | --- | ---: | ---: | ---: | ---: |
| OpenGL | static | 1.4 / 1.0 | — | — | — |
| OpenGL | property-edit | 22.5 / 22.0 | — | — | — |
| OpenGL | list-edit | 30.0 / 22.1 | — | — | — |
| Vulkan | static | 54.8 / 40.1 | 7.7 / 6.3 | — | — |
| Vulkan | property-edit | 168.6 / 144.0 | 7.7 / 6.8 | — | — |
| Vulkan | list-edit | 166.0 / 154.8 | 7.2 / 7.0 | — | — |
| Vulkan HUD | static | 227.0 / 222.8 | 11.3 / 10.7 | 199.6 / 192.6 | 15127.6 / 15211.4 |
| Vulkan HUD | property-edit | 322.4 / 343.1 | 8.9 / 9.2 | 164.9 / 177.4 | 13796.5 / 13734.8 |
| Vulkan HUD | list-edit | 330.5 / 354.1 | 9.0 / 9.9 | 166.4 / 183.5 | 13828.8 / 13572.8 |

Ожидание presentation (~13.6–15.2ms) входит в host frame, а не в Update. UI factory Creates/Resize=0 за каждый batch; Vulkan UI image handle постоянен. HUD swapchain generation1; после Dispose live swapchains/semaphores/fences/command pools0. Это counters конкретных wrapper/owned handles; отсутствие всех native allocations или накопления VRAM ими не доказано.

## Конфигурация и артефакты

Windows11 Pro for Workstations build26300 x64; Ryzen9 9900X12cores/24threads; NVIDIA GeForce RTX5070Ti для GL и Vulkan. NVIDIA driver32.0.16.1692 (GL616.92), date2026-09-04; Vulkan DriverVersion2585198592, ApiVersion4211039. AMD Radeon integrated присутствует, в этих замерах не выбран. Power scheme «Максимальная производительность». SDK10.0.401, runtime.NET10.0.12, Release/net10.0, workstation GC/Interactive, debugger отсутствует; TieredPGO/TieredCompilation env не переопределены. Silk.NET2.23.0, SkiaSharp4.153.1.

Git main HEAD93a5cae22f3dd10574f318ee29d0d9d073c4ce57, dirty worktree. HEAD не идентифицирует измеренный код: JSON содержит перечни SHA256 source/binary/asset/трёх Segoe font files. SourceFingerprint **0AC27C1E45575B3B56339E9C6B10ADFAB351636FC6699B62933DB616A8728E7D** одинаков в двух прогонах. AlloyVulkanTest.dll **BA1325F91BE965D7F3889059418E40F6E614C607EF033DF216943DF1597B05DA**. Source fingerprint включает harness/runner и исходники участвующих модулей, исключая bin/obj; новые docs не входят.

Native ABI1/win-x64, SkiaSharp revision4783f51448f9b070dda4f87b83e941c9599e466e, Skia revision45afab4f1f0921f3feb97f58cf89b136fffa85e6; actual DLL SHA256 **96525E8EFC1E3BB4B9C64000307D4D11C378FEDAEEB7C1E62D518EF84CDD1ABE**. PNG SHA256 **BB07D1AC6AC509E052F45F2304980DCCBB42B3089DA49BE75C52DA99A3089B3E**. Segoe/system fallback — конфигурация этой машины; доставляемых fixed-font fixtures ещё нет, другие возможные fallback fonts не полностью идентифицированы.

Raw JSON/CSV/Markdown/environment/stdout сохранены в **ignored** `TestResults/AlloyPerformance/baseline/20261007-215144-*`; для переноса результатов их нужно сохранять отдельно от Git. JSON hashes:

- run1: `6A2F10CCC758369092A6D0434BA3A11C0E6C349F41D9EE6BF808FCACB89E83B1`.
- run2: `8AE947B56B09310671BBF5E1CAAFDF381A76023F41FCF3D4710A0628CA48A6FA`.

Локальный `TestResults/AlloyPerformance/verify-baseline.ps1` пересчитал12 000 кадров/216 phase summaries/216 CSV rows; checked nested intervals/allocations, call counts, warmup, nodes, stable Vulkan image, presenter cleanup и совпадение source/binary/font/native/scene/runtime fingerprints. Итоговые build/UI/native correctness результаты и оставшиеся alpha требования — [STATUS.md](STATUS.md). Последующее сравнение требует совпадающих scene/harness/configuration и новых прогонов; изменение любой границы нужно зафиксировать явно.

## Профиль и оптимизация 2026-10-08

Выполнен следующий шаг K9: профиль managed stacks/allocations, ограниченное переиспользование ресурсов рисования и новый baseline до/после. Исходные результаты2026-10-07 выше сохранены как исторические. Для вывода об оптимизации используются только свежие пары2026-10-08: GPU driver теперь32.0.16.1742, а не32.0.16.1692 прежнего baseline.

### Сбор и границы профиля

Локально установлен dotnet-trace10.0.750501 в ignored TestResults/AlloyPerformanceProfile/tools. До и после записаны EventPipe traces всей Settings-сцены (включая setup/warmup/teardown) с gc-verbose,dotnet-sampled-thread-time,100 measured frames/case, warmup≥100frames/≥500ms. Это sampled managed thread time и approximate allocation ticks; native CPU stacks, GPU timestamps и VRAM не собирались. Thread samples включают ожидания native calls и не являются точным CPU execution profile. [Профили dotnet-trace](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace), [TraceEvent stack API](https://github.com/microsoft/perfview/blob/main/documentation/TraceEvent/TraceEventProgrammersGuide.md).

Исходный verbose trace:3100 allocation events,5969 sampled-thread events, lost0; один allocation event без stack. После:2688/6014/lost0, missing stacks0. Read-only локальный analyzer на TraceEvent DLL из этого tool фильтрует allocation stacks, содержащие SkiaDrawingContext, и сохраняет тип/approximate bytes/full stack плюс inclusive managed method samples. В исходном пути есть SKFont, SKPaint, Byte[], Object/WeakReference и SKTextBlob/Builder. Стек Measure → new SKFont/HandleDictionary и Text/Fill → SKPaint подтверждён; повторное создание font/paint выбрано первым кандидатом. После reuse в отфильтрованных allocation samples остались SKTextBlobBuilder/SKTextBlob и служебный Object; отсутствие sampled font/paint events не означает отсутствие cold-start native allocations.

Первый trace с дополнительным dotnet-common дал thread samples, но не allocation ticks: runtime verbosity оказалась Informational. Он не использован для allocation вывода. Итоговая коллекция исключает dotnet-common и явно показывает runtime0x8003/Verbose5. Trace timing не включён в сравнение baseline; разные warmup call counts запрещают сравнивать суммарные sampled bytes как per-frame выигрыш.

Воспроизведение из src (Release publish подготовлен обычным способом):

```powershell
dotnet tool install dotnet-trace --version 10.0.750501 --tool-path TestResults/AlloyPerformanceProfile/tools
./TestResults/AlloyPerformanceProfile/tools/dotnet-trace.exe collect --profile gc-verbose,dotnet-sampled-thread-time --output TestResults/AlloyPerformanceProfile/profile.nettrace -- ./TestResults/AlloyPerformanceProfile/after/publish/AlloyVulkanTest.exe --settings-baseline --baseline-samples 100 --baseline-warmup 100 --baseline-warmup-ms 500 --baseline-output TestResults/AlloyPerformanceProfile/traced.json
```

Install выполняется один раз; для чтения traces можно использовать PerfView/TraceEvent. Текущий локальный analyzer source/exe/profile JSON находятся в ignored TestResults/AlloyPerformanceProfile/Analyzer и before/after, это диагностические артефакты, не новый production проект/dependency.

### Реализация и владение

[SkiaDrawingResources](../../TrueMoon.Alloy.Rendering.Skia/SkiaDrawingContext.cs) принадлежит одной UI surface: один SKPaint и FIFO-кэш максимум16 SKFont/metrics по точным family/size. Measure/Draw разделяют cache, который живёт через resize/scale/suspend и освобождается при surface teardown; сессии не разделяют mutable resources. Font eviction освобождает native font. SKFont сохраняет native reference typeface, временный managed SKTypeface освобождается при создании font; cache не удерживает shared managed typeface wrappers. Цвет/style/stroke width устанавливаются перед каждым Fill/Stroke/Text. Constructor rollback/terminal failure cleanup включают cache, не меняя borrowed canvas/context/device ownership.

TextLayout/resource operations теперь явно проверяют creator thread и disposed state, включая ранее полученный сервис после закрытия surface. Cache строки/text blob, dirty-subtree/partial redraw и изменение shaping/fallback алгоритма не вводились. Native DLL/ABI, packages, backend contracts и public API не менялись. [InternalsVisibleTo](../../TrueMoon.Alloy.Rendering.Skia/AssemblyInfo.cs) добавлен только для focused tests.

### Свежий baseline до/после

По два независимых Release-процесса до и после,12cases×500samples/process — **24 000 raw frames** всего. Сцена/границы/harness прежние:32 строки/195–200 элементов, viewport900×1100/scale1; warmup≥100frames и≥500ms, validation выключен. Power/affinity/priority/GC не менялись. Локальный verify.ps1 пересчитал raw nearest-rank median/p95/allocations, nested intervals/bytes, calls/warmup/nodes и fingerprints; статичный UI во всех16batches даёт0Measure/Arrange/Draw/Update bytes, stable tree/surface/image.

Median Update в **µs**, пары run1/run2; распределения не объединены:

| Вариант | Режим | До µs | После µs |
| --- | --- | ---: | ---: |
| raster | property-edit | 3798.2 / 3803.4 | 3271.1 / 3293.8 |
| raster | list-edit | 3919.5 / 3901.3 | 3432.2 / 3506.3 |
| OpenGL | property-edit | 1067.8 / 1054.6 | 717.7 / 601.1 |
| OpenGL | list-edit | 1114.9 / 1064.9 | 709.5 / 650.6 |
| Vulkan | property-edit | 837.7 / 880.3 | 344.9 / 354.0 |
| Vulkan | list-edit | 887.1 / 887.0 | 387.5 / 391.2 |
| Vulkan HUD | property-edit | 943.2 / 977.2 | 471.5 / 460.5 |
| Vulkan HUD | list-edit | 989.0 / 1087.0 | 537.4 / 544.0 |

Managed Update bytes/frame: property~120072–120112 →~46448–46496 (**−61.3%**); list~140193–140233 →~65569–65617 (**−53.2%**), с небольшими raster variations. Measure:31240 →10072B/property,31680 →10224B/list; Render:~86424–86464 →~33968–34016B/property и~87608–87648 →~34440–34488B/list. Эти вложенные bytes не складываются с Update повторно. Median Measure после~59–84µs/property,~63–90µs/list; при raster общий CPU render остаётся основной затратой.

В этих процессах median Update снизилась примерно10–14% raster и33–60% GPU-варианты; это наблюдение этой конфигурации, без confidence interval/performance budget и без утверждения GPU throughput. OpenGL заметно различается между двумя after runs, фон не контролировался. Host phases продолжают записываться отдельно, система presentation/HUD не оптимизировалась.

Windows11build26300x64, Ryzen9 9900X/12cores/24threads, RTX5070Ti, NVIDIA driver32.0.16.1742, SDK10.0.401/runtime10.0.12, Release/net10.0, workstation GC/Interactive, no debugger. Font/PNG/native hashes совпадают до/после; renderer SHA256 до62539C68FC5B3ADC5A362DBD477F4A19ABC46800627C2D8BFDBAB6F72E6C09BB, после33B33FDF43868B5B09E204875120AA2143CFE92D812E73873088042A5F8A70ED. SourceFingerprint до0AC27C1E45575B3B56339E9C6B10ADFAB351636FC6699B62933DB616A8728E7D, после58BDEB1D204F56F05DA901C5F98B9389B629C34A8E9D2DEA10AA63DD20ED4E85. Fingerprints обоих processes внутри каждой пары совпали; verifier подтвердил current source/after publish bytes.

Raw JSON/CSV/Markdown/environment/logs: ignored TestResults/AlloyPerformanceProfile/before/baseline/20261008-145623-run1/run2 и after/baseline/20261008-150549-run1/run2; traces/profile JSON в before/after, verifier/Analyzer/TRX/native logs рядом. Эти локальные артефакты нужно отдельно сохранить при переносе результатов.

### Проверки и оставшиеся ограничения

Focused **6passed**, UI **410passed/0failed/0skipped**, solution non-incremental build **0errors/1339warnings**, Release backend/standalone publish exit0, solution discovery видит все6новых cases. [SkiaDrawingResourceTests](../../TrueMoon.Alloy.Tests/SkiaDrawingResourceTests.cs) сравнивают все raster bytes с uncached drawing при scales1/1.5/2, eviction/measurement/native handles, foreign-thread mutation/disposal rejection и text-service resize/disposed lifetime.

Итоговый native runner **16/16 exit0**, по8Debug/published Release: controlled graphics failure/recreation, Settings comparison3scales, HUD90compositions/36draws/2sessions, correctness baseline12cases×4samples, standalone OpenGL/Vulkan, short KHR/legacy soak5sessions/50frames/10swapchains каждый. Vulkan validation **0errors/0warnings включая teardown**, GL checks NoError, resources/subscriptions/retained roots0 в soak.

**Нерегулярный сбой не скрыт:** первый runner остановился на Release standalone Vulkan SettingsFormSmoke.Frame stage2/line83, VerifyValues/line311, Name binding mismatch (exit−532462766). Тот же exe без изменения кода прошёл отдельный retry и итоговый полный runner. Причина/actual values не установлены, runtime fix не делался. Debugger discovery: нет run point для Program.cs, существующая AlloyTest configuration supportsDynamicLaunchOverrides=false, поэтому точный published --vulkan --smoke --validation запуск через эти MCP не выполнен; конфигурация IDE не менялась. Логи first-failure/first-run/retry/final сохранены. Повторный успех не доказывает устранение нестабильности; при повторе нужен runtime values/call order/native input evidence.

Полный solution test suite, long200soak, physical DPI/device loss, fixed-font golden, native CPU/driver/VRAM profile и package consumer не выполнялись. K9 подэтап достигнут в этих границах; alpha не завершён. Следующий этап —4.9a оконный prototype/capability matrix; нерегулярный standalone smoke остаётся отдельным риском итогового alpha прохода.

<a id="uia42-view-cost-2026-11-27"></a>
## View-слой: точечная стоимость UIA4.2 — 2026-11-27

Замеряет то, чего в baseline не было: типизированные свойства, фазы layout и обе привязки
(списка и свойства) на масштабируемой сцене. Стенд — `Benchmarks/TrueMoon.Argentis.Benchmarks`
([протокол](../../Benchmarks/TrueMoon.Argentis.Benchmarks/README.md)), сцена «инспектор»:
строка = подпись + редактор фиксированного размера + две подписи значения, то есть 5 узлов на строку
и 196 узлов при 39 строках — ровно размер K9 Settings. Метрики текста фиксированы,
`HeadlessSurface.Render` не обходит и не рисует дерево, поэтому замер охватывает уведомления,
привязки и layout без отрисовки, окон, GPU, ввода, шрифтов и native-памяти. Whole-frame
с реальной отрисовкой по-прежнему измеряет только K9-раздел выше; складывать цифры нельзя.

Протокол: 24 свежих процесса (4 группы × 3 размера × 2 прохода), 162 результата, `--verify`
перед замером, медиана и P95 nearest rank по 200 сырым сэмплам, `NoOp`-пол 0 µs и 0 байт
во всех группах. Проход 1 и проход 2 приводятся парами; расхождение между проходами одного
случая — шум, а не результат.

Конфигурация: Windows 11 Pro for Workstations 10.0.26340 x64, AMD Ryzen 9 9900X, .NET SDK
11.0.100 / runtime Microsoft.NETCore.App 11.0.0, конфигурация Release, план электропитания
«Максимальная производительность», `Stopwatch` 10 МГц, переменные GC/JIT не заданы.
HEAD `3fcd200e819a997c1bcbe980852f4c70d2e58387`, рабочее дерево на момент прогона грязное;
полные SHA256 `TrueMoon.Argentis.dll` (`8C4AAF0E…`), `TrueMoon.Alloy.dll` (`0C97A189…`) и
стенда, отпечатки 212 исходников и окружение — в `summary.json` папки прогона.

### Свойства элемента

| Глубина | Метод | µs медиана п1/п2 | P95 п1/п2 | Байт медиана |
|---|---|---|---|---|
| 2496 | GetLocalValue | 0 / 0 | 0 / 0 | 0 |
| 2496 | GetThemeValue | 0 / 0 | 0 / 0 | 0 |
| 2496 | GetUnknownDefault | 0 / 0 | 0 / 0 | 0 |
| 2496 | SetSameValue | 0 / 0 | 0 / 0 | 24 |
| 39 | SetChangedValue | 0 / 0 | 0 / 0 | 24 |
| 312 | SetChangedValue | 1 / 1 | 1 / 1 | 24 |
| 2496 | SetChangedValue | 4 / 6 | 6 / 6 | 24 |
| 312 | InvalidateRender | 1 / 1 | 1 / 1 | 0 |
| 2496 | InvalidateRender | 5 / 5 | 7 / 6 | 0 |

Чтение (в том числе «промах» со всеми ветками сравнения стиля и темы) не различимо на таймере и не
аллоцирует — оптимизировать `Get` нечем. Инвалидация к корню стоит примерно 2 нс на уровень,
то есть на реальных глубинах она не проблема; дорогой её делает пара «сеттер → обход», а не обход.
Каждая запись в словарь свойств отдаёт 24 байта на боксированное значение — это единственный
аллокационный след свойства, и он оплачивается даже когда значение не меняется.

### Узлы и layout

| Узлов | Метод | µs медиана п1/п2 | P95 п1/п2 | Байт медиана |
|---|---|---|---|---|
| 196 | BuildScene | 354 / 227 | 421 / 316 | 273 192 |
| 1561 | BuildScene | 1954 / 1832 | 2951 / 3123 | 2 181 280 |
| 12481 | BuildScene | 16291 / 16313 | 23695 / 24301 | 17 591 080 |
| 12481 | BuildAndDisposeScene | 8810 / 9742 | 24810 / 25161 | 23 442 248 |
| 196 | MeasureScene | 107 / 17 | 155 / 32 | 4 800 |
| 1561 | MeasureScene | 151 / 143 | 155 / 260 | 37 560 |
| 12481 | MeasureScene | 1202 / 1191 | 1261 / 1795 | 299 640 |
| 196 | ArrangeScene | 9 / 10 | 16 / 16 | 3 200 |
| 1561 | ArrangeScene | 78 / 77 | 86 / 131 | 25 040 |
| 12481 | ArrangeScene | 629 / 636 | 660 / 1008 | 199 760 |
| 12481 | MeasureAndArrangeScene | 1848 / 1842 | 1956 / 2506 | 499 400 |
| 12481 | LeafEditThenLayout | 1820 / 1875 | 1875 / 2623 | 499 400 |

- Сборка сцены линейна и стоит 1394–1410 байт на узел (273 192/196 → 17 591 080/12 481),
  то есть постоянные затраты узла не зависят от размера сцены: 12 481 узел — 17.6 МБ за 16.3 мс,
  39 строк инспектора — 273 КБ. Это бюджет UIA4.4 (четыре коллекции на узел плюс scope и слой).
- Полный обход measure — 96 нс и 24 байта на узел, arrange — 50 нс и 16 байт: сами обходы
  дешевле, чем сборка тех же узлов, и тоже аллоцируют, хотя изменённых значений нет.
- `LeafEditThenLayout` (1820/1875) совпадает с `MeasureAndArrangeScene` (1848/1842) при одном
  грязном листе: положение изменённого узла не даёт ничего, весь обход оплачивается целиком.
  Числовое подтверждение UIA4.3 — адресной инвалидации и кэша measure нет.

### Привязка коллекций

Размер — число строк; один вызов содержит одну мутацию источника и один `Update` сессии.

| Строк | Метод | µs медиана п1/п2 | P95 п1/п2 | Байт медиана |
|---|---|---|---|---|
| 39 | UpdateStatic | 0 / 0 | 0 / 0 | 0 |
| 2496 | UpdateStatic | 0 / 0 | 0 / 0 | 0 |
| 39 | AppendRemoveRow | 109 / 116 | 154 / 192 | 41 488 |
| 312 | AppendRemoveRow | 1056 / 804 | 1329 / 1217 | 244 616 |
| 2496 | AppendRemoveRow | 7771 / 8248 | 11160 / 13148 | 2 002 048 |
| 39 | MoveFirstRowToEnd | 141 / 105 | 167 / 163 | 37 528 |
| 312 | MoveFirstRowToEnd | 1464 / 1083 | 1928 / 1610 | 236 920 |
| 2496 | MoveFirstRowToEnd | 1715 / 1670 | 3012 / 3388 | 1 994 312 |
| 39 | NotifyOneRowName | 168 / 194 | 187 / 208 | 8 472 |
| 2496 | NotifyOneRowName | 975 / 1035 | 1485 / 1566 | 499 872 |
| 39 | RefreshAllRows | 5 / 5 | 6 / 6 | 9 248 |
| 2496 | RefreshAllRows | 88 / 114 | 168 / 180 | 410 336 |
| 39 | ResetAllRows | 367 / 356 | 476 / 489 | 426 456 |
| 2496 | ResetAllRows | 29109 / 33726 | 43653 / 51220 | 27 058 744 |

- `UpdateStatic` — 0 µs и 0 байт: в покое привязка ничего не стоит, вся цена в пересборке.
- Одна вставка или удаление отдают 802 байта и ~3.1 мкс на **каждую** строку (39 → 109 µs,
  2496 → 7771 µs): стоимость мутации линейна размеру списка. Это бюджет UIA4.1 и прямое
  объяснение, почему инспектор держит словари адресов, а не `BindItems`.
- `Move` первой строки в конец — 800 байт и ~0.67–3.6 мкс на строку, то есть перестановка
  оплачивает порядок всего списка, несмотря на сохранённую идентичность.
- `RefreshItems` без изменений источника — 164 байта и 35 нс на строку: цена только снимка
  источника и сверки порядка, полезной работы нет.
- `Clear` + повторное добавление того же количества строк — 10.8 КБ и ~11.7 мкс на строку
  (29.1 мс на 2496). Для сравнения: та же сцена целиком строится за 16.3 мс, то есть
  «перезагрузка» списка дороже первоначальной сборки. Это стоимость UIA4.5.

### Привязки свойств

Размер — число `Bind` в одном экране.

| Привязок | Метод | µs медиана п1/п2 | P95 п1/п2 | Байт медиана |
|---|---|---|---|---|
| 1 | CreateBindings | 9 / 10 | 10 / 11 | 11 480 |
| 64 | CreateBindings | 181 / 192 | 291 / 264 | 272 640 |
| 512 | CreateBindings | 1723 / 1596 | 2579 / 2093 | 2 129 376 |
| 1 | AttachAndFirstSync | 18 / 19 | 29 / 32 | 18 840 |
| 512 | AttachAndFirstSync | 3183 / 2865 | 4200 / 3482 | 2 647 872 |
| 1 | NotifyOneAndUpdate | 6 / 4 | 6 / 4 | 1 208 |
| 64 | NotifyOneAndUpdate | 104 / 92 | 109 / 96 | 1 208 |
| 512 | NotifyOneAndUpdate | 580 / 539 | 843 / 584 | 1 208 |
| 512 | NotifyAllAndUpdate | 217 / 157 | 225 / 162 | 39 144 |
| 512 | UiEditWritesSource | 71 / 97 | 127 / 119 | 1 280 |

- Объявление привязки — 3.4 мкс и 4.2 КБ (11.5 КБ на первую, включая сам экран): декларативное
  дерево из 512 привязок обходится в 1.7 мс и 2.1 МБ до первого кадра, по 4 КБ на строку XAML-
  аналога. Причина в выражении: `Expression` разбирается и компилируется на каждую привязку.
- `NotifyOneAndUpdate` растёт линейно с числом привязок (6 → 104 → 580 µs при одном изменении
  источника): уведомление оплачивает все живые привязки контейнера. Байт при этом 1.2 КБ —
  время уходит в обход и повторные сверки значений.
- `NotifyAllAndUpdate` (512 изменений за раз — 217/157 µs) **дешевле** одного уведомления:
  проходы пишут одно и то же строковое значение, поэтому `Set` видит совпадение и не объявляет
  layout. Пара читается как «значение изменилось / не изменилось», а не как «одно / все».
- Обратный ход (правка из UI в источник) — 71–97 µs на 512 привязок при 1.3 КБ: он на порядок
  дешевле входящего уведомления, потому что идёт по одной привязке.

### Кандидаты, которые задают эти цифры

| Цифра | Задача плана |
|---|---|
| 1394–1410 Б/узел на сборку | UIA4.4 — отложенные коллекции и компактный store |
| 24 Б/узел в measure, 16 Б/узел в arrange, аллокации без изменений значений | UIA4.4 — атрибюция и устранение |
| 24 Б на запись свойства, включая запись того же значения | UIA4.4 — store свойств без бокса |
| `LeafEditThenLayout` == полный обход | UIA4.3 — адресная инвалидация и кэш measure |
| 802 Б и 3.1 мкс на строку за одну вставку, ~3.6 мкс на строку за `Move` | UIA4.1 — стабильные ключи и инкрементальный reconcile |
| 10.8 КБ и 11.7 мкс на строку при `Clear`+`Add` | UIA4.5 — условный слот без пересборки |
| 3.4 мкс и 4.2 КБ на объявление привязки | UIA4.2-следствие — источник выражений вместо `Expression` на привязку |
| 580 µs на одно уведомление при 512 привязках | UIA4.1/UIA4.3 — обход привязок и инвалидация по адресу |

Столбцы медианы/P95/байт и GC на батч остаются во всех последующих сравнениях; повторный прогон
— `pwsh -NoProfile -File Benchmarks/TrueMoon.Argentis.Benchmarks/RunBaseline.ps1`, вывод в
`TestResults/ViewBaseline/run-*` с отпечатками сцены, исходников и бинарей.

<a id="uia41-collection-reconcile-2026-11-27"></a>
## Привязка списков: ключи и инкрементальный reconcile UIA4.1 — 2026-11-27

Пара к [замеру UIA4.2](#uia42-view-cost-2026-11-27) на той же сцене inspector-v1, тех же размерах и
том же протоколе: 30 свежих процессов (пять групп × три размера × два прохода), 204 результата,
200 сырых сэмплов, `--verify` перед замером, `NoOp`-пол 0 µs и 0 байт во всех группах. Изменён
движок: `ElementCollection.Reconcile` строит план в переиспользуемых буферах и объявляет
`TreeChange` по длине буфера, `CollectionBinding<TItem,TKey>` читает `IReadOnlyList` и опознаёт
строку по ключу с необязательным `rebind`, контейнер может нести несколько привязок, список без
`INotifyCollectionChanged` синхронизируется через `RefreshItems`. Отпечатки прогона
`TestResults/ViewBaseline/run-20261127-024517011`: `TrueMoon.Argentis.dll` `D1BCE799…`,
`TrueMoon.Alloy.dll` `7E1B9D9E…`, стенда `05261989…`, HEAD `3fcd200e`, рабочее дерево грязное.

`collection-binding` — действующая reference identity (совместимый путь, `ObservableCollection`),
`keyed-binding` — стабильный ключ (`row => row.Id`) по простому `List<T>` с явным `RefreshItems`.
Столбцы µs — медиана проходов 1/2, байт — медиана.

| Строк | Операция (reference identity) | µs до | µs после | Байт до | Байт после |
|---|---|---:|---:|---:|---:|
| 39 | AppendRemoveRow | 109 | 98 / 96 | 41 488 | 23 584 |
| 312 | AppendRemoveRow | 1056 | 759 / 754 | 244 616 | 153 880 |
| 2496 | AppendRemoveRow | 7771 | 7196 / 7236 | 2 002 048 | 1 162 664 |
| 312 | MoveFirstRowToEnd | 1464 | 1277 / 1254 | 236 920 | 146 344 |
| 2496 | MoveFirstRowToEnd | 1715 | 2059 / 2750 | 1 994 312 | 1 159 648 |
| 39 | RefreshAllRows | 5 | 3 / 3 | 9 248 | 704 |
| 2496 | RefreshAllRows | 88 | 95 / 131 | 410 336 | 20 360 |
| 39 | ResetAllRows | 367 | 330 / 321 | 426 456 | 408 896 |
| 2496 | ResetAllRows | 29109 | 27155 / 34246 | 27 058 744 | 26 258 104 |
| 2496 | UpdateStatic | 0 | 0 / 0 | 0 | 0 |

| Строк | Операция (стабильный ключ) | µs | Байт |
|---|---|---:|---:|
| 39 | KeyedUpdateStatic | 0 / 0 | 0 |
| 39 | KeyedRefreshUnchanged | 3 / 2 | 704 |
| 2496 | KeyedRefreshUnchanged | 198 / 200 | 20 360 |
| 39 | KeyedAppendRemoveRow | 93 / 153 | 23 512 |
| 312 | KeyedAppendRemoveRow | 761 / 765 | 153 808 |
| 2496 | KeyedAppendRemoveRow | 2104 / 6783 | 1 162 592 |
| 39 | KeyedMoveFirstRowToEnd | 112 / 148 | 19 600 |
| 2496 | KeyedMoveFirstRowToEnd | 2130 / 2348 | 1 159 648 |
| 39 | KeyedRebuildSameKeys | 11 / 12 | 3 744 |
| 312 | KeyedRebuildSameKeys | 28 / 43 | 28 152 |
| 2496 | KeyedRebuildSameKeys | 125 / 121 | 290 232 |
| 39 | KeyedReplaceRowInstance | 119 / 138 | 8 808 |
| 312 | KeyedReplaceRowInstance | 231 / 233 | 65 592 |
| 2496 | KeyedReplaceRowInstance | 1018 / 1006 | 519 864 |

- Аллокации структурной мутации снижены примерно вдвое: одна вставка или удаление отдают 466 байта
  на каждую строку списка вместо 802 (1 162 664/2496), на 312 строк — 493 байта.
- `RefreshItems` без изменения источника — 20 360 байт вместо 410 336 (−95%), то есть 8 байт на
  строку: остаётся только снимок источника, сверка порядка больше не плодит коллекций.
- Время структурной мутации почти не изменилось (7771 → 7196 µs): 12 481 узел сцены измеряются и
  раскладываются полным обходом за ~1.8 мс (UIA4.2), остальное — снимок и валидация всего списка.
  `Move` на 2496 строк — 2059/2750 µs против 1715/1670 до при −42% байт: разброс проходов
  сопоставим с разницей, отдельный проход ничего не утверждает.
- Ключ меняет цену перезагрузки списка: `Clear` + те же id — 27 155 µs и 26.3 МБ при reference
  identity (строки пересоздаются) против 125 µs и 290 КБ при ключе (строки выживают): 217× быстрее
  и 90× меньше памяти, 50 нс и 116 байт на строку — в основном сами новые модели источника.
- Замена экземпляра строки с сохранённым ключом через `rebind` стоит как обычное уведомление
  свойства (1018 µs против 950 µs `NotifyOneRowName` при 2496 строках): узел не пересоздаётся,
  поэтому фокус, ввод и подписки строки не теряются.
- Пустой refresh по ключу стоит 198 µs против 95 µs по ссылке (≈30 нс на строку) — это цена
  отдельной карты ключей и экземпляров вместо сравнения ссылок.
- `KeyedAppendRemoveRow` на 2496 расходится между проходами (2104 и 6783 µs) сильнее, чем разница
  групп: байты при этом совпадают (1 162 592), так что по времени на этом размере вывод не делается.

Прикладное подтверждение — `TrueMoon.Aether.Editor.UI/Editing/EditorInspectorView.cs`: списки
добавления компонентов и скрытых вхождений переведены на ключи (`(TypeId, Version)` и `ComponentId`)
вместо `_addButtons`/`_restoreButtons` с ручными `Items.Move` и `Dispose`; с 27.11.2026 там же
привязаны секции компонентов (`(ComponentId, поколение контекста)`) и строки свойств внутри тела
секции (`(Key, запись свойства, поколение контекста)`), восемь словарей адресов убраны. Поведение
проверяют `InspectorReorderTests`, `InspectorIslandStyleTests` и `EditorInspectorCompositionTests`.
Секция UIA4.1 в [PLAN](PLAN.md#uia4-view-layer-audit).

Следующий кандидат по этим цифрам — [UIA4.3](#uia43-addressable-layout-2026-11-27): пока measure/arrange
оплачиваются полным обходом, структурная мутация в привязанном списке не может стать дешевле ~1.8 мс
на 12 481 узел.

<a id="uia43-addressable-layout-2026-11-27"></a>
## Layout: кэш валидности measure и arrange UIA4.3 — 2026-11-27

Пара к [замеру UIA4.2](#uia42-view-cost-2026-11-27) и [UIA4.1](#uia41-collection-reconcile-2026-11-27)
на той же сцене `inspector-v1`, тех же размерах и том же протоколе: 30 свежих процессов, 204 результата,
200 сырых сэмплов, `--verify` перед замером, `NoOp`-пол 0 µs и 0 байт во всех группах. Стенд не менялся
(`Cases.cs` `DC19740F…`, `Harness.cs` `5D2F9066…`, dll `CFD29BEF…`). Изменён движок: `Element.Measure`
и `Element.Arrange` кэшируют результат по тому же ограничения размера и тому же экземпляру
`ITextLayoutService`; `Invalidate(Layout|Tree)` снимает флаги только на изменённом узле и его предках,
поэтому соседи остаются валидными; повторный меряющий вызов внутри `ScrollViewer` выполняется только
если контенту действительно нужны снятые под скроллбар 10 единиц (`Element.cs:549-576`,
`Layout/ScrollViewer.cs:87-96`). Отпечатки прогона `TestResults/ViewBaseline/run-20261127-042340835`:
`TrueMoon.Argentis.dll` `6F357EAF…`, `TrueMoon.Alloy.dll` `81C8E768…`, HEAD `3fcd200e`, рабочее дерево
грязное.

| Узлов | Операция | µs до | µs после (п1/п2) | Байт до | Байт после |
|---|---|---:|---:|---:|---:|
| 196 | MeasureScene | 107 / 17 | 0 / 0 | 4 800 | 0 |
| 1561 | MeasureScene | 151 / 143 | 0 / 0 | 37 560 | 0 |
| 12481 | MeasureScene | 1202 / 1191 | 0 / 0 | 299 640 | 0 |
| 12481 | ArrangeScene | 629 / 636 | 0 / 0 | 199 760 | 0 |
| 12481 | MeasureAndArrangeScene | 1848 / 1842 | 0 / 0 | 499 400 | 0 |
| 12481 | LeafEditThenLayout | 1820 / 1875 | 124 / 120 | 499 400 | 400 |
| 2496 | LeafEditThenLayout (строки) | — | 2 / 4 | — | 400 |
| 312 | LeafEditThenLayout (строки) | — | 15 / 28 | — | 400 |

Мутации привязанного списка (одна мутация источника + один `Update`; сравнение с колонкой «после»
UIA4.1 — она же была «до» для UIA4.3):

| Строк | Операция | µs до (UIA4.1) | µs после (п1/п2) | Байт до | Байт после |
|---|---|---:|---:|---:|---:|
| 312 | AppendRemoveRow | 759 / 754 | 228 / 233 | 153 880 | 86 888 |
| 2496 | AppendRemoveRow | 7196 / 7236 | 1651 / 1611 | 1 162 664 | 663 464 |
| 312 | NotifyOneRowName | — | 21 / 25 | 499 872 (при 2496) | 872 |
| 2496 | NotifyOneRowName | 950 / 1018 | 214 / 132 | 499 872 | 872 |
| 312 | MoveFirstRowToEnd | 1277 / 1254 | 779 / 749 | 146 344 | 108 904 |
| 2496 | MoveFirstRowToEnd | 2059 / 2750 | 1504 / 1608 | 1 159 648 | 860 200 |
| 2496 | KeyedReplaceRowInstance | 1018 / 1006 | 170 / 291 | 519 864 | 20 864 |
| 2496 | KeyedAppendRemoveRow | 2104 / 6783 | 1690 / 1701 | 1 162 592 | 663 392 |
| 2496 | ResetAllRows | 27155 / 34246 | 29343 / 28890 | 26 258 104 | 26 497 720 |
| 2496 | KeyedRebuildSameKeys | 125 / 121 | 231 / 243 | 290 232 | 290 232 |
| 12481 | BuildScene | 16291 / 16313 | 18460 / 17978 | 17 591 080 | 18 090 352 |
| 2496 | InvalidateRender (глубина) | 5 / 5 | 6 / 6 | 0 | 0 |

- Цена обхода снята целиком: повторный `Measure`/`Arrange` неизменного дерева — 0 µs и 0 байт на всех
  трёх размерах в обоих проходах; `MeasureCore`/`ArrangeCore` неизменённых поддеревьев не вызываются
  (`LayoutInvalidationTests.UnchangedAndRootOnlyInvalidation_SkipChildWalks`).
- Один грязный лист при 12 481 узле: 1820/1875 µs и 499 400 байт → 120–124 µs и 400 байт, то есть
  ~15× по времени и 1248× по памяти; обход идёт по пути от листа до корня (≈7 уровней сцены),
  сёстры не меряются и не раскладываются (`LeafValueEdit_WalksAncestorPathAndLeavesSiblingAlone`).
- Уведомление одного свойства в списке строк дешевает вслед за обходом: `NotifyOneRowName` при 2496
  строках 950–1018 µs → 132–214 µs, аллокации 499 872 → 872 байта. `Move` первой строки в конец —
  1504/1608 µs при 860 200 байт (−42% байт); хвост P95 у этой операции остаётся 7 мс, отдельный
  проход его не утверждает.
- Пересборка всех строк не меняется как класс: `ResetAllRows` 29343/28890 µs против 27155/34246 —
  все узлы новые, кэшу нечего переиспользовать. `KeyedRebuildSameKeys` (те же id, новые записи
  источника) напротив стал дороже: 125/121 → 231/243 µs, байты те же 290 232. Причина — `rebind`
  трогает каждую строку, и каждая строка объявляет инвалидацию своего пути; 2496 частичных путей
  дороже одного сплошного обхода. Это кандидат на адресное снятие черновика при перепривязке.
- Постоянные затраты узла выросли ровно на размер кэша: +499 272 байта при 12 481 узле, то есть
  +40 байт на узел (17 591 080 → 18 090 352), время сборки 16 291/16 313 → 17 978–18 460 µs.
  Пополняет бюджет [UIA4.4](PLAN.md#uia4-view-layer-audit): четыре коллекции плюс теперь и поля
  валидности layout.
- Инвалидация как таковая не подорожала: `InvalidateRender` на глубине 2496 — 6 µs (было 5 µs),
  запис Properties — 24 байта без изменений.

Поведение закреплено `TrueMoon.Alloy.Tests/LayoutInvalidationTests.cs` (четыре теста: ноль обходов при
покое и при инвалидации корня, путь до корня при правке листа с нетронутой соседней строкой,
полная перемерка при смене размера вьюпорта, повторная раскладка контента при смене `Offset`).
Проверка правки: `TrueMoon.Alloy.Tests` — 1300 тестов без падений, `TrueMoon.Aether.Editor.Tests` —
1588 без падений, `TrueMoon.Alloy.Hosting.Tests` — 216 без падений, `--verify` стенда —
`VIEW_BASELINE_VERIFIED`.

<a id="uia431-viewport-virtualization-2026-11-27"></a>
## Списки: viewport-виртуализация окна строк UIA4.3.1 — 2026-11-27

Третья пара к [UIA4.2](#uia42-view-cost-2026-11-27), [UIA4.1](#uia41-collection-reconcile-2026-11-27) и
[UIA4.3](#uia43-addressable-layout-2026-11-27) на сцене `inspector-v1`: те же размеры, тот же протокол — 36 свежих
процессов, 252 результата, 200 сырых сэмплов, `--verify` перед замером, `NoOp`-пол 0 µs и 0 байт. Стенд расширен
группой `virtualized-binding` (семь сцен плюс метод-пол); `Harness.cs` не менялся (`5D2F9066…`), `Cases.cs`
`C6675FF6…`, `Probe.cs` `3794BF54…` — зонд пишет время старта процесса, потому что операционная система вправе
выдать тот же PID повторно и проверка «одна группа, размер и проход — отдельный процесс» по одному PID даёт
ложный сбой. Отпечатки прогона `TestResults/ViewBaseline/run-20261127-133309579`: `TrueMoon.Argentis.dll`
`8210D168…`, `TrueMoon.Alloy.dll` `03BA18C2…`, HEAD `3fcd200e`, рабочее дерево грязное.

Изменён движок: `Layout/VirtualizingStack.cs` меряет и раскладывает только строки окна плюс `Buffer` (4 с каждой
стороны по умолчанию) — вне окна детей нет вовсе; полный extent собирается из измеренных высот строк и среднего по
ним для нереализованных (`EstimatedRowHeight` = 24 до первой мерки). Привязка окна —
`Bindings/VirtualizingCollectionBinding.cs` и `Bindings/ItemsBindingConnection.cs`
(`ElementExtensions.BindVirtualizedItems`) поверх любого `IReadOnlyList` с явным `RefreshItems`: уведомление
источника помечает окно устаревшим, реализация и переиспользование пула (до 128 строк) происходят на мерке. В
`ScrollViewer` запрос `ScrollToEnd` для контента без окна фиксируется в `ArrangeCore` по фактическому вьюпорту,
для контейнера с окном окно пересобирается в `MeasureCore` по оценке offset (`Layout/ScrollViewer.cs:91-131`).

Медианы двух проходов; «без окна» — тот же список на `VStack` с ключевой привязкой внутри `ScrollViewer`
(2496 строк = 7 489 узлов, окно виртуализующего контейнера — 15 строк = 45 узлов):

| Строк | Операция | µs без окна | µs с окном | Байт без окна | Байт с окном |
|---|---|---:|---:|---:|---:|
| 2496 | Сборка сцены и два `Update` | 19 589 / 17 831 | 746 / 729 | 20 209 624 | 996 400 |
| 2496 | `Update` без изменений | 0 / 0 | 0 / 0 | 0 | 0 |
| 2496 | `RefreshItems` без изменений | 228 / 202 | 28 / 21 | 20 360 | 448 |
| 2496 | Вставка и удаление строки вне окна | 1 730 / 1 669 | 8 / 8 | 668 080 | 448 |
| 2496 | Шаг прокрутки (0 ↔ 400) | 401 / 391 | 56 / 54 | 199 792 | 106 856 |
| 312 | Сборка сцены и два `Update` | 3 514 / 3 623 | 878 / 1 058 | 2 498 672 | 725 584 |
| 312 | Вставка и удаление строки вне окна | 220 / 219 | 30 / 28 | 91 504 | 448 |
| 312 | Шаг прокрутки (0 ↔ 400) | 82 / 80 | 91 / 154 | 25 072 | 106 856 |
| 39 | Сборка сцены и два `Update` | 374 / 471 | 385 / 341 | 326 984 | 326 472 |
| 39 | Вставка и удаление строки | 37 / 37 | 53 / 53 | 14 744 | 14 368 |
| 39 | Шаг прокрутки (0 ↔ 400) | 1 / 0 | 1 / 1 | 0 | 0 |

- Стоимость списка перестаёт расти с длиной: вставка и удаление строки вне окна — 8 µs и 448 байта при 2496
  строках против 1 730/1 669 µs и 668 080 байт (−99,5 % по времени, −1487× по байтам); `RefreshItems` без
  изменений — 28/21 µs и 448 байта против 228/202 µs и 20 360 байт.
- Сборка сцены: 996 400 байт против 20 209 624 (−95 %), 746/729 µs против 19 589/17 831 (−96 %), потому что
  создаются 45 узлов вместо 7 489.
- Шаг прокрутки дешевает по времени в 7 раз (56/54 µs против 401/391 µs), но байты делятся лишь вдвое
  (106 856 против 199 792): в замере окна 0 и 400 не пересекаются, поэтому за шаг реализуются 15 новых строк.
  Поступательная прокрутка переиспользует пул — число созданных строк не растёт
  (`VirtualizingListTests.ScrollingMovesWindowAndRebindsPooledRows`).
- Виртуализация не окупается, когда список целиком помещается в окно: при 39 строках сборка та же
  (385/341 против 374/471 µs), а мутация источника дороже — 53 µs против 37 µs; при 312 строках шаг прокрутки
  тоже не дешевле (91/154 µs и 106 856 байт против 82/80 µs и 25 072 байт). Экрану с коротким списком
  `VirtualizingStack` не нужен.
- Постоянные затраты узла не изменились: `node-cost BuildScene` при 2496 строках остаётся 18 090 352 байта, как
  в [UIA4.3](#uia43-addressable-layout-2026-11-27) — виртуализация уменьшает число узлов, а не сам узел, поэтому
  [UIA4.4](PLAN.md#uia4-view-layer-audit) остаётся в силе.
- Попутно снят регресс прошлой правки `ScrollViewer`: `ScrollToEnd` потреблялся в `MeasureCore`, где высота
  вьюпорта известна только из ограничения мерки; когда раскладка даёт более узкий слот (мерка 174 против
  раскладки 154), `Offset` фиксировался на 2250 вместо 2270 — на строку раньше конца. Закреплено
  `LayoutTests.ScrollToEnd_UsesNewExtentAndPaddedViewportInOneUpdate` и
  `EditorDiagnosticsViewTests.CopyFollowAndReopen_UseRetainedModelAndDisposeOnlyOwnedControls`.

Поведение закреплено `TrueMoon.Alloy.Tests/VirtualizingListTests.cs` (десять тестов: окно и extent, прокрутка с
переиспользованием строк из пула, освобождение строк без `rebind`, мутация источника внутри и вне окна, контейнер
без `ScrollViewer`, `ScrollToEnd`, размер окна при `Buffer` 0, снятие фокуса с ушедшей строки). Проверка правки:
`TrueMoon.Alloy.Tests` — 1310 тестов без падений, `TrueMoon.Aether.Editor.Tests` — 1588,
`TrueMoon.Alloy.Hosting.Tests` — 216, `--verify` стенда — `VIEW_BASELINE_VERIFIED`.

<a id="uia43-next-candidates"></a>
Следующие кандидаты: [UIA4.4](PLAN.md#uia4-view-layer-audit) — постоянные 1394–1410 байта на узел плюс 40 байт
полей валидности; адресное снятие черновика при перепривязке (`KeyedRebuildSameKeys` 230/133 µs против 125 µs до
UIA4.3); переиспользование строки пула требует обновлять и `Bind`-привязки, объявленные на старый экземпляр
источника, — сейчас это обязанность `rebind`; перенос `VirtualizingStack` в динамические списки Editor (UIA4.1
закрыта ключевыми привязками, окон пока нет).


<a id="uia44-node-cost-2026-11-27"></a>
## Узел: стоимость состава и пакетной вставки UIA4.4 — 2026-11-27

Пятая пара к [UIA4.2](#uia42-view-cost-2026-11-27), [UIA4.1](#uia41-collection-reconcile-2026-11-27),
[UIA4.3](#uia43-addressable-layout-2026-11-27) и [UIA4.3.1](#uia431-viewport-virtualization-2026-11-27) на сцене
`inspector-v1`: те же размеры и протокол — 36 свежих процессов, 252 результата, 200 сырых сэмплов, два прохода,
`--verify` перед замером (`VIEW_BASELINE_VERIFIED`), `NoOp`-пол 0 µs и 0 байт. Стенд не менялся: `Cases.cs`
`C6675FF6…`, `Harness.cs` `5D2F9066…`, `Probe.cs` `3794BF54…`, `Program.cs` `36DFC750…`. Отпечатки прогона
`TestResults/ViewBaseline/run-20261127-145820626`: `TrueMoon.Argentis.dll` `19A4B920…`, `TrueMoon.Alloy.dll`
`74A865C9…`, HEAD `3fcd200e`, рабочее дерево грязное.

Изменены постоянные затраты узла и путь пакетной вставки:

- `Element` больше не аллоцирует четыре коллекции на каждый узел: первое локальное свойство живёт в inline-слоте
  `_localKey`/`_localValue`, а `_values`, `_subscriptions`, `_attachmentSubscriptions` и `_boundProperties`
  создаются только когда для них есть основание (`TrueMoon.Argentis/Element.cs:13-17`).
- пакетная вставка (`WithChildren` → `ElementCollection.AddRange`) не раскрывает детей в `[owner, .. children]`:
  блокировка — конструктор `TreeChange(Element owner, Element[] children)`, который читает массив вызывающего
  и выделяет один буфер пар «элемент/корень» (`Layout/ElementCollection.cs:55-80`, `TreeChange.cs:52-69`),
  проверка черновика — `ValidateChildChange(Element[] added, int addedCount, Element[]? removed, int
  removedCount)` без перечислителей (`Element.cs:416-435`); набор проверки дубликатов позже стал
  переиспользуемым (`ElementCollection.cs:62`, см. ниже)
- обходы проверки поддерева (`ValidateScopeThemes`, `ValidateUnattachedSubtree`) идут индексно
  (`Element.cs:403`, `:427`), а список отложенных инвалидаций заводится ёмкостью по размеру изменения
  (`TreeChange.cs:12`, `:107-112`).

Попутно сняты два дефекта, которые держало грязное рабочее дерево (правки предыдущего среза не были проверены
тестами) — это не оптимизации:

- `TreeChange` проверял reentrancy, уже заняв первую блокировку, и снимал общий корень дважды. `Add`/`Remove`
  элемента с родителем бросал «UI tree changes cannot be reentrant» внутри конструктора изменения, а неудачный
  конструктор не вызывает `Dispose` — блокировка контейнера утекала; накопленный дисбаланс глубины при этом
  глушил сам замок, и часть проверок переставала срабатывать. Теперь элементы изменения проверяются до первой
  блокировки (`TreeChange.cs:19`, `:31`, `:48-49`), `Lock` возвращает корень только когда блокировал его
  отдельно, а `Dispose` снимает ровно занятые слоты (`TreeChange.cs:65-78`, `:130-137`). Закреплено
  `TreeAndPropertyTests.RepeatedChildEdits_KeepBlockingReentrantMutationFromNotifications`.
- `ApplyThemeCore` перестал быть двухфазным: исключение из фабрики значения стиля посреди обхода оставляло
  поддереву половину нового `Theme`. Разрешение стилей снова собирается в переиспользуемый `[ThreadStatic]`
  буфер и коммитится только после успешного обхода (`Element.cs:550-575`); контракт держит
  `ViewStyleTests.FailedThemeChange_KeepsPreviousThemeAndEffectiveValuesAcrossTheTree`.

Медианы двух проходов; 2496 строк сцены = 12 481 узел, 312 = 1 560, 39 = 195:

| Узлов | Операция | µs до | µs после | Байт до | Байт после |
|---:|---|---:|---:|---:|---:|
| 12 481 | `BuildScene` | 17 039 / 18 300 | 3 922 / 2 506 | 18 090 352 | 6 868 064 |
| 1 560 | `BuildScene` | 1 872 / 1 679 | 1 253 / 1 095 | 2 243 752 | 840 728 |
| 195 | `BuildScene` | 237 / 226 | 231 / 148 | 281 064 | 105 448 |
| 12 481 | `BuildAndDisposeScene` | 9 545 / 8 816 | 5 595 / 2 690 | 23 941 520 | 11 800 384 |
| 12 481 | `LeafEditThenLayout` | 122 / 119 | 106 / 100 | 400 | 400 |
| 12 481 | `MeasureScene`, `ArrangeScene` | 0 / 0 | 0 / 0 | 0 | 0 |
| 2496 строк | `ResetAllRows` (ссылочная привязка) | 34 301 / 28 305 | 25 786 / 20 657 | 26 497 720 | 19 406 008 |
| 2496 строк | `KeyedRebuildSameKeys` | 230 / 133 | 112 / 106 | 290 232 | 290 232 |
| 2496 строк | `KeyedRefreshUnchanged` | 228 / 202 | 240 / 339 | 20 360 | 20 360 |
| 312 строк | `KeyedMoveFirstRowToEnd` | 488 / 486 | 867 / 439 | 108 832 | 108 568 |
| 39 строк | `KeyedMoveFirstRowToEnd` | 65 / 65 | 100 / 83 | 14 920 | 14 656 |
| 45 узлов окна | сборка виртуализованного списка 2496 строк | 746 / 729 | 723 / 800 | 996 400 | 788 640 |
| 7 489 узлов | тот же список без окна | 19 589 / 17 831 | 15 316 / 14 088 | 20 209 624 | 14 320 680 |
| 2496 строк | шаг прокрутки окна | 56 / 54 | 49 / 48 | 106 856 | 87 376 |
| 512 привязки | `CreateBindings` | 1 483 / 1 796 | 1 456 / 1 572 | 2 149 816 | 1 697 928 |
| 512 привязки | `AttachAndFirstSync` | 3 057 / 3 061 | 2 366 / 2 596 | 2 668 312 | 2 220 112 |

- Постоянная стоимость узла: 1449 байт → 550 байт (6 868 064 / 12 481); при 39 строках — 1441 → 541 байт, при
  312 — 1438 → 539. Сборка 12 481 узла дешевеет по времени в 4–7 раз (3 922/2 506 µs против
  17 039/18 300 µs), освобождение сцены — 11 800 384 байта вместо 23 941 520.
- Служебная стоимость пакетной вставки выпрямлена: кривая `WithChildren`, снятая временным зондом
  `GC.GetAllocatedBytesForCurrentThread` на листе в 328 байт — 1 120 байта при N=1, 2 272 при N=4, 3 896 при
  N=8 и 7 120 при N=16, то есть 54–112 байта служебного на ребёнка поверх самого листа; 16 отдельных `Add` той
  же строки стоят 9 568 байт против 7 120 при пакетной вставке. Зонд удалён после использования, в стенд
  кривая не входила.
- Байты снижаются и на списках, и на привязках: `ResetAllRows` −27 %, сборка списка без окна −29 %,
  `AttachAndFirstSync` −17 %, `CreateBindings` −21 %, шаг прокрутки окна −18 % (106 856 → 87 376 байта).
- Кэш валидности [UIA4.3](#uia43-addressable-layout-2026-11-27) держится: `MeasureScene` и `ArrangeScene` —
  0 µs и 0 байт, правка одного листа — 106/100 µs и 400 байт.
- Не подтверждены парой `KeyedRefreshUnchanged` (240/339 µs против 228/202, байты те же) и
  `KeyedMoveFirstRowToEnd` при 39 строках (100/83 против 65/65): разброс между проходами больше эффекта
  правки (при 312 — 867 и 439 µs), регрессия не доказана и требует повторного замера. По виртуализованной
  сборке байты −21 %, время того же прогона — в пределах разброса (723 и 800 против 746 и 729).

Проверка: `TrueMoon.Alloy.Tests` — 1311 тестов без падений (в том числе новый
`TreeAndPropertyTests.RepeatedChildEdits_KeepBlockingReentrantMutationFromNotifications`),
`TrueMoon.Aether.Editor.Tests` — 1588, `TrueMoon.Alloy.Hosting.Tests` — 216; `--verify` стенда —
`VIEW_BASELINE_VERIFIED`. Сборка `TrueMoon.Argentis` Release без инкрементации — 284 предупреждения
(преимущественно CS1591) и 0 ошибок; инкрементная пересборка ничего не компилирует и печатает ноль, поэтому
запись про «0 предупреждений» относилась к инкрементному прогону и не описывает дерево.

<a id="uia44-next-candidates"></a>
Следующие кандидаты: остаток [UIA4.4](PLAN.md#uia4-view-layer-audit) — `Get<T>` при промахе (две стилевые
цепочки и примерно 14 `ReferenceEquals` с виртуальными `Theme*`, `Element.cs:212-237`); объект `TreeChange`
на каждую очистку узла (104 байта) и делегат `Action` на каждого ребёнка в `Dispose` (56 байт) — весь остаток
освобождения сцены, см. [замер ниже](#uia44-detach-and-store-2026-11-27); адресный сброс черновика при
перепривязке (`KeyedRebuildSameKeys` по-прежнему 290 232 байта на 2496 строк); повторный замер
`MoveFirstRowToEnd` (312), `NotifyOneRowName` (39), `LeafEditThenLayout` (12 481), `KeyedRebuildSameKeys` (312)
и `KeyedReplaceRowInstance` (312), где время выросло при неизменных байтах и разбросе проходов больше эффекта.

<a id="uia44-detach-and-store-2026-11-27"></a>
## Узел: отсоединение, очистка и хранилище свойств UIA4.4 — 2026-11-27

Шестая пара к [UIA4.2](#uia42-view-cost-2026-11-27), [UIA4.1](#uia41-collection-reconcile-2026-11-27),
[UIA4.3](#uia43-addressable-layout-2026-11-27), [UIA4.3.1](#uia431-viewport-virtualization-2026-11-27) и
[первой части UIA4.4](#uia44-node-cost-2026-11-27). База «до» — `TestResults/ViewBaseline/run-20261127-153454561`
(дерево сразу после первой части UIA4.4), «после» — `run-20261127-155252955`. Протокол тот же: 42 свежих процесса,
276 результатов, 200 сырых сэмплов, два прохода, `--verify` перед замером (`VIEW_BASELINE_VERIFIED`), `NoOp`-пол
0 µs и 0 байт. Отпечатки «до»: `TrueMoon.Argentis.dll` `025ECC17…`, `TrueMoon.Alloy.dll` `2C15A813…`, `Cases.cs`
`0C02CA25…`, `WorkloadVerification.cs` `D382C6BF…`; «после»: `TrueMoon.Argentis.dll` `DB6B309B…`,
`TrueMoon.Alloy.dll` `995A69BB…`, `Cases.cs` `7828841E…`, `WorkloadVerification.cs` `3E1746C3…`. `Harness.cs`
`5D2F9066…` и `Probe.cs` `3794BF54…` в обоих прогонах совпадают, сцена `inspector-v1`; HEAD `aa281cd`, рабочее
дерево грязное. Стенд дополнен третьим случаем хранения и проверкой жизненного цикла — сравнение сцены держит
`WorkloadVerification`, а не хэш `Cases.cs`.

Изменено:

- `Element.DetachCore` больше не собирает поддерево LINQ-ом (`Subtree().Where(…).ToArray()`): обход индексный
  в переиспользуемый `[ThreadStatic]`-буфер `_subtreeScratch`, диапазон освобождается после обхода
  (`TrueMoon.Argentis/Element.cs:494-504`, `:539-578`). Порядок контракта не менялся — колбэк хоста, отмена
  ввода, уведомление об отсоединении, от самых глубоких узлов к корню; его держит
  `TreeAndPropertyTests.DetachAndDispose_ReportHostCallbackThenInputCancelThenAttachmentOrder`.
- `TreeChange` переиспользует буфер отложенных инвалидаций и очередь уведомлений: `[ThreadStatic]`-слоты
  `_pooledInvalidations` и `_pooledNotifications` возвращаются только опустошёнными и с ограниченной ёмкостью,
  иначе незакрытое изменение отдало бы чужие уведомления (`TreeChange.cs:19-21`, `:98-112`, `:146-158`).
- база `PropertiesBase` больше не заводит `Dictionary<string, IProperty>` на каждый `Element` — поле стало
  отложенным (`PropertiesBase.cs:5`).
- локальное хранилище значений держит два значения в inline-слотах `_localKey`/`_localValue` и
  `_localKey2`/`_localValue2`, словарь появляется на третьем; снятие первого сжимает второй слот, чтение —
  три `ReferenceEquals` и словарь (`Element.cs:13`, `:212-237`, `:249-256`, `:257-277`).
- стенд: случай `StoreTwoProperties` отделяет цену второго значения от появления словаря, а `--verify` проверяет
  ещё и жизненный цикл узла (создание → attach → detach → dispose) (`Cases.cs:264-308`,
  `WorkloadVerification.cs`).

Медианы двух проходов; 2496 строк сцены = 12 481 узел, 312 = 1 560, 39 = 195; случаи `property-store` считают
отсоединённые узлы без сцены:

| Узлов | Операция | µs до | µs после | Байт до | Байт после |
|---:|---|---:|---:|---:|---:|
| 12 481 | `BuildScene` | 2 594 / 2 514 | 2 346 / 2 205 | 6 868 064 | 5 849 712 |
| 1 560 | `BuildScene` | 1 145 / 1 251 | 1 237 / 1 268 | 840 728 | 713 448 |
| 195 | `BuildScene` | 229 / 224 | 221 / 213 | 105 448 | 89 552 |
| 12 481 | `BuildAndDisposeScene` | 6 330 / 2 715 | 3 873 / 3 476 | 11 800 384 | 7 846 616 |
| 1 560 | `BuildAndDisposeScene` | 477 / 430 | 200 / 319 | 1 457 464 | 963 152 |
| 195 | `BuildAndDisposeScene` | 159 / 159 | 80 / 95 | 182 736 | 120 856 |
| 2 496 | `StoreOneProperty` | 1 144 / 717 | 880 / 912 | 1 437 696 | 1 178 112 |
| 2 496 | `StoreTwoProperties` (новый) | — | 147 / 145 | — | 1 238 016 |
| 2 496 | `StoreThreeProperties` | 478 / 187 | 321 / 322 | 2 096 640 | 1 837 056 |
| 2 496 | `ResetAllRows` (ссылочная привязка) | 38 238 / 18 007 | 34 177 / 36 966 | 19 406 008 | 16 238 864 |
| 2496 строк | шаг прокрутки окна | 69 / 46 | 68 / 44 | 87 376 | 66 088 |
| 312 строк | шаг прокрутки окна | 359 / 67 | 115 / 80 | 89 928 | 66 088 |
| 45 узлов окна | сборка виртуализованного списка 2496 строк | 1 288 / 765 | 1 118 / 717 | 788 640 | 738 656 |
| 7 489 узлов | тот же список без окна | 25 700 / 14 159 | 24 130 / 13 272 | 14 320 680 | 13 440 968 |
| 39 строк | `AppendRemoveRow` | 51 / 34 | 46 / 48 | 13 912 | 12 656 |
| 512 привязки | `CreateBindings` | 2 436 / 1 424 | 2 105 / 1 332 | 1 697 928 | 1 550 080 |
| 512 привязки | `AttachAndFirstSync` | 3 579 / 2 245 | 3 651 / 2 218 | 2 220 112 | 1 960 768 |
| 12 481 | `MeasureScene`, `ArrangeScene` | 0 / 0 | 0 / 0 | 0 | 0 |
| 12 481 | `LeafEditThenLayout` | 163 / 100 | 184 / 180 | 400 | 400 |
| 2 496 | `GetLocalValue`, `GetThemeValue`, `GetUnknownDefault` | 0 / 0 | 0 / 0 | 0 | 0 |
| 2 496 | `SetChangedValue`, `SetSameValue` | 6 / 5 | 6 / 6 | 24 | 24 |

- Постоянная стоимость узла: 550 → 469 байт (5 849 712 / 12 481); при 312 строках 539 → 457, при 39 — 541 → 459.
  Сборка 12 481 узла — 2 346/2 205 µs вместо 2 594/2 514 µs.
- Отсоединение и очистка: разница `BuildAndDisposeScene` − `BuildScene` = 1 996 904 байта, то есть 160 байт на
  узел вместо 4 932 320 (395 байт) — −59 %; время освобождения сцены 3 873/3 476 µs вместо 6 330/2 715 µs.
- Хранилище свойств: узел с одним локальным значением — 472 байта вместо 576; второе значение добавляет ровно
  24 байта (496 против 472 — только коробка значения, оба слота уже в узле), третье — 240 байт (736 против
  496), это появление словаря; второе и третье вместе дают 264 байта, как и в базе (840 против 576), то есть
  цена самого словаря не менялась, экономия легла на inline-слоты.
  Время случая с одним значением 880/912 µs вместо 1 144/717 µs, с тремя — 321/322 вместо 478/187.
- Списки и привязки дешевеют по байтам: `ResetAllRows` −16 %, шаг прокрутки окна −24 %, сборка виртуализованного
  списка −6 %, без окна −6 %, `AttachAndFirstSync` −12 %, `CreateBindings` −9 %, `AppendRemoveRow` при 39 строках
  −9 %. Кэш валидности [UIA4.3](#uia43-addressable-layout-2026-11-27) держится: `MeasureScene`/`ArrangeScene` —
  0 µs и 0 байт, `LeafEditThenLayout` — те же 400 байт.
- Остаток освобождения разложен временным зондом `GC.GetAllocatedBytesForCurrentThread` по зонам
  `Dispose`/`DetachCore` на сцене 12 481 узел: объект `TreeChange` — 1 298 024 байта (104 байта на узел),
  делегат `Action` на каждого ребёнка в цикле `Dispose` — 705 312 байта (56 байт на ребёнка), а `DetachCollect`,
  `DetachHostCallbacks`, `DetachNotifications`, `Flush`, `Subscriptions`, `Finally` и `Tail` — 0 байт. Зонд
  удалён после использования, в стенд не входил; он же искажал промежуточный прогон `property-store`
  (1 132 байта на узел вместо 472), поэтому пара снята после удаления зонда.
- Не подтверждены как ухудшение: `MoveFirstRowToEnd` при 312 (765/634 → 903/830 µs), `NotifyOneRowName` при 39
  (11/16 → 20/22 µs), `LeafEditThenLayout` при 12 481 (163/100 → 184/180 µs), `KeyedRebuildSameKeys` при 312
  (43/61 → 56/69 µs), `KeyedReplaceRowInstance` при 312 (30/119 → 54/175 µs). Байты в этих случаях не изменились
  (108 552, 872, 400, 28 152 и 3 392), а разброс между проходами в самой базе («до» 765 против 634, 30 против
  119) больше эффекта правки — нужен повторный замер.
- Порог по времени освобождения проверен повтором на финальной сборке: четыре свежих процесса группы
  `node-cost` при 2496 строках дают `BuildAndDisposeScene` 1 853 / 1 896 / 1 904 / 1 936 µs при тех же
  7 846 616 байтах — ниже обеих медиан пары (3 873 и 3 476 µs) и ниже порога отката 2 690 µs. Разброс самой
  базы (6 330 против 2 715 µs) больше любых различий между проходами, шаг с пулом буферов не откачен.
- Пункт `Get<T>` при промахе закрыт числом, код не менялся: `GetLocalValue`, `GetThemeValue` и
  `GetUnknownDefault` на цепочке 2 496 узлов — 0 µs и 0 байт в обоих прогонах, включая промах по стилю и
  теме (`Element.cs:212-237`) — оптимизировать в этой цепочке нечего.

Проверка: `TrueMoon.Alloy.Tests` — 1313 тестов без падений (в том числе
`TreeAndPropertyTests.DetachAndDispose_ReportHostCallbackThenInputCancelThenAttachmentOrder` и
`NestedDisposeDuringNotification_KeepsOuterNotificationsQueued`), `TrueMoon.Aether.Editor.Tests` — 1588,
`TrueMoon.Alloy.Hosting.Tests` — 216; `--verify` стенда — `VIEW_BASELINE_VERIFIED` вместе с новой проверкой
жизненного цикла узла. Сборка `TrueMoon.Argentis` Release без инкрементации — 284 предупреждения
(преимущественно CS1591) и 0 ошибок; инкрементная сборка молчит, поэтому счётчик предупреждений имеет смысл
только с `--no-incremental`.

<a id="uia45-conditional-slot-2026-11-27"></a>
## Условный контент: ручная замена ветви против слота UIA4.5 — 2026-11-27

Восьмая запись к [UIA4.2](#uia42-view-cost-2026-11-27), [UIA4.1](#uia41-collection-reconcile-2026-11-27),
[UIA4.3](#uia43-addressable-layout-2026-11-27), [UIA4.3.1](#uia431-viewport-virtualization-2026-11-27) и обеим
частям [UIA4.4](#uia44-node-cost-2026-11-27). Это не пара прогонов: «до» и «после» — разные случаи одной группы
`conditional-slot`, обе ветки сняты в одних и тех же процессах. Протокол тот же: 10 свежих процессов,
350 результатов, 200 сырых сэмплов, `--verify` перед замером (`VIEW_BASELINE_VERIFIED`), `NoOp`-пол 0 µs и
0 байт; два прохода при 39 строках и по четыре при 312 и 2496. Отпечатки: `TrueMoon.Argentis.dll` `07FA75D8…`,
`TrueMoon.Alloy.dll` `3A7DFE3E…`, `Cases.cs` `E6F7B9B9…`, `WorkloadVerification.cs` `85F762FA…`,
`Harness.cs` `5D2F9066…` и `Probe.cs` `3794BF54…` (совпадают с предыдущими прогонами), сцена `inspector-v1`,
поставка `1.2611.27.5-alpha`, HEAD `badf282`, рабочее дерево грязное.

Изменено:

- `TrueMoon.Argentis/Layout/ConditionalContent.cs` — общий условный слот одной ветки: typed `State` плюс
  `StateProperty` для привязки, фабрика `TState → Element?`; равный состоянию запрос не пересобирает ветвь,
  `null` снимает её, освобождение снятой ветви принадлежит слоту, пересборка стоит в очереди диспетчера
  текущего attach и отбрасывается при отсоединении (`ConditionalContent.cs:13-102`).
- приложение: поповер выбора меша, материала и материала слота в инспекторе больше не собирает `VStack`
  строк в `ToggleRenderChoices` и не освобождает его в обработчике нажатия — строки живут в
  `ConditionalContent<RenderChoicesState?>` внутри хоста строки свойства, состояние несёт компонент, ключ поля,
  слот и версию снимка ассетов, которую ведёт `SynchronizeRenderAssets`
  (`Editing/EditorInspectorView.cs:555-625`, `Editing/InspectorPropertyEditorFactory.cs:13-79`).
- образец: `ManualTests/AlloyTest/LoadStatusRegion.cs` удалён, область состояний собрана тем же слотом
  (`ManualTests/AlloyTest/View1.cs:15,52-59`), проверки перенесены в
  `TrueMoon.Alloy.Tests/ConditionalContentTests.cs` (26 методов, 33 случая).
- стенд: группа `conditional-slot` из четырёх случаев и проверка жизненного цикла слота в `--verify`
  (`Cases.cs:113-214`, `WorkloadVerification.cs:196-218`, `RunBaseline.ps1:31,33`).

Медианы сырых сэмплов; размер — число строк внутри ветки; байты во всех процессах одного случая совпали
точно, время дано диапазоном по проходам:

| Строк в ветке | Случай | µs | Байт |
|---:|---|---|---:|
| 39 | `ManualSetContentAndDispose` («до») | 77–93 | 39 152 |
| 39 | `SlotNewState` | 100–101 | 39 584 |
| 39 | `SlotSameState` (равный запрос) | 0–0,1 | 24 |
| 39 | `SlotCloseAndReopen` | 59–64 | 40 472 |
| 312 | `ManualSetContentAndDispose` («до») | 565–675 | 301 584 |
| 312 | `SlotNewState` | 452–702 | 302 016 |
| 312 | `SlotSameState` (равный запрос) | 0–0,1 | 24 |
| 312 | `SlotCloseAndReopen` | 84–453 | 303 624 |
| 2496 | `ManualSetContentAndDispose` («до») | 1 299–3 310 | 2 524 608 |
| 2496 | `SlotNewState` | 706–757 | 2 604 912 |
| 2496 | `SlotSameState` (равный запрос) | 0–0,1 | 24 |
| 2496 | `SlotCloseAndReopen` | 706–713 | 2 605 800 |

- Пересборка новой ветки через слот стоит столько же, сколько ручная замена: +432 байта при 39 и 312 строках
  и +80 304 байта (32 байта на строку) при 2496 — слой слота глубже на один узел внутри `ScrollViewer`,
  поэтому layout и инвалидация считают чуть больше. Время в пределах разброса: ручная замена при 2496 дала
  1 299–3 310 µs (разброс базы больше эффекта), слот — 706–757 µs.
- Срезается именно повторный запрос к слоту: `SlotSameState` — 0 µs и 24 байта (одна коробка запроса) на
  любом размере вместо 39 152 / 301 584 / 2 524 608 байт и 77–3 310 µs. Это стоимость примитива: в поповере
  инспектора повторное нажатие на то же поле закрывает список и сбрасывает состояние
  (`Editing/EditorInspectorView.cs:567-569`), как и любое изменение снимка ассетов, поэтому равный запрос
  текущей проводкой не достигается. Выигрыш приложения в ином: снятая ветвь освобождается слотом, `Dispose`
  уходит из обработчика нажатия, состояние списка выражено одним значением.
- Снятие и повторное открытие (`SlotCloseAndReopen`) равны пересборке: 40 472 / 303 624 / 2 605 800 байт.
  Освобождение прежней ветви входит в слот, вызывающий код не вызывает `Dispose` внутри обработчика нажатия.
- Не подтверждены как ухудшение: `SlotCloseAndReopen` при 312 (84 против 453 µs между проходами) и
  `SlotNewState` при 312 (452–702 µs) — разброс процессов больше различий; байты в этих случаях стабильны.
- Следующие кандидаты: `EditorView.cs:285-289` (ручная замена блока свойств, слоту нужен токен
  идентичности набора), несколько именованных секций в одном условном слоте, условные части генератора
  ([UIA4.7](PLAN.md#uia4-view-layer-audit)) и +32 байта на строку ветки против ручной замены.

Проверка: `TrueMoon.Alloy.Tests` — 1311 тестов без падений (фильтр `ConditionalContent` — 33 случая, в том
числе `UnchangedState_KeepsBranchAndSkipsFactory`, `ClearingState_RemovesBranchAndDisposesIt`,
`ReattachWithEqualState_PreservesOldChildAndRestoresOneModelSubscription` и
`AcceptedReplacement_DisposesOldSubtreeSubscriptionsAndInputExactlyOnce`), `TrueMoon.Aether.Editor.Tests` —
1612 без падений, плюс отдельный зелёный прогон фильтра `Inspector` — 246 без падений на том же собранном
дереве. До правки последнего утверждения в тесте проводки тот фильтр давал 245/246; зелёный прогон фильтра
снят уже после правки, полного прогона на старом утверждении не было. Сборка
`TrueMoon.Aether.Editor.UI` Release — 0 ошибок; стенд `--verify` — `VIEW_BASELINE_VERIFIED` вместе с новой
проверкой слота.
Проводка поповера на экране: `TrueMoon.Aether.Editor.Tests/Inspector/InspectorRenderChoicesWiringTests.cs` —
открытие по списку ассетов, закрытие тем же полем со снятием и освобождением строк, пересборка после
settled-закрытия, закрытие на новом снимке ассетов и выключенная строка выбора, которая остаётся в списке,
но не получает ввод и ничего не готовит.

<a id="uia44-change-pool-and-dispose-2026-11-28"></a>
## Узел: объект структурного изменения и освобождение subtree UIA4.4 — 2026-11-28

Третья часть [UIA4.4](PLAN.md#uia4-view-layer-audit) закрывает остаток освобождения из
[предыдущей пары](#uia44-detach-and-store-2026-11-27) — объект `TreeChange` (104 байта на узел) и делегат
`Action` на каждого ребёнка (56 байт). Это не пара `run-*` прогонов: оба замера сняты группой `node-cost`
на одном дереве стенда — шесть свежих процессов (два прохода × размеры 39 / 312 / 2496), 200 сырых
сэмплов, `NoOp`-пол 0 µs и 0 байт; сырые прогоны — `TestResults/NodeCostPool-20261128/`
(`before-size{39,312,2496}-pass{a,b}.json`, `after-size{39,312,2496}-pass{c,d}.json`). Отпечатки «до»:
`TrueMoon.Argentis.dll` `7543B6FD…`, HEAD `4829BD1`;
«после»: `TrueMoon.Argentis.dll` `4C4900BE…`. Код `Alloy` в срезе не менялся, `Cases.cs` `E6F7B9B9…`,
`WorkloadVerification.cs` `85F762FA…`, `Harness.cs` `5D2F9066…` и `Probe.cs` `3794BF54…` в обоих замерах
совпадают; сцена `inspector-v1`.

| случай | размер | байты «до» | байты «после» | µs «до» | µs «после» |
| --- | --- | --- | --- | --- | --- |
| `BuildAndDisposeScene` | 39 | 120 856 | 81 440 | 23 / 22 | 23 / 23 |
| `BuildAndDisposeScene` | 312 | 963 152 | 648 552 | 176 / 177 | 178 / 177 |
| `BuildAndDisposeScene` | 2496 | 7 846 616 | 5 330 544 | 1532 / 1529 | 1477 / 1501 |
| `BuildScene` | 39 | 89 552 | 81 440 | 143 / 149 | 146 / 158 |
| `BuildScene` | 312 | 713 448 | 648 552 | 1093 / 1141 | 1111 / 1116 |
| `BuildScene` | 2496 | 5 849 712 | 5 330 544 | 1201 / 2215 | 1213 / 1201 |

Изменено:

- `TreeChange` больше не создаётся конструктором: три перегрузки `Acquire` берут объект из
  `[ThreadStatic]`-стека и заполняют слоты блокировки (`TrueMoon.Argentis/TreeChange.cs:31-84`), пустые
  слоты и буферы готовит `Rent` (`:86-105`), слоты пула — `:22-27` с потолком 16 записи, то есть по глубине
  вложенности, а не по размеру subtree. В пул возвращаются только разблокированные и пустые изменения,
  флаг `_rented` не даёт вернуть одно изменение дважды (`:189-214`). Переиспользование буферов
  уведомлений и инвалидаций осталось прежним.
- Переведены все 14 точек создания: `Element.cs:477`, `:535`, `:584`, `:708`,
  `Layout/ContentControl.cs:22`, `Layout/ElementCollection.cs:72`, `:107`, `:209-210`, `:226`, `:239`,
  `Views/View.cs:69`, `:79`, `Windows/WindowFrame.cs:62`.
- Освобождение ребёнка идёт без делегата: `change.RunDispose(child)` вместо `change.Run(child.Dispose)`
  (`TreeChange.cs:128-133`, `Element.cs:721`). Ошибки колбэков по-прежнему собираются в `_errors` и
  всплывают одной `AggregateException` после продолжения очистки — контракт держит
  `OwnershipTests.Dispose_ThrowingSubscriptions_StillReleasesSiblingsSurfaceAndRegistryExactlyOnce`.

Числа:

- Освобождение subtree больше не аллоцирует ничего: разница `BuildAndDisposeScene` минус `BuildScene`
  была 31 304 / 249 704 / 1 996 904 байта (160 байт на узел), стала 0 байт при всех трёх размерах —
  байты случая совпали с `BuildScene` точно. Прежний результат `7 846 616` воспроизведён в замере «до».
- Сборка подешевела на тот же механизм: `BuildScene` −8 112 / −64 896 / −519 168 байта, то есть
  41 байт на узел; постоянные затраты узла 469 → 427 байта (5 330 544 на 12 481 узел).
- Время не ухудшено: `BuildAndDisposeScene` 1477 / 1501 µs вместо 1532 / 1529 при 2496 строках;
  `BuildScene` 1213 / 1201 вместо 1201 / 2215, где разброс проходов «до» больше эффекта.

Проверка: отдельный прогон `--verify` (единственный аргумент, `Benchmarks/TrueMoon.Argentis.Benchmarks/Program.cs:3`)
на собранном дереве среза — `VIEW_BASELINE_VERIFIED`, отпечаток `TrueMoon.Argentis.dll` `4C4900BE…` тот же,
что в замере «после». `TrueMoon.Alloy.Tests` — 1311 без падений, `TrueMoon.Alloy.Hosting.Tests` — 216,
`TrueMoon.Aether.Editor.Tests` — 1612; сборка Release `TrueMoon.Argentis` и стенда — 0 ошибок.

Следующие кандидаты: остаток UIA4.4 по составу — `ElementScope` на каждый View (`Views/View.cs:17`) и
обязательный слой `ContentControl`; адресный сброс черновика при перепривязке (`KeyedRebuildSameKeys`
по-прежнему 290 232 байта на 2496 строк); повторный замер случаев с разбросом проходов больше эффекта
(`MoveFirstRowToEnd` 312, `NotifyOneRowName` 39, `LeafEditThenLayout` 12 481).

<a id="uia45-property-token-slot-2026-11-28"></a>
## Условный контент: токен набора свойств вместо ручной замены UIA4.5 — 2026-11-28

Остаток [UIA4.5](PLAN.md#uia4-view-layer-audit): legacy-блок свойств Editor менялся вручную — собрать
`VStack`, `SetContent`, `old?.Dispose()`. Теперь панель держит `ConditionalContent<PropertySetToken?>`
(`TrueMoon.Aether.Editor.UI/EditorView.cs:185-186`, `:281-321`), а модель отдаёт токен из двух массивов
ключей и значений: равный по содержимому токен оставляет установленные строки, изменённый пересобирает
блок. Замер — группа `conditional-slot`, шесть свежих процессов (два прохода × 39 / 312 / 2496 строк
блока), 200 сырых сэмплов, `--verify` → `VIEW_BASELINE_VERIFIED`; артефакты
`TestResults/ConditionalSlotToken-20261128/`. Отпечатки: `TrueMoon.Aether.Editor.UI.dll` `7E5DE6A9…`,
`Cases.cs` `CCA9A33E…`, `WorkloadVerification.cs` `BB406E4E…`, `Harness.cs` `5D2F9066…`,
`Probe.cs` `3794BF54…`; HEAD `4d6192f`. Случаи сравнения (`ManualSetContentAndDispose`, `SlotNewState`,
`SlotSameState`) сняты в тех же процессах, отдельного прогона «до» нет.

| строк блока | случай | байт | µs |
| --- | --- | --- | --- |
| 2496 | `ManualSetContentAndDispose` (ручная замена) | 1 865 456 | 2193 / 2240 |
| 2496 | `SlotFreshEqualToken` (уведомление с тем же набором) | 40 016 | 3,1 / 4,3 |
| 2496 | `SlotNewState` (новый набор, полная пересборка) | 1 945 760 | 864 / 1214 |
| 312 | `ManualSetContentAndDispose` | 219 008 | 759 / 808 |
| 312 | `SlotFreshEqualToken` | 5 072 | 0,5 / 0,5 |
| 39 | `ManualSetContentAndDispose` | 28 648 | 115 / 117 |
| 39 | `SlotFreshEqualToken` | 704 | 0,5 / 1,4 |

Числа:

- Уведомление модели без изменения набора: 40 016 байт и 3,1–4,3 µs вместо 1 865 456 байт и 2193–2240 µs
  при 2496 строках — 16 байт на строку, две копии указателей; контролы и строки не создаются. Это цена
  сравнения по содержимому: `SlotSameState` с равным состоянием-ссылкой стоит 24 байта.
- Ручная база случая подешевела после [пула `TreeChange`](#uia44-change-pool-and-dispose-2026-11-28):
  2 524 608 → 1 865 456 байта при 2496 строках против чисел [UIA4.5](#uia45-conditional-slot-2026-11-27).
- Полная пересборка через слот по-прежнему дороже ручной замены (1 945 760 против 1 865 456 байта) —
  прежний вывод UIA4.5 подтверждён на этом дереве.
- Равенство содержимого обеспечивает сам токен: `ImmutableArray<T>` сравнивается по ссылке, поэтому у
  `PropertySetToken` переопределены `Equals` и `GetHashCode` (`EditorView.cs:296-310`). Вариант на
  synthesized-равенстве записи ветку не переиспользовал — это ловит новая проверка стенда
  `fresh equal token keeps the block` (`WorkloadVerification.cs:217-237`).

Проверка: `TrueMoon.Alloy.Tests` — 1311 и `TrueMoon.Alloy.Hosting.Tests` — 216 без падений;
`TrueMoon.Aether.Editor.Tests` — 1618 тестов, 1616 пройдено: падают оба варианта
`AetherEditorCompositionTests.ScenePlayback_DefaultCompositionEnablesRunForUnsavedDocumentWithoutCandidate`
(провайдеры DI), при этом тот же фильтр изолированно проходит — падение зависит от полного прогона. Причина
не установлена: сравнение с чистым HEAD не выполнено, сборка checkout на другом томе падает на копировании
native-библиотек (MSB3021/MSB3027).

Контракт переиспользования блока в экране держит
`DocumentShellTests.ShellRefresh_UpdatesOnlyAuthoringPartsKeepsHeaderAndSelectionAndBlocksLegacyCommands`:
`panel.Child` — слот, равный по содержимому набор сохраняет экземпляр ветки, изменённый набор заменяет её и
освобождает прежнюю.

Следующие кандидаты: остаток UIA4.4 по составу (`ElementScope` на каждый View и обязательный слой
`ContentControl`); если модель начнёт публиковать версию набора свойств, токен можно строить из неё, а не
из `_model.Properties` на каждое уведомление.
