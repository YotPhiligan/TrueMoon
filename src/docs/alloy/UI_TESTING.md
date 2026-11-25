# Проверки UI на Alloy / Argentis

Принято 16.11.2026. Переиспользуемая инструкция для фреймворка и его consumers.
Применять перед изменением View, ввода, layout, оформления или окружения окна.
Явные требования задачи к настоящему окну/платформе сохраняют приоритет.

По умолчанию начинать с детерминированных проверок настоящего UI-дерева без
Computer Use, добавляя CPU raster и нужные интеграционные проверки. Выбирать
уровень по проверяемому контракту. Наличие тестового host или PNG не означает,
что уже проверены реальные Windows messages, GPU present или физический DPI.

## Матрица проверок

| Уровень | Что остаётся настоящим | Что можно подменять | Граница вывода |
|---|---|---|---|
| Данные/команды | Прикладные правила и observable state | I/O, clock, remote service по контракту задачи | Не проверяет дерево и маршрутизацию UI |
| UI interaction | Argentis View/controls, UiSession, layout, hit testing, focus/capture и routed input | IUiRenderSurface/TextLayout, clipboard, внешние сервисы | Fake text metrics и пустой Render не доказывают изображение |
| CPU raster | То же дерево и Skia drawing/text adapter | Native window, GPU, внешние сервисы | Проверяет CPU-изображение; логический scale не равен physical DPI |
| Окружение окна | Window, AttachHost, состояния/close handlers и UiSession | IWindowController и публикация host state | Подтверждает контракт UI↔тестовый host, не поведение ОС |
| Сквозной consumer | UI и выбранные реальные App/DI/IPC/process/storage services | Только окружение, исключённое scope | Состояние UI дополнять проверкой реального результата и cleanup |
| Native/GPU | Поддержанный platform host, renderer/presenter и реальный процесс | Тестовые данные и сервисы вне scope | Только указанные OS/RID/backend/device/input scenarios |

Computer Use — один из способов ручной/внешней автоматизации, а не зависимость
UiSession и не обязательный этап каждого UI-теста. Собственным native harness
можно управлять через API его хоста на owner thread и проверять результаты
программно. Сам факт создания окна не подтверждает успешный present.

## Поведение и ввод

Создать тот же View через штатную DI/IFactory, что использует consumer. В fixture
подменять зависимости в composition root; не обходить UI командами ViewModel,
если проверяется именно взаимодействие. Контролы и дерево оставлять настоящими.
Для изолированного дерева не запускать native presentation из App-конфигурации.

При проверке прямого внедрения дочерних View создавайте родителя через фабрику,
зарегистрированную `.View<TParent>()`. Отдельно проверяйте освобождение детей при
сбое до присоединения и передачу владения после принятия в дерево.

1. Выделить UI owner thread, задать размер/scale, подготовить модель и выполнить
   UiSession.Update до проверки геометрии и ввода. После resize повторить layout.
2. Передавать UiInput через UiSession.HandleInput. Координаты выводить из
   расположения нужного контрола; проверять, что hit target соответствует сценарию.
3. Проверять наблюдаемое состояние до действия, во время gesture, после команды
   и после повторного чтения/кадра, если операция асинхронная.
4. Проверять результат и отсутствие лишних действий: ровно одна команда, no-op,
   отмена, сохранность чужого состояния, недоступность запрещённых действий.
5. Включать отдельные error/timeout/cancel/reconnect paths. Не считать один
   параметризованный happy path доказательством всех состояний.

Например, fixture может воспроизвести удержание указателя без системного мышиного
ввода. В этом фрагменте x/y и dx — проверенные координаты конкретной сцены:

```csharp
session.HandleInput(new UiInput(InputKind.PointerDown, x, y));
session.HandleInput(new UiInput(InputKind.PointerMove, x + dx, y));
session.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Escape));
session.HandleInput(new UiInput(InputKind.PointerUp, x + dx, y));
session.Update();
```

Escape, FocusLost, потеря capture, скрытие/disable, resize и Dispose — разные
причины отмены. Для каждого требуемого пути проверять захват, черновик, UI state
и побочные эффекты. Срабатывание FocusLost не доказывает обработку Escape.

Для времени использовать поддержанный UiSession overload с заимствованным
TimeProvider, если сценарию нужен управляемый clock. Async-результат применять
на owner через Post/InvokeAsync либо существующий owner-thread polling модели.
InvokeAsync принимает синхронный callback; он не переносит async continuations.
Не блокировать owner ожиданием работы, уже поставленной в его очередь. Готовность
определять по observable condition с дедлайном, а не произвольному sleep.

Существующие примеры: [interaction](../../TrueMoon.Alloy.Tests/ControlInteractionTests.cs),
[ownership](../../TrueMoon.Alloy.Tests/OwnershipTests.cs),
[window component](../../TrueMoon.Alloy.Tests/WindowComponentTests.cs).

