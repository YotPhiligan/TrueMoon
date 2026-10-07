# Skia/Vulkan: проверенный прототип и ограничение внешнего обмена

Дата исходной проверки: 2026-09-27. Дополнено 2026-10-02 прототипом bindings. Относится к пунктам **1.1–1.6** [плана](PLAN.md).

## Текущий OpenGL шаг, 2026-10-04

OpenGL реализован через общие UiSession/IRenderBackend/IWindowHost, SkiaOpenGLRenderBackend и borrowed OpenGLUiTarget. UseSkiaOpenGL поддерживает external host и standalone Silk. GPU frame композитится в host-owned RGBA8 framebuffer; host обеспечивает live current context и заново binds GL state. Closing освобождает Skia до уничтожения GL context; callback errors переходят в Completion/StopAsync после закрытия. Native DLL/ABI не менялись.

Debug/Release GL App/GPU3/standalone30/error propagation прошли. Unchanged view на raster/OpenGL/Vulkan:3scales, matching layout/input/selected pixels, Vulkan validation0/0. Debug raw HUD90/90/12/2/3/6 и standalone Vulkan30/2 повторены; xUnit53/53, full solution build0errors/1341warnings. Full solution tests/длительный retirement soak не запускались. [Текущие точные команды/границы](STATUS.md), [GL host contract](../../TrueMoon.Alloy.Hosting/README.md#opengl-hud-и-окно). Следующий шаг — разделение оставшихся legacy Skia/Silk dependencies.

## Предыдущий raster/Vulkan runtime, 2026-10-04

Ниже сохранены исходные исторические исследования API/пакетов. Текущий renderer использует SkiaSharp 4.153.1 и extended DLL из Rendering.Skia. Общие UiSession/UseAlloy/Hosting и window presentation уже реализованы.

[VulkanHudWindowProbe](../../ManualTests/AlloyVulkanTest/VulkanHudWindowProbe.cs) объединяет retained Argentis HUD, независимые scene/HUD graphics pipelines и swapchain. UI lease возвращается с ShaderReadOnlyOptimal; compositor передаёт собственную готовую scene texture в PresentImage и получает callback её layout после GPU completion. CPU readback используется только на кадрах assertions; интерактивный --hud-demo не выполняет readback.

В предыдущем HUD шаге --hud-window --validation прошли 90 presented frames/90 compositions, 12 UI draws, 2 UiSession, 3 swapchains и 6 scene readbacks. Проверены alpha/text/button pixels, input passthrough, dynamic tree/focus, cancel capture, resize/minimize/restore, логические scale 1/1.5/2. Vulkan core/synchronization validation: 0 errors/0 warnings, включая teardown. Published Release повторил тот же результат. Offscreen --interop-compose повторно прошёл 36/18, validation 0/0. [Команды](../../ManualTests/AlloyVulkanTest/README.md), [полный актуальный статус и ограничения](STATUS.md).

Добавлен [VulkanHostContext](../../TrueMoon.Alloy.Rendering.Skia/VulkanHostContext.cs): raw borrowed instance/physical device/device/graphics queue/family, API version, enabled extensions/features и host loader/DeviceWaitIdle callbacks. Устройство, feature memory/pNext и loader остаются собственностью движка и живут до закрытия UI. `VulkanUiTarget` принимает descriptor; Silk convenience overload сохранён. На UI-пути не создаются и не уничтожаются native device/instance/swapchain.

В предыдущем raw-handle шаге `--raw-host --validation` проверил **3 дополнительных engine-owned logical devices без VulkanDevice-обёртки, 6 hosted UI-сессий, 54 GPU readbacks и 3 direct sessions после App.Stop**. Режимы создания device: без optional features, enabled core SamplerAnisotropy/DualSrcBlend, enabled features2 с pNext 16-bit storage. Loader/completion/lifetime/lease checks и published Release прошли validation 0/0; результаты не считаются текущим повторным запуском raw-host.

Предыдущий raster шаг: `SkiaRasterRenderBackend`/`RasterUiTarget` и caller-owned immutable `SnapshotRasterImage` используют тот же UiSession и Skia drawing/text adapter без GPU/window. **24 новых CPU raster xUnit cases и все 53 UI cases прошли**, новые cases обнаружены через solution. `--raster` прошёл UseAlloy/App/DI/input/invalidation/PNG export и session/registry disposal; snapshot читается после закрытия UI. `--backend-compare --validation` проверил unchanged BackendHudView при scale1/1.5/2: matching bounds/input и selected premultiplied RGBA pixels, validation **0 errors/0 warnings** в Debug и published Release. Это не полное glyph/screenshot сравнение. Raw HUD regression90/90/12/2/3/6 и standalone30/2 также прошли validation0/0. Full solution build: 0 errors/1351 warnings; full solution tests не запускались. [CPU пример/проверки](../../ManualTests/AlloyVulkanTest/README.md), [актуальный статус](STATUS.md).

Последовательный GPU WaitIdle сохраняется. Retirement использует maintenance1 fences или legacy reacquisition: предыдущий retirement-шаг прошёл по 200 сессий/2000 кадров/400 swapchains и 10 minimize/restore, после raw handles был smoke5/50/10 в обоих режимах. В предыдущем raster шаге soak не повторялся. Legacy shutdown limitation, EXT-only hardware, полный driver/VRAM profiling, реальный сторонний engine и monitor DPI transitions остаются. Сохранённая после raster точка продолжения была migration legacy OpenGL; актуальная точка указана выше и в STATUS. [Точный объём проверок и ограничения](STATUS.md).

## Что проверено запуском

[AlloyVulkanTest](../../ManualTests/AlloyVulkanTest/Program.cs) работает на Silk.NET `2.23.0`, SkiaSharp и SkiaSharp.NativeAssets.Win32 `4.152.1`, Windows, .NET SDK `10.0.401`.

Запуск из `src`:

```powershell
dotnet run --project ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj --verbosity quiet
```

В проверенной сессии restore уже был выполнен; команда запускалась с `--no-restore`. Код завершения — **0**, итог:

```text
Vulkan / Skia smoke passed: 12 GPU compositions, premultiplied alpha, text, surface recreation and 2 Skia sessions on one host-owned device verified.
```

Пример проверяет:

1. Хост создаёт одно Vulkan-устройство; на нём последовательно создаются и освобождаются два Skia-контекста. После каждого освобождения вызов `DeviceWaitIdle` и следующий контекст продолжают использовать устройство хоста.
2. В каждом контексте поверхности создаются заново в размерах 128×96, 192×128 и снова 128×96.
3. UI рисуется в поверхность RGBA8888 с premultiplied alpha: прозрачный фон, полупрозрачная панель, текст и непрозрачный маркер.
4. `Snapshot()` возвращает `SKImage` с `IsTextureBacked == true`. Одна UI-текстура используется поверх двух фонов сцены через GPU `DrawImage`.
5. После композиции readback используется только для проверок результата: неизменённый фон вне HUD, ожидаемое alpha blending с погрешностью один уровень канала, непрозрачный маркер, прозрачность и наличие текстовых пикселей.

В этом примере UI и сцена используют **один Skia-контекст в каждой сессии**. Он не проверяет внешнее потребление `VkImage`, swapchain, presentation, оконный resize/DPI, нулевой framebuffer, input routing или отсутствие утечек в длительном прогоне. Пересоздание поверхностей в примере не заменяет проверку resize реального окна. Этап 1 остаётся незавершённым.

## Что требуется внешнему Vulkan-хосту

По [документации Vulkan-бекенда Skia](https://skia.org/docs/user/special/vulkan/), при совместном использовании изображения клиенту нужно знать layout, оставленный Skia, сообщать изменения layout обратно и обеспечивать необходимую синхронизацию. Ожидание окончания GPU-работы решает вопрос времени исполнения, но само по себе не передаёт актуальное состояние изображения.

Нужный контракт должен позволять:

- получить идентификатор UI-изображения, формат, размеры, layout и queue family после отрисовки;
- завершить работу Skia до чтения текстуры хостом;
- дать хосту использовать текстуру, сообщить окончание использования и вернуть актуальные layout/queue family;
- обновить представление состояния ресурса внутри Skia до следующей отрисовки;
- освободить UI-ресурсы после возврата текстуры, сохранив device/queue/swapchain хоста.

## Что найдено в публичном API SkiaSharp 4.152.1

Проверены публичные методы и конструкторы установленной сборки, включая поиск по её публичным типам. Повторяемая инспекция собранного примера:

```powershell
pwsh -NoProfile -File ManualTests/AlloyVulkanTest/InspectVulkanApi.ps1
```

Скрипт читает `bin/Debug/net10.0/SkiaSharp.dll` примера, не создавая Vulkan-устройства. Можно передать явный `-AssemblyPath`. Он выводит сигнатуры; вывод является инвентаризацией API, а не автоматическим доказательством поддержки всех способов interop.

| Возможность | Наблюдение |
| --- | --- |
| Импорт описанного Vulkan-изображения | Есть конструкторы `GRBackendTexture`/`GRBackendRenderTarget` с `GRVkImageInfo`, а также API создания Skia surface/image из backend-объектов. |
| Вывод состояния Skia-owned Vulkan-текстуры | У `SKSurface`/`SKImage` не найдены публичные методы экспорта Ganesh backend texture/render target; `Snapshot()` возвращает `SKImage`, а не описание `VkImage` для внешнего хоста. |
| Получение актуального Vulkan layout | У `GRBackendTexture`/`GRBackendRenderTarget` отсутствует публичный `GetVkImageInfo`; публичные getters backend-данных относятся к OpenGL. |
| Обновление состояния существующего Vulkan backend-объекта | Нет публичного `SetVkImageLayout`/операции mutable Vulkan state. Свойство `GRVkImageInfo.ImageLayout` изменяет поле структуры-описания; публичного метода применения нового значения к уже созданному backend-объекту не найдено. |
| Ожидание GPU | Есть `GRContext.Flush()`, `Submit(bool)` и перегрузки Flush; прототип использует `Submit(true)`. |
| Semaphore interop | В проверенных публичных Ganesh API не найдены контракты передачи GPU-семафоров. `GRBackendState`/`GRGlBackendState`, которые может показать скрипт, описывают сброс состояния контекста и не предоставляют Vulkan image layout handoff. |

Это конкретное препятствие для **планируемого обмена UI-текстурой с внешним Vulkan-рендерером через текущий публичный Ganesh API**. Создание Vulkan-контекста и отрисовка в нём при этом проверены и работают.

## Проверка нативного C ABI

Дополнительно прочитана PE-таблица экспортов фактически восстановленной `runtimes/win-x64/native/libSkiaSharp.dll`: **990 именованных exports**. SHA-256: `3DEEAA66897FB0C4EBDA7D85044702B05B7FBFB380DF9BA86DACB82DF2171D3D`. Библиотека не загружалась и её функции при этой инспекции не вызывались.

Найдены `gr_backendtexture_new_vulkan`, `gr_backendrendertarget_new_vulkan`, `gr_direct_context_make_vulkan`, `sk_surface_new_backend_texture` и `sk_surface_new_backend_render_target`. Vulkan getters для image info, операции изменения Vulkan image layout и Ganesh surface/image export в C ABI не найдены. Getters backend-данных есть только для GL (`gr_backendtexture_get_gl_textureinfo`, `gr_backendrendertarget_get_gl_framebufferinfo`). Наличие дополнительных Graphite exports не реализует контракт текущего Ganesh `GRContext`.

Таким образом, для выбранного пути недостаточно добавить managed P/Invoke к имеющейся DLL: нужен соответствующий native API. Проверка касается Windows x64 пакета `4.152.1`; другие платформы и версии здесь не исследовались.

## Следующий технический шаг

Подготовить минимальное расширение native/managed bindings под совместимую версию Skia: экспорт backend-текстуры, чтение актуального `GrVkImageInfo` и применение возвращённого состояния изображения. Альтернатива — проверить конкретную версию пакета, уже предоставляющую этот API. Полноценный рендерер следует строить после отдельного прототипа такого обмена, как предписано этапом 1 плана.

После получения доступного API проверить фактическую передачу текстуры внешнему хосту, возврат состояния и Vulkan validation. Затем подключать swapchain, resize и presentation. Последовательное ожидание GPU остаётся допустимым первым режимом по плану; параллельная работа требует отдельного контракта синхронизации.

Совместное рисование сцены и UI одним Skia-контекстом, показанное smoke-примером, не заменяет согласованный контракт независимого Vulkan-хоста. Архитектурное решение плана о владении устройством и UI-ресурсами сохраняется.

## Продолжение 2026-10-02: native/managed прототип

Этот раздел описывает историческое состояние; результаты текущего продолжения приведены ниже.

Подготовлены [C ABI extension и source-build scripts](../../ManualTests/AlloyVulkanTest/NativeInterop/README.md), [managed lease](../../TrueMoon.Alloy.Rendering.Skia/VulkanInteropSurface.cs) и [внешний consumer через Silk.NET](../../ManualTests/AlloyVulkanTest/VulkanHostReadback.cs). В исторической проверке они закреплены на исходниках SkiaSharp 4.152.1 и Skia commit `723c5a03a7d18de5fb1a041e6cae2f09c4cb3c7e`; текущий bundled asset — 4.153.1 (см. build.json renderer).

Export использует `SkSurfaces::GetBackendTexture`/`GrBackendTextures::GetVkImageInfo`; возврат применяет shared mutable state через `setMutableState(MakeVulkan(layout, queueFamily))`. На native/managed границе передаётся собственный 48-байтный descriptor, а не изменчивые внутренние allocation-структуры Skia. Поверхность владеет VkImage, lease владеет только копией backend descriptor. Режим первой проверки — последовательная работа с одной graphics queue и ожиданием GPU. Архитектура плана не изменена.

Managed build прошёл с кодом 0; исходный GPU smoke повторно прошёл 12 композиций. **Native extension не собран:** preflight остановлен отсутствием MSVC Build Tools. Штатная DLL ожидаемо не содержит новых exports. Поэтому внешний VkImage handoff, GPU-композиция внешней сцены и validation пока **не проверены**, API-разрыв ещё не закрыт рабочей native-сборкой. Подробные команды, фактические проверки и ограничения находятся в README прототипа.

## Продолжение 2026-10-04: handoff и validation прошли

Этот раздел описывает проверку handoff до добавления graphics compositor; текущий результат приведён в следующем разделе.

Native extension собран из точных исходников SkiaSharp **4.153.1**, соответствующих текущему managed-пакету; source pins для 4.152.1 и 4.153.1 закреплены в прототипе. Сборка 4.152.1 тоже прошла, но её native 152 несовместим с managed 153. Официальная проверка версии добавлена в preflight до ABI/GPU; отрицательный сценарий завершился кодом 2 с диагностикой, без обхода version gate.

ABI preflight и **18 внешних чтений VkImage** через raw Vulkan consumer прошли: два Skia-контекста на устройстве хоста, три размера, три кадра, возврат фактического layout и повторная отрисовка. Защиты lease и последующее использование device также проверены. Это закрывает экспериментальный последовательный обмен изображением на одной graphics queue; публичный renderer-контракт ещё не создан.

В VulkanDevice добавлен opt-in validation layer/debug messenger с удержанием delegate до уничтожения instance и synchronization validation. Interop и исходный smoke с patched DLL прошли с **0 ошибок и 0 предупреждений**, включая освобождение ресурсов. Отсутствующий layer явно останавливает запуск с кодом 2. Слой извлечён из подписанного LunarG SDK 1.4.363.0 в локальный каталог; системный Vulkan runtime не менялся.

Исходный smoke со штатной DLL тоже прошёл **12 GPU-композиций**. Managed build: 0 ошибок, финальный incremental — 7 предупреждений. UI unit tests и всё решение 2026-10-04 не запускались. Команды, точные revisions, hash DLL и логи: [README прототипа](../../ManualTests/AlloyVulkanTest/NativeInterop/README.md).

Следующий шаг — GPU sampling и premultiplied alpha-композиция UI-текстуры поверх независимой Vulkan-сцены без CPU-копирования с возвратом состояния и validation. Текущий внешний consumer использует readback для assertions; внешний graphics pipeline и оконный swapchain/resize/presentation ещё отсутствуют. Архитектура и критерии PLAN.md не изменены.

## Продолжение 2026-10-04: внешняя GPU-композиция

Добавлен [VulkanHostCompositor](../../ManualTests/AlloyVulkanTest/VulkanHostCompositor.cs): собственный offscreen image, render pass, framebuffer, две graphics pipelines, sampler/descriptor и повторно используемый command buffer. Сцена и наложение HUD рисуются двумя Vulkan draw calls; Skia рисует только UI. HUD sampling использует exported VkImage, barrier в ShaderReadOnlyOptimal и premultiplied alpha blend ONE/ONE_MINUS_SRC_ALPHA. Поверхность/устройство хоста не передаются в собственность compositor; image view заимствованного ресурса освобождается после завершения GPU-work, layout возвращается Skia через lease.

Текущий запуск `--interop-compose --validation` прошёл: **36 GPU-композиций на 18 leases**, два варианта сцены для каждого UI-кадра, три размера, три кадра и два Skia-контекста. Проверены фон через прозрачную UI-область, alpha панели на обеих половинах сцены, opaque marker, текст, opacity результата, стабильность image handle/borrow guards и последующая отрисовка после возврата shader-read layout. CPU readback выполняется только после GPU-композиции для assertions.

Core/synchronization validation: **0 ошибок / 0 предупреждений**, включая teardown. Предыдущие raw readback и Skia smoke повторно прошли соответственно 18 чтений/12 композиций с validation 0/0. Managed build: 0 ошибок/7 предупреждений NU1507. GLSL скомпилирован под Vulkan 1.1, три SPIR-V модуля прошли spirv-val и встроены как resources. Инструкции: [Shaders/README.md](../../ManualTests/AlloyVulkanTest/Shaders/README.md). Native DLL остаётся прежней; её пересборка не потребовалась.

Следующий шаг — оконный Vulkan-хост со swapchain/presentation, framebuffer resize, minimize/restore и validation. Этап 1 и K2/K3/K5/K6/K7 целиком не завершены; offscreen доказательство композиции не заменяет оконные и длительные проверки. Общий runtime, routing/input и public renderer loader остаются последующими задачами плана.
