# TrueMoon.Alloy: план первой рабочей версии

План восстановлен 2026-09-27 из предоставленного пользователем текста предыдущей сессии. Сохранены цели, архитектурные решения, последовательность работ, проверки и границы первой версии. Идентификаторы пунктов добавлены для отслеживания выполнения.

## Область плана — уточнение формата 2026-10-09

Это локальный план UI направления **Alloy / Argentis**: TrueMoon.Argentis, TrueMoon.Alloy, Rendering.Skia, Platform.Silk, Alloy.Hosting, их compatibility path, UI tests и manual consumers. Общие задачи bootstrap/DI/IPC/units, build всего solution и общий выпуск относятся к [общему плану TrueMoon](../PLAN.md) и планам Core/Cobalt/Enerit/Mithril.

Разделение документов не меняет UI цели, архитектуру, этапы1–5, K1–K9, PA1–PA10, net10.0/xUnit или требования alpha. Общий build и ошибки других модулей учитываются в [общем статусе](../STATUS.md); UI хранит собственные проверки и влияние общих зависимостей на интеграцию. Подробные прежние записи сохранены в [HISTORY.md](HISTORY.md), текущая сводка — в STATUS. Правила ведения — [PLANNING.md](../PLANNING.md).

Текущее состояние, доказательства и точка продолжения находятся в [STATUS.md](STATUS.md). Наличие пункта в этом документе означает требование к результату; статус реализации определяется отдельно.

## Цель и основные решения

Создать гибкий UI-фреймворк с Fluent API на C# и сменяемыми бекендами, пригодный для самостоятельных Windows-приложений и встраивания в качестве GUI/HUD в Vulkan-приложение. Представления и пользовательские элементы описываются в C# классах.

Первая версия должна демонстрировать два сценария:

1. Небольшое самостоятельное приложение с динамическим содержимым.
2. Интерактивный HUD поверх графической сцены.

В обоих сценариях используются одинаковые контролы, layout, состояние и механизм обновления UI. Различаются владение окном и графическими ресурсами, а также организация цикла приложения. HUD получает размер поверхности, ввод и запрос на отрисовку от игры; собственное окно и цикл приложения ему не требуются.

Основная модель — постоянное дерево элементов с изменяемыми свойствами, явными привязками данных и заменяемыми поддеревьями. Точечные изменения включают обновление свойств, замену содержимого контейнера, добавление и удаление элементов коллекции. Неизменённые экземпляры сохраняют фокус, выделение и прокрутку.

Fluent-вызовы создают и настраивают элементы. Для динамических областей используются наблюдаемые коллекции и явная замена содержимого. Полное сопоставление заново построенных деревьев откладывается до появления такой потребности.