## CPU raster и изображения

Доступны публичные SkiaRasterRenderBackend, RasterUiTarget и
RasterUiSessionExtensions.SnapshotRasterImage. Они не создают окно или GPU device.
Это настоящий drawing/text adapter Skia; для сопоставимых кадров подключать
SkiaFontCatalog с фиксированными font snapshots и доставлять их лицензию.
Не полагаться на системный font fallback для эталонной геометрии текста.

Фрагмент fixture: root заранее получен из DI/factory, находится в нужном состоянии
и ещё не attached/disposed; fonts загружены из подготовленных файлов; outputPng —
новый путь артефакта. Пример не является отдельным поставляемым test runner:

```csharp
using TrueMoon.Alloy.Runtime;
using TrueMoon.Alloy.Rendering.Skia;
using SkiaSharp;

// Логические 800×600 при scale=1.5 дают физические 1200×900 pixels.
using var session = UiSession.Create(root, new SkiaRasterRenderBackend(fonts),
    new RasterUiTarget(), new UiViewport(1200, 900, 1.5f));
session.Update();
using var image = session.SnapshotRasterImage();
using var png = image.Encode(SKEncodedImageFormat.Png, 100);
using var output = new FileStream(outputPng, FileMode.CreateNew, FileAccess.Write);
png.SaveTo(output);
```

Если модель ещё loading, сначала довести её до нужного observable condition и
выполнить Update. Snapshot допустим после успешного render nonempty viewport;
наличие необработанного visual dirty требует нового Update. Свежий snapshot
принадлежит caller и освобождается отдельно; он не блокирует последующий resize.

Сначала утверждать состояние/геометрию/focus, затем pixel comparison. В матрицу
включать значимые viewport/scale, состояния темы/доступности/loading/error,
кириллицу/длинные строки и clipping, если это входит в контракт consumer.

Правила шрифтов, provenance, RGBA/alpha, допуска, diff PNG и обновления эталона
ведутся в [GOLDEN_TESTS.md](GOLDEN_TESTS.md). Его допуски конкретной Settings-сцены
не становятся универсальным допуском для нового consumer. Normal run не записывает
baseline; кандидаты записываются отдельно, проверяются и лишь затем принимаются.
Не расширять tolerance или masks только ради passing результата.

## Тестовый host окна

Публичная точка: Window.AttachHost(IWindowController) возвращает
WindowHostConnection. Через Synchronize host публикует presentation state,
активность и фактический logical client size. UiSession отдельно получает viewport
и события ввода. Тестовый host должен согласованно выполнять оба контракта,
например публиковать деактивацию и направлять требуемый FocusLost в UI.

IWindowController позволяет наблюдать minimize/maximize/restore/close requests.
Реализация close policy принадлежит host: snapshot closing handlers, veto,
ошибка, повторный запрос, drain и cleanup должны проверяться в нужном scope.
Callback, просто возвращающий true, не доказывает штатное закрытие приложения.
Примеры уже есть в WindowComponentTests; это тестовые реализации, не отдельный
публичный SimulatedWindowHost API.

В проверенной реализации HostedUiWindow напрямую создаёт SilkWindowHost.
IUiWindowSessionFactory описывает совместимость поверхности/presenter с native
graphics API, а не замену оконной системы. RasterUiTarget предназначен для
UiSession без окна. Не выдавать подмену renderer за виртуальный native backend.
Общий переиспользуемый host/driver можно предложить владельцу UI по конкретной
потребности consumer; наличие этой инструкции не объявляет его реализованным.

Контракты: [WindowHostConnection](../../TrueMoon.Argentis/Windows/WindowHostConnection.cs),
[IWindowController](../../TrueMoon.Argentis/Windows/IWindowController.cs),
[native Hosting](../../TrueMoon.Alloy.Hosting/HostedUiWindow.cs).

## Сквозная проверка и ресурсы

При проверке App/IPC/storage оставлять нужные сервисы настоящими и изолировать
данные. Проверять не только подпись на экране, но и revision/результат команды,
отсутствие replay, files/history по контракту приложения и финальные owners.
Чисто UI fixture с fake reader не является end-to-end proof IPC.

При использовании Mithril явно фиксировать consumer EXE/assembly и полный
build/publish output: ChildProcess запускает текущий host, которым может оказаться
testhost. Это не доказывает запуск требуемого consumer. Жизненный цикл процесса
принадлежит Mithril/App, UI не должен подменять его собственной политикой restart.

После успешной передачи дерева UiSession владеет root и surface. Factory caller
отвечает за непринятое дерево при failed creation. Host connection заимствует
controller и не владеет native ресурсами/деревом. App-owned services/ViewModel
заимствуются View; порядок disposal не должен освобождать их дважды.

