# AlloyTest: редактируемая форма OpenGL/Silk

Оконный пример для Windows x64 / .NET 10. [View1](View1.cs) создаёт постоянное дерево формы, а [SettingsModel](SettingsModel.cs) хранит редактируемые значения отдельно от контролов. Используются explicit Hosting/OpenGL/Silk и одна модель из DI. Это первая форма 5.1a; коллекции, условные области и темы ещё предстоят.

Регистрация из примера (diagnostics опущены):

```csharp
using AlloyTest;
using TrueMoon;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Extensions.DependencyInjection;

await App.Builder(builder => builder.UseDI()).RunAsync(context => context
    .UsePresentation<View1>(options => options.UseSkiaOpenGL().UseSilkWindow())
    .Services(services => services.Singleton<SettingsModel>()));
```

`View1 : View<SettingsModel>` редактирует Name, Enabled и Volume через TextBox/CheckBox/Slider BindTwoWay. Односторонний BindText показывает Summary модели. «Загрузить пример» и «Сбросить» изменяют модель, а существующие контролы получают значения при следующем Update. Форма смешивает Text.Plain/Button.Simple/TextBox.Create/CheckBox.Create и обычные new Text/new Slider/new Button. View2 остаётся прежним прототипом и в запуске не участвует.

Попробуйте изменить имя, включая кириллицу и emoji, переключить уведомления и перетащить ползунок. Сводка показывает модель после изменения. Tab/Shift+Tab перемещает фокус; TextBox поддерживает выделение и Ctrl+A/C/X/V, CheckBox — Space/Enter, Slider — стрелки. Кнопки позволяют увидеть обратное обновление model → UI. Полные OS clipboard/font/input проверки остаются отдельным alpha проходом.

## Запуск

Команды выполняются из `src`; нужен доступный desktop OpenGL context. Native Skia доставляется через Rendering.Skia, ручное копирование DLL не требуется.

```powershell
dotnet run --project ManualTests/AlloyTest/AlloyTest.csproj
```

Без `--smoke` окно работает через App lifecycle до обычного закрытия.

```powershell
dotnet run --project ManualTests/AlloyTest/AlloyTest.csproj -- --smoke
```

`--smoke` создаёт тот же View1 и запускает [SettingsFormSmoke](SettingsFormSmoke.cs) в FramePresented на UI-потоке. Probe проверяет initial model → UI, редактирование текста/checkbox/slider → model, обновление Summary, сохранение экземпляров дерева, focus/выделение целой графемы при изменении соседнего свойства, загрузку примера, сброс и прекращение invalidation неизменённой формы. Ввод передаётся программно через UiSession.HandleInput в реально рисуемом окне; OS-события и системный clipboard этим не проверяются.

После минимум 30 кадров окно закрывается, код ожидает Completion/StopAsync и проверяет Dispose/detach всех элементов. Cancellation bound для StartAsync/ожидания/StopAsync — 30 секунд. Timeout, lifecycle error или нативное падение не являются успешным smoke. Сценарий не заменяет screenshot/font golden, Vulkan validation или долгий ресурсный soak.

Оконные probes запускайте последовательно: другое окно или OS focus event может снять фокус с редактора во время проверки его сохранения. Проверка намеренно не восстанавливает фокус между model update и assertion.

```powershell
dotnet run --project ManualTests/AlloyTest/AlloyTest.csproj -- --window-lifetime
```

[WindowLifetimeProbe](WindowLifetimeProbe.cs) проверяет шесть сценариев OpenGL/Vulkan: закрытие после трёх render events, закрытие до Run и перехваченную ошибку render callback с последующим закрытием. Native handle должен оставаться прежним в Closing и после Run; input остаётся доступным, затем Dispose уничтожает окно, повторный Dispose безопасен. Это отдельный platform probe, без Skia/App. Исключения callback перехватываются внутри managed handler, как в Hosting, и не выходят через GLFW.

SilkWindowHost.Run теперь завершает цикл без Reset: input context снимает GLFW callbacks в Dispose, пока окно ещё существует; затем уничтожается окно. Create/Run/Dispose выполняются на одном потоке. Не вызывайте NativeWindow.Reset/Dispose вручную до освобождения host.

Проверка опубликованной версии из `src`:

```powershell
dotnet publish ManualTests/AlloyTest/AlloyTest.csproj -c Release --no-restore -o TestResults/AlloyFactories/publish
./TestResults/AlloyFactories/publish/AlloyTest.exe --smoke
```

## Alpha migration и границы

31 legacy-файл из Alloy root/Presenters перенесён в сборку Hosting, namespaces `TrueMoon.Alloy.Hosting.Compatibility` и `.Compatibility.Presenters`. Это **breaking assembly/namespace migration без type forwarders**: consumers старых типов должны обновить ссылки/imports/callers и пересобраться. No-options `UsePresentation<TView>()` доступен только с opt-in Compatibility и не рекомендуется для нового UI; старые `Visual.GetEnumerator` и lifetime bugs не исправлены, закомментированные прототипы не являются рендерерами. Этот пример использует неизменённый новый explicit Hosting API, а не legacy путь.

Alloy теперь содержит только Runtime/csproj и ProjectReference на Argentis, без пакетов и Core/Contracts. Hosting явно подключает Silk.NET/SkiaSharp/Topten.RichTextKit и Core/Contracts вместе с UI adapters. Direct renderer не требует Hosting/Platform.Silk или транзитивных Core/Contracts/Windowing через Alloy; Platform.Silk остаётся без Skia. Native ABI, lease/ownership и retirement не менялись. [Hosting](../../TrueMoon.Alloy.Hosting/README.md), [Compatibility](../../TrueMoon.Alloy.Hosting/Compatibility/README.md), [native Skia](../../TrueMoon.Alloy.Rendering.Skia/README.md).

Текущая проверка 2026-10-07 после исправления window lifetime: UI188/188, solution build0errors/1339warnings; по10 form smoke с отладочной кучей в Debug и published Release, каждый30–31 кадров; по6 lifetime сценариев в обоих вариантах. До исправления дампы двух запусков показывали input callback cleanup после разрушения окна; lifetime probe также падал до правки и прошёл после. Исходный 0xC0000374 отдельно не перехвачен, поэтому его связь с найденной ошибкой является обоснованной гипотезой. Полный перечень проверок, ограничений и следующий шаг — в [STATUS](../../docs/alloy/STATUS.md).