Согласовано с пользователем 2026-10-06: контролы можно создавать как публичным конструктором (`new Button(...)`, `new Text(...)`), так и статической фабрикой на самом типе (`Button.Simple(...)`, `Text.Plain(...)`). Оба способа равноправны и могут смешиваться в одной Fluent-разметке. Фабрика возвращает новый экземпляр конкретного контрола для дальнейшей настройки существующими Fluent-методами. Фабрики **4.8** реализованы для существующих типов 2026-10-07; исходное решение сохраняется. [Синтаксис и пример](../../TrueMoon.Alloy.Hosting/README.md#создание-контролов-и-fluent-разметка).

Основной графический стек — SkiaSharp поверх Vulkan. Возможность создания контекста через `GRContext.CreateVulkan` уже исследовалась в прототипе. Пригодность конкретных пакетов, работа поверх внешнего GPU-хоста, создание устройства, swapchain и синхронизация должны быть проверены первым графическим этапом.

## Архитектура и публичные границы

Сохранить Argentis и Alloy; конкретные интеграции вынести в отдельные сборки.

| Модуль | Ответственность |
| --- | --- |
| `TrueMoon.Argentis` | Контролы, композиция, свойства, стили, Fluent API и независимые контракты layout/рисования |
| `TrueMoon.Alloy` | Выполнение UI: обход дерева, layout, обновления, маршрутизация ввода, фокус и жизненный цикл |
| `TrueMoon.Alloy.Rendering.Skia` | Отрисовка и измерение текста через Skia; поверхности Vulkan, OpenGL и raster |
| `TrueMoon.Alloy.Platform.Silk` | Окна, события ОС и ввод через Silk.NET |
| `TrueMoon.Alloy.Hosting` | Интеграция с `App.Builder`, DI, жизненным циклом TrueMoon и связывание выбранного renderer с оконной платформой |

Уточнение имени по запросу пользователя, 2026-10-04: hosting-проект, каталог, сборка и namespace называются `TrueMoon.Alloy.Hosting`. Повторный суффикс `TrueMoon` удалён; ответственность модуля и API подключения сохраняются.

Уточнение поставки native Skia, 2026-10-04: проверенная DLL с Vulkan interop хранится в существующем `TrueMoon.Alloy.Rendering.Skia/NativeAssets/win-x64` вместе с revisions/hash. Renderer копирует её при build/publish в отдельный `truemoon-native/win-x64`, чтобы избежать конфликта со штатными native assets. Публичный SkiaNativeLibrary загружает один экземпляр DLL для обоих bindings. Отдельного native-проекта не создаётся. Инструкция пересборки: [README renderer](../../TrueMoon.Alloy.Rendering.Skia/README.md).

Argentis не зависит от Alloy, SkiaSharp или Silk.NET. Реализация поведения и визуального представления контрола находится рядом с контролом и использует независимые контракты; Alloy организует их выполнение.

Основные точки расширения:

- `UiSession` — корневое представление, обработка ввода, обновление, layout, рендеринг и освобождение ресурсов.
- `IWindowHost` — окно, размер framebuffer, DPI, платформенные события и сервисы.
- `IRenderBackend` — создание графической сессии и вывод UI на поверхность.
- `IDrawingContext` и `ITextLayoutService` — геометрия, clipping, трансформации, изображения и текст без типов Skia.
- Отдельный Vulkan-контракт подключения внешнего устройства и обмена GPU-ресурсами. Vulkan-типы остаются внутри соответствующей интеграции.

Существующий `UsePresentation<TView>()` сохранить как удобную точку запуска с явным выбором платформы и рендерера. Для HUD предоставить создание `UiSession` без окна, DI-контейнера и собственного цикла приложения.

Уточнение подключения, согласованное и реализованное 2026-10-04: `Hosting.UseAlloy(options => options.UseSkiaVulkan().UseExternalHost())` регистрирует renderer и `IUiSessionFactory` без окна/устройства. `UsePresentation<TView>(options => options.UseSkiaVulkan().UseSilkWindow(...))` запускает Argentis Element на выделенном UI-потоке и освобождает ресурсы через App lifecycle. Прямой `UiSession.Create` доступен без App. Выбор backend/хоста обязателен. После 2.1b (2026-10-05) старая перегрузка без настроек находится только в `Hosting.Compatibility` как opt-in legacy OpenGL API; из общего Alloy Skia/Silk/TrueMoon dependencies удалены.

Vulkan surface/lease и публичный `SkiaNativeLibrary` перенесены в Rendering.Skia. `VulkanUiTarget` принимает raw-handle `VulkanHostContext`; после разделения 2.1a Silk convenience target создаётся через `Hosting.SilkVulkanHost.CreateTarget(device)`. GPU обмен пока последовательный с ожиданием completion; контракта нескольких frames-in-flight ещё нет. [Примеры подключения и владение](../../TrueMoon.Alloy.Hosting/README.md).

Уточнение raw-handle подключения, 2026-10-04: host передаёт instance/physical device/device/graphics queue/family, запрошенную API version >=1.1, enabled extensions и указатели enabled features/features2/pNext вместе с собственным loader и проверяемым DeviceWaitIdle callback. Descriptor не владеет native handles; features memory/loader/callback targets живут до закрытия всех UI-сессий. Skia owns только свои context/surfaces/textures. Protected content, другие queues и frames-in-flight остаются за пределами последовательного режима. Первоначальные convenience overloads и Silk window presenter были сохранены в renderer; в 2.1a (2026-10-05) они заменены factories в Hosting, presenter/counters перенесены туда же, а dependency Rendering.Skia → Platform.Silk удалена. Это реализация предусмотренного внешнего Vulkan-контракта 1.5/2.2, цели и критерии alpha сохраняются.

Уточнение presentation, 2026-10-04: игровой хост композитит retained HUD со сценой на GPU, возвращает UI lease после sampling и передаёт готовую scene texture в `VulkanWindowPresenter.PresentImage`. Presenter заимствует источник и сообщает его фактический layout после завершённой GPU-работы; источник остаётся собственностью игры. `Present(UiSession)` сохраняется для standalone UI. Это реализация исходных целей 1.2–1.6/5.2–5.3; цели, архитектурные границы и критерии K1–K9 не меняются. [Оконный пример и автоматический сценарий](../../ManualTests/AlloyVulkanTest/README.md).

Уточнение retirement, 2026-10-04: window device согласует KHR/EXT swapchain_maintenance1 и feature с необходимыми instance dependencies. Presenter использует present fences для reuse и уничтожения swapchain generations. Legacy fallback сохраняет поколение до подтверждённой reacquisition показанного image нового swapchain; shutdown без extension сохраняет ограничение WaitIdle базового Vulkan. Это не основание объявлять WaitIdle доказательством завершения presentation. K7 проверяется отдельным --retirement-soak с ownership counters, registry/subscriptions и WeakReference; счётчики handles не заменяют VRAM/driver profiling. Цели и критерии alpha не меняются. [Алгоритм, ограничения и команды](../../ManualTests/AlloyVulkanTest/README.md).

Уточнение raster, 2026-10-04: `SkiaRasterRenderBackend`/`RasterUiTarget` используют общий UiSession и Skia drawing/text adapter на CPU без window/device. RGBA premultiplied snapshot экспортируется как caller-owned immutable SKImage, который живёт независимо от session/resize и не требует lease Return. Native Skia Windows x64 сохраняется; ABI Vulkan не требуется на raster-пути. Выбор — `UseAlloy(...UseSkiaRaster().UseExternalHost())` или direct Create. На момент raster шага standalone был Vulkan-only, comparison охватывал raster/Vulkan; после OpenGL шага standalone поддерживает оба GPU backend, а comparison — все три. Selected-pixel/layout/input проверки не заменяют полное font golden сравнение K2/K4.

Уточнение OpenGL, 2026-10-04: второй явно выбираемый GPU renderer — `SkiaOpenGLRenderBackend` с borrowed `OpenGLUiTarget` (host loader/current-context guard). Общие UiSession/Argentis/Skia drawing/text сохраняются; GPU retained frame композитится в host-owned RGBA8 FBO через PresentOpenGL. Host owns context/FBO/window, сериализует работу и восстанавливает своё GL state после Skia. Cross-context texture lease/async handoff не включаются в эту миграцию. `UseSkiaOpenGL` работает с external host и standalone Silk presentation; последний освобождает Skia в Closing до уничтожения GL context. Managed callback errors переходят в Completion/StopAsync после закрытия, без unwind через GLFW. Одинаковый view проверяется на raster/OpenGL/Vulkan; legacy no-options Visual API остаётся compatibility path без экспериментального drawing/CreateVulkan/empty catch. Это завершает миграцию пути 2.5, но не разделение зависимостей 2.1; цели и K1–K9 сохраняются. Предыдущее ограничение standalone Vulkan-only отменено.

## Последовательность реализации

### Актуальная точка продолжения — реализация DPI 2026-10-09

Реализованы функциональные подэтапы3.5a/b/c/d,4.8,5.1a/b/c и общая Settings-сцена raster/OpenGL/Vulkan/HUD, контракт2.4/K8 с controlled failure/recreate, K9 baseline/profile/reuse и Windows appearance/custom chrome/native Snap4.9b/c/d. 2026-10-09 добавлен native Windows DPI contract: Scale, логические размеры и нормализованный ввод. Фактически выполненные текущие build/tests/GPU/publish и границы проверки находятся в STATUS. Реализация не означает итоговую готовность alpha.

Следующий конкретный шаг — **4.9d / K3: физические 100/200% и переходы между разными DPI**, с сохранением логического client size, focus/selection и повтором chrome/input/alpha. Доступный монитор 150% проверяется отдельно; directed WM_GETDPISCALEDSIZE для96/144/192 не заменяет физический переход. Затем закрыть оставшуюся interaction/settings/lifecycle matrix4.9, fixed-font/full goldenK2/K4, длительныйK7 и real loss evidenceK8, затем итоговыйK1–K9 и отдельный/package consumer5.4–5.5. Нерегулярный standalone Name binding mismatch и прежние opacity/legacy capture failures остаются отдельными нерасследованными рисками.

Этот маршрут актуализирует более ранние датированные формулировки «следующий шаг» ниже; цели, архитектура, net10.0/xUnit и критерии готовности сохраняются. Точная область прежних проверок и открытые ограничения — [STATUS.md](STATUS.md).

### Контракт Windows DPI 4.9d / K3 — уточнение 2026-10-09

По запросу начать следующий этап alpha реализовано уточнение координат существующего Silk host. Размеры UI, параметры создания/Resize и custom-frame MinimumSize/MaximumSize задаются в логических 96-DPI единицах. На Windows UiViewport.Width/Height — физические framebuffer pixels, Scale — GetDpiForWindow(HWND)/96 независимо от framebuffer/client ratio. NativeWindow.Size/Position и raw Win32 messages остаются физическими координатами. Silk mouse position и native caption input преобразуются в логические координаты ровно один раз; wheel delta не масштабируется. Hosting перед input синхронизирует изменённый viewport и layout/region map.

Логический client resize округляется до ближайшего физического пикселя (середина от нуля); native minimum округляется вверх, maximum вниз по текущему HWND DPI, в том числе до первой публикации regions. Custom chrome отвергает snapshot с устаревшими размером или Scale и сбрасывает его при WM_DPICHANGED. WM_GETDPISCALEDSIZE вычисляет новый physical client из прежнего logical client, для обычного окна добавляет рамку через AdjustWindowRectExForDpi. Предложенный Windows rectangle при WM_DPICHANGED применяет GLFW; второго SetWindowPos в DPI hook нет.

Окно создаётся в scoped PER_MONITOR_AWARE_V2 context потока; прежний context восстанавливается. Viewport/Resize и оконный loop используют такой же scope для callers с другим DPI awareness. Новый адаптер не меняет process-wide DPI settings приложения. DPI hook владеет только subclass/GC root и borrowed HWND, снимается до уничтожения окна; managed ошибки не выходят через native callback. HUD с внешним host сохраняет прежний контракт: host задаёт UiViewport/Scale и нормализует ввод сам. Public API, renderer ownership, native Skia ABI, пакеты и net10.0/xUnit не меняются.

Это исправляет Windows поведение на high DPI: клиент600×400 логических единиц при150% получает900×600 физических пикселей вместо прежнего physical600×400/Scale1. K3 по-прежнему требует реальные100/150/200% и переходы; арифметические/headless/directed проверки записываются отдельно от физической matrix.

Основания реализации: [GetDpiForWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getdpiforwindow), [WM_DPICHANGED](https://learn.microsoft.com/en-us/windows/win32/hidpi/wm-dpichanged), [thread DPI context](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setthreaddpiawarenesscontext), [GLFW Windows DPI handling](https://github.com/glfw/glfw/blob/3.4/src/win32_window.c).


### Контракт отказа графики 2.4/K8 — уточнение 2026-10-07

`UiRenderingException` обозначает подтверждённый terminal failure с backend/operation, original cause и причиной DeviceLost, ContextLost либо BackendFailure. Обычные ошибки DrawCore сохраняют dirty state и допускают повторный Update; они не объявляются потерей устройства. Vulkan ErrorDeviceLost классифицируется явно, abandoned Skia context сообщает ContextLost. Для внешнего presentation/engine host сообщает подтверждённый отказ через UiSession.ReportRenderingFailure.

Первый отказ сохраняется в RenderingFailure, уведомляет RenderingFailed один раз на owner thread, отменяет focus/capture/hover и pending posts. Update/Resize/input/Post/theme/export/presentation больше не допускаются. Повторный report не заменяет первый cause. VerifyAccess и VerifyAvailable проверяют lifetime/thread/lease, допускают cleanup faulted context; проверка GPU health выполняется отдельно. Dispose из notification/Update и с borrowed frame отвергается до разрушения дерева. Ошибки cancellation/observers/cleanup сохраняются вместе с исходным отказом, остальные безопасные cleanup действия выполняются. Cleanup errors могут образовывать вложенные AggregateException.

Host владеет model/device/window/queues, UI — своим деревом, подписками и Skia wrappers. Model сохраняется независимо от Dispose; после освобождения старой сессии создаются новые controls/session из той же модели. Transient focus/capture/selection/scroll автоматически не переносятся. При настоящей потере устройства host сначала освобождает старый UI, затем сам пересоздаёт device/context/target. Автоматическое бесшовное восстановление не включается.

Vulkan lease обычно возвращается после завершённой host GPU работы с actual layout. Только подтверждённый DeviceLost разрешает AbandonAfterDeviceLoss без GPU ожидания и state-return; Skia context abandon выполняется без backend calls. Borrowed Vulkan handles и loader остаются живы до окончания cleanup даже после abandon. При неизвестном отказе completion нельзя разрушать потенциально in-flight host ресурсы: standalone сохраняет device, если UI/presenter cleanup не завершился. Best-effort cleanup не является обещанием полного освобождения driver allocations при неизвестном состоянии графики. [Подробный контракт и пример](../../TrueMoon.Alloy.Hosting/README.md#отказ-графики-и-явное-пересоздание-24k8).

Воспроизводимый `--settings-failure --validation` проверяет controlled BackendFailure на реальных raster/OpenGL/Vulkan surfaces, borrowed-frame guard/normal Return, renderer result classification, hosting draw/external-report/creation failures и explicit recreation с прежней SettingsModel. Контролируемые сценарии учитываются отдельно от физического device/context loss; последний этим probe не индуцируется. Критерии K1–K9 и требования итогового alpha прохода сохраняются.

### Согласованная корректировка порядка работ — 2026-10-06

После оценки текущей реализации пользователь согласовал уточнение плана. Архитектура модулей, retained дерево, net10.0/xUnit, два сценария запуска и критерии K1–K9 сохраняются. Крупные пункты разделяются на проверяемые подэтапы; графический прототип и функциональные механизмы учитываются отдельно от итоговой готовности alpha. Исходные номера пунктов сохраняются для ссылок и исторических записей. Это изменение плана, не реализация перечисленных возможностей.

Ближайший результат — одно небольшое AlloyTest-приложение с редактируемой моделью, динамическим списком и сохранением состояния неизменённых элементов при изменении дерева. Пример развивается вместе с API и затем используется для проверки общего runtime и графических адаптеров.

| Порядок | Пункты плана | Проверяемый результат |
| --- | --- | --- |
| 1 | 3.5b / 3.6 | Общие двусторонние привязки: TextBox.Value, CheckBox.IsChecked и Slider.Value; пользовательское редактирование, нормализация модели, отсутствие feedback и корректный attachment lifetime. |
| 2 | 4.8 / 5.1a | Фабрики рядом с публичными конструкторами и первая форма AlloyTest с моделью; примеры обоих способов создания контролов. |
| 3 | 3.5c / 3.5d / 5.1b | Наблюдаемый список и условные области загрузка/ошибка/содержимое; корректное владение generated nodes и сохранение неизменённых экземпляров. |
| 4 | 4.1–4.7 / 5.1c / 5.2–5.3 | Полные взаимодействия заявленных контролов, прокрутка, темы, пользовательский виджет и динамическая демонстрация в standalone/HUD. |
| 5 | 2.4 / K8 | Передача ошибок хосту, безопасное закрытие и создание новой сессии после графической ошибки. |
| 6 | K1–K9 / 5.4–5.5 | Воспроизводимый итоговый проход на заявленной Windows-конфигурации, measurements baseline, документация и проверка поставки отдельному consumer. |

При завершении каждого подэтапа STATUS должен указывать реализацию, конкретные прошедшие проверки, их дату и оставшийся объём. Завершённый подэтап не объявляет завершённым весь родительский пункт или alpha.

### Этап 1. Проверить графический стек и встраивание

Создать минимальный Vulkan-хост: простая сцена, текст и полупрозрачная панель Skia поверх неё.

- **1.1. Совместимость пакетов.** Проверить совместимость зафиксированных версий Silk.NET, SkiaSharp и нативных библиотек.
- **1.2. Жизненный цикл графики.** Реализовать создание GPU-контекста, поверхности, вывод кадра, изменение размера и освобождение ресурсов.
- **1.3. Графический пример.** Показать сцену, текст и полупрозрачную панель Skia в минимальном Vulkan-хосте.
- **1.4. UI-текстура HUD.** Использовать прозрачную GPU-текстуру с premultiplied alpha; хост накладывает её на сцену без копирования через CPU.
- **1.5. Владение ресурсами.** Хост владеет устройством, очередью и swapchain; Alloy владеет собственным Skia-контекстом и UI-текстурами.
- **1.6. Синхронизация.** Для первой версии использовать одно устройство и согласованное использование графической очереди. Явно передавать зависимости GPU-работы и сигнал завершения использования текстуры хостом. Проверить синхронизацию и переходы состояния изображений при обмене ресурсами со Skia.

Результат этапа — работающий пример и зафиксированный контракт взаимодействия. Если возможности пакета ограничивают интеграцию, оформить конкретное техническое препятствие до разработки основного рендерера.

### Этап 2. Разделить прототип на независимые слои

- **2.1. Сборки и зависимости.** Вынести зависимости Skia и Silk из общего API; переместить интеграцию с TrueMoon в hosting-сборку.
- **2.2. Контракты запуска.** Заменить привязанный к Silk.NET `IGraphicsPlatform` отдельными контрактами окна и рендерера. Реализовать `UiSession`, оконный и встраиваемый сценарии; сохранить `UsePresentation<TView>()` с явным выбором бекендов.
- **2.3. Дерево и время жизни.** Исправить хранение и обход дочерних элементов, родительские связи и освобождение подписок.
- **2.4. Диагностика.** Убрать экспериментальный вывод из основного пути рендеринга; обеспечить передачу ошибок в диагностику.
- **2.5. Другие графические пути.** Перенести существующий OpenGL-прототип на новые контракты как второй явно выбираемый GPU-путь. Raster использовать для проверок без окна.

Результат — одно представление запускается с разными графическими адаптерами без изменения кода контролов.

### Ближайшие подэтапы 2.1: разделение зависимостей

Сверка исходников 2026-10-04 показала: графический путь 2.5 уже был реализован, однако общий Alloy ещё содержал legacy presentation, а Rendering.Skia ссылался на Platform.Silk. Переносить связку renderer/window в Hosting: этот модуль уже зависит от обоих адаптеров и организует standalone запуск. Перенос 2.1a выполнен и проверен 2026-10-05: presenter/counters и `SilkVulkanHost` находятся в Hosting, renderer принимает только raw descriptor и больше не ссылается на Platform.Silk. Перенос 2.1b также выполнен и проверен 2026-10-05: 31 legacy-файл вынесен в Hosting.Compatibility, Alloy оставлен только с Runtime/Argentis, AlloyTest использует explicit Hosting API. Требуемые зависимости compatibility явно подключены в Hosting; транзитивные legacy dependencies через Alloy удалены. Фактические проверки и ограничения записаны в STATUS. Пять основных модулей, net10.0, xUnit и критерии K1–K9 сохраняются.

Целевые направления ProjectReference:

| Модуль | Зависимости UI после разделения |
| --- | --- |
| Argentis | Без зависимости от других UI-модулей, Skia и Silk |
| Alloy | Argentis; runtime и независимые контракты |
| Rendering.Skia | Alloy/Argentis, SkiaSharp; Vulkan bindings допустимы внутри Vulkan-интеграции, без Platform.Silk и оконного API |
| Platform.Silk | Alloy/Argentis, Silk; без Rendering.Skia и SkiaSharp |
| Hosting | Alloy, Rendering.Skia, Platform.Silk, TrueMoon lifecycle/DI contracts; интеграционные adapters и standalone presentation |

**2.1a — выполнен и проверен 2026-10-05: убрать Rendering.Skia → Platform.Silk.**

1. Перенести VulkanWindowPresenter и его presentation resource counters в Hosting; сохранить алгоритм retirement, Present/PresentImage, ownership и синхронизацию.
2. В Hosting вынести Silk→VulkanHostContext adapter. Заменить renderer API `FromSilkDevice`, `VulkanUiTarget(VulkanDevice)`/`Device` и `VulkanInteropSurface(VulkanDevice, ...)` на raw descriptor плюс явные convenience factories в Hosting. Raw host descriptor, enabled capabilities, lifetime callbacks и native ABI сохраняются.
3. Обновить HostedUiWindow, GPU manual probes и README на новые namespace/factories; удалить ProjectReference renderer→platform. Vulkan types, нужные renderer для lease/layout, оставить backend-specific package dependency; не переносить их в общий Alloy.
4. Проверить прямой raw/raster/OpenGL renderer без Hosting/Platform: исходники, ProjectReference и фактические сборочные зависимости. Наличие Silk.NET.Vulkan package внутри Vulkan renderer не равнозначно зависимости от оконного Platform.Silk. До 2.1b отдельно учитывать transitive Skia/Silk/TrueMoon зависимости через legacy Alloy: удаление renderer→platform не доказывает полного очищения всего графа.

Готовность 2.1a: нет циклов между сборками и ссылки renderer→Platform.Silk; публичный raw-host путь работает без Silk window/device wrapper; общие3backend/input/layout, external App и обе standalone ветки сохранены. Проверки после переноса: сборка решения и UI xUnit; raw-host, raw HUD, backend comparison, OpenGL external/window/error, Vulkan window, короткий retirement smoke в maintenance1/legacy режимах; publish и запуск опубликованного примера. Новые результаты записать в STATUS, прошлые логи не засчитывать автоматически. Это завершает только 2.1a, Alloy в этот момент ещё может содержать legacy зависимости.

**2.1b — выполнен и проверен 2026-10-05: оставить общий Alloy независимым.**

1. Вынести старую связку VisualTree/IGraphicsPlatform/ViewHandle/ViewManager, presenters и no-options UsePresentation из Alloy в явно выделенную compatibility область Hosting. Она переходная, не часть нового runtime. Не возвращать зависимости Hosting/Skia/Silk в Alloy ради совместимости.
2. Перевести найденный consumer `ManualTests/AlloyTest` на Hosting.UsePresentation с явным backend/host; View1/View2 уже наследуют Argentis.View/Element. Проверить оставшиеся callers, imports и сборочную принадлежность типов. Сохранение namespace само по себе не обеспечивает совместимость уже собранных приложений; миграция alpha API допускается планом и документируется с обновлением примеров.
3. Удалить из Alloy PackageReference Silk.NET/SkiaSharp/Topten.RichTextKit и ProjectReference Core/Contracts после переноса использующего их кода; проверить фактическую assembly dependency. Выделение compatibility области не означает исправление старого Visual.GetEnumerator: его проверка/вывод из использования учитывается отдельно в 2.3.

Готовность 2.1b: Alloy содержит runtime/независимые контракты и зависит только от Argentis среди проектов; обычное создание UiSession не требует TrueMoon App, Skia или Silk. Hosting остаётся опциональным. Примеры и проверки 2.1a повторяются для изменённых границ; путь миграции legacy callers описан. Реализация 2026-10-05 использует namespace `TrueMoon.Alloy.Hosting.Compatibility` (presenters — `.Compatibility.Presenters`), без type forwarders: consumers обновляют references/imports и пересобираются. Проверки assembly references и отдельные core-only/renderer-only consumers подтверждают новые границы; compatibility consumer проверяет регистрацию, не старый window/lifetime путь. [Миграция и ограничения](../../TrueMoon.Alloy.Hosting/Compatibility/README.md). Завершение 2.1 не объявляет завершёнными 2.3/2.4 или alpha.

После разделения: ownership Remove/SetContent/transfer (3.4/2.3) уточнён и проверен 2026-10-05; one-way3.5a и BindTwoWay3.5b реализованы и проверены 2026-10-06 в своих контрактных границах. Фабрики4.8, первая форма AlloyTest5.1a, коллекции3.5c и условные области3.5d/демонстрация5.1b реализованы и проверены 2026-10-07; window lifetime defect исправлен и повторно проверен в ограниченном Debug/published Release наборе. Следующий шаг — полные взаимодействия5.1c в той же сцене. Итоговые K1–K9 идут по согласованному порядку; реализация отдельных подэтапов не завершает весь3.6 или alpha. Дополнительные GPU/EXT-only/VRAM исследования вести по выявленным рискам; async frames-in-flight остаются за пределами текущего последовательного режима.

### Уточнение ownership и failure semantics — 2026-10-05

Это уточнение lifetime постоянного дерева для 2.3/3.4 перед bindings. Модули, net10.0/xUnit и критерии K1–K9 сохраняются.

- Контейнер владеет текущими детьми; UiSession владеет корнем и surface после успешного создания. Remove/Clear/замена элемента коллекции/SetContent отсоединяют старое поддерево без Dispose и возвращают владение вызывающему коду. Оно обязано либо повторно добавить его, либо Dispose.
- Перенос — явные Remove и Add/SetContent. Между вызовами поддерево принадлежит вызывающему коду. После detach очищаются focus/hover/capture/pressed state и ссылки прежней сессии; повторное подключение не восстанавливает ввод. Сохранённые данные, selection/scroll и экземпляры остаются. Move внутри одной коллекции не отсоединяет элемент и сохраняет ввод/подписки; замена соседней ветви сохраняет состояние неизменённых контролов.
- `Own` освобождает ресурс при Dispose и сохраняет его при Remove/переносе. Новый `OwnAttachment` требует live attached element, освобождает ресурс при текущем detach и не переносит subscription в новую attachment. Повторное подключение может создать новую подписку через AttachmentChanged. Это lifetime foundation, не реализация bindings.
- Прямые Detach/Dispose принадлежащего контейнеру ребёнка отклоняются до изменения. Сначала Remove/SetContent(null). Dispose контейнера автоматически освобождает оставшихся детей, в том числе virtual cleanup контролов. Корнем управляет UiSession; вручную отсоединять или передавать корень живой сессии нельзя.
- Invalid owner/cycle/disposed subtree/index/thread проверки выполняются до изменения дерева. Такие ошибки сохраняют прежнее владение и ввод. Внешние уведомления выполняются после согласованного изменения всех parent/attachment связей. Ошибка observer/cleanup не откатывает уже видимый commit: продолжаются остальные notifications/cleanup, затем исходная одиночная ошибка или AggregateException передаётся вызывающему коду. После такой ошибки Add/SetContent может уже передать владение контейнеру; автоматически повторять изменение нельзя.
- Структурные изменения и Dispose сессии внутри tree/lifetime notifications отклоняются; отложенную работу следует отправлять через UiSession.Post. Queued Element.Dispatch привязан к текущей attachment и пропускается после удаления/переноса. Прямой UiSession.Post остаётся явно управляемой работой вызывающего кода.
- Ошибка attachment при создании UiSession откатывает подключение всех узлов и attachment subscriptions; корень остаётся вызывающему коду. При Dispose ошибки отдельной подписки не прерывают освобождение siblings, surface и уведомление registry; повторный Dispose не повторяет cleanup.

Реализация и проверки описаны в STATUS и [Hosting README](../../TrueMoon.Alloy.Hosting/README.md#владение-динамическим-деревом). Это закрывает policy текущего retained дерева; legacy Visual.GetEnumerator и остальные требования 2.3/2.4 остаются отдельно.

### Этап 3. Реализовать дерево, layout и динамический контент

- **3.1. Свойства.** Ввести типизированные свойства с уведомлениями и признаками влияния на layout и отрисовку.
- **3.2. Layout.** Реализовать Measure/Arrange, логические координаты независимо от DPI, размеры, ограничения, margin, padding и выравнивание.
- **3.3. Контейнеры.** Добавить вертикальный и горизонтальный контейнеры, наложение элементов, clipping и прокрутку.
- **3.4. Динамическое дерево.** Поддержать замену содержимого контейнера и изменения коллекций. Неизменённые экземпляры сохраняют фокус и состояние; удалённые освобождают подписки и захват ввода.
- **3.5. Привязки.** Добавить явные типизированные привязки к `INotifyPropertyChanged` и наблюдаемым коллекциям, включая двустороннее обновление редактируемых значений.
- **3.6. Потоки.** Выполнять изменения UI последовательно на потоке-владельце. Фоновые обновления поступают через диспетчер; в HUD поток и последовательность вызовов задаёт хост.

Результат — постоянное дерево с корректным layout и точечными обновлениями, сохраняющими состояние оставшихся элементов.

### Проверяемые подэтапы привязок и динамической разметки

- **3.5a. Односторонние свойства.** Bind/BindText, direct selector, initial sync, owner-thread Dispatch и отключение/reconnect при detach/reattach. Контракт ниже; фактическая готовность отдельно в STATUS.
- **3.5b. Двустороннее редактирование.** Общий typed read/write механизм для TextBox.Value, CheckBox.IsChecked и Slider.Value. Для первой версии запись в модель выполняется при каждом изменении редактируемого значения; режим commit по потере фокуса/команде откладывается. UI setter и source getter/setter работают в согласованном owner-thread контексте; фоновые source events проходят через dispatcher.
- **3.5c. Наблюдаемые коллекции.** Initial population и INotifyCollectionChanged Add/Remove/Replace/Move/Reset. Добавление/удаление/перестановка сохраняют экземпляры и состояние незатронутых элементов; Move не отсоединяет nodes. До реализации определить соответствие model item → generated node, обработку повторяющихся items и Reset. Явно описать, какие generated nodes освобождает binding, а какие передаются caller; исходные Remove/SetContent ownership правила сохраняются.
- **3.5d. Условные области.** В той же демонстрации показать загрузку/ошибку/содержимое через ContentControl.SetContent. Неизменённые соседние ветви сохраняют состояние. Новую ветвь создавать и проверять до замены; старую переиспользовать либо Dispose по существующим правилам. BindContent — возможный следующий helper по выявленной потребности демонстрации, не обязательная новая API alpha и не автоматическое наблюдение за произвольными factory lambdas.

До реализации 3.5b зафиксировать публичную typed API и полный контракт: equality/feedback/reentrancy guard, чтение канонического значения после setter normalization, отображение отказа setter и состояние контрола после отказа. Ошибка не должна молча теряться или нарушать native callback boundary; нельзя считать её доказательством отката уже committed изменений. Обе стороны обязаны соблюдать инварианты контрола, включая selection/caret TextBox. Новое корректное изменение после ошибки должно допускать восстановление. Проверки: реальный ввод, переключение и drag/keys; отсутствие обратной записи при source → UI; normalization/equality/validation failure; detach/reattach/Move/transfer; stale queued/in-flight события и cleanup.

Для 3.5c отдельно проверить empty/single/multiple items, все пять действий коллекции, source changes во время detach, reconnect без повторных subscriptions, фоновые события и ошибки node factory/notifications. Неуспешное создание нового subtree должно иметь определённое владение и cleanup; ошибка notification после commit не повод автоматически повторять mutation. Для 3.5d проверить ошибку создания новой ветви, освобождение заменённой ветви и сохранение focus/selection/scroll соседей.

### Контракт коллекционных привязок 3.5c — согласован 2026-10-07 после 93a5cae

Пользователь разрешил реализацию предложенного контракта. Цели retained дерева, ownership обычных Remove/SetContent, границы модулей, net10.0/xUnit и K1–K9 сохраняются. Первая версия намеренно использует уникальные reference items; keys и повторяющиеся occurrences отложены.

- Публичная API Argentis: `container.BindItems(source, factory)` для ObservableCollection<TItem>/ReadOnlyObservableCollection<TItem>, `TItem : class`, factory возвращает Element; конкретный тип контейнера сохраняется. Источник фиксирован до Dispose, автоматическое наблюдение за заменой model property не добавляется. Один binding на первоначально пустой Items; после регистрации структура Items управляется исключительно binding, даже detached. Статические controls располагаются в соседних контейнерах. `RefreshItems()` запрашивает повторную синхронизацию; Unbind не вводится.
- Сопоставление только ReferenceEquals: null и повтор одной ссылки отклоняются до изменения дерева; равные по Equals разные объекты допустимы. Свойства item обслуживают существующие property bindings. Reset/reattach/Refresh сохраняют nodes для оставшихся объектов, переставляют без detach, создают новые и освобождают отсутствующие. Новый объект с прежним Id создаёт новую строку. Add/Remove/Replace/Move/Reset, включая range events, запрашивают сверку актуального snapshot; промежуточные состояния могут coalesce. Add+Remove до Update может не вызвать factory, Remove+Add той же ссылки — сохранить node. Replace той же ссылкой — no-op.
- Source и items принадлежат вызывающему коду. Factory возвращает новый live unowned/unattached subtree; disposed/owned/repeated nodes и ancestors/cycles отклоняются. До commit принятые drafts принадлежат binding, после — контейнеру. При удалении binding получает ownership отсоединённой строки и обязан Dispose. Detach всего контейнера сохраняет строки и отключает subscription; Dispose контейнера освобождает детей обычным путём без повторного cleanup binding. Factory отвечает за ресурсы, выделенные до throw и не возвращённые binding; чужой owned node binding не освобождает.
- Snapshot проверяется, все новые строки полностью создаются и проверяются до batch commit. Один внутренний TreeChange фиксирует конечный порядок детей, parent/attachment связи и mapping до notifications. Сохранённые строки не отсоединяются: focus/selection/subscriptions сохраняются, scroll offset ограничивается обычным layout clamp. Ошибка snapshot/factory/validation сохраняет прежнее дерево, cleanup drafts продолжается после отдельных ошибок. Source mutation из factory отклоняет подготовку по version guard; исходная модель не откатывается. Source changes из observers ставят следующую синхронизацию в очередь, без вложенного commit.
- Notification/attachment callback/cleanup failure после commit не откатывает дерево и mapping. Остальные observers и cleanup удалённых строк выполняются best-effort; одна ошибка сохраняет исходный тип, несколько передаются AggregateException. Повторное применение committed mutation не допускается. Следующий корректный event/RefreshItems допускает recovery.
- Registration живёт через Own, connection через OwnAttachment. До attach source не читается и factory не вызывается; attach/Bind уже attached контейнера подписывает source и ставит initial sync в очередь следующего Update до layout. Структурные mutations внутри AttachmentChanged по-прежнему запрещены. Detach/Dispose отключают subscription; captured attachment dispatcher и active guard отбрасывают старые queued/in-flight callbacks после reattach/transfer.
- Background notifications только запрашивают sync; snapshot/factory/tree mutations выполняются на owner thread. Владелец source обязан сериализовать changes/notifications и исключить конкурентное перечисление; ObservableCollection не становится потокобезопасной. AlloyTest меняет коллекцию на UI-потоке; фоновые результаты доставляются через dispatcher. Factory failure в initial Update оставляет direct session живой с прежним списком для recovery; standalone HostedUiWindow использует существующий callback boundary и передаёт ошибку через Completion/StopAsync после закрытия. Inline error rows — отдельный 3.5d через SetContent.

Проверки: initial empty/single/multiple, все пять actions/ranges, identity/duplicates/null/coalescing, retained input/selection/scroll/subscriptions, factory drafts/validation/errors, coherent postcommit notifications/cleanup/recovery, detach/reattach/transfer/background/stale events и эксклюзивность Items. Затем динамический редактируемый список в существующей SettingsModel/View1, sequential Debug/published Release window smoke. Фактические результаты реализации и пределы проверок — в STATUS; контракт не объявляет завершёнными 3.5d, 5.1b целиком или alpha.

### Реализация условных областей 3.5d / завершение 5.1b — 2026-10-07

В AlloyTest отдельная область переключается между загрузкой, ошибкой и готовым содержимым через существующий ContentControl.SetContent; форма и наблюдаемый список остаются соседними retained ветвями. Состояния демонстрации контролируются явными действиями модели, без реального сетевого запроса или обещания async loading/cancellation. Ошибка содержит действие «Повторить», возвращающее состояние загрузки. BindContent и новые library API/dependencies не вводятся.

Sample-specific LoadStatusRegion использует обычную typed property binding immutable LoadStatus snapshot (phase/message). Property hook ставит замену в очередь owner thread, включая initial attach; attachment generations отбрасывают stale work, reattach повторно сверяет desired/displayed состояние. Одинаковый snapshot сохраняет текущую ветвь без redraw/rebuild. Новая ветвь полностью строится до SetContent validation/commit. При factory/validation failure прежний child остаётся; принятый unowned draft очищается, foreign owned node/ancestor не освобождается. Factory отвечает за ресурсы, выделенные до throw без возврата.

После commit, в том числе когда SetContent бросил notification error, принятая ветвь остаётся собственностью контейнера, старая освобождается caller-кодом виджета. Cleanup error не останавливает оставшийся cleanup; single error сохраняет тип, несколько передаются AggregateException. Desired model/property snapshot не откатывается при ошибке подготовки; displayed child/status остаются прежними до успешного event/RefreshContent. Postcommit ошибка не запускает повторную замену автоматически. Общие Remove/SetContent ownership правила не меняются.

Проверки используют точные исходники sample widget через Compile links в существующем xUnit проекте, без ProjectReference на manual executable/App/Silk. Проверять все три ветви/messages, фабрику и validation before commit, old/draft cleanup и notification failures, equality/coalescing, owner-thread/background/attachment lifetime, input cleanup заменённой ветви и focus/selection/scroll соседей. Реальный View1/window smoke дополняет headless проверки transitions/retry/Dispose, отдельно от fonts/OS input/long soak/alpha. Результаты текущего запуска — в STATUS.

### Контракт двусторонних property bindings 3.5b — 2026-10-06

Для реализации принят `element.BindTwoWay(uiProperty, source, x => x.Property)`, сохраняющий конкретный тип Fluent-цепочки. Выбирается прямое readable/writable public instance property точно того же TValue; computed/nested/field/conversion/read-only/private-set/init-only selectors отклоняются до подключения. One-way Bind сохраняет свой контракт, включая стандартное приведение типа. На одно UiProperty остаётся одна registration любого направления до Dispose.

- Начальное значение и reattach берутся из source, без обратной записи. Пока элемент detached, обе стороны отключены. UI-изменение attached свойства записывает значение немедленно на owner thread, только если оно отличается от source. Локальное программное изменение также считается UI-изменением; source → UI и изменения во время синхронизации не вызывают повторный setter.
- После setter значение source перечитывается и применяется к контролу, включая normalization и setter без PropertyChanged. Same-thread echo выбранного свойства во время source setter подавляется; остальные source events, в том числе фоновые и изменения модели из UI observers при source → UI sync, остаются queued. Reentrant UI property events не запускают вложенный setter; по завершении записи применяется актуальное canonical source value. Это не транзакция модели.
- При setter/getter ошибке write-back пытается перечитать и применить актуальное source value, затем передаёт ошибку из Set/HandleInput. Если source уже частично изменён setter, применяется его фактическое состояние, а не предполагаемый rollback. Если readback/validation/observer cleanup также неудачны, одиночная ошибка сохраняет тип, несколько передаются AggregateException; последнее committed UI значение сохраняется там, где восстановление невозможно. Новое корректное изменение допускает recovery.
- Property change после commit выполняет invariant hook контрола, invalidation и всех подписчиков, даже когда отдельный observer бросает исключение; ошибки передаются после попыток уведомления. TextBox selection/caret остаются в пределах canonical текста и на границах графем при generic Set и normalized editing. Failed slider drag освобождает capture. Native callback boundary использует существующую передачу ошибки хосту; direct UiSession caller обрабатывает исключение.
- Registration/connection используют прежние Own/OwnAttachment, active guard и attachment-scoped Dispatch. Все source/UI subscriptions удаляются при detach/Dispose; stale queued/in-flight callbacks не читают source и не пишут в новую attachment. Потокобезопасность модели остаётся обязанностью её владельца.

Контракт реализован и проверен в подэтапе 2026-10-06: общий binding, property notification/invariant hooks, TextBox grapheme selection/caret и failed Slider capture cleanup. Конкретные команды, результаты и ограничения отдельно в STATUS; этот результат не заменяет полные control/window/alpha проверки.

### Уточнение односторонних property bindings — 2026-10-06

Первый подэтап 3.5/3.6 реализуется в Argentis без зависимостей от App, renderer или платформы. Общая цель привязок, net10.0/xUnit и критерии K1–K9 сохраняются; двусторонние и collection bindings остаются отдельными следующими подэтапами.

- Публичная Fluent API: `element.Bind(uiProperty, source, x => x.Property)`; для Text — `text.BindText(source, x => x.Name)`. Возвращается тот же конкретный тип контрола. Source — reference type с INotifyPropertyChanged; selector выбирает прямое instance property, включая стандартное приведение типа. Nested paths, поля и вычисляемые выражения явно отклоняются, автоматического наблюдения за ними нет.
- На одно UiProperty допускается одна binding registration до Dispose. Повторный Bind отклоняется без замены прежней привязки. Для этого подэтапа Unbind/замена регистрации не вводятся. Разные свойства можно привязывать независимо; DataContext не подключается автоматически.
- Регистрация живёт через Own до Dispose; connection живёт через OwnAttachment. До attach модель не читается и source subscription отсутствует. Attach/reattach синхронно считывает актуальное значение на UI-потоке и создаёт одну подписку. Detach отключает subscription, не меняя последнего значения; Move сохраняет connection.
- PropertyChanged выбранного свойства, null или пустое имя отправляет чтение через Element.Dispatch в следующий host Update, в том числе при уведомлении с UI-потока. Читается последнее состояние модели, не snapshot на момент события; getter должен быть безопасен на потоке-владельце. Прежние queued/in-flight notifications после detach/reattach/transfer не читают модель и не изменяют элемент.
- Направление только source → UI. Локальное изменение контрола не записывается в source. Validation и invalidation выполняются обычным Element.Set; одинаковое значение не вызывает redraw/property event.
- Ошибка первичной регистрации на attached element отключает её subscription и освобождает слот для повторного Bind. При неуспешном UiSession attach действуют существующие rollback/ownership правила: регистрация сохраняется для следующего attach. Ошибка getter/validation при queued update передаётся из Update; новое корректное уведомление позволяет повторить синхронизацию. Уже committed property/tree changes не откатываются из-за ошибки внешнего observer.
- Критерии этого подэтапа: initial attach, выбор уведомлений, layout invalidation, отсутствие обратной записи, повторные detach/reattach и Move, разные UI-потоки, stale queued/in-flight events, failure/retry и отсутствие повторных subscriptions. Фактические результаты и границы проверок — в STATUS.

### Этап 4. Добавить ввод, контролы и оформление

Уточнение реализации 5.1c /4.1–4.7, 2026-10-07: typed `UiProperties.Padding` и `Spacing` участвуют в local → style → theme → default. Тема содержит валидируемые immutable `ButtonPadding`, `EditorPadding`, `StackSpacing`; исходные constructor defaults сохраняются (button12/8, editor8, stack0). CheckBox добавляет24px слева для индикатора. Произвольные контейнеры по умолчанию имеют padding0; theme button/editor tokens применяются только к соответствующим controls. Width/Height/local padding/spacing и типографика продолжают перекрывать тему. Palette/density меняются в существующем дереве AlloyTest, без повторного создания формы/строк.

Для scrolling один `Element.ChildClipBounds` задаёт child viewport одновременно drawing/hit testing; ScrollViewer исключает padding из него. Wheel на границе не потребляется и может всплыть во внешний ScrollViewer. `BringIntoView(Rect)` раскрывает arranged root-coordinate bounds с clamp, oversized content выравнивается сверху; UiSession.Focus (включая Tab/Shift+Tab) применяет его от внутреннего viewport к внешнему с поправкой на фактическую смену Offset. Это не горизонтальная прокрутка и не виртуализация. Secondary pointer down/up не меняет primary Slider gesture или focus; FocusLost/disabled/detach сохраняют existing input cancellation policy.

AlloyTest показывает все заявленные control types, PNG32×32 через borrowed Image source, owned decoded source освобождается View1 после детей. Asset поставляется build/publish через PreserveNewest. VolumeMeter — sample-specific widget с собственным IDrawingContext drawing и существующим normalized ProgressBar property. Новых backend/native ABI/packages не вводится. Headless tests Compile-link точные demo sources, raster проверяет image/custom drawing и clipping; native OpenGL smoke дополняет keyboard/pointer/theme/density/scroll/disposal. OS clipboard и full shared raster/OpenGL/Vulkan/HUD/font/DPI/soak результаты учитываются отдельно в STATUS; этот функциональный этап не подменяет итоговый alpha проход.

- **4.1. Контролы первой поставки.** `Text`, `Image`, `Button`, `CheckBox`, однострочный `TextBox`, `Slider`, `ProgressBar`, `Border`, контейнеры и `ScrollViewer`.
- **4.2. Маршрутизация ввода.** Реализовать hit testing, порядок наложения, pointer capture, фокус, Tab-навигацию и активацию с клавиатуры.
- **4.3. Редактирование текста.** Разделить клавиши и текстовый ввод; поддержать латиницу, кириллицу, выделение и clipboard в `TextBox`. Согласованное уточнение 2026-10-07: Windows Get/Set используют единый Win32 путь с borrowed HWND. Get возвращает null только при отсутствии текстового формата; borrowed HGLOBAL не освобождается, lock/clipboard закрываются в finally. Set готовит UTF-16 GMEM_MOVEABLE до Open, владеет памятью до успешного SetClipboardData, затем передаёт её Windows; при отказе до передачи освобождает draft. Close failure после публикации не возвращает владение. Set заменяет содержимое clipboard текстом, откат прежних форматов после EmptyClipboard не обещается. Open допускает десять retry waits по10ms только при error5, operation/cleanup errors сохраняются. Operational errors оборачиваются в UiClipboardException с Read/Write и original cause; UiSession.HandleInput сообщает ClipboardFailed и сохраняет сессию. Прямой Get/Set, programming errors и ошибки notification handlers продолжают бросать исключения. TextBox изменяет value при Cut только после успешной записи. Clipboard доступен на owner thread до Dispose; null/embedded NUL отвергаются до native записи. GLFW error callback не меняется; другие платформы сохраняют Silk путь. Native copy/cut/paste smoke запускается только при точном заменяемом fixture `Alloy clipboard smoke fixture v1`; сохраняет/восстанавливает текст, не произвольные форматы. Controlled clipboard smoke проверяет окно/редактор отдельно от обмена с ОС. Фактические результаты и блокеры — в STATUS. Новых packages или изменения renderer ABI нет.
- **4.4. Совместная работа HUD и игры.** Возвращать хосту результат обработки события и состояние захвата мыши/клавиатуры.
- **4.5. Тема и состояния.** Добавить тему с цветами, типографикой и отступами, а также состояния hover, pressed, focused и disabled.
- **4.6. Приоритеты значений.** Локальное значение свойства имеет приоритет над стилем, стиль — над темой и значением по умолчанию.
- **4.7. Пользовательские виджеты.** Поддержать создание виджета композицией элементов либо собственным рисованием через `IDrawingContext`.
- **4.8. Статические фабрики контролов.** Добавить фабрики на типах контролов Argentis для удобной Fluent-разметки, сохранив публичные конструкторы и возможность смешивать оба способа создания. Начальные согласованные имена — `Button.Simple(string text = "")` и `Text.Plain(string value = "")`. Фабрика обычного контрола создаёт экземпляр с теми же значениями по умолчанию и текущим оформлением темы, что соответствующий конструктор; `Plain` обозначает обычный текст без обработки разметки. Для остальных контролов подобрать понятные имена при их реализации; если отдельного варианта нет, использовать `Create(...)` (например, `TextBox.Create()`, `VStack.Create()`). Имена дополнительных вариантов связывать с явно определённым внешним видом или поведением.

Приоритет 4.8 уточнён 2026-10-06: реализовать после 3.5b вместе с первой формой 5.1a, до collection bindings и завершения всего набора контролов. Принадлежность к этапу 4 и требования фабрик сохраняются.

Правила и готовность 4.8:

- Каждая фабрика объявляется на собственном типе и возвращает его конкретный тип, чтобы последующая Fluent-цепочка сохраняла все методы контрола. Для производных контролов нельзя подменять фабрику унаследованным методом, возвращающим базовый тип.
- Каждый вызов создаёт новый независимый элемент. Фабрика находится в Argentis, принимает параметры создания и не требует окна, UiSession, App/DI или графического backend. Attach/detach, ownership, свойства и динамическое дерево сохраняют принятые правила.
- Публичные конструкторы продолжают поддерживаться для обычного создания, наследования и пользовательских контролов. Фабрики и конструкторы можно использовать рядом в одном дереве; отдельного промежуточного builder не требуется.
- При реализации проверить конкретный возвращаемый тип и доступность Fluent-цепочки, независимость создаваемых экземпляров и эквивалентность обычной фабрики конструктору по исходным свойствам/поведению. Примеры и демонстрации должны показывать оба способа создания. Фактические проверки 2026-10-07 перечислены отдельно в STATUS.

Реализация 4.8 / 5.1a — 2026-10-07: доступны Button.Simple, Text.Plain и собственные Create на TextBox, CheckBox, ProgressBar, Slider, Image, Panel, ContentControl, Border, ScrollViewer, VStack, HStack, Line и Rectagle. Производные Slider/Border/ScrollViewer явно скрывают базовый Create, возвращая свой тип; это не virtual factory и не способ создания пользовательских наследников. Shapes и public constructors сохраняются; Line.Create не добавляет рисование к прежнему пустому Line. AlloyTest получает SettingsModel через DI, редактирует три свойства через BindTwoWay и показывает Summary через BindText; две кнопки меняют модель без перестройки дерева. После первоначального native0xC0000374 диагностика выявила input cleanup после уничтожения GLFW window. Этот lifetime defect исправлен; UI188/188, по10 form smoke с отладочной кучей и по6 lifecycle сценариев в Debug/published Release прошли. Связь исходного неперехваченного0xC0000374 с найденным дефектом — гипотеза, не отдельный полученный стек; long soak/alpha остаются впереди. Цели, зависимости, ownership и K1–K9 не меняются; следующий ближайший шаг — 3.5c/d / 5.1b.

Уточнение window lifetime, 2026-10-07: SilkWindowHost.Run завершает цикл без Reset, сохраняет native window/context и input до host.Dispose на том же потоке. Hosting освобождает renderer/session, input снимает GLFW callbacks с ещё живого окна, затем host уничтожает окно. OpenGL session по-прежнему освобождается в Closing; Render/Input callback errors перехватываются до выхода через native boundary. Прямой NativeWindow.Reset/Dispose до host.Dispose нарушает ownership. Это исправление teardown порядка внутри Platform.Silk, без новых зависимостей или изменений backend/Argentis tree contracts.

Результат — интерактивный базовый набор контролов, работающий через общую сессию UI в обоих сценариях запуска.

### Подзадача 4.9. Прозрачные окна и собственное оформление — добавлена 2026-10-08

По запросу пользователя расширить первую рабочую версию поддержкой прозрачных/полупрозрачных standalone-окон, отключения стандартной рамки и заголовка, а также опционального системного поведения при перетаскивании собственного заголовка. Отсутствие рамки, способ прозрачности и системное поведение настраиваются раздельно. Обычное окно по умолчанию сохраняет стандартное оформление и непрозрачность.

- **4.9a. Проверка оконной композиции.** До реализации публичного API сделать минимальный Windows x64 prototype на текущих OpenGL/Vulkan presentation путях: непрозрачное окно, общая opacity, per-pixel alpha0/0.5/1 поверх видимого фона другого окна. Проверить фактическую поддержку desktop composition, alpha format/blending и для Vulkan supported composite alpha; прозрачная offscreen UI-текстура/HUD не доказывает прозрачность standalone-окна. Зафиксировать capability matrix и ограничения Silk/GLFW/driver. Если текущий presenter не поддерживает требуемый режим, явно спроектировать отдельный Windows composition adapter; не переключать backend и не объявлять непрозрачный fallback успехом молча.
- **4.9b. Прозрачность.** Предусмотреть явные режимы opaque, общая непрозрачность окна (opacity0–1) и попиксельная прозрачность с полупрозрачным UI/непрозрачными контролами. Валидация и reporting неподдерживаемых сочетаний обязательны. Для текущего GLFW пути общую opacity и transparent framebuffer не включать одновременно: документация объявляет результат неопределённым. Поведение ввода в прозрачных областях определить отдельно и проверить нативно; визуальная alpha сама по себе не является контрактом click-through. Управляемый click-through, blur/Acrylic/Mica и тени DWM — отдельные возможные расширения, в обязательный объём4.9 не входят.
- **4.9c. Окно без системного оформления.** Отключаемая системная рамка/заголовок, собственный заголовок из Argentis-элементов и команды minimize/maximize/restore/close через оконный контракт. Явно определяемые drag regions и исключённые интерактивные области: кнопки, TextBox и другие controls не начинают перемещение окна. Для resizable окон определить невидимые границы/углы resize; учитывать min/max размеры, maximized work area и DPI. Визуально окно остаётся без системного оформления; необходимые Win32 styles выбираются с сохранением системного поведения.
- **4.9d. Опциональное системное перемещение и Snap.** Для собственного заголовка предоставить opt-in native move/resize: системная drag loop, Snap к краям/углам и Snap bar/layouts на поддерживаемой Windows11 при включённых пользователем настройках. Восстановление maximized окна перетаскиванием, double-click maximize/restore и системное меню должны сохранять ожидаемое поведение. Если есть собственная кнопка maximize/restore, поддержать Windows11 Snap Layouts при наведении и Win+Z. Направление реализации — Win32 non-client hit testing (`HTCAPTION`, resize codes, `HTMAXBUTTON`) и системная обработка сообщений; точный механизм согласовать после prototype, не имитировать системные previews рисованием в UI. Уважать Windows settings, не менять их; Aero Shake проверять отдельно при включённой настройке. Peek обозначает просмотр рабочего стола и не является названием Snap при перетаскивании.

**Границы архитектуры.** Конфигурация и capability/команды доступны через Hosting и независимый оконный контракт Alloy; HWND/DWM/message hooks и их owner-thread lifetime остаются в Platform.Silk/Windows adapter, выбор alpha/presentation — в Rendering/Hosting. Argentis описывает собственный заголовок через независимые роли/геометрию, без Win32/Skia/Silk типов. Конкретные имена API пока не согласованы. Внешний HUD не меняет оформление окна игры. Native callbacks/hooks снимаются до уничтожения HWND; исключения не выходят через native boundary. net10.0, xUnit, retained дерево и ownership сохраняются.

**Порядок и готовность.** Ближайший performance profile/подтверждённая оптимизация K9 сохраняются; затем4.9a,4.9b/c/d и оконная демонстрация до итогового alpha прохода5.5. Это явное расширение объёма первой версии: к K3 добавить прозрачность/собственный заголовок/native Snap, к K6/K7 — validation и lifecycle новых presentation/hooks. Проверять OpenGL/Vulkan на заявленной конфигурации, реальные desktop alpha, drag/excluded controls/resize/buttons/Snap, включённые и выключенные системные настройки, DPI100/150/200% и переход между мониторами, minimize/zero-size/restore, повторные create/close без retained callbacks/resources; выполнить Debug/published Release smoke и регрессию обычного окна/HUD. Прототип, реализация и прошедшие проверки фиксируются отдельно; неподдержанный обязательный режим остаётся блокером4.9, изменение требований фиксируется явно.

Источники для реализации: [GLFW: прозрачность окна](https://www.glfw.org/docs/latest/window_guide.html#window_transparency), [Win32 custom frame/DWM](https://learn.microsoft.com/en-us/windows/win32/dwm/customframe), [WM_NCHITTEST](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-nchittest), [Snap Layouts для custom title bar](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/ui/apply-snap-layout-menu), [Snap/Snap bar и настройки Windows](https://support.microsoft.com/en-us/windows/experience/snap-your-windows).

**Исторический результат 4.9a, 2026-10-08.** В Debug/published Release проверена реальная desktop-композиция поверх другого окна: OpenGL поддержал opaque/общую opacity/per-pixel alpha; Vulkan поддержал opaque/opacity, но не per-pixel ни с обычным Opaque, ни с internal PreMultiplied experiment. Driver объявляет Opaque|PreMultiplied, однако это не доказало прозрачность HWND. Публичный default presenter сохраняет Opaque-first; эксперимент не является публичным API или принятым режимом поставки. Подробные raw RGB, конфигурация и регрессия — [STATUS](STATUS.md).

**Пересмотр направления 4.9a.1 после изоляции, 2026-10-08.** Вывод о необходимости отдельного composition adapter был преждевременным: отсутствие desktop alpha в двух проверенных конфигурациях не исключало проблему HWND/DWM. По согласованной проверке сначала выполнен Vulkan proof без native Skia: `vkCmdClearColorImage` → существующий GPU blit/presenter → GLFW swapchain. В Debug/published Release transparent framebuffer + PreMultiplied в decorated окне повторил смешивание с белым; `PatBlt(BLACKNESS)` клиентского DC после show/resize устранил его. Alpha 0/128/255 над двумя фонами прошла initial/resize/restore; тот же эксперимент с настоящим Skia UI также прошёл. Borderless raw Vulkan прошёл на этой машине и без очистки; это наблюдение не отменяет явной инициализации redirection surface. Источники гипотезы: [GLFW PR #2815](https://github.com/glfw/glfw/pull/2815), [PatBlt](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-patblt). Фактические измерения и ограничения — STATUS.

1. Следующий шаг — **4.9b: оконный configuration/capability contract и интеграция подтверждённого GLFW/Windows пути**. Для per-pixel явно запросить TransparentFramebuffer до создания окна, проверить supported composite alpha и выбрать PreMultiplied для premultiplied output. Windows adapter должен обеспечивать инициализацию redirection surface в нужные моменты lifecycle; выбирать механизм WM_PAINT/resize после проверки, а не копировать диагностическую очистку перед каждым frame. HWND/DC/hooks и owner-thread cleanup остаются в Platform/Hosting, Argentis/HUD не получают Windows типов.
2. Сохранить обычный opaque путь и отдельную whole-window opacity; неподдержанный per-pixel сообщает причину, без скрытого fallback. Обычный VkSwapchainKHR используется для подтверждённого режима, SkiaVulkan renderer и UI image ownership сохраняются. Readback в prototype проверяет источник и не участвует в desktop presentation.
3. Повторить desktop proof с публичной конфигурацией: оба backend, decorations on/off, resize/minimize/restore, repeated create/close, DPI/monitor transitions и регрессия обычного окна/HUD. Диагностический успех ещё не означает готовность lifecycle/configuration/input контракта. Borderless title bar/native Snap 4.9c/d следуют по прежнему порядку.
4. Layered HWND/UpdateLayeredWindow с явным CPU transfer и GPU DirectComposition/DXGI interop остаются резервными направлениями только для конфигураций, где существующий GLFW/Vulkan путь не проходит проверку. Их реализация сейчас не требуется; для любого будущего transport отдельно подтвердить capabilities, стоимость, ownership и cross-API completion.

Это явное изменение порядка технической реализации после нового desktop proof. Цели Vulkan/per-pixel, net10.0/xUnit, retained UI, HUD ownership и критерии готовности 4.9 сохраняются.

**Принятый и реализованный контракт 4.9b, 2026-10-08.** Alloy содержит immutable `WindowAppearance` (`Transparency`: Opaque/Opacity/PerPixel, initial `Opacity`, `Decorated`) и необязательный `IWindowAppearanceHost`, не меняющий существующий IWindowHost. Параметры валидируются до native allocation; finite opacity0–1 допускается только в Opacity режиме, другие режимы требуют initial opacity1. Native `WindowAppearanceCapabilities` отдельно сообщает transparent framebuffer и uniform opacity; Vulkan presenter отдельно проверяет supported composite alpha. Hosting выбирает premultiplied для PerPixel и opaque для остальных явных режимов, без скрытого fallback. No-argument Vulkan presenter сохраняет прежнюю alpha precedence для существующих callers/HUD. HostedUiWindow фиксирует snapshot options, сообщает combined per-pixel support после startup и ставит SetOpacity в очередь owner thread. Прямой SilkWindowHost принимает те же appearance settings и предоставляет thread-bound SetOpacity.

Windows per-pixel adapter использует SetWindowSubclass, сохраняет GLFW WndProc и обрабатывает creation/WM_PAINT/WM_SIZE/show/DPI/composition события. BLACKNESS очистка выполняется до GLFW damage callback, а не на каждом presentation frame. HWND заимствован, client DC имеет SafeHandle lifetime; static callback и GC root живут до RemoveWindowSubclass/WM_NCDESTROY. Ошибки native callback сохраняются для последующего managed reporting; hooks снимаются на owner thread до HWND destruction. Политика per-pixel input — обычная client-area hit testing, alpha не включает managed click-through. На проверенной Windows alpha0/128/255 области по WindowFromPoint принадлежат окну; полный native mouse interaction и другие configurations остаются отдельными проверками. Uniform opacity пока заявлена в adapter только для Windows.

`Decorated=false` в4.9b реализовал создание без системной рамки. Принятый ниже4.9c добавляет собственный Argentis title bar/команды/resize regions; полная native DPI/monitor/Snap matrix остаётся открыта. Реализация4.9b отделена от её полной переносимой верификации: фактические Debug/published Release, source/desktop/input/lifecycle checks и промежуточные failures — STATUS. net10.0/xUnit и исходные критерии готовности сохраняются.

**Принятый контракт4.9c, 2026-10-08.** Необязательные независимые `IWindowCommands` и `IWindowChromeHost` предоставляют state/minimize/maximize/restore/close, capabilities и публикацию immutable `WindowRegionMap`. `WindowChromeOptions` задаёт NativeDrag (defaultfalse), Resizable, DPI-aware resize border и min/max client sizes. Chrome opt-in требует Windows x64 и Decorated=false; null сохраняет исходный GLFW behavior. У Argentis появился WindowRegionRole Inherit/Caption/Client и WindowFrame с отдельной высотой заголовка; TitleBarFactory Hosting создаёт Argentis content на owner thread с независимыми commands. UI session владеет title/content, HUD не меняется.

Геометрия копируется с учётом paint order/clipping/visibility после update. Focusable controls (включая disabled) и explicit Client subtree исключены из перемещения; custom non-focusable input controls должны явно задавать Client. Native callback не вызывает tree/пользовательские delegates; stale caption map после resize не применяется. Windows adapter сохраняет SYSMENU/minimize/maximize/THICKFRAME semantics по настройкам, снимает видимую рамку через WM_NCCALCSIZE и возвращает native HTCAPTION/resize codes. WM_GETMINMAXINFO использует work area текущего монитора и application limits; resize через оконный контракт задаёт точный client size, обходя GLFW стандартный frame adjustment. Resize border масштабируется по GetDpiForWindow, limits/regions — по существующему UI viewport scale; общая DPI-модель не меняется. Hook/GC root lifetime и reporting callback errors сохраняют правила4.9b.

Это уточняет границу4.9c/d: базовый opt-in native move/resize нужен уже для4.9c;4.9d завершает Snap Layouts/Win+Z/maximize-button hit testing, menu/double-click/maximized drag и реальные interaction/settings/DPI/monitor проверки. Native previews не рисуются вручную. Наличие API и directed-message/desktop proofs не означает прохождение всего4.9 или alpha; фактические результаты — STATUS.

**Реализованный контракт4.9d, 2026-10-08; bounded physical/desktop proof выполнен, полная matrix остаётся открытой.** `WindowChromeOptions.NativeSnapLayouts` — отдельный opt-in (default false), требует Resizable и Windows11; platform capability `SnapLayouts` не означает включённые пользовательские настройки. Явная Argentis роль `Maximize` на enabled кнопке даёт `HTMAXBUTTON`, disabled/Client ancestor сохраняют Client. Non-client hover/leave передаётся DWM; press/release/cancel копируются в очередь UiInput и доставляются вне native callback. Командой владеет Argentis OnClick, native default maximize на press подавляется; Space/Enter сохраняются. Direct Silk hosts должны вызвать DispatchPendingWindowInput после DoEvents, Hosting делает это автоматически.

`SystemMenu` default true: Alt+Space, caption right-click и optional `IWindowSystemMenu.ShowSystemMenu(x,y)` с logical client point, queued equivalent в Hosting. Disabled menu не открывается; native menu учитывает state/Resizable и отправляет выбранный system command самому окну. Caption double-click делегируется системе. Пример использует adaptive header и minimum330×300 вместо760×400 для узких Snap zones. Это уточнение API/демонстрации не сокращает критерии готовности4.9d: real system move/resize, maximized drag, edge/corner/bar/hover/Win+Z, settings on/off, Aero Shake, physical DPI/monitor и lifetime требуют отдельного подтверждения. Исторический LockApp foreground/desktop blocker и новые physical результаты — STATUS; не считать направленные сообщения доказательством Shell Snap UI.

**Уточнение native styles после physical prototype4.9d, 2026-10-08.** Для custom frame снимается GLFW `WS_POPUP`: окно использует overlapped semantics с SYSMENU/minimize/THICKFRAME/maximize по настройкам, без `WS_CAPTION`. Caption regions задаются `HTCAPTION`; полноразмерный client сохраняется через `WM_NCCALCSIZE` с wParam=true. Добавление WS_CAPTION оказалось ненужным для Snap и добавляло невидимые maximize margins; этот вариант отвергнут. Рабочая область/max-size контракт4.9c сохраняется. Проверки видимого оформления используют actual HWND/client geometry, а не один style bit. Alt+Space обрабатывается также на WM_SYSKEYDOWN с Alt context и без autorepeat, до стандартного GLFW keyboard-menu/WM_SYSCHAR suppression; глобальные hotkeys ОС/других приложений имеют приоритет. Native Snap previews остаются ответственностью Windows. Фактические physical/desktop proofs и оставшиеся settings/DPI/monitor критерии — STATUS; прежний LockApp blocker относится к историческому запуску.

### Этап 5. Собрать демонстрации и подготовить alpha

- **5.1. Самостоятельное приложение.** Развивать один AlloyTest вместе с API: **5.1a** — форма с редактируемой моделью, TextBox/CheckBox/Slider и смешанным созданием через фабрики/конструкторы; **5.1b** — динамический список и условные области; **5.1c** — прокрутка, смена темы и полноценные взаимодействия всех заявленных контролов. Добавление/удаление, редактирование и переключатели должны сохранять состояние незатронутых элементов. Пример становится общей контрольной сценой для standalone, backend comparison и адаптируемого HUD.
- **5.2. Vulkan HUD.** Добавить показатели, динамический список и интерактивную панель поверх сцены.
- **5.3. Обновление по необходимости.** Самостоятельное приложение перерисовывается при изменениях и анимациях; HUD обновляет UI-текстуру по необходимости, а композицию выполняет хост.

Уточнение реализации общей сцены5.1c/5.2–5.3, 2026-10-07: AlloyTest сохраняет View1/SettingsModel и выбирает OpenGL по умолчанию или Vulkan через `--vulkan`. AlloyVulkanTest compile-link исходники той же формы, модели, generated rows/условной области/VolumeMeter и SettingsFormSmoke, доставляет тот же PNG; новой shared assembly или зависимости на manual executable нет. Один interaction script используется на raster/OpenGL/Vulkan при одинаковых logical viewport/scale1/1.5/2 и в Vulkan HUD. HUD оборачивает ту же форму прозрачным Panel, добавляет показатели/кнопку сцены, использует borrowed raw Vulkan handles и host-owned composition/presentation; вне панели ввод проходит в сцену. Хост композитит retained UI независимо от UiSession.Update, модель сохраняется при явном пересоздании UI. Нового runtime/backend/native API не вводится. Сравнение всех logical bounds/model/input steps и выбранных RGBA pixels с допуском1 — функциональный smoke, не fixed-font/full screenshot golden. Фактические проверки и K2/K3/K7–K9 ограничения находятся в STATUS. Следующий этап по согласованному порядку —2.4/K8: контракт ошибки backend и явного пересоздания из модели.

- **5.4. Документация.** Описать запуск, создание виджета, привязки, подключение бекенда, потоковую модель и владение GPU-ресурсами.
- **5.5. Выпуск.** Выпустить alpha после прохождения критериев готовности ниже.

## Проверки и критерии готовности

- **K1. Автоматические проверки ядра.** Тесты дерева, layout, свойств, привязок, коллекций, фокуса и маршрутизации ввода без GPU.
- **K2. Рендеринг.** Проверки на raster-поверхности с фиксированными шрифтами; сравнение GPU-результатов с допустимой погрешностью.
- **K3. Windows.** Проверки resize, сворачивания, восстановления и DPI 100/150/200%; отсутствие рисования в поверхность нулевого размера.
- **K4. Сменяемость бекендов.** Одинаковое поведение демонстрационного UI через Vulkan и OpenGL.
- **K5. HUD.** Необработанный ввод проходит в сцену; сохраняется прозрачность; ресурсы хоста не уничтожаются библиотекой.
- **K6. Vulkan validation.** В демонстрационных сценариях нет ошибок синхронизации и времени жизни ресурсов.
- **K7. Повторное создание сессий.** Создание и закрытие сессий не вызывает накопления ресурсов или подписок.
- **K8. Потеря устройства.** Сессия сообщает ошибку хосту и допускает пересоздание. Автоматическое бесшовное восстановление не входит в первую версию.
- **K9. Производительность.** Зафиксировать время layout/отрисовки и аллокации на контрольной сцене. Статичный интерфейс не должен непрерывно перестраивать дерево и создавать графические ресурсы.

### Воспроизводимый итоговый проход alpha

Метод baseline K9 уточнён и реализован 2026-10-07: shared Settings View1,32 строки/195 исходных элементов (до200 при append), view720×1000/viewport900×1100/scale1, static/property-edit/list-edit на raster/OpenGL/Vulkan и Vulkan HUD. Два независимых Release-процесса, warmup≥100frames и≥500ms на case,500 samples; validation выключен при timing и включён в отдельных correctness checks. Owner-thread managed allocations и CPU wall time Update/MeasureCore/ArrangeCore/Render измеряет manual profiling root/surface proxy; вложенные фазы не суммируются повторно. GL completion, Vulkan export/return, HUD composition/presentation учитываются отдельно; GPU timestamps/native/VRAM не измеряются. Raw samples и source/binary/font/native/config fingerprints сохраняются в JSON, итоги — [PERFORMANCE_BASELINE.md](PERFORMANCE_BASELINE.md). Статичный UI проверяет сохранение tree/surface/image,0 layout/draw/Update allocations. Архитектура/runtime API и критерии K1–K9 сохраняются; performance budget не назначен. Следующая оптимизация выбирается после CPU/allocation profile выявленной фазы и проверяется повтором baseline.

Критерии K1–K9 сохраняются; функциональные результаты отдельных подэтапов не заменяют итоговый проход с актуальной демонстрацией. Зафиксировать поддержанную конфигурацию Windows x64, GPU/driver, SDK/packages/native revisions, команды и версии артефактов. Изменение конфигурации требует явно ограничить область вывода. Другие OS/GPU/EXT-only конфигурации — отдельные исследования по выявленным рискам.

- **K1 / controls.** На финальном коде выполнить UI suite, включающий two-way/collections/dynamic regions и реальные interactions всех заявленных контролов. Указать summary и ограничения; failures полного solution suite разбирать отдельно, не скрывая их результатами UI suite.
- **K2/K4.** Один view/model сценарий на raster/OpenGL/Vulkan: фиксированные доставляемые шрифты, одинаковые logical bounds и input results, прозрачность/clipping/text/images и документированный допуск сравнения pixels. Selected-pixel smoke отдельно от полного набора golden fixtures.
- **K3/K5/K6.** Resize/minimize/zero-size/restore и DPI 100/150/200%, включая переход DPI окна; HUD event handling/passthrough и сохранение ресурсов игры. На поддержанном Vulkan пути validation без ошибок синхронизации/lifetime, включая teardown.
- **K7.** Повторяемое создание/закрытие/resize с заранее фиксированным числом циклов; после завершения отсутствие накопления owned ресурсов, source subscriptions и retained roots. Counters не объявлять полным VRAM/driver профилем.
- **K8 / 2.4.** Определить контракт передачи backend/device/context ошибки хосту; прекратить недопустимую работу, выполнить безопасный cleanup и явно создать новую сессию/дерево из сохраняемой модели. Ошибка и recreate должны иметь воспроизводимый failure scenario с указанием, что проверялось реальной потерей устройства, а что контролируемой инъекцией. Автоматическое бесшовное восстановление не включается.
- **K9.** Зафиксировать сцену и размеры, число элементов, static/property-edit/list-edit режимы, warmup и число samples; измерить layout/draw time и allocations. Static UI не выполняет повторный layout/draw и не создаёт новые UI graphics resources; HUD composition и оконный presentation учитываются отдельно. Текущая invalidation приводит к layout от корня, когда layout требуется; dirty-subtree layout/partial redraw не обещаются. Оптимизацию выбирать после baseline и повторного измерения выявленной проблемы.
- **5.4/5.5 / поставка.** Debug/Release publish smoke на заявленной конфигурации, создание view из отдельного consumer с явными references и проверка доставки native assets. Проверить package/NuGet consumer для выбранного способа поставки и описать alpha migration/compatibility ограничения. Исторический успешный publish не заменяет проверку итогового артефакта.

Уточнение K9,2026-10-08: после sampled managed-stack/allocation profile реализован surface-owned paint/FIFO16fonts reuse для общего Skia drawing/text adapter, с owner-thread/lifetime guards и cleanup при eviction/disposal. Свежие baseline до/после и UI/native regression — [PERFORMANCE_BASELINE](PERFORMANCE_BASELINE.md#профиль-и-оптимизация-2026-10-08)/STATUS; это одна ограниченная оптимизация, без dirty-subtree/partial redraw/text-blob cache, public API/native ABI/package изменений. Следующий этап по согласованному порядку —4.9a. Alpha/K1–K9 сохраняются; первый нерегулярный standalone smoke failure учитывается отдельно от повторных успешных checks.

## Допущения и границы первой версии

- Использовать текущий `net10.0` и принятый в решении xUnit.
- Допускаются изменения API прототипа с обновлением примеров.
- Переключение бекенда происходит при создании сессии.
- Поддерживаемая ОС первой версии — Windows. Vulkan HUD проверяется небольшим самостоятельным хостом.
- Linux/macOS, нативные системные контролы, IME, полноценная accessibility-интеграция, сложный редактор текста, виртуализация, docking и дизайнер относятся к следующим этапам.
- Сроки оценивать после графического прототипа, снимающего основную неопределённость интеграции Skia/Vulkan.

## Развитие UI после alpha

Согласовано с пользователем **2026-10-09**. Этот раздел фиксирует будущие работы над TrueMoon.Alloy и TrueMoon.Argentis; статус всех новых пунктов — **запланировано**. Требования alpha, её критерии K1–K9 и текущая точка продолжения по DPI сохраняются. Конкретные публичные API и сроки уточняются при начале соответствующего подэтапа.

### Цель beta и контрольное приложение

Развивать UI на одном регулярно используемом приложении: редакторе настроек и ресурсов с деревом навигации, списком и фильтрацией, формой свойств и журналом операций. Приложение развивается вместе с библиотекой и показывает, где создание UI требует лишней инфраструктуры. Его повторяемые сценарии становятся проверками поведения, диагностики и производительности.

Приоритет первой beta — контролы, формы и инструменты диагностики. Целевой результат: приложение с меню, диалогами, редактируемыми формами и большим списком, которое удобно писать на Alloy и диагностировать. Виртуализация сначала вводится для одного списка контрольного приложения; развитие деревьев и таблиц идёт следующими подэтапами. Сроки и объём остальных направлений определяются по потребностям приложения.

### Направления и порядок работ

| Пункт | Приоритет | Планируемый результат |
| --- | --- | --- |
| PA1. Контрольное приложение | Начало beta | Навигация, фильтрация, редактирование и журнал операций на общей модели; воспроизводимые пользовательские сценарии и перечень неудобств API. |
| PA2. Инструменты разработчика | Первая beta, начиная с PA1 | Инспектор дерева и выбранного элемента: bounds, clipping, фокус, capture, источники свойств, причины invalidation и timings layout/draw. |
| PA3. Общие overlays и команды | Основа контролов первой beta | Единые правила popup/menu/tooltip/dialog для порядка рисования, clipping, размещения, маршрутизации ввода и возврата фокуса. Команды доступны кнопкам, меню и shortcuts, сообщают возможность и состояние выполнения. |
| PA4. Контролы и навигация | Первая beta после PA3 | Menu, context menu, ComboBox, tabs, dialogs, popup/tooltip; согласованная навигация с клавиатуры и shortcuts. Набор вводится по сценариям PA1. |
| PA5. Формы и модель | Первая beta | Валидация, преобразования значений, режимы commit; async loading/cancellation и состояния выполнения без ручного повторения инфраструктуры в каждой форме. |
| PA6. Большие данные | Список в первой beta; дерево и таблицы далее | Виртуализированные списки и деревья, затем таблицы; стабильные ключи, сохранение выделения, фокуса и прокрутки при обновлении и повторном использовании элементов. |
| PA7. Текст и доступность | Клавиатура вместе с PA3–PA5; отдельные следующие подэтапы | IME/composition, развитие текстового ввода, semantic tree и platform accessibility adapter для повседневной работы и автоматизации. |
| PA8. Оформление | После определения поведения новых контролов | Системные theme tokens, templates контролов, состояния, transitions и animations; разные визуальные стили поверх общего поведения. |
| PA9. Vulkan/HUD | Отдельная ветка по реальной интеграции с движком | Асинхронный обмен кадрами, несколько frames-in-flight и явные completion tokens с новым контрактом владения ресурсами. |
| PA10. Производительность | По измерениям PA1 и последующих сценариев | Виртуализация, кеширование текста, уменьшение повторного layout и рисования; для каждого изменения профиль, baseline до/после и проверки корректности. |

### Архитектурные ориентиры

- **Постоянное дерево и состояние.** Сохранить текущую модель элементов, точечных обновлений и явного lifetime. Повторное использование визуальных элементов при виртуализации требует определённых правил смены модели, подписок, выделения, фокуса и capture. Стабильные ключи коллекций проектируются отдельно от существующего BindItems с reference identity.
- **Overlays.** Сначала определить общий механизм, затем строить меню, popup, tooltip и dialog. Уточнить размещение у границ поверхности, порядок рисования относительно обычного контента, modal/input policy, закрытие и восстановление фокуса. Общий runtime должен работать в standalone и HUD; оконные возможности запрашиваются через независимые контракты.
- **Команды.** Кнопка, меню и shortcut вызывают одну операцию и используют одно состояние доступности/выполнения. Async-команды определяют cancellation и lifetime; изменения UI доставляются на owner thread. Существующие обработчики событий и bindings сохраняют явные правила владения.
- **Forms.** Определить правила conversion, validation, commit/cancel, нормализации модели и показа ошибок до расширения binding API. Async loading задаёт поведение устаревших результатов, cancellation и detach/dispose.
- **Templates и оформление.** Отделить заменяемое визуальное представление контрола от его поведения. Template сохраняет ввод, фокус, доступность и состояния; Fluent API остаётся понятным при композиции и настройке. Для animations определить scheduler, invalidation и остановку при detach/dispose.
- **Текст и accessibility.** Argentis/Alloy содержат независимые input/semantic контракты; native IME и accessibility adapters остаются в платформенной интеграции. Навигация с клавиатуры и focus policy входят в разработку новых контролов с самого начала.
- **Диагностика.** Инспектор показывает актуальные данные runtime и происхождение свойств. Определить lifetime диагностических подписок, чтобы инспектор не удерживал закрытые сессии и элементы.
- **Vulkan/HUD.** PA9 проектирует версионируемое расширение последовательного GPU-контракта: ownership каждого кадра, completion, image layout, reuse/retirement, resize и failure cleanup. Потребность и приоритет проверяются на конкретном engine host. Простое подключение standalone и существующий последовательный путь сохраняются.
- **Производительность.** PA10 опирается на замеры контрольного приложения. Кеш текста и ограничение layout/redraw выбираются по подтверждённым затратам; managed allocations, native ресурсы, GPU completion и presentation учитываются раздельно.

Текущие границы модулей, net10.0/xUnit и владение деревом/ресурсами остаются исходной архитектурой. Перечисленные ориентиры требуют уточнения контрактов и проверок при реализации; добавление раздела не утверждает готовность этих механизмов.

### Планируемые критерии первой beta

- Контрольное приложение проходит повторяемые сценарии меню/диалогов, редактирования с validation/commit/cancel, async операции и большого виртуализированного списка.
- Новые controls/overlays/commands имеют согласованные keyboard/focus/capture/disabled правила; закрытие popup/dialog возвращает фокус, detach/dispose освобождает подписки и отменяет связанную работу.
- Основные общие сценарии подтверждены на raster/OpenGL/Vulkan; переносимый набор контролов и overlays проверен в HUD с явной политикой передачи ввода игре. Фиксированные шрифты и допустимая погрешность сравнения сохраняют требования alpha.
- Инспектор объясняет layout, источники свойств и invalidation; диагностические подписки освобождаются при закрытии UI.
- Для большого списка и обычных форм есть воспроизводимые timings/allocations/resource baseline; static UI сохраняет отсутствие лишнего layout/draw. Число элементов, workload и бюджеты уточняются при подготовке PA1/PA6.
- Документированы создание приложения, новые контракты, compatibility/migration и поставка отдельному consumer. Реализация, фактически прошедшие проверки и ограничения каждого подэтапа отражаются отдельно в STATUS.

IME/accessibility, развитие trees/tables, templates/animations и асинхронный GPU-контракт имеют собственные критерии при старте подэтапов. Их порядок и включение в конкретный выпуск после первой beta уточняются по результатам контрольного приложения.

## Решения и свидетельства из восстановленных логов

Эти записи сохраняют контекст предыдущей сессии. Проверенное состояние текущего дерева находится в [STATUS.md](STATUS.md).

1. Исходный Alloy собирался, но содержал заглушки коллекций и layout; началась переработка ядра.
2. Сообщалось о доступности Vulkan и двух GPU на машине.
3. Сообщалось об отсутствии публичного API обмена Vulkan-семафорами в установленной версии SkiaSharp. Для первой рабочей интеграции была выбрана последовательная передача ресурса с ожиданием завершения GPU. Ограничение по параллельности должно быть отражено в API и документации. Это решение не снимает требований к владению ресурсами и переходам состояния изображений; ограничение API нужно проверить для фактически используемой версии перед реализацией контракта.
4. Первый прототип успешно создал Vulkan-контекст Skia и проверил цвет и прозрачность пикселей на GPU.
5. Последняя заявленная точка работы: независимые контракты Argentis и постоянное дерево элементов; затем подключение layout, ввода и рендерера.

## Поддержание плана между сессиями

Перед работой читать этот план и [STATUS.md](STATUS.md); для межмодульных задач также [общий план](../PLAN.md). После завершённого изменения обновлять дату сверки, статусы затронутых пунктов, результаты фактически выполненных проверок и следующий конкретный шаг. При изменении архитектурного решения фиксировать причину и затронутые критерии готовности. Длинные записи запусков хранить в [HISTORY.md](HISTORY.md), текущий статус оставлять кратким. Общие build/test/release вопросы обновлять в общем STATUS по [правилам](../PLANNING.md).