У каждого native/process probe должны быть deadline, журнал, подтверждённое
владение и finally cleanup при успехе, ошибке старта, отмене и таймауте. Перед
завершением проверить отсутствие своих descendants/окон/каналов. Не останавливать
пользовательские процессы или чужие instances по одному PID/имени.

## Отчёт и границы приёмки

Сохранять сценарий, input script, expected/actual state и assertions; SDK/TFM/
Features, версии пакетов, backend, OS/RID при native-проверке; viewport/scale,
font/native/asset SHA, PNG/diff/manifest и результат cleanup. Команду и passing
summary брать из фактически завершённого запуска текущего runner проекта.
Старые команды net10/датированные proofs в GOLDEN_TESTS и HISTORY не означают
новую проверку текущей версии; профиль сначала сверять с global.json/props.

В отчёте явно различать реализовано, проверено на указанной конфигурации,
запланировано и не проверено. Raster scale не закрывает Windows monitor DPI,
программный UiInput — физическую клавиатуру/IME/OS capture, CPU PNG — GPU present,
compositor, driver/device loss, media/export parity или другие платформы.
Для этих контрактов добавлять соответствующий native/hardware/input profile.
Computer Use может помогать исследованию, но его отсутствие не блокирует
независимые headless/raster проверки и не является дефектом приложения.

<a id="ui-semantic-snapshot"></a>
## Семантический снимок UI

Задавайте `SemanticId` для целей сценария. Он уникален в `UiSession`, привязан
к роли или ключу модели и сохраняется при смене темы, геометрии и экземпляра
контрола. Используйте ASCII-буквы, цифры, точку, дефис или подчёркивание.
Снимок отклоняет повторяющиеся ID среди скопированных узлов.

```csharp
session.Update();
var snapshot = session.CaptureSemantics();
var apply = snapshot.Nodes.Single(node => node.Id == "editor.inspector.apply");
```

Вызывайте `CaptureSemantics()` на UI owner thread после завершения layout.
После изменения дерева или размера сначала выполните `Update()`.
Снимок можно читать с другого потока. `ParentIndex` и порядок узлов отражают
вложенность и порядок отрисовки. `Bounds` и `VisibleBounds` используют
логические координаты корня. `VisibleBounds` учитывает клип предков до
перекрытия соседними узлами. `TreeView` представлен как один контрол с
признаком выбора. Строки дерева принадлежат данным контрола.

Проверяйте `IsVisible` и `IsEnabled` отдельно. Снимок содержит фокус и захват
указателя на момент чтения. Для ограниченного снимка `IsTruncated` сообщает,
что часть дерева отсутствует. Значения текстовых полей и сообщения об ошибках
не выгружаются. `includeAccessibleNames: true` уместен для доверенных подписей.
ID должен содержать технический ключ без пользовательского текста.

<a id="ui-semantic-input"></a>
## Ввод по семантическому ID

`FindSemanticTarget(snapshot, id)` сверяет ID с живым деревом на UI owner thread.
Для адресного ввода используйте `HandleSemanticInput(snapshot, id, input)`.
Поиск и ввод требуют полного снимка той же сессии с текущими поколениями дерева
и layout. После изменения дерева, геометрии или доступности вызовите `Update()`
и получите новый снимок. Скрытая и отключённая цели различаются в результате.

`Element.IsInputBlocked` временно исключает поддерево из маршрута указателя,
фокуса и Tab. Сессия при следующем обновлении или обычном вводе отменяет
фокус и захват внутри этого поддерева. Поиск цели возвращает `Disabled`
на время блокировки. `UiSemanticNode.IsEnabled` продолжает описывать
оформленное состояние `IsEnabled` с учётом предков.

Координаты pointer-событий остаются логическими координатами `UiSession`.
Выбирайте точку внутри `VisibleBounds`. Сессия повторяет hit test и проверяет
захват до передачи события. Если точку перекрывает другой контрол, результат
`Obstructed` указывает его ближайший ID. При захвате движение и отпускание
могут продолжаться вне bounds только для владельца захвата и его предков.
Текст и клавиши требуют текущего фокуса внутри цели.

Каждый вызов передаёт одно событие. Для клика передайте `PointerDown` и
`PointerUp`. Для drag добавьте `PointerMove`, для прокрутки используйте `Wheel`.
В `Routed` поле `Input.Handled` показывает, был ли ввод обработан. События
всплывают по штатному маршруту. `Intercepted` означает, что событие обработал
preview-наблюдатель, например при закрытии popup. Результат также содержит
ближайшие семантические ID фактической цели, фокуса и захвата. Событие
`FocusLost` передаётся сессии через обычный `HandleInput`.

Формат общего сценария и отчёта ведётся в [ENG9b](../engine/PLAN.md#eng9b-scenario-testing).
