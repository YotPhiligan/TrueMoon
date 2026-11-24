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

Контракты: [WindowHostConnection](../../TrueMoon.Argentis/WindowHostConnection.cs),
[IWindowController](../../TrueMoon.Argentis/IWindowController.cs),
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

Семантический снимок, поиск целей по ID и воспроизведение действий для агентов
запланированы в [UIQA](PLAN.md#uiqa-agent-checks). Контракт общего сценария,
отчёта и пакетного Player ведётся в [ENG9b](../engine/PLAN.md#eng9b-scenario-testing).

Эта фиксация — документация существующих API и способа выбора проверок. Она не
добавляет fixture/host/runner и не объявляет новые passing результаты.
