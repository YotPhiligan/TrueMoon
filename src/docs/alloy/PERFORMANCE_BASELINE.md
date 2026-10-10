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
