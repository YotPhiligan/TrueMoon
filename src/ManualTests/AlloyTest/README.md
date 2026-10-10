# AlloyTest: редактируемая форма OpenGL/Vulkan/Silk

Оконный пример для Windows x64 / .NET 10. [View1](View1.cs) создаёт постоянное дерево формы, динамический список, условную область и controls/theme демонстрацию, а [SettingsModel](SettingsModel.cs) хранит значения отдельно от контролов. Explicit Hosting/Silk с OpenGL по умолчанию или Vulkan через `--vulkan`, одна модель из DI. Функциональный standalone набор и OS clipboard проверены; та же сцена используется в [backend comparison и Vulkan HUD](../AlloyVulkanTest/README.md#общая-форма-rasteropenglvulkan-и-hud).

Параметры окна: `--transparent` включает per-pixel alpha, `--opacity 0.5` задаёт общую opacity, `--borderless` убирает системную рамку независимо от alpha. OpenGL — default; для Vulkan добавьте `--vulkan --validation`. `--transparent` и `--opacity` вместе запрещены. В smoke проверяются capabilities и очередь смены opacity0.5→0.75. Непрозрачный фон View1 сохраняет свой цвет даже в transparent режиме; прозрачность самого окна не меняет alpha UI автоматически. [API и ограничения](../../TrueMoon.Alloy.Hosting/README.md#прозрачность-standalone-окна).

Регистрация из примера (diagnostics опущены):

```csharp
using AlloyTest;
using TrueMoon;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Extensions.DependencyInjection;

await App.Builder(builder => builder.UseDI()).RunAsync(context => context
    .UsePresentation<View1>(options => options.UseSkiaOpenGL().UseSilkWindow(window =>
    { window.Width = 900; window.Height = 960; window.Title = "TrueMoon Alloy — настройки"; }))
    .Services(services => services.Singleton<SettingsModel>()));
```

`View1 : View<SettingsModel>` редактирует Name, Enabled и Volume через TextBox/CheckBox/Slider BindTwoWay. Односторонний BindText показывает Summary модели. «Загрузить пример» и «Сбросить» изменяют модель, а существующие контролы получают значения при следующем Update. Форма смешивает Text.Plain/Button.Simple/TextBox.Create/CheckBox.Create и обычные new Text/new Slider/new Button. View2 остаётся прежним прототипом и в запуске не участвует.

Попробуйте изменить имя, включая кириллицу и emoji, переключить уведомления и перетащить ползунок. Сводка показывает модель после изменения. Tab/Shift+Tab перемещает фокус; TextBox поддерживает выделение и Ctrl+A/C/X/V, CheckBox — Space/Enter, Slider — стрелки. Кнопки позволяют увидеть обратное обновление model → UI. Полные OS clipboard/font/input проверки остаются отдельным alpha проходом.

Список «Дела» использует `VStack.BindItems(model.Rows, factory)` и [SettingsRow](SettingsRow.cs) с BindTwoWay редактора. «Добавить дело» выполняет Add; кнопки строки «↑», «Заменить» и «Удалить» выполняют Move/Replace/Remove. «Развернуть (Reset)» публикует один Reset с обратным порядком тех же объектов: прежние строки сохраняются, включая выделение и подписки. Новый объект при Replace создаёт другую строку, старая освобождается. ScrollViewer ограничивает высоту списка, поддерживает wheel и раскрывает сфокусированную строку при Tab. Рисование и hit testing используют один child viewport; wheel на границе может всплыть во внешний ScrollViewer.

Область состояния показывает «Данные загружены», загрузку с ProgressBar или ошибку с кнопкой «Повторить». Кнопки «Загрузка», «Ошибка», «Готово» меняют immutable [LoadStatus](LoadStatus.cs) модели; «Повторить» возвращает Loading. Это управляемая демонстрация состояний без сетевого запроса. Внутренний [LoadStatusRegion](LoadStatusRegion.cs) использует Bind и SetContent: готовит новую ветвь до замены, освобождает старую, сохраняет соседние форму/строки. При factory failure прежний child остаётся до успешного RefreshContent; desired model/status не откатывается. Ownership и postcommit errors следуют существующему контракту, нового BindContent API нет.

«Светлая»/«Тёмная» переключают палитру. «Компактно» меняет theme padding Button/TextBox и stack spacing; существующие controls, строки и соседнее выделение сохраняются. «Ввод включён» включает/отключает группу редактируемых полей, а controls оформления остаются доступными. Local/style Padding/Spacing перекрывают тему; constructor defaults сохранены. Пользовательский [VolumeMeter](VolumeMeter.cs) рисует индикатор и подпись через IDrawingContext, получая Volume модели через one-way Bind.

[PNG32×32](Assets/alloy-mark.png) — программно созданная двухцветная сетка; Image показывает borrowed source, View1.Own освобождает decoded SkiaImageSource после UI-детей. MSBuild доставляет PNG в `Assets` при build/publish через PreserveNewest. Все изменяемые значения модели и controls обновляются событийно; неизменённая сцена не требует нового layout/draw.

## Собственный заголовок

`--custom-frame` включает Windows 11 custom frame с Argentis title bar, editable TextBox и кнопками minimize/maximize-restore/close. Пустая часть заголовка задаёт Caption, controls исключены из drag. Включены native drag, невидимые resize borders и NativeSnapLayouts; enabled maximize/restore кнопка имеет роль Maximize. Minimum client size330×300; [CustomTitleBar](CustomTitleBar.cs) сохраняет editor и правые кнопки в узкой зоне. Alt+Space/правый клик по Caption открывает нативное системное меню. Флаг можно совместить с `--transparent` либо `--opacity 0.5`, а также с `--vulkan --validation`. В `--smoke` проверяется та же форма внутри WindowFrame; обычный запуск позволяет проверить перемещение и кнопки вручную.

```powershell
dotnet run --project ManualTests/AlloyTest/AlloyTest.csproj -- --custom-frame
dotnet run --project ManualTests/AlloyTest/AlloyTest.csproj -- --custom-frame --vulkan --validation --smoke
```

На Windows заданные UseSilkWindow Width/Height и MinimumSize — логические96-DPI единицы. Native Scale берётся из HWND DPI; на150% окно900×960 получает framebuffer1350×1440. Нормализованный input остаётся в координатах UI; raw NativeWindow.Size/Position — физические pixels. Текущая проверка150% не закрывает физические100/200% и monitor transitions. [API/ownership](../../TrueMoon.Alloy.Hosting/README.md#собственный-заголовок-и-оконные-команды), [актуальные проверки](../../docs/alloy/STATUS.md).

## Запуск

Команды выполняются из `src`; нужен доступный desktop OpenGL context. Native Skia доставляется через Rendering.Skia, ручное копирование DLL не требуется.

```powershell
dotnet run --project ManualTests/AlloyTest/AlloyTest.csproj
```

Без smoke-флагов окно работает через App lifecycle до обычного закрытия.

```powershell
dotnet run --project ManualTests/AlloyTest/AlloyTest.csproj -- --vulkan
dotnet run --project ManualTests/AlloyTest/AlloyTest.csproj -- --vulkan --smoke --validation
```

`--vulkan` меняет backend при создании сессии; view/model/input script остаются теми же. `--validation` требует `--vulkan` и установленный validation layer; в bounded smoke warning/error сообщения, включая teardown, завершают проверку неуспешно. Настройка process-only VK_LAYER_PATH описана в [GPU-примере](../AlloyVulkanTest/README.md). OS clipboard проверяется только с явным `--smoke-clipboard` и подготовленным fixture при любом backend.

```powershell
dotnet run --project ManualTests/AlloyTest/AlloyTest.csproj -- --smoke
```

`--smoke` создаёт тот же View1 и запускает [SettingsFormSmoke](SettingsFormSmoke.cs) в FramePresented на UI-потоке. Probe проверяет initial model → UI, редактирование текста/checkbox/slider → model, обновление Summary, сохранение экземпляров формы, focus/выделение целой графемы при изменении соседнего свойства, загрузку примера и сброс. Для списка проверяются порядок/значения строк, редактирование → model, Add/Remove/Replace/Move/Reset, сохранение строки/focus/выделения при Add/Move/Reset/замене соседа, снятие focus при Remove, кнопки добавления/удаления, Dispose всех generated subtrees и прекращение invalidation неизменённого UI. Ввод передаётся программно через UiSession.HandleInput в реально рисуемом окне; OS-события и системный clipboard этим не проверяются.

Дополнительно smoke переключает Loading/Error/Ready/retry, Dark/Light/compact, disable/re-enable, сохраняет соседние строки/focus/grapheme selection/Offset36. Проверяет Tab/ShiftTab, Space/Enter, текст Latin/Cyrillic/emoji, Delete/Backspace/Home/End, drag Slider за пределами и release/clamp, wheel36→72 и auto-scroll last editor, FocusLost, Image/Meter. После минимум **40 кадров** окно закрывается, код ожидает Completion/StopAsync и проверяет Dispose/detach всех замеченных form/row/status nodes. Cancellation bound —30 секунд. Timeout/lifecycle error/native failure не являются успешным smoke. Сценарий не заменяет screenshot/font golden, Vulkan validation или long soak.

Системный clipboard проверяется отдельно:

```powershell
dotnet run --project ManualTests/AlloyTest/AlloyTest.csproj -- --smoke-clipboard
```

Перед запуском скопируйте точную строку `Alloy clipboard smoke fixture v1` в заменяемый тестовый clipboard. Это тот же smoke с дополнительными Ctrl+C/X/V и восстановлением fixture **текстом**; остальные форматы он не сохраняет. Если исходный текст отличается от fixture, probe останавливается до любой записи, включая restore. Содержимое clipboard в логи не выводится. Windows Get/Set используют общий Win32 путь с явной передачей HGLOBAL. Native OS copy/cut/paste прошёл в Debug/published Release после подготовки fixture пользователем2026-10-07; причина прежнего OpenClipboard/error5 отдельно не установлена. Default `--smoke` сообщает `OS clipboard: not requested`.

Восстановление редактора/окна после ошибок проверяется независимо от буфера ОС:

```powershell
dotnet run --project ManualTests/AlloyTest/AlloyTest.csproj -- --smoke-clipboard-failure
```

[ClipboardFailureSmoke](ClipboardFailureSmoke.cs) подставляет контролируемый IUiClipboard через декоратор IUiSessionFactory в реальном HostedUiWindow. Три отказа copy/cut/paste сообщаются через UiSession.ClipboardFailed; value, model, focus и grapheme selection сохраняются. После переключения сервиса в успешный режим все три повтора проходят; обычный interaction smoke продолжается, сессия освобождается и registry становится0. Этот probe **не обращается к системному clipboard** и не доказывает native OS recovery. Флаги `--smoke-clipboard` и `--smoke-clipboard-failure` несовместимы.

Оконные probes запускайте последовательно: другое окно или OS focus event может снять фокус с редактора во время проверки его сохранения. Проверка намеренно не восстанавливает фокус между model update и assertion.

```powershell
dotnet run --project ManualTests/AlloyTest/AlloyTest.csproj -- --window-lifetime
```

[WindowLifetimeProbe](WindowLifetimeProbe.cs) проверяет шесть сценариев OpenGL/Vulkan: закрытие после трёх render events, закрытие до Run и перехваченную ошибку render callback с последующим закрытием. Native handle должен оставаться прежним в Closing и после Run; input остаётся доступным, затем Dispose уничтожает окно, повторный Dispose безопасен. Get/Set с другого потока, null/NUL запись и доступ после Dispose должны быть отвергнуты до native записи. Два read-only Get после Run требуют доступного OS clipboard; ошибка доступа завершает probe неуспешно. Это отдельный platform probe, без Skia/App. Исключения callback перехватываются внутри managed handler, как в Hosting, и не выходят через GLFW.

SilkWindowHost.Run теперь завершает цикл без Reset: input context снимает GLFW callbacks в Dispose, пока окно ещё существует; затем уничтожается окно. Create/Run/Dispose выполняются на одном потоке. Не вызывайте NativeWindow.Reset/Dispose вручную до освобождения host.

Ошибки фабрики отдельно проверяются через неизменённый HostedUiWindow:

```powershell
dotnet run --project ManualTests/AlloyTest/AlloyTest.csproj -- --collection-failure
```

[CollectionFailureProbe](CollectionFailureProbe.cs) последовательно создаёт два реальных OpenGL/Silk окна: factory failure в initial Update и после первого presentation. Проверяет точную ошибку в Completion/StopAsync, session registry/collection subscriptions0 и once-only Dispose drafts/committed tree. Probe автоматически завершает окна; он не подменяет device-loss/native heap/long soak проверки.

Проверка опубликованной версии из `src`:

```powershell
dotnet publish ManualTests/AlloyTest/AlloyTest.csproj -c Release --no-restore -o TestResults/AlloyInteractions/publish
./TestResults/AlloyInteractions/publish/AlloyTest.exe --smoke
./TestResults/AlloyInteractions/publish/AlloyTest.exe --collection-failure
./TestResults/AlloyInteractions/publish/AlloyTest.exe --smoke-clipboard-failure
./TestResults/AlloyInteractions/publish/AlloyTest.exe --smoke-clipboard
```

## Alpha migration и границы

31 legacy-файл из Alloy root/Presenters перенесён в сборку Hosting, namespaces `TrueMoon.Alloy.Hosting.Compatibility` и `.Compatibility.Presenters`. Это **breaking assembly/namespace migration без type forwarders**: consumers старых типов должны обновить ссылки/imports/callers и пересобраться. No-options `UsePresentation<TView>()` доступен только с opt-in Compatibility и не рекомендуется для нового UI; старые `Visual.GetEnumerator` и lifetime bugs не исправлены, закомментированные прототипы не являются рендерерами. Этот пример использует неизменённый новый explicit Hosting API, а не legacy путь.

Alloy теперь содержит только Runtime/csproj и ProjectReference на Argentis, без пакетов и Core/Contracts. Hosting явно подключает Silk.NET/SkiaSharp/Topten.RichTextKit и Core/Contracts вместе с UI adapters. Direct renderer не требует Hosting/Platform.Silk или транзитивных Core/Contracts/Windowing через Alloy; Platform.Silk остаётся без Skia. Native ABI, lease/ownership и retirement не менялись. [Hosting](../../TrueMoon.Alloy.Hosting/README.md), [Compatibility](../../TrueMoon.Alloy.Hosting/Compatibility/README.md), [native Skia](../../TrueMoon.Alloy.Rendering.Skia/README.md).

Последняя проверка2026-10-07, после подготовки пользователем fixture: native `--smoke-clipboard` прошёл Debug40/published Release41 frames, включая Unicode whole-grapheme copy/cut/paste и binding; `--window-lifetime` прошёл по6 OpenGL/Vulkan сценариев в обеих сборках. Независимое чтение подтвердило точное восстановление fixture; metadata sequence103/formats4/open window0. Build/xUnit/publish в этом запуске не повторялись. Предыдущий запуск: focused44/44, UI386/386, solution build0errors/1339warnings, Release publish exit0, controlled recovery Debug41/Release40, factory failure обе сборки; детали и прежние failed attempts — в [STATUS](../../docs/alloy/STATUS.md). Реальный OS отказ записи/recovery отдельно не индуцировался. Debug heap/native crash capture сейчас не запускались; полный backend/HUD/font/DPI/soak/alpha проход ещё предстоит.

Shared-scene запуск2026-10-07: standalone `--smoke` OpenGL и `--vulkan --smoke --validation` каждый40 frames в Debug/published Release, Vulkan validation0/0. UI386/386, solution build0errors/1339warnings; оба manual примера published и PNG/native hash delivery проверены. Те же linked sources прошли raster/OpenGL/Vulkan comparison scale1/1.5/2 и HUD90 compositions/36 UI draws/2 sessions в обеих сборках. OS clipboard сейчас не запрашивался; его результаты выше исторические. Следующий этап —2.4/K8; full font/golden/DPI/soak/alpha checks остаются, [подробности](../../docs/alloy/STATUS.md).
